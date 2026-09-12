import { useEffect, useState } from 'react';
import { createBooking, type BookingConfirmation, type BookingForm, type Conflict } from './api';

export function BookingFormView({ roomId, timeZone }: { roomId: string; timeZone: string }) {
  const [form, setForm] = useState<BookingForm>({ roomId, startsAt: '', endsAt: '' });
  const [startsAtLocal, setStartsAtLocal] = useState('');
  const [endsAtLocal, setEndsAtLocal] = useState('');
  const [confirmation, setConfirmation] = useState<BookingConfirmation>();
  const [conflict, setConflict] = useState<Conflict>();
  const [errors, setErrors] = useState<Record<string, string[]>>({});
  const submit = async (event: React.FormEvent) => {
    event.preventDefault();
    setConfirmation(undefined);
    setConflict(undefined);
    setErrors({});
    try {
      const result = await createBooking(form);
      setConfirmation(result.confirmation);
      setConflict(result.conflict);
      setErrors(result.errors ?? {});
    } catch {
      setErrors({ form: ['Booking could not be submitted. Check your connection and try again.'] });
    }
  };
  useEffect(() => setForm(current => ({ ...current, roomId })), [roomId]);
  const selectAlternative = (start: string, end: string) => {
    setForm({ roomId, startsAt: start, endsAt: end });
    setStartsAtLocal(utcToLocalInput(start, timeZone));
    setEndsAtLocal(utcToLocalInput(end, timeZone));
  };
  if (confirmation) return <section aria-live="polite"><h2>Booking confirmed</h2><p>{confirmation.startsAtLocal} – {confirmation.endsAtLocal} ({confirmation.timeZone})</p></section>;
  return <main><h1>Book a room</h1><form onSubmit={submit}>
    <label>Start <input type="datetime-local" value={startsAtLocal} onChange={e => { setStartsAtLocal(e.target.value); setForm({ ...form, startsAt: localInputToUtcIso(e.target.value, timeZone) }); }} /></label>
    <label>End <input type="datetime-local" value={endsAtLocal} onChange={e => { setEndsAtLocal(e.target.value); setForm({ ...form, endsAt: localInputToUtcIso(e.target.value, timeZone) }); }} /></label>
    <button type="submit">Book room</button>
  </form>
  {Object.entries(errors).map(([field, messages]) => <p role="alert" key={field}>{field}: {messages.join(' ')}</p>)}
  {conflict && <section aria-live="polite"><h2>That time is unavailable</h2><p>{formatRoomTime(conflict.conflictingStartUtc, timeZone)} – {formatRoomTime(conflict.conflictingEndUtc, timeZone)}</p>{conflict.alternatives.length === 0 ? <p>No alternatives were found.</p> : <ul>{conflict.alternatives.map(slot => <li key={slot.startsAtUtc}><button type="button" onClick={() => selectAlternative(slot.startsAtUtc, slot.endsAtUtc)}>{formatRoomTime(slot.startsAtUtc, timeZone)} – {formatRoomTime(slot.endsAtUtc, timeZone)}</button></li>)}</ul>}</section>}
  </main>;
}

export function localInputToUtcIso(value: string, timeZone: string): string {
  const match = /^(\d{4})-(\d{2})-(\d{2})T(\d{2}):(\d{2})$/.exec(value);
  if (!match) return '';
  const [, year, month, day, hour, minute] = match;
  const localAsUtc = Date.UTC(Number(year), Number(month) - 1, Number(day), Number(hour), Number(minute));
  const firstOffset = timeZoneOffsetMinutes(new Date(localAsUtc), timeZone);
  const adjusted = new Date(localAsUtc - firstOffset * 60_000);
  const secondOffset = timeZoneOffsetMinutes(adjusted, timeZone);
  return new Date(localAsUtc - secondOffset * 60_000).toISOString();
}

export function utcToLocalInput(value: string, timeZone: string): string {
  const parts = new Intl.DateTimeFormat('en-CA', {
    timeZone, year: 'numeric', month: '2-digit', day: '2-digit',
    hour: '2-digit', minute: '2-digit', hourCycle: 'h23'
  }).formatToParts(new Date(value));
  const get = (type: string) => parts.find(part => part.type === type)?.value ?? '';
  return `${get('year')}-${get('month')}-${get('day')}T${get('hour')}:${get('minute')}`;
}

function timeZoneOffsetMinutes(instant: Date, timeZone: string): number {
  const part = new Intl.DateTimeFormat('en', { timeZone, timeZoneName: 'shortOffset' })
    .formatToParts(instant).find(value => value.type === 'timeZoneName')?.value ?? 'GMT';
  const match = /^GMT([+-])(\d{1,2})(?::(\d{2}))?$/.exec(part);
  if (!match) return 0;
  const minutes = Number(match[2]) * 60 + Number(match[3] ?? 0);
  return match[1] === '-' ? -minutes : minutes;
}

function formatRoomTime(value: string, timeZone: string): string {
  return new Date(value).toLocaleString(undefined, { timeZone });
}
