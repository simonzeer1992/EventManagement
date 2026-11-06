import { useEffect, useState, type FormEvent } from 'react';
import { createRoot } from 'react-dom/client';
import { request, type EventItem, type EventInput } from './api';
import './styles.css';

function EventForm({ event, busy, onSave, onCancel }: {
  event: EventItem | null; busy: boolean;
  onSave: (input: EventInput) => void; onCancel: () => void;
}) {
  function submit(formEvent: FormEvent<HTMLFormElement>) {
    formEvent.preventDefault();
    const data = new FormData(formEvent.currentTarget);
    onSave({ title: String(data.get('title')).trim(), description: String(data.get('description')).trim(),
      date: new Date(String(data.get('date'))).toISOString(), maxCapacity: Number(data.get('capacity')) });
  }
  const localDate = event ? new Date(new Date(event.date).getTime() - new Date(event.date).getTimezoneOffset() * 60000).toISOString().slice(0, 16) : '';
  return <section className="panel"><h2>{event ? 'Edit event' : 'Create an event'}</h2>
    <form onSubmit={submit}>
      <label>Title<input name="title" required maxLength={200} defaultValue={event?.title} autoFocus /></label>
      <label>Description<textarea name="description" required maxLength={4000} rows={4} defaultValue={event?.description} /></label>
      <div className="form-row"><label>Date and time (local)<input name="date" type="datetime-local" required defaultValue={localDate} /></label>
      <label>Capacity<input name="capacity" type="number" required min={event?.registrationCount || 1} max={2147483647} step={1} defaultValue={event?.maxCapacity || 20} /></label></div>
      <div className="actions"><button disabled={busy}>Save event</button><button type="button" className="secondary" disabled={busy} onClick={onCancel}>Cancel</button></div>
    </form>
  </section>;
}

