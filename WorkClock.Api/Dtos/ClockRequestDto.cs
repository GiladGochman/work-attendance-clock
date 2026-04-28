using System.ComponentModel.DataAnnotations;

namespace WorkClock.Api.Dtos;

public record ClockRequestDto
{
    [Required]
    [StringLength(100, MinimumLength = 1)]
    public string EmployeeId { get; init; } = string.Empty;
}
