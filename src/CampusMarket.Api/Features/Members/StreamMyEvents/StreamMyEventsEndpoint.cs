using System.Net.ServerSentEvents;
using System.Security.Claims;
using CampusMarket.Api.Common.Auth;
using CampusMarket.Api.Common.Events;
using Microsoft.AspNetCore.Http.HttpResults;

namespace CampusMarket.Api.Features.Members.StreamMyEvents;

// GET /api/v1/members/me/events: Server-Sent Events for the logged-in member. The connection stays
// open and the server writes "event: message", "event: notification", "event: read" or "event: ping" lines on it;
// the browser reconnects when it drops. One way only (server to browser), plain HTTP, so no
// WebSocket or SignalR is needed. The token goes in the Authorization header like everywhere else
// (the frontend reads the stream with fetch, because EventSource cannot send headers).
public static class StreamMyEventsEndpoint
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapGet("/me/events", Handle)
            .WithName("StreamMyEvents")
            .RequireAuthorization();
    }

    // The SSE event name is the event type, so the browser can tell them apart before reading the data.
    private static ServerSentEventsResult<LiveEvent> Handle(
        ClaimsPrincipal user,
        ILiveEvents liveEvents,
        CancellationToken ct) =>
        TypedResults.ServerSentEvents(
            liveEvents.SubscribeAsync(user.GetMemberId(), ct)
                .Select(liveEvent => new SseItem<LiveEvent>(liveEvent, liveEvent.Type)));
}
