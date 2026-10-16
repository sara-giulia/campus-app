using CampusMarket.Api.Domain.Common;

namespace CampusMarket.Api.Domain.Members;

public class Member
{
    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public Email Email { get; private set; } = null!;
    public DateTimeOffset CreatedAt { get; private set; }

    private Member() { }

    public static Result<Member> Create(string name, string email, DateTimeOffset createdAt)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length < 2 || name.Trim().Length > 100)
            return Result<Member>.Failure(Error.Validation("Member.InvalidName", "Name must have between 2 and 100 characters."));

        var emailResult = Email.Create(email);

        if (emailResult.IsFailure)
            return Result<Member>.Failure(emailResult.Error);

        return new Member
        {
            Id = Guid.NewGuid(),
            Name = name.Trim(),
            Email = emailResult.Value,
            CreatedAt = createdAt
        };
    }

    public Result<Member> Update(string name, string email)
    {
        if (string.IsNullOrWhiteSpace(name) ||
            name.Trim().Length < 2 ||
            name.Trim().Length > 100)
            return Result<Member>.Failure(
                Error.Validation("Member.InvalidName", "Name must have between 2 and 100 characters."));

        var emailResult = Email.Create(email);

        if (emailResult.IsFailure)
            return Result<Member>.Failure(emailResult.Error);

        Name = name.Trim();
        Email = emailResult.Value;

        return this;
    }
}