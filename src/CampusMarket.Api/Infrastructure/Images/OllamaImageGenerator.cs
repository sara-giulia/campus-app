using System.Net.Http.Json;
using System.Text.Json.Serialization;
using CampusMarket.Api.Common.Images;
using CampusMarket.Api.Domain.Photos;
using Microsoft.Extensions.Options;

namespace CampusMarket.Api.Infrastructure.Images;

// A picture from a local image model through Ollama's OpenAI-compatible endpoint
// (POST /v1/images/generations, the image comes back as base64). Image generation in Ollama is
// experimental, so when Ollama is not running, is too slow, or answers with something that is not a
// PNG/JPEG/WebP, the SVG is used instead and the listing still gets its picture.
public sealed partial class OllamaImageGenerator(
    HttpClient httpClient,
    IOptions<ImageGenerationOptions> options,
    SvgImageGenerator fallback,
    ILogger<OllamaImageGenerator> logger) : IImageGenerator
{
    private const string GenerationsEndpoint = "v1/images/generations";
    private const int MaxDescriptionLength = 200;

    public async Task<GeneratedImage> GenerateAsync(ImagePrompt prompt, CancellationToken ct = default)
    {
        try
        {
            var request = new OllamaImageRequest(options.Value.Ollama.Model, BuildPrompt(prompt), "512x512", "b64_json");
            using var response = await httpClient.PostAsJsonAsync(GenerationsEndpoint, request, ct);
            response.EnsureSuccessStatusCode();

            var body = await response.Content.ReadFromJsonAsync<OllamaImageResponse>(ct);
            var content = Convert.FromBase64String(body?.Data?.FirstOrDefault()?.B64Json ?? string.Empty);
            var format = PhotoFormat.Detect(content);
            if (format is not null)
                return new GeneratedImage(content, format);

            LogNotAnImage(logger);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or FormatException or System.Text.Json.JsonException)
        {
            // TaskCanceledException is the HttpClient timeout here; a real shutdown is checked below.
            ct.ThrowIfCancellationRequested();
            LogFailed(logger, ex);
        }

        return await fallback.GenerateAsync(prompt, ct);
    }

    // Only what the seller wrote, and no text in the picture, so the model does not invent a brand, a price
    // or words that are not in the listing.
    private static string BuildPrompt(ImagePrompt prompt)
    {
        var description = prompt.Description ?? string.Empty;
        if (description.Length > MaxDescriptionLength)
            description = description[..MaxDescriptionLength];

        return $"A realistic photo of a second-hand item for students: {prompt.Title}. {description} "
               + "Plain light background, natural light. No text, no letters, no logos, no watermark.";
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Ollama did not return a PNG, JPEG or WebP image; using the SVG")]
    private static partial void LogNotAnImage(ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Ollama image generation failed; using the SVG")]
    private static partial void LogFailed(ILogger logger, Exception exception);

    private sealed record OllamaImageRequest(
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("prompt")] string Prompt,
        [property: JsonPropertyName("size")] string Size,
        [property: JsonPropertyName("response_format")] string ResponseFormat);

    private sealed record OllamaImageResponse([property: JsonPropertyName("data")] List<OllamaImageData>? Data);

    private sealed record OllamaImageData([property: JsonPropertyName("b64_json")] string? B64Json);
}
