namespace CampusMarket.Api.Infrastructure.ML;

public sealed class MlOptions
{
    public const string SectionName = "Ml";

    public string ModelsPath { get; set; } = "ml-models";
}
