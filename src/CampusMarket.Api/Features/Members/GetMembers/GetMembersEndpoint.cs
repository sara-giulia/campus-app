using CampusMarket.Api.Domain.Members;
using Microsoft.AspNetCore.Http.HttpResults;

namespace CampusMarket.Api.Features.Members.GetMembers;

public static class GetMembersEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapGet("/", Handle)
            .WithName("GetMembers");
    }

    // No request record: this slice has no input.
    private static async Task<Ok<GetMembersResponse>> Handle(
        IMemberRepository repository,
        CancellationToken ct)
    {
        var members = await repository.GetAllAsync(ct);

        var items = members
            .Select(GetMembersItem.From)
            .ToList();

        return TypedResults.Ok(new GetMembersResponse(items, items.Count));
    }
}

