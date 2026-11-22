namespace CampusMarket.Api.Common.Storage;

// "Storage" section of the configuration. PhotosPath is relative to the API folder unless it is absolute
// (in Docker: /app/data/photos, inside the volume, so the photos survive a restart).
public sealed class PhotoStorageOptions
{
    public const string SectionName = "Storage";

    public string PhotosPath { get; set; } = "photos";
}
