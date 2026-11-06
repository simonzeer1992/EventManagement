import { useEffect, useState } from 'react';
import {
  request,
  type EventItem,
  type EventInput,
  type Registration,
  type RegistrationInput,
} from './api';
import EventForm from './EventForm';
import RegistrationForm from './RegistrationForm';

export default function App() {
  const [events, setEvents] = useState<EventItem[]>([]);
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [attendees, setAttendees] = useState<Registration[]>([]);
  const [attendeesLoading, setAttendeesLoading] = useState(false);
  // undefined closes the editor; null creates an event; an event value edits it.
  const [editing, setEditing] = useState<EventItem | null | undefined>(undefined);
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const [notice, setNotice] = useState('');
  const selected = events.find((event) => event.id === selectedId);

  async function loadEvents() {
    setEvents(await request<EventItem[]>(''));
  }
  useEffect(() => {
    loadEvents()
      .catch((error) => setError(error.message))
      .finally(() => setLoading(false));
  }, []);
  useEffect(() => {
    setAttendees([]);
    if (!selectedId) return;
    let active = true;
    setAttendeesLoading(true);
    request<Registration[]>(`/${selectedId}/registrations`)
      .then((attendees) => {
        if (active) setAttendees(attendees);
      })
      .catch((error) => {
        if (active) setError(error.message);
      })
      .finally(() => {
        if (active) setAttendeesLoading(false);
      });
    // A slower response for the previous selection must not replace this event's attendees.
    return () => {
      active = false;
    };
  }, [selectedId]);

  async function performAction(action: () => Promise<void>) {
    setBusy(true);
    setError('');
    setNotice('');
    try {
      await action();
      return true;
    } catch (error) {
      setError(error instanceof Error ? error.message : 'Something went wrong.');
      return false;
    } finally {
      setBusy(false);
    }
  }
  function saveEvent(input: EventInput) {
    void performAction(async () => {
      const saved = await request<EventItem>(
        editing ? `/${editing.id}` : '',
        editing ? 'PUT' : 'POST',
        input,
      );
      setEditing(undefined);
      await loadEvents();
      setSelectedId(saved.id);
      setNotice('Event saved.');
    });
  }
  function deleteEvent() {
    if (!selected || !confirm(`Delete "${selected.title}" and all its registrations?`)) return;
    void performAction(async () => {
      await request(`/${selected.id}`, 'DELETE');
      setSelectedId(null);
      await loadEvents();
      setNotice('Event deleted.');
    });
  }
  async function refreshEvents(eventId = selectedId) {
    await loadEvents();
    if (eventId) setAttendees(await request<Registration[]>(`/${eventId}/registrations`));
  }
  async function registerAttendee(attendee: RegistrationInput) {
    if (!selected) return false;
    return performAction(async () => {
      await request(`/${selected.id}/registrations`, 'POST', attendee);
      await refreshEvents(selected.id);
      setNotice('Attendee registered.');
    });
  }
  function unregisterAttendee(attendeeId: string) {
    if (!selected) return;
    void performAction(async () => {
      await request(`/${selected.id}/registrations/${attendeeId}`, 'DELETE');
      await refreshEvents(selected.id);
      setNotice('Registration removed.');
    });
  }
  const registrationClosedReason =
    selected && new Date(selected.date) <= new Date()
      ? 'Registration is closed because this event has already started. Edit the event to a future date to allow registration.'
      : selected && selected.registrationCount >= selected.maxCapacity
        ? 'This event is full. A place becomes available when an attendee unregisters.'
        : '';
  return (
    <main>
      <header>
        <div>
          <span className="eyebrow">EVENT MANAGEMENT</span>
          <h1>
            Gather<span>.</span>
          </h1>
          <p>Good events start with a simple plan.</p>
        </div>
        <button disabled={busy} onClick={() => setEditing(null)}>
          + Create event
        </button>
      </header>
      {error && (
        <div className="message error" role="alert">
          {error}
        </div>
      )}
      {notice && (
        <div className="message success" role="status">
          {notice}
        </div>
      )}
      <div className="layout">
        <section>
          <div className="section-heading">
            <h2>
              Events <small>{events.length}</small>
            </h2>
            <button
              className="secondary"
              disabled={busy || loading}
              onClick={() => void performAction(() => refreshEvents())}
            >
              Refresh
            </button>
          </div>
          {loading ? (
            <p role="status">Loading events…</p>
          ) : events.length === 0 ? (
            <div className="empty">No events yet. Create your first gathering.</div>
          ) : (
            <div className="event-list">
              {events.map((event) => (
                <button
                  className={`event-card ${selectedId === event.id ? 'selected' : ''}`}
                  key={event.id}
                  disabled={busy}
                  onClick={() => {
                    setSelectedId(event.id);
                    setEditing(undefined);
                  }}
                >
                  <span className="badge">
                    {new Date(event.date) <= new Date()
                      ? 'Started'
                      : event.registrationCount >= event.maxCapacity
                        ? 'Full'
                        : 'Open'}
                  </span>
                  <h3>{event.title}</h3>
                  <p>{new Date(event.date).toLocaleString()}</p>
                  <span>
                    {event.registrationCount} / {event.maxCapacity} registered
                  </span>
                </button>
              ))}
            </div>
          )}
        </section>
        {editing !== undefined ? (
          <EventForm
            key={editing?.id || 'new'}
            event={editing}
            busy={busy}
            onSave={saveEvent}
            onCancel={() => setEditing(undefined)}
          />
        ) : selected ? (
          <section className="panel">
            <span className="eyebrow">EVENT DETAILS</span>
            <h2>{selected.title}</h2>
            <p className="description">{selected.description}</p>
            <dl>
              <dt>When</dt>
              <dd>{new Date(selected.date).toLocaleString()}</dd>
              <dt>Available places</dt>
              <dd>
                {selected.maxCapacity - selected.registrationCount} of {selected.maxCapacity}
              </dd>
            </dl>
            <div className="actions">
              <button className="secondary" disabled={busy} onClick={() => setEditing(selected)}>
                Edit
              </button>
              <button className="danger" disabled={busy} onClick={deleteEvent}>
                Delete
              </button>
            </div>
            <section className="attendee-section" aria-labelledby="attendees-heading">
              <h3 id="attendees-heading">
                Attendees <span className="badge">{selected.registrationCount}</span>
              </h3>
              {attendeesLoading ? (
                <p>Loading attendees…</p>
              ) : attendees.length === 0 ? (
                <p>No attendees yet. Add the first attendee below.</p>
              ) : (
                <ul className="attendees">
                  {attendees.map((attendee) => (
                    <li key={attendee.id}>
                      <div>
                        <strong>
                          {attendee.firstName} {attendee.lastName}
                        </strong>
                        <small>{attendee.email}</small>
                      </div>
                      <button
                        className="secondary"
                        disabled={busy}
                        onClick={() => unregisterAttendee(attendee.id)}
                        aria-label={`Remove ${attendee.firstName} ${attendee.lastName}`}
                      >
                        Remove
                      </button>
                    </li>
                  ))}
                </ul>
              )}
            </section>
            <RegistrationForm
              key={selected.id}
              busy={busy || attendeesLoading}
              closedReason={registrationClosedReason}
              onRegister={registerAttendee}
            />
          </section>
        ) : (
          <section className="panel empty">
            <h2>A place for every gathering.</h2>
            <p>Select an event to manage its details and attendees.</p>
          </section>
        )}
      </div>
      <footer>In-memory demo · Events reset when the API restarts · No authentication</footer>
    </main>
  );
}
