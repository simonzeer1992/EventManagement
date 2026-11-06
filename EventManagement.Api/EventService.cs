using System.ComponentModel.DataAnnotations;

namespace EventManagement.Api;

/// <summary>Owns in-memory events and registrations for one running API process.</summary>
public class EventService(TimeProvider clock)
{
    private readonly object sync = new();
    private readonly Dictionary<Guid, EventResponse> events = new();
    private readonly Dictionary<Guid, Dictionary<Guid, RegistrationResponse>> registrations = new();

    public EventResponse[] GetEvents()
    {
        lock (sync) return events.Values.OrderBy(e => e.Date).ToArray();
    }

    public EventResponse GetEvent(Guid eventId)
    {
        lock (sync) return FindEvent(eventId);
    }

    public EventResponse CreateEvent(EventRequest request)
    {
        ValidateEvent(request);
        lock (sync)
        {
            var item = new EventResponse(Guid.NewGuid(), request.Title.Trim(),
                request.Description.Trim(), request.Date.ToUniversalTime(), request.MaxCapacity, 0);
            events.Add(item.Id, item);
            registrations.Add(item.Id, new Dictionary<Guid, RegistrationResponse>());
            return item;
        }
    }

    public EventResponse UpdateEvent(Guid eventId, EventRequest request)
    {
        ValidateEvent(request);
        lock (sync)
        {
            var existing = FindEvent(eventId);
            if (request.MaxCapacity < existing.RegistrationCount)
                throw new EventException(409, "Capacity cannot be lower than the registration count.");
            var updated = existing with
            {
                Title = request.Title.Trim(),
                Description = request.Description.Trim(),
                Date = request.Date.ToUniversalTime(),
                MaxCapacity = request.MaxCapacity
            };
            events[eventId] = updated;
            return updated;
        }
    }

    public void DeleteEvent(Guid eventId)
    {
        lock (sync)
        {
            FindEvent(eventId);
            events.Remove(eventId);
            registrations.Remove(eventId);
        }
    }

    public RegistrationResponse[] GetRegistrations(Guid eventId)
    {
        lock (sync)
        {
            FindEvent(eventId);
            return registrations[eventId].Values.OrderBy(attendee => attendee.LastName)
                .ThenBy(attendee => attendee.FirstName).ToArray();
        }
    }

    public RegistrationResponse RegisterAttendee(Guid eventId, RegistrationRequest request)
    {
        // Email identifies an attendee within an event; names are display information.
        var attendee = request with
        {
            FirstName = request.FirstName?.Trim() ?? "",
            LastName = request.LastName?.Trim() ?? "",
            Email = request.Email?.Trim().ToLowerInvariant() ?? ""
        };
        // Service validation also protects callers that do not go through MVC model validation.
        if (attendee.FirstName.Length is < 1 or > 100)
            throw new EventException(400, "A first name of up to 100 characters is required.");
        if (attendee.LastName.Length is < 1 or > 100)
            throw new EventException(400, "A last name of up to 100 characters is required.");
        if (attendee.Email.Length is < 1 or > 254 || !new EmailAddressAttribute().IsValid(attendee.Email))
            throw new EventException(400, "A valid email address is required.");

        // Keep checks and changes together so concurrent requests cannot overbook.
        lock (sync)
        {
            var item = FindEvent(eventId);
            if (item.Date <= clock.GetUtcNow())
                throw new EventException(409, "Registration is closed because the event has started.");
            if (registrations[eventId].Values.Any(existing => existing.Email == attendee.Email))
                throw new EventException(409, "This email is already registered for this event.");
            if (item.RegistrationCount >= item.MaxCapacity)
                throw new EventException(409, "This event is full.");

            var registration = new RegistrationResponse(
                Guid.NewGuid(), attendee.FirstName, attendee.LastName, attendee.Email);
            registrations[eventId].Add(registration.Id, registration);
            events[eventId] = item with { RegistrationCount = item.RegistrationCount + 1 };
            return registration;
        }
    }

    public void UnregisterAttendee(Guid eventId, Guid attendeeId)
    {
        lock (sync)
        {
            var item = FindEvent(eventId);
            if (!registrations[eventId].Remove(attendeeId))
                throw new EventException(404, "Registration was not found.");
            events[eventId] = item with { RegistrationCount = item.RegistrationCount - 1 };
        }
    }

    private EventResponse FindEvent(Guid eventId) => events.TryGetValue(eventId, out var item)
        ? item : throw new EventException(404, "Event was not found.");

    private static void ValidateEvent(EventRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Title) || request.Title.Length > 200)
            throw new EventException(400, "A title of up to 200 characters is required.");
        if (string.IsNullOrWhiteSpace(request.Description) || request.Description.Length > 4000)
            throw new EventException(400, "A description of up to 4000 characters is required.");
        if (request.Date == default)
            throw new EventException(400, "An event date is required.");
        if (request.MaxCapacity < 1)
            throw new EventException(400, "Capacity must be at least one.");
    }
}
