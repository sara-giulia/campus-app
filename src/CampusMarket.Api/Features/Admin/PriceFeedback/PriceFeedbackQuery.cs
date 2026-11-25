using CampusMarket.Api.Common.CQRS;

namespace CampusMarket.Api.Features.Admin.PriceFeedback;

public sealed record PriceFeedbackQuery : IQuery<PriceFeedbackResponse>
{
    public const string FileName = "feedback.csv";
    public const string ContentType = "text/csv";

    // Same header and column order as ml/data/listings.csv and ml/data/external.csv.
    public const string Header = "Title,Description,Category,Condition,Price,IsSpam,TemplateId";
    public const string TemplateId = "feedback";
}
