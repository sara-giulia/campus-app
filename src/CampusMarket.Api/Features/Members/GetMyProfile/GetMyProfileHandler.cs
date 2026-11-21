using CampusMarket.Api.Common.CQRS;
using CampusMarket.Api.Domain.Common;
using CampusMarket.Api.Domain.Members;

namespace CampusMarket.Api.Features.Members.GetMyProfile;

public sealed class GetMyProfileHandler(IMemberRepository repository)
    : IQueryHandler<GetMyProfileQuery, GetMyProfileResponse>
{
    public async Task<Result<GetMyProfileResponse>> HandleAsync(GetMyProfileQuery query, CancellationToken ct)
    {
        var member = await repository.GetByIdAsync(query.MemberId, ct);

        if (member is null)
            return Result<GetMyProfileResponse>.Failure(
                Error.NotFound("Member.NotFound", $"Member '{query.MemberId}' was not found."));

        return GetMyProfileResponse.From(member);
    }
}