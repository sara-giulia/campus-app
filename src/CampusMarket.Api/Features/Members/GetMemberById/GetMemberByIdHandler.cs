using CampusMarket.Api.Common.CQRS;
using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Members;

namespace CampusMarket.Api.Features.Members.GetMemberById;

public sealed class GetMemberByIdHandler(IMemberRepository repository)
    : IQueryHandler<GetMemberByIdQuery, GetMemberByIdResponse>
{
    public async Task<Result<GetMemberByIdResponse>> HandleAsync(
        GetMemberByIdQuery query, CancellationToken ct)
    {
        var member = await repository.GetByIdAsync(query.Id, ct);

        if (member is null)
            return Result<GetMemberByIdResponse>.Failure(
                Error.NotFound("Member.NotFound", $"Member '{query.Id}' was not found."));

        return GetMemberByIdResponse.From(member);
    }
}
