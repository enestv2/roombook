import { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { createBooking, type BookingConfirmation, type BookingForm, type Conflict } from './api';
import { currentLanguage } from './i18n';
import { Button } from './components/ui/Button';
import { Card } from './components/ui/Card';
import { Field, fieldDescribedBy } from './components/ui/Field';
import { SectionHeader } from './components/ui/SectionHeader';
import { StatusBadge } from './components/ui/StatusBadge';
import { Toast } from './components/ui/Toast';

export function BookingFormView({ roomId, roomName, timeZone }: { roomId: string; roomName?: string; timeZone: string }) {
  const { t } = useTranslation();
  const [form, setForm] = useState<BookingForm>({ roomId, startsAt: '', endsAt: '' });
  const [startsAtLocal, setStartsAtLocal] = useState('');
  const [endsAtLocal, setEndsAtLocal] = useState('');
  const [confirmation, setConfirmation] = useState<BookingConfirmation>();
  const [conflict, setConflict] = useState<Conflict>();
  const [errors, setErrors] = useState<Record<string, string[]>>({});
  const [isSubmitting, setIsSubmitting] = useState(false);

  useEffect(() => {
    setForm(current => ({ ...current, roomId }));
    setConfirmation(undefined);
    setConflict(undefined);
    setErrors({});
  }, [roomId]);

  const submit = async (event: React.FormEvent) => {
    event.preventDefault();
    if (isSubmitting) return;
    setConfirmation(undefined);
    setConflict(undefined);
    setErrors({});
    setIsSubmitting(true);
    try {
      const result = await createBooking(form);
      setConfirmation(result.confirmation);
      setConflict(result.conflict);
      setErrors(result.errors ?? {});
    } catch {
      setErrors({ form: [t('booking.submitFailed')] });
    } finally {
      setIsSubmitting(false);
    }
  };

  const selectAlternative = (start: string, end: string) => {
    setForm({ roomId, startsAt: start, endsAt: end });
    setStartsAtLocal(utcToLocalInput(start, timeZone));
    setEndsAtLocal(utcToLocalInput(end, timeZone));
    setConflict(undefined);
    setErrors({});
  };

  if (confirmation) return <Card aria-live="polite" className="border-emerald-200 bg-emerald-50/70 p-6 sm:p-8" id="booking-confirmation">
    <div className="flex flex-col gap-6 sm:flex-row sm:items-start sm:justify-between">
      <div>
        <StatusBadge tone="success">{t('booking.confirmed')}</StatusBadge>
        <h2 className="mt-3 text-2xl font-bold text-emerald-950">{t('booking.confirmedTitle')}</h2>
        <p className="mt-2 text-sm leading-6 text-emerald-900/80">{confirmation.startsAtLocal} – {confirmation.endsAtLocal} ({confirmation.timeZone})</p>
      </div>
      <Button onClick={() => { setConfirmation(undefined); setStartsAtLocal(''); setEndsAtLocal(''); setForm({ roomId, startsAt: '', endsAt: '' }); }} variant="secondary">{t('booking.bookAnother')}</Button>
    </div>
  </Card>;

  const startError = errors.startsAt?.join(' ');
  const endError = errors.endsAt?.join(' ');
  const formErrors = Object.entries(errors).filter(([field]) => !['startsAt', 'endsAt'].includes(field));
  return <Card className="overflow-hidden" id="booking-form">
    <div className="border-b border-slate-100 px-6 py-6 sm:px-8">
      <SectionHeader description={t('booking.formDescription')} title={t('booking.formTitle')}>
        {roomName && <StatusBadge>{roomName}</StatusBadge>}
      </SectionHeader>
      <p className="mt-3 text-xs font-medium text-slate-500">{t('booking.timeZoneHint', { timeZone })}</p>
    </div>
    <form className="grid gap-6 p-6 sm:p-8" noValidate onSubmit={submit}>
      {formErrors.length > 0 && <div className="grid gap-2">
        {formErrors.map(([field, messages]) => <Toast key={field}>{`${t(`fields.${field}`, { defaultValue: field })}: ${messages.join(' ')}`}</Toast>)}
      </div>}
      <div className="grid gap-5 md:grid-cols-2">
        <Field error={startError} hint={t('booking.dateHint')} id="booking-start" label={t('booking.start')}>
          <input aria-describedby={fieldDescribedBy('booking-start', Boolean(startError), true)} aria-invalid={Boolean(startError)} className="min-h-12 rounded-xl border border-slate-200 bg-white px-3.5 text-slate-900 shadow-sm transition-colors focus:border-brand-500 focus:ring-4 focus:ring-brand-100" id="booking-start" required type="datetime-local" value={startsAtLocal} onChange={event => { setStartsAtLocal(event.target.value); setForm(current => ({ ...current, startsAt: localInputToUtcIso(event.target.value, timeZone) })); }} />
        </Field>
        <Field error={endError} hint={t('booking.dateHint')} id="booking-end" label={t('booking.end')}>
          <input aria-describedby={fieldDescribedBy('booking-end', Boolean(endError), true)} aria-invalid={Boolean(endError)} className="min-h-12 rounded-xl border border-slate-200 bg-white px-3.5 text-slate-900 shadow-sm transition-colors focus:border-brand-500 focus:ring-4 focus:ring-brand-100" id="booking-end" required type="datetime-local" value={endsAtLocal} onChange={event => { setEndsAtLocal(event.target.value); setForm(current => ({ ...current, endsAt: localInputToUtcIso(event.target.value, timeZone) })); }} />
        </Field>
      </div>
      {conflict && <ConflictPanel conflict={conflict} onSelect={selectAlternative} timeZone={timeZone} />}
      <div className="flex flex-col gap-3 border-t border-slate-100 pt-5 sm:flex-row sm:items-center sm:justify-between">
        <p className="text-xs leading-5 text-slate-500">{t('booking.submitHint')}</p>
        <Button loading={isSubmitting} type="submit">{isSubmitting ? t('booking.submitting') : t('booking.submit')}</Button>
      </div>
    </form>
  </Card>;
}

function ConflictPanel({ conflict, onSelect, timeZone }: { conflict: Conflict; onSelect: (start: string, end: string) => void; timeZone: string }) {
  const { t } = useTranslation();
  return <div aria-live="polite" className="grid gap-4 rounded-2xl border border-amber-200 bg-amber-50 p-5" role="status">
    <div>
      <StatusBadge tone="warning">{t('booking.conflictBadge')}</StatusBadge>
      <h2 className="mt-2 text-base font-bold text-amber-950">{t('booking.conflictTitle')}</h2>
      <p className="mt-1 text-sm leading-6 text-amber-900/80">{conflict.message || `${formatRoomTime(conflict.conflictingStartUtc, timeZone)} – ${formatRoomTime(conflict.conflictingEndUtc, timeZone)}`}</p>
    </div>
    {conflict.alternatives.length === 0 ? <p className="text-sm font-medium text-amber-900">{t('booking.noAlternatives')}</p> : <div className="grid gap-2 sm:grid-cols-2">
      {conflict.alternatives.map(slot => <Button key={slot.startsAtUtc} onClick={() => onSelect(slot.startsAtUtc, slot.endsAtUtc)} type="button" variant="secondary">{formatRoomTime(slot.startsAtUtc, timeZone)} – {formatRoomTime(slot.endsAtUtc, timeZone)}</Button>)}
    </div>}
  </div>;
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
  const parts = new Intl.DateTimeFormat('en-CA', { timeZone, year: 'numeric', month: '2-digit', day: '2-digit', hour: '2-digit', minute: '2-digit', hourCycle: 'h23' }).formatToParts(new Date(value));
  const get = (type: string) => parts.find(part => part.type === type)?.value ?? '';
  return `${get('year')}-${get('month')}-${get('day')}T${get('hour')}:${get('minute')}`;
}

function timeZoneOffsetMinutes(instant: Date, timeZone: string): number {
  const part = new Intl.DateTimeFormat('en', { timeZone, timeZoneName: 'shortOffset' }).formatToParts(instant).find(value => value.type === 'timeZoneName')?.value ?? 'GMT';
  const match = /^GMT([+-])(\d{1,2})(?::(\d{2}))?$/.exec(part);
  if (!match) return 0;
  const minutes = Number(match[2]) * 60 + Number(match[3] ?? 0);
  return match[1] === '-' ? -minutes : minutes;
}

function formatRoomTime(value: string, timeZone: string): string {
  return new Date(value).toLocaleString(currentLanguage(), { timeZone });
}
