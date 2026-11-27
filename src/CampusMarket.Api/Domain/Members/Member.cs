using System.Globalization;
using CampusMarket.Api.Domain.Common;

namespace CampusMarket.Api.Domain.Members;

public sealed class Member
{
    public Guid Id { get; private set; }
    // Stryker disable once String : the factory always sets Name, so the default value is never observable
    public string Name { get; private set; } = string.Empty;
    public Email Email { get; private set; } = null!;
    public DateTimeOffset CreatedAt { get; private set; }
    
    public string? PasswordHash { get; private set; }
    public MemberRole Role { get; private set; } = MemberRole.Student;

    public bool IsAdmin => Role == MemberRole.Admin;

    public const int MaxSuspensionReasonLength = 500;
    // A temporary suspension lasts at most this long; anything longer is a permanent one (no end date).
    public static readonly TimeSpan MaxSuspensionLength = TimeSpan.FromDays(365);

    // Set by an Admin (Features/Moderation/SuspendMember). SuspendedUntil null = permanent.
    // A temporary suspension ends by itself when SuspendedUntil passes (IsSuspended compares with the clock),
    // so no background job is needed; the fields stay as they were until the next suspension.
    public DateTimeOffset? SuspendedAt { get; private set; }
    public DateTimeOffset? SuspendedUntil { get; private set; }
    public string? SuspensionReason { get; private set; }
    public Guid? SuspendedById { get; private set; }

    public bool IsSuspended(DateTimeOffset now) =>
        SuspendedAt is not null && (SuspendedUntil is null || now < SuspendedUntil);

    private Member() { }

    
    private static Result<string> ValidateName(string? name)
    {
        const string errorMessage = "Name must have between 2 and 100 characters.";

        if (string.IsNullOrWhiteSpace(name))
            return Result<string>.Failure(
                Error.Validation("Member.InvalidName", errorMessage));

        var trimmedName = name.Trim();

        if (trimmedName.Length < 2 || trimmedName.Length > 100)
            return Result<string>.Failure(
                Error.Validation("Member.InvalidName", errorMessage));

        return trimmedName;
    }


    public static Result<Member> Create(string name, string email, DateTimeOffset createdAt)
    {
        var nameResult = ValidateName(name);

        if (nameResult.IsFailure)
            return Result<Member>.Failure(nameResult.Error);

        var emailResult = Email.Create(email);

        if (emailResult.IsFailure)
            return Result<Member>.Failure(emailResult.Error);

        return new Member
        {
            Id = Guid.NewGuid(),
            Name = nameResult.Value,
            Email = emailResult.Value,
            CreatedAt = createdAt
        };
    }
    
    public static Result<Member> Register(
        string name,
        string email,
        string passwordHash,
        DateTimeOffset createdAt,
        MemberRole role = MemberRole.Student)
    {
        if (string.IsNullOrWhiteSpace(passwordHash))
            return Result<Member>.Failure(
                Error.Validation("Member.PasswordRequired", "A password is required."));

        var created = Create(name, email, createdAt);

        if (created.IsFailure)
            return created;

        var member = created.Value;
        member.PasswordHash = passwordHash;
        member.Role = role;

        return member;
    }

    public Result<Member> Update(string name, string email)
    {
        var nameResult = ValidateName(name);

        if (nameResult.IsFailure)
            return Result<Member>.Failure(nameResult.Error);

        var emailResult = Email.Create(email);

        if (emailResult.IsFailure)
            return Result<Member>.Failure(emailResult.Error);

        Name = nameResult.Value;
        Email = emailResult.Value;

        return this;
    }

    // Replaces the password hash; the handler checks the current password and the rules first.
    public Result<Member> ChangePassword(string newPasswordHash)
    {
        if (string.IsNullOrWhiteSpace(newPasswordHash))
            return Result<Member>.Failure(
                Error.Validation("Member.PasswordRequired", "A password is required."));

        PasswordHash = newPasswordHash;
        return this;
    }

    // Only an Admin suspends (the endpoint needs the "Admin" policy). Always with a reason; until null = permanent,
    // otherwise a moment in the future, at most MaxSuspensionLength away. An Admin cannot be suspended.
    public Result<Member> Suspend(Guid adminId, string? reason, DateTimeOffset? until, DateTimeOffset now)
    {
        if (adminId == Id)
            return Result<Member>.Failure(
                Error.Forbidden("Suspension.Self", "You cannot suspend yourself."));

        if (IsAdmin)
            return Result<Member>.Failure(
                Error.Forbidden("Member.CannotSuspendAdmin", "An Admin cannot be suspended."));

        if (IsSuspended(now))
            return Result<Member>.Failure(
                Error.Conflict("Member.AlreadySuspended", "The member is already suspended. Lift the suspension first."));

        if (string.IsNullOrWhiteSpace(reason))
            return Result<Member>.Failure(
                Error.Validation("Suspension.ReasonEmpty", "Reason must not be empty."));
        if (reason.Trim().Length > MaxSuspensionReasonLength)
            return Result<Member>.Failure(
                Error.Validation("Suspension.ReasonTooLong", $"Reason must have at most {MaxSuspensionReasonLength} characters."));

        if (until is not null && until <= now)
            return Result<Member>.Failure(
                Error.Validation("Suspension.UntilInPast", "The end of the suspension must be in the future."));
        if (until is not null && until > now.Add(MaxSuspensionLength))
            return Result<Member>.Failure(
                Error.Validation("Suspension.TooLong",
                    $"A temporary suspension lasts at most {MaxSuspensionLength.TotalDays:0} days; leave the end empty for a permanent one."));

        SuspendedAt = now;
        SuspendedUntil = until;
        SuspensionReason = reason.Trim();
        SuspendedById = adminId;
        return this;
    }

    // An Admin ends the suspension before its date (or a permanent one). A suspension that already
    // ended by itself cannot be lifted.
    public Result<Member> LiftSuspension(DateTimeOffset now)
    {
        if (!IsSuspended(now))
            return Result<Member>.Failure(
                Error.Conflict("Member.NotSuspended", "The member is not suspended."));

        SuspendedAt = null;
        SuspendedUntil = null;
        SuspensionReason = null;
        SuspendedById = null;
        return this;
    }

    // The answer of login (403) and refresh (401) for a suspended member. The reason is not in it: it was
    // sent by email to the member only.
    public Error AccountSuspendedError() =>
        Error.Forbidden(AccountSuspendedCode, SuspendedUntil is { } until
            ? $"This account is suspended until {until.UtcDateTime.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)} UTC."
            : "This account is suspended permanently.");

    public const string AccountSuspendedCode = "Auth.AccountSuspended";
}
