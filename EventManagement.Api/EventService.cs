namespace EventManagement.Api;

public class EventService(TimeProvider clock)
{
    private readonly object sync = new();
    private readonly Dictionary<Guid, EventResponse> events = new();
    private readonly Dictionary<Guid, HashSet<Guid>> registrations = new();

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
            registrations.Add(item.Id, new HashSet<Guid>());
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
            var updated = existing with { Title = request.Title.Trim(), Description = request.Description.Trim(),
                Date = request.Date.ToUniversalTime(), MaxCapacity = request.MaxCapacity };
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

    public Guid[] GetRegistrations(Guid eventId)
    {
        lock (sync)
        {
            FindEvent(eventId);
            return registrations[eventId].Order().ToArray();
        }
    }

    public void RegisterAttendee(Guid eventId, Guid attendeeId)
    {
        // Keep checks and changes together so concurrent requests cannot overbook.
        lock (sync)
        {
            var item = FindEvent(eventId);
            if (attendeeId == Guid.Empty) throw new EventException(400, "An attendee ID is required.");
            if (item.Date <= clock.GetUtcNow()) throw new EventException(409, "Registration is closed because the event has started.");
            if (registrations[eventId].Contains(attendeeId)) throw new EventException(409, "This attendee is already registered.");
            if (item.RegistrationCount >= item.MaxCapacity) throw new EventException(409, "This event is full.");
            registrations[eventId].Add(attendeeId);
            events[eventId] = item with { RegistrationCount = item.RegistrationCount + 1 };
        }
    }

    public void UnregisterAttendee(Guid eventId, Guid attendeeId)
    {
        lock (sync)
        {
            var item = FindEvent(eventId);
            if (!registrations[eventId].Remove(attendeeId)) throw new EventException(404, "Registration was not found.");
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
        if (request.Date == default) throw new EventException(400, "An event date is required.");
        if (request.MaxCapacity < 1) throw new EventException(400, "Capacity must be at least one.");
    }
}
