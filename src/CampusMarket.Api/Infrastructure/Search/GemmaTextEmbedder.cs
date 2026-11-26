using CampusMarket.Api.Common.Search;
using Microsoft.Extensions.Options;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.Tokenizers;
using SessionOptions = Microsoft.ML.OnnxRuntime.SessionOptions;

namespace CampusMarket.Api.Infrastructure.Search;

// EmbeddingGemma-300m (Google, multilingual, Romanian included), exported to ONNX with 4-bit weights
// (onnx-community/embeddinggemma-300m-ONNX, model_q4), run locally with ONNX Runtime; the text is split
// into tokens with the model's own SentencePiece file through Microsoft.ML.Tokenizers (ML.NET).
// About 200 MB on disk and in memory; a short text takes ~30 ms on one core.
// Why this model and the prompts below: docs/ml/semantic-search.md.
public sealed class GemmaTextEmbedder : ITextEmbedder, IDisposable
{
    public const string ModelFile = "model_q4.onnx";
    public const string WeightsFile = "model_q4.onnx_data";
    public const string TokenizerFile = "tokenizer.model";
    public static readonly string[] Files = [ModelFile, WeightsFile, TokenizerFile];

    // The model sees at most this many tokens; a long description is cut (the title is at the start).
    private const int MaxTokens = 256;

    // The prompts the model was trained with: one for the search, one for the documents it searches in.
    private const string QueryPrompt = "task: search result | query: ";
    private const string DocumentPrompt = "title: none | text: ";

    private static readonly string[] InputNames = ["input_ids", "attention_mask"];
    private static readonly string[] OutputNames = ["sentence_embedding"];

    private readonly SentencePieceTokenizer _tokenizer;
    private readonly InferenceSession _session;
    private readonly RunOptions _runOptions = new();
    // One text at a time: on Render there is a tenth of a CPU, running two at once only uses more memory.
    private readonly Lock _gate = new();

    public GemmaTextEmbedder(IOptions<SemanticSearchOptions> options)
    {
        var folder = options.Value.FullModelPath;
        using (var tokenizerModel = File.OpenRead(Path.Combine(folder, TokenizerFile)))
            _tokenizer = SentencePieceTokenizer.Create(tokenizerModel, false, false);

        using var sessionOptions = new SessionOptions
        {
            IntraOpNumThreads = options.Value.Threads,
            InterOpNumThreads = 1,
            GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL
        };
        // model_q4.onnx_data (the weights) is read from the same folder.
        _session = new InferenceSession(Path.Combine(folder, ModelFile), sessionOptions);
    }

    public string ModelName => "embeddinggemma-300m-q4@5090578";

    public static bool FilesExist(string folder) => Files.All(file => File.Exists(Path.Combine(folder, file)));

    public float[] EmbedQuery(string query) => Embed(QueryPrompt + query);

    public float[] EmbedListing(string title, string? description) =>
        Embed(DocumentPrompt + (string.IsNullOrWhiteSpace(description) ? title : $"{title}. {description}"));

    // Lower case: titles are often typed in capitals or with random capitals ("CASTI wireless"),
    // which the model reads as other words; the evaluation is better this way.
    // The ids are the ones of the reference tokenizer: <bos> (2), the text, <eos> (1).
    public long[] TokenIds(string text)
    {
        var pieces = _tokenizer.EncodeToIds(text.ToLowerInvariant());
        var count = Math.Min(pieces.Count, MaxTokens - 2);
        var ids = new long[count + 2];
        ids[0] = _tokenizer.BeginningOfSentenceId;
        for (var i = 0; i < count; i++)
            ids[i + 1] = pieces[i];
        ids[^1] = _tokenizer.EndOfSentenceId;
        return ids;
    }

    private float[] Embed(string text)
    {
        var ids = TokenIds(text);
        var mask = new long[ids.Length];
        Array.Fill(mask, 1L);
        long[] shape = [1, ids.Length];

        float[] vector;
        lock (_gate)
        {
            using var idsValue = OrtValue.CreateTensorValueFromMemory(ids, shape);
            using var maskValue = OrtValue.CreateTensorValueFromMemory(mask, shape);
            using var outputs = _session.Run(_runOptions, InputNames, [idsValue, maskValue], OutputNames);
            vector = outputs[0].GetTensorDataAsSpan<float>().ToArray();
        }

        // The model already gives TextEmbedding.Dimensions values of length 1 (checked in ML.Tests);
        // normalised again so a cosine is always exactly a dot product.
        var norm = MathF.Sqrt(vector.Sum(v => v * v));
        for (var i = 0; i < vector.Length; i++)
            vector[i] /= norm;
        return vector;
    }

    public void Dispose()
    {
        _session.Dispose();
        _runOptions.Dispose();
    }
}
