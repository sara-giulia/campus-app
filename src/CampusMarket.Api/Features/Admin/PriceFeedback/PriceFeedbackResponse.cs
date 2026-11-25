namespace CampusMarket.Api.Features.Admin.PriceFeedback;

// RowCount = data rows in Csv (header not counted). Csv always has the header,
// so an empty database still exports a valid (header-only) file.
public sealed record PriceFeedbackResponse(int RowCount, string Csv);
