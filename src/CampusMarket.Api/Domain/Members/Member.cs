using CampusMarket.Api.Domain.Common;

namespace CampusMarket.Api.Domain.Members;

public sealed class Member
{
    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public Email Email { get; private set; } = null!;
    public DateTimeOffset CreatedAt { get; private set; }

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
}