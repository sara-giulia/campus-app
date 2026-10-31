using CampusMarket.Api.Common.CQRS;

namespace CampusMarket.Api.Features.Members.GetMyReservations;

public sealed record GetMyReservationsQuery(Guid MemberId) : IQuery<GetMyReservationsResponse>;
