using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Members;
using Microsoft.AspNetCore.Http.HttpResults;

namespace CampusMarket.Api.Features.Members.DeleteMember;

public static class DeleteMemberEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapDelete("/{id:guid}", Handle)
            .WithName("DeleteMember");
    }

    // No response record: a successful delete answers 204 No Content, with an empty body.
    private static async Task<Results<NoContent, NotFound<Error>>> Handle(
        [AsParameters] DeleteMemberRequest request,
        IMemberRepository repository,
        CancellationToken ct)
    {
        var member = await repository.GetByIdAsync(request.Id, ct);

        if (member is null)
            return TypedResults.NotFound(
                Error.NotFound("Member.NotFound", $"Member '{request.Id}' was not found."));

        repository.Delete(member);

        return TypedResults.NoContent();
    }
}

