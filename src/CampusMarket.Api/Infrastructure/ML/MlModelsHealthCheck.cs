using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace CampusMarket.Api.Infrastructure.ML;

// Ready only when the five trained models are next to the API.
public sealed class MlModelsHealthCheck(IOptions<MlOptions> options) : IHealthCheck
{
    public static readonly string[] ModelFiles =
        ["price-regression.zip", "category-classification.zip", "spam-detection.zip", "similar-listings.zip", "message-spam.zip"];

    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var folder = Path.Combine(AppContext.BaseDirectory, options.Value.ModelsPath);
        var missing = ModelFiles.Where(file => !File.Exists(Path.Combine(folder, file))).ToList();

        return Task.FromResult(missing.Count == 0
            ? HealthCheckResult.Healthy("All ML models are present.")
            : HealthCheckResult.Unhealthy("Missing ML models: " + string.Join(", ", missing)));
    }
}
