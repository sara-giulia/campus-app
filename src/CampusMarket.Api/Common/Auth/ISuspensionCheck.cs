namespace CampusMarket.Api.Common.Auth;

// Answers, on every authenticated request, whether the member of the token is suspended, so an access
// token issued before the suspension stops working at once instead of after it expires (15 minutes).
// The answer is cached for a short time (Infrastructure/Auth/CachedSuspensionCheck); suspending and
// lifting a suspension call Invalidate so the change applies to the very next request.
public interface ISuspensionCheck
{
    Task<bool> IsSuspendedAsync(Guid memberId, CancellationToken ct = default);

    void Invalidate();
}
