namespace WorkClock.Api.Services;

public interface ITimeService
{
    /// <summary>
    /// Returns the current Zurich local time, sourced from the external time API.
    /// The returned <see cref="DateTime"/> has <see cref="DateTimeKind.Utc"/> set so
    /// ASP.NET Core serializes it with a trailing 'Z' (preventing browser offset math).
    /// Throws <see cref="WorkClock.Api.Exceptions.TimeServiceException"/> on failure.
    /// </summary>
    Task<DateTime> GetNowAsync();
}
