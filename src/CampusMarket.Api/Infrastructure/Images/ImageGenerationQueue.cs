using System.Threading.Channels;
using CampusMarket.Api.Common.Images;
using Microsoft.Extensions.Options;

namespace CampusMarket.Api.Infrastructure.Images;

// The listing ids waiting for an image, in memory: one reader (ImageGenerationService), many writers
// (every publish). An id lost at a restart is not a problem: the service checks every Active listing
// without photos when it starts.
public sealed class ImageGenerationQueue(IOptions<ImageGenerationOptions> options) : IImageGenerationQueue
{
    private readonly Channel<Guid> _channel = Channel.CreateUnbounded<Guid>(new UnboundedChannelOptions { SingleReader = true });

    public void Enqueue(Guid listingId)
    {
        if (options.Value.Enabled)
            _channel.Writer.TryWrite(listingId);
    }

    public IAsyncEnumerable<Guid> ReadAllAsync() => _channel.Reader.ReadAllAsync();

    public void Complete() => _channel.Writer.TryComplete();
}
