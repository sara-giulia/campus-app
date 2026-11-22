namespace CampusMarket.Api.Common.Categories;

// Below MinConfidence the category suggestion is not shown (category null). The value is chosen on
// the validation data when the model is trained (CategoryTrainer, "settings" in
// ml/models/category-classification.json) and configured in appsettings.json as Ml:Category:MinConfidence.
public sealed class CategorySuggestionOptions
{
    public const string SectionName = "Ml:Category";

    public double MinConfidence { get; set; } = 0.6;
}
