namespace GolfProgress.Api.ErrorHandling;

/// <summary>
/// Something the caller asked for does not exist, or is not theirs -- deliberately
/// indistinguishable, like the ownership checks. Mapped to 404 by
/// <see cref="ProblemDetailsExceptionHandler"/> (#7).
///
/// Use this rather than InvalidOperationException, which EF and the framework also
/// throw for genuine faults; mapping that to 404 would hide real errors.
/// </summary>
public sealed class NotFoundException : Exception
{
    public NotFoundException(string message) : base(message) { }
}
