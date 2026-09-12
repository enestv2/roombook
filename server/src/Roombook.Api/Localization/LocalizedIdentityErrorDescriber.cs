using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Localization;

namespace Roombook.Api.Localization;

public sealed class LocalizedIdentityErrorDescriber(IStringLocalizer<ApiMessages> messages) : IdentityErrorDescriber
{
    private IdentityError Error(string code, string key) => new() { Code = code, Description = messages[key].Value };

    public override IdentityError DuplicateEmail(string email) => Error("DuplicateEmail", "identity.duplicate_email");
    public override IdentityError DuplicateUserName(string userName) => Error("DuplicateUserName", "identity.duplicate_username");
    public override IdentityError InvalidEmail(string? email) => Error("InvalidEmail", "identity.invalid_email");
    public override IdentityError PasswordTooShort(int length) => Error("PasswordTooShort", "identity.password_too_short");
    public override IdentityError PasswordRequiresDigit() => Error("PasswordRequiresDigit", "identity.password_requires_digit");
    public override IdentityError PasswordRequiresLower() => Error("PasswordRequiresLower", "identity.password_requires_lower");
    public override IdentityError PasswordRequiresUpper() => Error("PasswordRequiresUpper", "identity.password_requires_upper");
    public override IdentityError PasswordRequiresNonAlphanumeric() => Error("PasswordRequiresNonAlphanumeric", "identity.password_requires_symbol");
    public override IdentityError InvalidToken() => Error("InvalidToken", "identity.invalid_token");
}
