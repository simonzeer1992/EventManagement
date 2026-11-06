using System.ComponentModel.DataAnnotations;
namespace EventManagement.Api;

public record EventRequest(
    [Required, StringLength(200)] string Title,
    [Required, StringLength(4000)] string Description,
    DateTimeOffset Date,
    [Range(1, int.MaxValue)] int MaxCapacity);
public record RegistrationRequest(
    [Required, StringLength(100)] string FirstName,
    [Required, StringLength(100)] string LastName,
    [Required, StringLength(254)] string Email);
public record RegistrationResponse(Guid Id, string FirstName, string LastName, string Email);
public record EventResponse(Guid Id, string Title, string Description,
    DateTimeOffset Date, int MaxCapacity, int RegistrationCount);
/// <summary>An expected validation, missing-resource, or business-rule failure.</summary>
public class EventException(int statusCode, string message) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
}
