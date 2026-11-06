using System.ComponentModel.DataAnnotations;
namespace EventManagement.Api;

public record EventRequest(
    [Required, StringLength(200)] string Title,
    [Required, StringLength(4000)] string Description,
    DateTimeOffset Date,
    [Range(1, int.MaxValue)] int MaxCapacity);
public record RegistrationRequest(Guid AttendeeId);
public record EventResponse(Guid Id, string Title, string Description,
    DateTimeOffset Date, int MaxCapacity, int RegistrationCount);
public class EventException(int statusCode, string message) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
}
