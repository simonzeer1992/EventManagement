using EventManagement.Api;
namespace EventManagement.Tests;

public class EventServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
    private readonly EventService service = new(new FixedClock());
    private static EventRequest CreateEventRequest(int capacity = 2) => new(" Team meetup ", " A gathering ", Now.AddDays(1), capacity);
    private static RegistrationRequest CreateRegistrationRequest(string email = "alex@example.com") => new("Alex", "Smith", email);

    [Fact]
    public void CreateAndUpdateEventPreserveIdentityAndNormalizeValues()
    {
        var created = service.CreateEvent(CreateEventRequest());
        Assert.NotEqual(Guid.Empty, created.Id);
        Assert.Equal("Team meetup", created.Title);
        var updated = service.UpdateEvent(created.Id, CreateEventRequest(5) with { Title = "Updated", Date = Now.AddDays(2).ToOffset(TimeSpan.FromHours(3)) });
        Assert.Equal(created.Id, updated.Id);
        Assert.Equal("Updated", service.GetEvent(created.Id).Title);
        Assert.Equal(TimeSpan.Zero, updated.Date.Offset);
        Assert.Single(service.GetEvents());
    }

    [Fact]
    public void RegisterAndUnregisterAttendeeUpdatesCapacity()
    {
        var item = service.CreateEvent(CreateEventRequest(1));
        var attendee = service.RegisterAttendee(item.Id, CreateRegistrationRequest());
        Assert.NotEqual(Guid.Empty, attendee.Id);
        Assert.Equal(1, service.GetEvent(item.Id).RegistrationCount);
        Assert.Equal(attendee, Assert.Single(service.GetRegistrations(item.Id)));
        service.UnregisterAttendee(item.Id, attendee.Id);
        service.RegisterAttendee(item.Id, CreateRegistrationRequest());
        Assert.Equal(1, service.GetEvent(item.Id).RegistrationCount);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    public void CannotRegisterAtOrBeforeCurrentTime(int minutes)
    {
        var item = service.CreateEvent(CreateEventRequest() with { Date = Now.AddMinutes(minutes) });
        AssertStatus(409, () => service.RegisterAttendee(item.Id, CreateRegistrationRequest()));
    }

    [Fact]
    public void CannotRegisterTwiceOrExceedCapacity()
    {
        var item = service.CreateEvent(CreateEventRequest(1));
        service.RegisterAttendee(item.Id, CreateRegistrationRequest());
        AssertStatus(409, () => service.RegisterAttendee(item.Id, CreateRegistrationRequest()));
        AssertStatus(409, () => service.RegisterAttendee(item.Id, CreateRegistrationRequest("other@example.com")));
        Assert.Single(service.GetRegistrations(item.Id));
    }

    [Fact]
    public void DuplicateEmailIgnoresCaseSpacesAndChangedNames()
    {
        var item = service.CreateEvent(CreateEventRequest());
        var registered = service.RegisterAttendee(item.Id, new(" Alex ", " Smith ", " Alex@Example.COM "));
        Assert.Equal("Alex", registered.FirstName);
        Assert.Equal("Smith", registered.LastName);
        Assert.Equal("alex@example.com", registered.Email);
        AssertStatus(409, () => service.RegisterAttendee(item.Id, new("Different", "Name", "ALEX@example.com")));
        Assert.Single(service.GetRegistrations(item.Id));
    }

    [Fact]
    public void SameEmailCanRegisterForDifferentEvents()
    {
        var first = service.CreateEvent(CreateEventRequest());
        var second = service.CreateEvent(CreateEventRequest());
        service.RegisterAttendee(first.Id, CreateRegistrationRequest());
        service.RegisterAttendee(second.Id, CreateRegistrationRequest());
        Assert.Single(service.GetRegistrations(first.Id));
        Assert.Single(service.GetRegistrations(second.Id));
    }

    [Fact]
    public void DifferentEmailsCanRegisterWithSameName()
    {
        var item = service.CreateEvent(CreateEventRequest());
        service.RegisterAttendee(item.Id, CreateRegistrationRequest());
        service.RegisterAttendee(item.Id, CreateRegistrationRequest("other@example.com"));
        Assert.Equal(2, service.GetEvent(item.Id).RegistrationCount);
    }

    [Fact]
    public void EventsWithIdenticalDetailsHaveSeparateIdsAndRegistrations()
    {
        var first = service.CreateEvent(CreateEventRequest());
        var second = service.CreateEvent(CreateEventRequest());
        Assert.NotEqual(first.Id, second.Id);
        service.RegisterAttendee(first.Id, CreateRegistrationRequest());
        Assert.Single(service.GetRegistrations(first.Id));
        Assert.Empty(service.GetRegistrations(second.Id));
        Assert.Equal(2, service.GetEvents().Length);
    }

    [Fact]
    public void UpdateAllowsMatchingAnotherEventsTitleAndTime()
    {
        var first = service.CreateEvent(CreateEventRequest());
        var second = service.CreateEvent(CreateEventRequest() with { Title = "Different event" });
        service.UpdateEvent(first.Id, CreateEventRequest());
        var updated = service.UpdateEvent(second.Id, CreateEventRequest());
        Assert.Equal(first.Title, updated.Title);
        Assert.Equal(first.Date, updated.Date);
        Assert.NotEqual(first.Id, updated.Id);
    }

    [Fact]
    public void CannotReduceCapacityBelowRegistrationCount()
    {
        var item = service.CreateEvent(CreateEventRequest());
        service.RegisterAttendee(item.Id, CreateRegistrationRequest());
        service.RegisterAttendee(item.Id, CreateRegistrationRequest("other@example.com"));
        AssertStatus(409, () => service.UpdateEvent(item.Id, CreateEventRequest(1)));
        Assert.Equal(2, service.GetEvent(item.Id).MaxCapacity);
    }

    [Fact]
    public void DeleteRemovesEventAndRegistrationsAndAllowsRecreation()
    {
        var item = service.CreateEvent(CreateEventRequest());
        service.RegisterAttendee(item.Id, CreateRegistrationRequest());
        service.DeleteEvent(item.Id);
        Assert.Empty(service.GetEvents());
        AssertStatus(404, () => service.GetEvent(item.Id));
        AssertStatus(404, () => service.GetRegistrations(item.Id));
        service.CreateEvent(CreateEventRequest());
    }

    [Fact]
    public void MissingResourcesReturnNotFound()
    {
        var missing = Guid.NewGuid();
        AssertStatus(404, () => service.GetEvent(missing));
        AssertStatus(404, () => service.UpdateEvent(missing, CreateEventRequest()));
        AssertStatus(404, () => service.DeleteEvent(missing));
        AssertStatus(404, () => service.RegisterAttendee(missing, CreateRegistrationRequest()));
        var item = service.CreateEvent(CreateEventRequest());
        AssertStatus(404, () => service.UnregisterAttendee(item.Id, Guid.NewGuid()));
    }

    [Fact]
    public void InvalidEventInputsAreRejected()
    {
        foreach (var invalid in new[] { CreateEventRequest(0), CreateEventRequest(-1), CreateEventRequest() with { Title = " " },
            CreateEventRequest() with { Title = new string('x', 201) }, CreateEventRequest() with { Description = " " },
            CreateEventRequest() with { Description = new string('x', 4001) }, CreateEventRequest() with { Date = default } })
            AssertStatus(400, () => service.CreateEvent(invalid));
    }

    [Theory]
    [InlineData("", "Smith", "alex@example.com")]
    [InlineData("Alex", " ", "alex@example.com")]
    [InlineData("Alex", "Smith", "")]
    [InlineData("Alex", "Smith", "not-an-email")]
    [InlineData(null, "Smith", "alex@example.com")]
    [InlineData("Alex", null, "alex@example.com")]
    [InlineData("Alex", "Smith", null)]
    public void InvalidAttendeeDetailsAreRejected(string? firstName, string? lastName, string? email)
    {
        var item = service.CreateEvent(CreateEventRequest());
        AssertStatus(400, () => service.RegisterAttendee(item.Id, new(firstName!, lastName!, email!)));
        Assert.Empty(service.GetRegistrations(item.Id));
    }

    [Fact]
    public void OverlongAttendeeDetailsAreRejected()
    {
        var item = service.CreateEvent(CreateEventRequest());
        foreach (var invalid in new[] { CreateRegistrationRequest() with { FirstName = new string('x', 101) },
            CreateRegistrationRequest() with { LastName = new string('x', 101) }, CreateRegistrationRequest(new string('x', 250) + "@example.com") })
            AssertStatus(400, () => service.RegisterAttendee(item.Id, invalid));
    }

    [Fact]
    public void ConcurrentRegistrationsCannotOverbookLastPlace()
    {
        var item = service.CreateEvent(CreateEventRequest(1));
        Parallel.For(0, 100, index =>
        {
            try { service.RegisterAttendee(item.Id, CreateRegistrationRequest($"person{index}@example.com")); }
            catch (EventException error) { Assert.Equal(409, error.StatusCode); }
        });
        Assert.Single(service.GetRegistrations(item.Id));
        Assert.Equal(1, service.GetEvent(item.Id).RegistrationCount);
    }

    [Fact]
    public void ConcurrentRegistrationsForSameEmailOnlyCreateOneRegistration()
    {
        var item = service.CreateEvent(CreateEventRequest(100));
        Parallel.For(0, 100, index =>
        {
            try { service.RegisterAttendee(item.Id, CreateRegistrationRequest(index % 2 == 0 ? "ALEX@example.com" : " alex@example.com ")); }
            catch (EventException error) { Assert.Equal(409, error.StatusCode); }
        });
        Assert.Single(service.GetRegistrations(item.Id));
    }

    [Fact]
    public void RegistrationClosesWhenClockReachesEventStart()
    {
        var clock = new FixedClock();
        var timedService = new EventService(clock);
        var item = timedService.CreateEvent(CreateEventRequest());
        clock.UtcNow = item.Date;

        AssertStatus(409, () => timedService.RegisterAttendee(item.Id, CreateRegistrationRequest()));
        Assert.Empty(timedService.GetRegistrations(item.Id));
        Assert.Equal(0, timedService.GetEvent(item.Id).RegistrationCount);
    }

    [Fact]
    public void DuplicateRegistrationDoesNotConsumeAnotherPlace()
    {
        var item = service.CreateEvent(CreateEventRequest(2));
        service.RegisterAttendee(item.Id, CreateRegistrationRequest());
        AssertStatus(409, () => service.RegisterAttendee(item.Id, CreateRegistrationRequest(" ALEX@EXAMPLE.COM ")));

        service.RegisterAttendee(item.Id, CreateRegistrationRequest("second@example.com"));
        Assert.Equal(2, service.GetEvent(item.Id).RegistrationCount);
        Assert.Equal(2, service.GetRegistrations(item.Id).Length);
    }

    [Fact]
    public void InvalidUpdateLeavesExistingEventAndRegistrationsUnchanged()
    {
        var item = service.CreateEvent(CreateEventRequest());
        var attendee = service.RegisterAttendee(item.Id, CreateRegistrationRequest());
        var beforeUpdate = service.GetEvent(item.Id);

        AssertStatus(400, () => service.UpdateEvent(item.Id, CreateEventRequest() with { Title = " " }));

        Assert.Equal(beforeUpdate, service.GetEvent(item.Id));
        Assert.Equal(attendee, Assert.Single(service.GetRegistrations(item.Id)));
    }

    [Fact]
    public void CapacityCanEqualRegistrationCountAndBeIncreased()
    {
        var item = service.CreateEvent(CreateEventRequest(3));
        service.RegisterAttendee(item.Id, CreateRegistrationRequest());
        service.UpdateEvent(item.Id, CreateEventRequest(1));
        AssertStatus(409, () => service.RegisterAttendee(item.Id, CreateRegistrationRequest("second@example.com")));

        service.UpdateEvent(item.Id, CreateEventRequest(2));
        service.RegisterAttendee(item.Id, CreateRegistrationRequest("second@example.com"));
        Assert.Equal(2, service.GetEvent(item.Id).RegistrationCount);
    }

    [Fact]
    public void UnregisteringTwiceDoesNotReduceRegistrationCountBelowZero()
    {
        var item = service.CreateEvent(CreateEventRequest());
        var attendee = service.RegisterAttendee(item.Id, CreateRegistrationRequest());
        service.UnregisterAttendee(item.Id, attendee.Id);

        AssertStatus(404, () => service.UnregisterAttendee(item.Id, attendee.Id));
        Assert.Equal(0, service.GetEvent(item.Id).RegistrationCount);
        Assert.Empty(service.GetRegistrations(item.Id));
    }

    [Fact]
    public void AttendeesCanUnregisterAfterEventStarts()
    {
        var clock = new FixedClock();
        var timedService = new EventService(clock);
        var item = timedService.CreateEvent(CreateEventRequest());
        var attendee = timedService.RegisterAttendee(item.Id, CreateRegistrationRequest());
        clock.UtcNow = item.Date.AddHours(1);

        timedService.UnregisterAttendee(item.Id, attendee.Id);
        Assert.Empty(timedService.GetRegistrations(item.Id));
        Assert.Equal(0, timedService.GetEvent(item.Id).RegistrationCount);
    }

    private static void AssertStatus(int status, Action action) =>
        Assert.Equal(status, Assert.Throws<EventException>(action).StatusCode);

    private class FixedClock : TimeProvider
    {
        public DateTimeOffset UtcNow { get; set; } = Now;
        public override DateTimeOffset GetUtcNow() => UtcNow;
    }
}
