namespace CampusMarket.Api.Features.Members;

public static class MembersEndpoints
{
    public static IEndpointRouteBuilder MapMembersEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/members");   // => /api/v1/members

        // Fiecare slice isi adauga aici apelul propriu, de exemplu:
        // CreateMember.Endpoint.Map(group);

        return app;
    }
}