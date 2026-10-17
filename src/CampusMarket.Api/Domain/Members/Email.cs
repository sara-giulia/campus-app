using CampusMarket.Api.Domain.Common;

namespace CampusMarket.Api.Domain.Members;

public sealed record Email
{
    public string Value { get; }

    private Email(string value)
    {
        Value = value;
    }

    public static Result<Email> Create(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return Result<Email>.Failure(Error.Validation("Email.Empty", "Email must not be empty."));

        var normalized = value.Trim().ToLowerInvariant();
        var atIndex = normalized.IndexOf('@');

        if (atIndex <= 0 || atIndex == normalized.Length - 1 || normalized.LastIndexOf('@') != atIndex)
            return Result<Email>.Failure(Error.Validation("Email.Invalid", "Email must contain one @ with text before and after it."));

        return new Email(normalized);
    }
}