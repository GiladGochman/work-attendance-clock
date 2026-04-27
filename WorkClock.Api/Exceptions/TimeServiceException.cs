namespace WorkClock.Api.Exceptions;

/// <summary>
/// Thrown when the external time API is unreachable or returns an unparseable response.
/// Controllers catch this and return a 503 so the client can show a meaningful error.
/// </summary>
public class TimeServiceException : Exception
{
    public TimeServiceException(string message) : base(message) { }
    public TimeServiceException(string message, Exception innerException) : base(message, innerException) { }
}
