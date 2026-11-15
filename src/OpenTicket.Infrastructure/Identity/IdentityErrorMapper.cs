using Microsoft.AspNetCore.Identity;

using OpenTicket.Application.Common;
using OpenTicket.Domain.Common;

namespace OpenTicket.Infrastructure.Identity;

internal static class IdentityErrorMapper
{
    private static readonly HashSet<string> DuplicateCodes = ["DuplicateEmail", "DuplicateUserName"];

    public static Error ToError(IdentityResult result, string passwordField = "password")
    {
        if (result.Errors.Any(e => DuplicateCodes.Contains(e.Code)))
        {
            return AuthErrors.EmailTaken;
        }

        var fields = result.Errors
            .GroupBy(e => e.Code.StartsWith("Password", StringComparison.Ordinal) ? passwordField : "email")
            .ToDictionary(g => g.Key, g => g.Select(e => e.Description).ToArray());
        return Error.Validation("auth.invalid_input", "The submitted details are not valid.", fields);
    }
}
