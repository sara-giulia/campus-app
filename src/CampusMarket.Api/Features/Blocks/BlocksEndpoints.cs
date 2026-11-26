using CampusMarket.Api.Features.Blocks.BlockMember;
using CampusMarket.Api.Features.Blocks.GetMyBlocked;
using CampusMarket.Api.Features.Blocks.UnblockMember;

namespace CampusMarket.Api.Features.Blocks;

public static class BlocksEndpoints
{
    // Under /members, next to the member routes: POST and DELETE /members/{id}/block, GET /members/me/blocked.
    public static IEndpointRouteBuilder MapBlocksEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/members");

        BlockMemberEndpoint.Map(group);
        UnblockMemberEndpoint.Map(group);
        GetMyBlockedEndpoint.Map(group);

        return app;
    }
}
