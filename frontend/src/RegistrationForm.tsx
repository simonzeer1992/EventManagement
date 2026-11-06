import type { FormEvent } from 'react';
import type { RegistrationInput } from './api';

export default function RegistrationForm({
  busy,
  closedReason,
  onRegister,
}: {
  busy: boolean;
  closedReason: string;
  onRegister: (attendee: RegistrationInput) => Promise<boolean>;
}) {
  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    // Capture the form before awaiting; only clear it after a successful action.
    const form = event.currentTarget;
    const data = new FormData(form);
    const registered = await onRegister({
      firstName: String(data.get('firstName')).trim(),
      lastName: String(data.get('lastName')).trim(),
      email: String(data.get('email')).trim(),
    });
    if (registered) form.reset();
  }
  return (
    <form className="registration-form" onSubmit={submit} autoComplete="off">
      <h3>Add an attendee</h3>
      <p>Enter their details to reserve a place.</p>
      {closedReason && <p role="status">{closedReason}</p>}
      <fieldset disabled={busy || !!closedReason}>
        <div className="form-row">
          <label>
            First name
            <input name="firstName" required maxLength={100} />
          </label>
          <label>
            Last name
            <input name="lastName" required maxLength={100} />
          </label>
        </div>
        <label>
          Email
          <input name="email" type="email" required maxLength={254} />
        </label>
        <button>Register attendee</button>
      </fieldset>
    </form>
  );
}
