using CampusMarket.Api.Features.Members.CreateMember;
using CampusMarket.Api.Features.Members.DeleteMember;
using CampusMarket.Api.Features.Members.GetMemberById;
using CampusMarket.Api.Features.Members.GetMembers;
using CampusMarket.Api.Features.Members.UpdateMember;
using CampusMarket.Api.Features.Members.GetMyReservations;
using CampusMarket.Api.Features.Members.GetMyListings;
using CampusMarket.Api.Features.Members.GetMyProfile;
using CampusMarket.Api.Features.Members.UpdateMyProfile;
using CampusMarket.Api.Features.Members.GetPublicProfile;

namespace CampusMarket.Api.Features.Members;

public static class MembersEndpoints
{
    public static IEndpointRouteBuilder MapMembersEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/members");   // => /api/v1/members

        CreateMemberEndpoint.Map(group);
        GetMemberByIdEndpoint.Map(group);
        GetMembersEndpoint.Map(group);
        UpdateMemberEndpoint.Map(group);
        DeleteMemberEndpoint.Map(group);
        GetMyReservationsEndpoint.Map(group);
        GetMyListingsEndpoint.Map(group);
        GetMyProfileEndpoint.Map(group);
        UpdateMyProfileEndpoint.Map(group);
        GetPublicProfileEndpoint.Map(group);

        return app;
    }
}