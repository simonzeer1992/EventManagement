import type { FormEvent } from 'react';
import type { EventItem, EventInput } from './api';

export default function EventForm({
  event,
  busy,
  onSave,
  onCancel,
}: {
  event: EventItem | null;
  busy: boolean;
  onSave: (input: EventInput) => void;
  onCancel: () => void;
}) {
  function submit(formEvent: FormEvent<HTMLFormElement>) {
    formEvent.preventDefault();
    const data = new FormData(formEvent.currentTarget);
    onSave({
      title: String(data.get('title')).trim(),
      description: String(data.get('description')).trim(),
      date: new Date(String(data.get('date'))).toISOString(),
      maxCapacity: Number(data.get('capacity')),
    });
  }
  // datetime-local expects local wall-clock time without a UTC suffix.
  const date = event ? new Date(event.date) : null;
  const localDate = date
    ? new Date(date.getTime() - date.getTimezoneOffset() * 60000).toISOString().slice(0, 16)
    : '';
  return (
    <section className="panel">
      <h2>{event ? 'Edit event' : 'Create an event'}</h2>
      <form onSubmit={submit}>
        <label>
          Title
          <input name="title" required maxLength={200} defaultValue={event?.title} autoFocus />
        </label>
        <label>
          Description
          <textarea
            name="description"
            required
            maxLength={4000}
            rows={4}
            defaultValue={event?.description}
          />
        </label>
        <div className="form-row">
          <label>
            Date and time (local)
            <input name="date" type="datetime-local" required defaultValue={localDate} />
          </label>
          <label>
            Capacity
            <input
              name="capacity"
              type="number"
              required
              min={event?.registrationCount || 1}
              max={2147483647}
              step={1}
              defaultValue={event?.maxCapacity || 20}
            />
          </label>
        </div>
        <div className="actions">
          <button disabled={busy}>Save event</button>
          <button type="button" className="secondary" disabled={busy} onClick={onCancel}>
            Cancel
          </button>
        </div>
      </form>
    </section>
  );
}
