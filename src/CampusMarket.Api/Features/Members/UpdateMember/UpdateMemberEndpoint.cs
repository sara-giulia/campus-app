using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Members;
using Microsoft.AspNetCore.Http.HttpResults;

namespace CampusMarket.Api.Features.Members.UpdateMember;

public static class UpdateMemberEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapPut("/{id:guid}", Handle)
            .WithName("UpdateMember");
    }

    // No response record: a successful update answers 204 No Content, with an empty body.
    private static async Task<Results<NoContent, NotFound<Error>, BadRequest<Error>>> Handle(
        Guid id,
        UpdateMemberRequest request,
        IMemberRepository repository,
        CancellationToken ct)
    {
        // 1. Load it; 404 if it does not exist.
        var member = await repository.GetByIdAsync(id, ct);

        if (member is null)
            return TypedResults.NotFound(
                Error.NotFound("Member.NotFound", $"Member '{id}' was not found."));

        // 2. The domain checks the rules (name length, email format).
        var result = member.Update(request.Name, request.Email);

        if (result.IsFailure)
            return TypedResults.BadRequest(result.Error);

        // 3. Save and answer 204.
        repository.Update(member);

        return TypedResults.NoContent();
    }
}

