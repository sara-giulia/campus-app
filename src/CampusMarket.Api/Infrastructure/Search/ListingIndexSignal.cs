using System.Threading.Channels;

namespace CampusMarket.Api.Infrastructure.Search;

// "A listing changed": EfUnitOfWork rings it after saving a new or changed listing, ListingIndexService
// waits for it. Many rings while the service is busy count as one (capacity 1), because the service
// looks at every listing that needs a vector, not at the one that rang. Without the model nobody waits
// and the rings cost nothing.
public sealed class ListingIndexSignal
{
    private readonly Channel<bool> _channel = Channel.CreateBounded<bool>(
        new BoundedChannelOptions(1) { FullMode = BoundedChannelFullMode.DropWrite, SingleReader = true });

    public void Notify() => _channel.Writer.TryWrite(true);

    // Ends after Complete (ListingIndexService calls it when the app stops).
    public IAsyncEnumerable<bool> ReadAllAsync() => _channel.Reader.ReadAllAsync();

    public void Complete() => _channel.Writer.TryComplete();
}
