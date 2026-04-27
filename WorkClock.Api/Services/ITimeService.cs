namespace WorkClock.Api.Services;

public interface ITimeService
{
    /// <summary>
    /// Returns the current UTC time, sourced from the external time API.
    /// Throws <see cref="WorkClock.Api.Exceptions.TimeServiceException"/> on failure.
    /// </summary>
    Task<DateTime> GetUtcNowAsync();
}
