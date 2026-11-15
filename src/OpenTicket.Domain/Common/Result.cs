namespace OpenTicket.Domain.Common;

public enum ErrorType
{
    Validation,
    Unauthorized,
    Forbidden,
    NotFound,
    Conflict,
    BusinessRule,
}

/// <summary>An expected failure. <see cref="Code"/> is stable and machine-readable (e.g. "ticket.not_found").</summary>
public sealed record Error(string Code, string Message, ErrorType Type)
{
    /// <summary>Field-level messages for <see cref="ErrorType.Validation"/> errors, keyed by camelCase field name.</summary>
    public IReadOnlyDictionary<string, string[]>? Fields { get; init; }

    public static Error Validation(string code, string message, IReadOnlyDictionary<string, string[]>? fields = null) =>
        new(code, message, ErrorType.Validation) { Fields = fields };

    public static Error NotFound(string code, string message) => new(code, message, ErrorType.NotFound);

    public static Error Conflict(string code, string message) => new(code, message, ErrorType.Conflict);

    public static Error Forbidden(string code, string message) => new(code, message, ErrorType.Forbidden);

    public static Error Unauthorized(string code, string message) => new(code, message, ErrorType.Unauthorized);

    public static Error BusinessRule(string code, string message) => new(code, message, ErrorType.BusinessRule);
}

/// <summary>Outcome of an operation that has no value on success.</summary>
public readonly record struct Result
{
    private Result(Error? error) => Error = error;

    public Error? Error { get; }

    public bool IsSuccess => Error is null;

    public static Result Success() => new(null);

    public static Result Failure(Error error) => new(error);

    public static implicit operator Result(Error error) => Failure(error);
}

/// <summary>Outcome of an operation that returns <typeparamref name="T"/> on success.</summary>
public readonly record struct Result<T>
{
    private Result(T? value, Error? error)
    {
        Value = value;
        Error = error;
    }

    public T? Value { get; }

    public Error? Error { get; }

    public bool IsSuccess => Error is null;

    public static Result<T> Success(T value) => new(value, null);

    public static Result<T> Failure(Error error) => new(default, error);

#pragma warning disable CA2225 // Named alternates (Success/Failure) exist.
    public static implicit operator Result<T>(T value) => Success(value);

    public static implicit operator Result<T>(Error error) => Failure(error);
#pragma warning restore CA2225
}
