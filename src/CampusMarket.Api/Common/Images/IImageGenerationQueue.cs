namespace CampusMarket.Api.Common.Images;

// The listings waiting for a generated image. Publishing only adds the id here and answers at once; a
// background service (ImageGenerationService) makes the images one by one.
public interface IImageGenerationQueue
{
    // Nothing happens when image generation is turned off (ImageGeneration:Enabled).
    void Enqueue(Guid listingId);

    // The ids until Complete is called (the app is stopping).
    IAsyncEnumerable<Guid> ReadAllAsync();

    // No more ids: ReadAllAsync ends once the waiting ones are read.
    void Complete();
}
