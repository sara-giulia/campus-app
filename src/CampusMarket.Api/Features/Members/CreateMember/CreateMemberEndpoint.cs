using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Members;
using Microsoft.AspNetCore.Http.HttpResults;

namespace CampusMarket.Api.Features.Members.CreateMember;

public static class CreateMemberEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapPost("/", Handle)
            .WithName("CreateMember");
    }

    private static async Task<Results<CreatedAtRoute<CreateMemberResponse>, BadRequest<Error>, Conflict<Error>>> Handle(
        CreateMemberRequest request,
        IMemberRepository repository,
        TimeProvider timeProvider,
        CancellationToken ct)
    {
        // 1. The domain checks all the rules (name length, email format).
        var result = Member.Create(request.Name, request.Email, timeProvider.GetUtcNow());

        if (result.IsFailure)
            return TypedResults.BadRequest(result.Error);

        // 2. The repository rejects a duplicate Id.
        var member = result.Value;
        var added = await repository.AddAsync(member, ct);

        if (!added)
            return TypedResults.Conflict(
                Error.Conflict("Member.Duplicate", $"A member with id '{member.Id}' already exists."));

        // 3. 201 Created, with a Location header pointing to GetMemberById.
        return TypedResults.CreatedAtRoute(
            CreateMemberResponse.From(member),
            "GetMemberById",
            new { id = member.Id });
    }
}
