using CampusMarket.ML.Training.Evaluation;
using Microsoft.ML;

namespace CampusMarket.ML.Tests;

[CollectionDefinition(nameof(ConsoleOutputCollection), DisableParallelization = true)]
public sealed class ConsoleOutputCollection;

// FeedbackEvaluation (ml/data/feedback.csv, the real sales exported by the API): a missing file
// is a no-op; a small CSV in the export format (quoted text, a new line inside a description,
// a doubled quote, a spam row) is evaluated with the saved price model without throwing.
// The console is captured, so this class does not run in parallel with the others.
[Collection(nameof(ConsoleOutputCollection))]
public sealed class FeedbackEvaluationTests : IDisposable
{
    private const string Header = "Title,Description,Category,Condition,Price,IsSpam,TemplateId";

    private readonly TextWriter _originalOut = Console.Out;
    private readonly StringWriter _output = new();
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), "feedback-eval-" + Guid.NewGuid().ToString("N"));
    private readonly string _modelsDir = Path.Combine(TestPaths.MlDirectory, "models");

    public FeedbackEvaluationTests()
    {
        Directory.CreateDirectory(_tempDir);
        Console.SetOut(_output);
    }

    public void Dispose()
    {
        Console.SetOut(_originalOut);
        _output.Dispose();
        Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public void A_missing_feedback_file_is_a_no_op()
    {
        var path = Path.Combine(_tempDir, "feedback.csv");

        var act = () => FeedbackEvaluation.Run(new MLContext(seed: 0), path, _modelsDir);

        act.Should().NotThrow();
        _output.ToString().Should().BeEmpty();
    }

    [Fact]
    public void A_small_export_with_a_multiline_description_reports_n_and_the_metrics()
    {
        var path = WriteCsv(
            Header + "\n" +
            "\"Laptop Dell Latitude i5 256GB\",\"Stare buna\nbaterie noua\",Electronics,Good,1500,false,\"feedback\"\n" +
            "\"Manual \"\"Algebra\"\", anul 1\",\"\",Textbook,Worn,40.5,false,\"feedback\"\n" +
            "\"CASTIGA BANI\",\"scrie-mi\",Other,New,1,true,\"feedback\"\n");

        var act = () => FeedbackEvaluation.Run(new MLContext(seed: 0), path, _modelsDir);

        act.Should().NotThrow();
        var output = _output.ToString();
        output.Should().Contain("=== Feedback evaluation (ml/data/feedback.csv) ===")
            .And.Contain("| n | MAE (lei) | R² |")
            .And.Contain("| 2 | ")
            .And.Contain("Indicative only: n = 2;");
    }

    [Fact]
    public void A_header_only_export_reports_zero_rows()
    {
        var path = WriteCsv(Header + "\n");

        FeedbackEvaluation.Run(new MLContext(seed: 0), path, _modelsDir);

        _output.ToString().Should().Contain("Feedback rows: 0, nothing to evaluate.")
            .And.NotContain("| n | MAE (lei) | R² |");
    }

    private string WriteCsv(string content)
    {
        var path = Path.Combine(_tempDir, "feedback.csv");
        File.WriteAllText(path, content);
        return path;
    }
}
