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

    // Replaces the stored password hash. The handler validates the plain
    // password first (length, confirmation), so the domain only takes the hash.
    public void ChangePassword(string newPasswordHash)
    {
        PasswordHash = newPasswordHash;
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
}