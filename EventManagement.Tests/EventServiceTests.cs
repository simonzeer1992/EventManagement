using EventManagement.Api;
namespace EventManagement.Tests;

public class EventServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
    private readonly EventService service = new(new FixedClock());
    private static EventRequest Event(int capacity = 2) => new(" Team meetup ", " A gathering ", Now.AddDays(1), capacity);

    [Fact]
    public void CreateAndUpdateEventPreserveIdentityAndNormalizeValues()
    {
        var created = service.CreateEvent(Event());
        Assert.NotEqual(Guid.Empty, created.Id);
        Assert.Equal("Team meetup", created.Title);
        var updated = service.UpdateEvent(created.Id, Event(5) with { Title = "Updated", Date = Now.AddDays(2).ToOffset(TimeSpan.FromHours(3)) });
        Assert.Equal(created.Id, updated.Id);
        Assert.Equal("Updated", service.GetEvent(created.Id).Title);
        Assert.Equal(TimeSpan.Zero, updated.Date.Offset);
        Assert.Single(service.GetEvents());
    }

    [Fact]
    public void RegisterAndUnregisterAttendeeUpdatesCapacity()
    {
        var item = service.CreateEvent(Event(1));
        var attendee = Guid.NewGuid();
        service.RegisterAttendee(item.Id, attendee);
        Assert.Equal(1, service.GetEvent(item.Id).RegistrationCount);
        Assert.Equal(attendee, Assert.Single(service.GetRegistrations(item.Id)));
        service.UnregisterAttendee(item.Id, attendee);
        service.RegisterAttendee(item.Id, Guid.NewGuid());
        Assert.Equal(1, service.GetEvent(item.Id).RegistrationCount);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    public void CannotRegisterAtOrBeforeCurrentTime(int minutes)
    {
        var item = service.CreateEvent(Event() with { Date = Now.AddMinutes(minutes) });
        AssertStatus(409, () => service.RegisterAttendee(item.Id, Guid.NewGuid()));
    }

    [Fact]
    public void CannotRegisterTwiceOrExceedCapacity()
    {
        var item = service.CreateEvent(Event(1));
        var attendee = Guid.NewGuid();
        service.RegisterAttendee(item.Id, attendee);
        AssertStatus(409, () => service.RegisterAttendee(item.Id, attendee));
        AssertStatus(409, () => service.RegisterAttendee(item.Id, Guid.NewGuid()));
        Assert.Single(service.GetRegistrations(item.Id));
    }

    [Fact]
    public void CannotReduceCapacityBelowRegistrationCount()
    {
        var item = service.CreateEvent(Event());
        service.RegisterAttendee(item.Id, Guid.NewGuid());
        service.RegisterAttendee(item.Id, Guid.NewGuid());
        AssertStatus(409, () => service.UpdateEvent(item.Id, Event(1)));
        Assert.Equal(2, service.GetEvent(item.Id).MaxCapacity);
    }

    [Fact]
    public void DeleteRemovesEventAndRegistrations()
    {
        var item = service.CreateEvent(Event());
        service.RegisterAttendee(item.Id, Guid.NewGuid());
        service.DeleteEvent(item.Id);
        Assert.Empty(service.GetEvents());
        AssertStatus(404, () => service.GetEvent(item.Id));
        AssertStatus(404, () => service.GetRegistrations(item.Id));
    }

    [Fact]
    public void MissingResourcesReturnNotFound()
    {
        var missing = Guid.NewGuid();
        AssertStatus(404, () => service.GetEvent(missing));
        AssertStatus(404, () => service.UpdateEvent(missing, Event()));
        AssertStatus(404, () => service.DeleteEvent(missing));
        AssertStatus(404, () => service.RegisterAttendee(missing, Guid.NewGuid()));
        var item = service.CreateEvent(Event());
        AssertStatus(404, () => service.UnregisterAttendee(item.Id, Guid.NewGuid()));
    }

    [Fact]
    public void InvalidInputsAreRejected()
    {
        foreach (var invalid in new[] { Event(0), Event(-1), Event() with { Title = " " },
            Event() with { Title = new string('x', 201) }, Event() with { Description = " " },
            Event() with { Description = new string('x', 4001) }, Event() with { Date = default } })
            AssertStatus(400, () => service.CreateEvent(invalid));
        var item = service.CreateEvent(Event());
        AssertStatus(400, () => service.RegisterAttendee(item.Id, Guid.Empty));
    }

    [Fact]
    public void ConcurrentRegistrationsCannotOverbookLastPlace()
    {
        var item = service.CreateEvent(Event(1));
        Parallel.For(0, 100, _ =>
        {
            try { service.RegisterAttendee(item.Id, Guid.NewGuid()); }
            catch (EventException error) { Assert.Equal(409, error.StatusCode); }
        });
        Assert.Single(service.GetRegistrations(item.Id));
        Assert.Equal(1, service.GetEvent(item.Id).RegistrationCount);
    }

    [Fact]
    public void ConcurrentRegistrationsForSameAttendeeOnlyCreateOneRegistration()
    {
        var item = service.CreateEvent(Event(100));
        var attendee = Guid.NewGuid();
        Parallel.For(0, 100, _ =>
        {
            try { service.RegisterAttendee(item.Id, attendee); }
            catch (EventException error) { Assert.Equal(409, error.StatusCode); }
        });
        Assert.Single(service.GetRegistrations(item.Id));
    }

    private static void AssertStatus(int status, Action action) =>
        Assert.Equal(status, Assert.Throws<EventException>(action).StatusCode);

    private class FixedClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => Now;
    }
}