function App() {
  const [events, setEvents] = useState<EventItem[]>([]);
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [attendees, setAttendees] = useState<string[]>([]);
  const [attendeesLoading, setAttendeesLoading] = useState(false);
  const [attendeeId, setAttendeeId] = useState(() => {
    const saved = localStorage.getItem('attendeeId');
    const id = saved || crypto.randomUUID();
    localStorage.setItem('attendeeId', id);
    return id;
  });
  const [identityInput, setIdentityInput] = useState(attendeeId);
  const [editing, setEditing] = useState<EventItem | null | undefined>(undefined);
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const [notice, setNotice] = useState('');
  const selected = events.find(event => event.id === selectedId);

  async function loadEvents() { setEvents(await request<EventItem[]>('')); }
  useEffect(() => { loadEvents().catch(error => setError(error.message)).finally(() => setLoading(false)); }, []);
  useEffect(() => {
    setAttendees([]);
    if (!selectedId) return;
    let active = true;
    setAttendeesLoading(true);
    request<string[]>(`/${selectedId}/registrations`).then(ids => { if (active) setAttendees(ids); })
      .catch(error => { if (active) setError(error.message); })
      .finally(() => { if (active) setAttendeesLoading(false); });
    return () => { active = false; };
  }, [selectedId]);

  async function performAction(action: () => Promise<void>) {
    setBusy(true); setError(''); setNotice('');
    try { await action(); } catch (error) { setError(error instanceof Error ? error.message : 'Something went wrong.'); }
    finally { setBusy(false); }
  }
  function changeIdentity(id: string) {
    if (!/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(id) || /^0{8}-0{4}-0{4}-0{4}-0{12}$/.test(id)) {
      setError('Enter a valid, non-empty attendee GUID.'); return;
    }
    const normalized = id.toLowerCase();
    localStorage.setItem('attendeeId', normalized); setAttendeeId(normalized); setIdentityInput(normalized); setError('');
  }
  function saveEvent(input: EventInput) {
    void performAction(async () => {
      const saved = await request<EventItem>(editing ? `/${editing.id}` : '', editing ? 'PUT' : 'POST', input);
      setEditing(undefined); await loadEvents(); setSelectedId(saved.id); setNotice('Event saved.');
    });
  }
  function deleteEvent() {
    if (!selected || !confirm(`Delete "${selected.title}" and all its registrations?`)) return;
    void performAction(async () => {
      await request(`/${selected.id}`, 'DELETE'); setSelectedId(null); await loadEvents(); setNotice('Event deleted.');
    });
  }
  function changeRegistration(id: string, remove: boolean) {
    if (!selected) return;
    void performAction(async () => {
      await request(`/${selected.id}/registrations${remove ? `/${id}` : ''}`, remove ? 'DELETE' : 'POST', remove ? undefined : { attendeeId: id });
      setAttendees(await request<string[]>(`/${selected.id}/registrations`)); await loadEvents();
      setNotice(remove ? 'Registration removed.' : 'You are registered!');
    });
  }
  const registered = attendees.includes(attendeeId);
  const registrationClosedReason = selected && new Date(selected.date) <= new Date()
    ? 'Registration is closed because this event has already started. Edit the event to a future date to allow registration.'
    : selected && selected.registrationCount >= selected.maxCapacity
      ? 'This event is full. A place becomes available when an attendee unregisters.'
      : '';
  return <main>
    <header><div><span className="eyebrow">EVENT MANAGEMENT</span><h1>Gather<span>.</span></h1><p>Good events start with a simple plan.</p></div>
      <button disabled={busy} onClick={() => setEditing(null)}>+ Create event</button></header>
    <section className="identity"><div><strong>Attendee used for registration</strong><p>Register me uses this ID. Creating events does not register you. Switch IDs to try another attendee.</p></div>
      <form onSubmit={e => { e.preventDefault(); changeIdentity(identityInput.trim()); }}>
        <input aria-label="Attendee GUID" value={identityInput} onChange={e => setIdentityInput(e.target.value)} />
        <button className="secondary" disabled={busy}>Use ID</button><button type="button" className="secondary" disabled={busy} onClick={() => changeIdentity(crypto.randomUUID())}>New ID</button>
      </form></section>
    {error && <div className="message error" role="alert">{error}</div>}
    {notice && <div className="message success" role="status">{notice}</div>}
    <div className="layout"><section><div className="section-heading"><h2>Events <small>{events.length}</small></h2><button className="secondary" disabled={busy || loading} onClick={() => void performAction(async () => { await loadEvents(); if (selectedId) setAttendees(await request<string[]>(`/${selectedId}/registrations`)); })}>Refresh</button></div>
      {loading ? <p role="status">Loading events…</p> : events.length === 0 ? <div className="empty">No events yet. Create your first gathering.</div> :
        <div className="event-list">{events.map(event => <button className={`event-card ${selectedId === event.id ? 'selected' : ''}`} key={event.id} disabled={busy} onClick={() => { setSelectedId(event.id); setEditing(undefined); }}>
          <span className="badge">{new Date(event.date) <= new Date() ? 'Started' : event.registrationCount >= event.maxCapacity ? 'Full' : 'Open'}</span>
          <h3>{event.title}</h3><p>{new Date(event.date).toLocaleString()}</p><span>{event.registrationCount} / {event.maxCapacity} registered</span>
        </button>)}</div>}
    </section>
    {editing !== undefined ? <EventForm key={editing?.id || 'new'} event={editing} busy={busy} onSave={saveEvent} onCancel={() => setEditing(undefined)} /> : selected ?
      <section className="panel"><span className="eyebrow">EVENT DETAILS</span><h2>{selected.title}</h2><p className="description">{selected.description}</p>
        <dl><dt>When</dt><dd>{new Date(selected.date).toLocaleString()}</dd><dt>Available places</dt><dd>{selected.maxCapacity - selected.registrationCount} of {selected.maxCapacity}</dd></dl>
        {!registered && registrationClosedReason && <p role="status">{registrationClosedReason}</p>}
        <div className="actions"><button disabled={busy || attendeesLoading || (!registered && (new Date(selected.date) <= new Date() || selected.registrationCount >= selected.maxCapacity))} onClick={() => changeRegistration(attendeeId, registered)}>{registered ? 'Cancel my registration' : 'Register me'}</button>
        <button className="secondary" disabled={busy} onClick={() => setEditing(selected)}>Edit</button><button className="danger" disabled={busy} onClick={deleteEvent}>Delete</button></div>
        <h3>Attendees ({selected.registrationCount})</h3>{attendeesLoading ? <p>Loading attendees…</p> : attendees.length === 0 ? <p>No registrations yet.</p> : <ul className="attendees">{attendees.map(id => <li key={id}><span><code>{id}</code>{id === attendeeId && <small> You</small>}</span><button className="secondary" disabled={busy} onClick={() => changeRegistration(id, true)} aria-label={`Remove attendee ${id}`}>Remove</button></li>)}</ul>}
      </section> : <section className="panel empty"><h2>A place for every gathering.</h2><p>Select an event to manage its details and attendees.</p></section>}
    </div><footer>In-memory demo · Events reset when the API restarts · No authentication</footer>
  </main>;
}
createRoot(document.getElementById('root')!).render(<App />);
