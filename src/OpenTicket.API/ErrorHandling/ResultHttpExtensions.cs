using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

using OpenTicket.Domain.Common;

namespace OpenTicket.API.ErrorHandling;

/// <summary>The single place where expected failures become HTTP ProblemDetails responses.</summary>
internal static class ResultHttpExtensions
{
    public const string CodeExtension = "code";

    public static Results<Ok<T>, ProblemHttpResult> ToOk<T>(this Result<T> result) =>
        result.IsSuccess ? TypedResults.Ok(result.Value) : result.Error!.ToProblem();

    public static ProblemHttpResult ToProblem(this Error error)
    {
        ProblemDetails problem = error.Type == ErrorType.Validation
            ? new HttpValidationProblemDetails(error.Fields?.ToDictionary() ?? [])
            : new ProblemDetails();

        problem.Status = error.Type switch
        {
            ErrorType.Validation => StatusCodes.Status400BadRequest,
            ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
            ErrorType.Forbidden => StatusCodes.Status403Forbidden,
            ErrorType.NotFound => StatusCodes.Status404NotFound,
            ErrorType.Conflict => StatusCodes.Status409Conflict,
            ErrorType.BusinessRule => StatusCodes.Status422UnprocessableEntity,
            _ => StatusCodes.Status500InternalServerError,
        };
        problem.Title = error.Message;
        problem.Extensions[CodeExtension] = error.Code;
        return TypedResults.Problem(problem);
    }
}
