namespace CampusMarket.Api.Common.Images;

// "ImageGeneration" section of the configuration.
public sealed class ImageGenerationOptions
{
    public const string SectionName = "ImageGeneration";
    public const string SvgProvider = "Svg";
    public const string OllamaProvider = "Ollama";

    // Off: no image is generated and the background service does not run.
    public bool Enabled { get; set; } = true;

    // "Svg" (default) or "Ollama".
    public string Provider { get; set; } = SvgProvider;

    public OllamaImageOptions Ollama { get; set; } = new();
}

// Ollama's OpenAI-compatible endpoint, POST {BaseUrl}/v1/images/generations. Image generation in Ollama
// is experimental, so any failure falls back to the SVG.
public sealed class OllamaImageOptions
{
    public string BaseUrl { get; set; } = "http://localhost:11434";
    public string Model { get; set; } = "x/z-image-turbo";
    public int TimeoutSeconds { get; set; } = 120;
}
