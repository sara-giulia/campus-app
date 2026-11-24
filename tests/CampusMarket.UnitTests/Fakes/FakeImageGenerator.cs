using CampusMarket.Api.Common.Images;
using CampusMarket.Api.Domain.Photos;

namespace CampusMarket.UnitTests.Fakes;

public sealed class FakeImageGenerator : IImageGenerator
{
    public static readonly byte[] DefaultSvg = "<svg xmlns='http://www.w3.org/2000/svg'></svg>"u8.ToArray();

    public GeneratedImage Image { get; set; } = new(DefaultSvg, PhotoFormat.Svg);

    public List<ImagePrompt> Prompts { get; } = new();

    public Task<GeneratedImage> GenerateAsync(ImagePrompt prompt, CancellationToken ct = default)
    {
        Prompts.Add(prompt);
        return Task.FromResult(Image);
    }
}
