namespace WindowsCompanion.Models;

/// <summary>Severity of a message shown to the user.</summary>
public enum FeedbackType
{
    /// <summary>The operation succeeded.</summary>
    Success,

    /// <summary>Neutral information.</summary>
    Info,

    /// <summary>Point of attention, no failure.</summary>
    Warning,

    /// <summary>Failure the user must know about.</summary>
    Danger,
}

/// <summary>Expected failure returned by a service instead of an exception, or a feedback message.</summary>
/// <param name="Type">Severity shown to the user.</param>
/// <param name="Message">Title, already worded for the user.</param>
/// <param name="Description">Message shown under the title.</param>
public sealed record ResultError(FeedbackType Type, string Message, string? Description = null);

/// <summary>Empty value for a <see cref="Result{T}"/> that carries no data.</summary>
public sealed record Unit
{
    /// <summary>Gets the single instance.</summary>
    public static Unit Value { get; } = new();
}

/// <summary>Outcome of a service operation: data on success, a <see cref="ResultError"/> otherwise.</summary>
/// <typeparam name="T">Type of the data returned on success.</typeparam>
// CA1000: Result<T>.Success / Result<T>.Failure is the single result contract mandated by rules/errors.md.
#pragma warning disable CA1000
public readonly record struct Result<T>
{
    /// <summary>Gets a value indicating whether the operation succeeded.</summary>
    public bool Ok { get; private init; }

    /// <summary>Gets the data, set when <see cref="Ok"/> is true.</summary>
    public T? Data { get; private init; }

    /// <summary>Gets the error, set when <see cref="Ok"/> is false.</summary>
    public ResultError? Error { get; private init; }

    /// <summary>Builds a successful result.</summary>
    /// <param name="data">The returned data.</param>
    /// <returns>A result with <see cref="Ok"/> set.</returns>
    public static Result<T> Success(T data) => new() { Ok = true, Data = data };

    /// <summary>Builds a failed result.</summary>
    /// <param name="error">The error to report.</param>
    /// <returns>A result with <see cref="Error"/> set.</returns>
    public static Result<T> Failure(ResultError error) => new() { Ok = false, Error = error };
}
#pragma warning restore CA1000
