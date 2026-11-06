namespace CampusMarket.Api.Infrastructure.ErrorHandling;

public static class ErrorHandlingExtensions
{
    public static IServiceCollection AddCampusMarketErrorHandling(this IServiceCollection services)
    {
        services.AddProblemDetails(options =>
            options.CustomizeProblemDetails = context =>
            {
                context.ProblemDetails.Instance = context.HttpContext.Request.Path;
                context.ProblemDetails.Extensions.TryAdd("code", CodeFor(context.ProblemDetails.Status));
                context.ProblemDetails.Extensions.TryAdd("traceId", context.HttpContext.TraceIdentifier);
            });

        services.AddExceptionHandler<GlobalExceptionHandler>();

        return services;
    }

    // Errors written by ASP.NET itself (no token, Admin policy, unknown route, rate limit)
    // get a code in the same style as the handler errors.
    private static string CodeFor(int? status) => status switch
    {
        StatusCodes.Status400BadRequest => "Request.Invalid",
        StatusCodes.Status401Unauthorized => "Auth.Unauthorized",
        StatusCodes.Status403Forbidden => "Auth.Forbidden",
        StatusCodes.Status404NotFound => "Route.NotFound",
        StatusCodes.Status405MethodNotAllowed => "Request.MethodNotAllowed",
        StatusCodes.Status429TooManyRequests => "Request.TooManyRequests",
        _ => "Server.Error"
    };
}