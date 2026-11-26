using CampusMarket.Api.Common.CQRS;

namespace CampusMarket.Api.Features.Views.RecordView;

// MemberId comes from the token (null when not logged in); VisitorId from the X-Visitor-Id header.
// The result says whether the view was counted (false: the seller, a Draft/Hidden listing, or already counted today).
public sealed record RecordViewCommand(Guid ListingId, Guid? MemberId, Guid? VisitorId) : ICommand<bool>;
