using CampusMarket.Api.Common.CQRS;
using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Members;

namespace CampusMarket.Api.Features.Members.GetMembers;

public sealed class GetMembersHandler(IMemberRepository repository)
    : IQueryHandler<GetMembersQuery, GetMembersResponse>
{
    public async Task<Result<GetMembersResponse>> HandleAsync(
        GetMembersQuery query, CancellationToken ct)
    {
        var members = await repository.GetAllAsync(ct);

        var items = members
            .Select(GetMembersItem.From)
            .ToList();

        return new GetMembersResponse(items, items.Count);
    }
}
