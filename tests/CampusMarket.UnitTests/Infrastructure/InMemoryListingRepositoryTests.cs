using CampusMarket.Api.Domain.Listings;
using CampusMarket.Api.Infrastructure;
using Xunit;

namespace CampusMarket.UnitTests.Infrastructure;

public class InMemoryListingRepositoryTests
{
    private readonly InMemoryListingRepository _repository = new();

    private static Listing NewListing(string title = "Java Book") =>
        Listing.Create(Guid.NewGuid(), title, Category.Textbook, Condition.Good, 50, DateTimeOffset.UtcNow).Value;

    [Fact]
    public async Task AddAsync_NewListing_ShouldBeFoundById()
    {
        var listing = NewListing();

        var added = await _repository.AddAsync(listing);
        var found = await _repository.GetByIdAsync(listing.Id);

        Assert.True(added);
        Assert.Same(listing, found);
    }

    [Fact]
    public async Task AddAsync_SameIdTwice_ShouldReturnFalse()
    {
        var listing = NewListing();
        await _repository.AddAsync(listing);

        var addedAgain = await _repository.AddAsync(listing);

        Assert.False(addedAgain);
        Assert.Single(await _repository.GetAllAsync());
    }

    [Fact]
    public async Task GetByIdAsync_UnknownId_ShouldReturnNull()
    {
        var found = await _repository.GetByIdAsync(Guid.NewGuid());

        Assert.Null(found);
    }

    [Fact]
    public async Task GetAllAsync_ShouldReturnASnapshot()
    {
        await _repository.AddAsync(NewListing("First"));
        await _repository.AddAsync(NewListing("Second"));

        var snapshot = await _repository.GetAllAsync();
        await _repository.AddAsync(NewListing("Third"));

        // The list we already received does not change when the store changes.
        Assert.Equal(2, snapshot.Count);
        Assert.Equal(3, (await _repository.GetAllAsync()).Count);
    }

    [Fact]
    public async Task Update_ExistingListing_ShouldKeepTheChanges()
    {
        var listing = NewListing();
        await _repository.AddAsync(listing);

        listing.UpdateDetails("New title", Category.Notes, Condition.Worn, 10);
        _repository.Update(listing);

        var found = await _repository.GetByIdAsync(listing.Id);
        Assert.Equal("New title", found!.Title);
    }

    [Fact]
    public async Task Update_UnknownListing_ShouldNotAddIt()
    {
        var listing = NewListing();

        _repository.Update(listing);

        Assert.Null(await _repository.GetByIdAsync(listing.Id));
        Assert.Empty(await _repository.GetAllAsync());
    }

    [Fact]
    public async Task Delete_ExistingListing_ShouldRemoveIt()
    {
        var listing = NewListing();
        await _repository.AddAsync(listing);

        _repository.Delete(listing);

        Assert.Null(await _repository.GetByIdAsync(listing.Id));
    }
}

