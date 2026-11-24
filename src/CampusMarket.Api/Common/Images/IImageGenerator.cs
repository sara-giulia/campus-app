using CampusMarket.Api.Domain.Listings;
using CampusMarket.Api.Domain.Photos;

namespace CampusMarket.Api.Common.Images;

// Makes a picture for a listing that has no photo, from its title, description and category only, so
// nothing is made up. Two implementations, like the email senders: SvgImageGenerator (always works, used
// on Render, where there is no GPU) and OllamaImageGenerator (a local image model, optional).
public interface IImageGenerator
{
    Task<GeneratedImage> GenerateAsync(ImagePrompt prompt, CancellationToken ct = default);
}

// CategoryName is the display name an Admin gave the category ("Manuale"), shown on the SVG.
public sealed record ImagePrompt(string Title, string? Description, Category Category, string CategoryName);

public sealed record GeneratedImage(byte[] Content, PhotoFormat Format);
