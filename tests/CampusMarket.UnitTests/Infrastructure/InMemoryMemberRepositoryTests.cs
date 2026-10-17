using CampusMarket.Api.Domain.Members;
using CampusMarket.Api.Infrastructure;
using Xunit;

namespace CampusMarket.UnitTests.Infrastructure;

public class InMemoryMemberRepositoryTests
{
    private readonly InMemoryMemberRepository _repository = new();

    private static Member NewMember(string name = "Ana") =>
        Member.Create(name, $"{name.ToLowerInvariant()}@gmail.com", DateTimeOffset.UtcNow).Value;

    [Fact]
    public async Task AddAsync_NewMember_ShouldBeFoundById()
    {
        var member = NewMember();

        var added = await _repository.AddAsync(member);
        var found = await _repository.GetByIdAsync(member.Id);

        Assert.True(added);
        Assert.Same(member, found);
    }

    [Fact]
    public async Task AddAsync_SameIdTwice_ShouldReturnFalse()
    {
        var member = NewMember();
        await _repository.AddAsync(member);

        var addedAgain = await _repository.AddAsync(member);

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
        await _repository.AddAsync(NewMember("Ana"));
        await _repository.AddAsync(NewMember("Maria"));

        var snapshot = await _repository.GetAllAsync();
        await _repository.AddAsync(NewMember("Ioana"));

        // The list we already received does not change when the store changes.
        Assert.Equal(2, snapshot.Count);
        Assert.Equal(3, (await _repository.GetAllAsync()).Count);
    }

    [Fact]
    public async Task Update_ExistingMember_ShouldKeepTheChanges()
    {
        var member = NewMember();
        await _repository.AddAsync(member);

        member.Update("Maria", "maria@gmail.com");
        _repository.Update(member);

        var found = await _repository.GetByIdAsync(member.Id);
        Assert.Equal("Maria", found!.Name);
    }

    [Fact]
    public async Task Update_UnknownMember_ShouldNotAddIt()
    {
        var member = NewMember();

        _repository.Update(member);

        Assert.Null(await _repository.GetByIdAsync(member.Id));
        Assert.Empty(await _repository.GetAllAsync());
    }

    [Fact]
    public async Task Delete_ExistingMember_ShouldRemoveIt()
    {
        var member = NewMember();
        await _repository.AddAsync(member);

        _repository.Delete(member);

        Assert.Null(await _repository.GetByIdAsync(member.Id));
    }
}

