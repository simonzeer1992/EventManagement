using Microsoft.AspNetCore.Mvc;
namespace EventManagement.Api;

[ApiController]
[Route("api/events")]
public class EventsController(EventService service) : ControllerBase
{
    [HttpGet]
    public ActionResult<EventResponse[]> GetEvents() => service.GetEvents();

    [HttpGet("{eventId:guid}")]
    public ActionResult<EventResponse> GetEvent(Guid eventId) => service.GetEvent(eventId);

    [HttpPost]
    public ActionResult<EventResponse> CreateEvent(EventRequest request)
    {
        var item = service.CreateEvent(request);
        return CreatedAtAction(nameof(GetEvent), new { eventId = item.Id }, item);
    }

    [HttpPut("{eventId:guid}")]
    public ActionResult<EventResponse> UpdateEvent(Guid eventId, EventRequest request) => service.UpdateEvent(eventId, request);

    [HttpDelete("{eventId:guid}")]
    public IActionResult DeleteEvent(Guid eventId)
    {
        service.DeleteEvent(eventId);
        return NoContent();
    }

    [HttpGet("{eventId:guid}/registrations")]
    public ActionResult<Guid[]> GetRegistrations(Guid eventId) => service.GetRegistrations(eventId);

    [HttpPost("{eventId:guid}/registrations")]
    public IActionResult RegisterAttendee(Guid eventId, RegistrationRequest request)
    {
        service.RegisterAttendee(eventId, request.AttendeeId);
        return StatusCode(201);
    }

    [HttpDelete("{eventId:guid}/registrations/{attendeeId:guid}")]
    public IActionResult UnregisterAttendee(Guid eventId, Guid attendeeId)
    {
        service.UnregisterAttendee(eventId, attendeeId);
        return NoContent();
    }
}
