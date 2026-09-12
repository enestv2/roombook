import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import * as api from './api';
import { BookingFormView } from './BookingForm';
import { setLanguage } from './i18n';

describe('booking form workspace', () => {
  beforeEach(() => setLanguage('en'));
  afterEach(() => { cleanup(); vi.restoreAllMocks(); });

  it('prevents duplicate submissions and communicates pending state', async () => {
    let resolveBooking: (value: api.BookingResult) => void = () => undefined;
    const createBooking = vi.spyOn(api, 'createBooking').mockReturnValue(new Promise(resolve => { resolveBooking = resolve; }));
    render(<BookingFormView roomId="room" roomName="Board room" timeZone="UTC" />);

    fireEvent.change(screen.getByLabelText('Start date and time'), { target: { value: '2030-01-01T09:00' } });
    fireEvent.change(screen.getByLabelText('End date and time'), { target: { value: '2030-01-01T10:00' } });
    const submit = screen.getByRole('button', { name: 'Book room' });
    await userEvent.click(submit);

    expect((screen.getByRole('button', { name: 'Booking…' }) as HTMLButtonElement).disabled).toBe(true);
    await userEvent.click(submit);
    expect(createBooking).toHaveBeenCalledTimes(1);
    expect(createBooking).toHaveBeenCalledWith({ roomId: 'room', startsAt: '2030-01-01T09:00:00.000Z', endsAt: '2030-01-01T10:00:00.000Z' });

    resolveBooking({ confirmation: { id: 'booking', roomId: 'room', startsAtLocal: '09:00', endsAtLocal: '10:00', timeZone: 'UTC' } });
    expect(await screen.findByRole('heading', { name: 'Your room is booked' })).toBeTruthy();
  });

  it('renders API errors in the form context', async () => {
    vi.spyOn(api, 'createBooking').mockResolvedValue({ errors: { startsAt: ['Start must be aligned to 15 minutes.'] } });
    render(<BookingFormView roomId="room" timeZone="UTC" />);

    fireEvent.change(screen.getByLabelText('Start date and time'), { target: { value: '2030-01-01T09:00' } });
    fireEvent.change(screen.getByLabelText('End date and time'), { target: { value: '2030-01-01T10:00' } });
    await userEvent.click(screen.getByRole('button', { name: 'Book room' }));

    expect(await screen.findByText('Start must be aligned to 15 minutes.')).toBeTruthy();
    expect(screen.getByLabelText('Start date and time').getAttribute('aria-invalid')).toBe('true');
  });

  it('lets the member choose a returned conflict alternative', async () => {
    vi.spyOn(api, 'createBooking').mockResolvedValue({
      conflict: {
        roomId: 'room', requestedStartUtc: '', requestedEndUtc: '', conflictingStartUtc: '', conflictingEndUtc: '', timeZone: 'UTC',
        message: 'The requested time is already booked.',
        alternatives: [{ startsAtUtc: '2030-01-01T10:00:00.000Z', endsAtUtc: '2030-01-01T11:00:00.000Z' }],
      },
    });
    render(<BookingFormView roomId="room" timeZone="UTC" />);

    fireEvent.change(screen.getByLabelText('Start date and time'), { target: { value: '2030-01-01T09:00' } });
    fireEvent.change(screen.getByLabelText('End date and time'), { target: { value: '2030-01-01T10:00' } });
    await userEvent.click(screen.getByRole('button', { name: 'Book room' }));

    expect(await screen.findByText('The requested time is already booked.')).toBeTruthy();
    const alternative = screen.getByRole('button', { name: /10:00/ });
    await userEvent.click(alternative);
    await waitFor(() => expect((screen.getByLabelText('Start date and time') as HTMLInputElement).value).toBe('2030-01-01T10:00'));
  });
});
