import { afterEach, describe, expect, it, vi } from 'vitest';
import { createBooking, getRooms, loginMember, setAccessToken } from './api';
import { setLanguage } from './i18n';
import { localInputToUtcIso, utcToLocalInput } from './BookingForm';

describe('booking API states', () => {
  afterEach(() => vi.restoreAllMocks());

  it('sends the selected language on API requests', async () => {
    setLanguage('tr');
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response(JSON.stringify([]), { status: 200 })));

    await getRooms();

    const [, request] = (fetch as ReturnType<typeof vi.fn>).mock.calls[0];
    expect(request.headers['Accept-Language']).toBe('tr');
    setLanguage('en');
  });

  it('sends a production bearer token when a member signs in', async () => {
    vi.stubGlobal('fetch', vi.fn()
      .mockResolvedValueOnce(new Response(JSON.stringify({
        tokenType: 'Bearer', accessToken: 'access-token', expiresIn: 300, refreshToken: 'refresh-token'
      }), { status: 200 }))
      .mockResolvedValueOnce(new Response(JSON.stringify([]), { status: 200 })));

    await loginMember('member@example.test', 'correct horse battery staple');
    await getRooms();
    const [, request] = (fetch as ReturnType<typeof vi.fn>).mock.calls[1];
    expect(request.headers.Authorization).toBe('Bearer access-token');
    setAccessToken(undefined);
  });

  it('returns conflict details and alternatives without treating them as a success', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response(JSON.stringify({
      roomId: 'room',
      alternatives: [],
      message: 'No suitable alternatives were found.',
    }), { status: 409 })));

    const result = await createBooking({ roomId: 'room', startsAt: '2030-01-01T09:00:00Z', endsAt: '2030-01-01T10:00:00Z' });
    expect(result.conflict?.message).toContain('No suitable');
    expect(result.confirmation).toBeUndefined();
  });

  it('preserves localized field errors and stable error codes from the API', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response(JSON.stringify({
      code: 'validation.failed',
      errors: { startsAt: ['Başlangıç 15 dakikalık aralığa uymalıdır.'] },
      errorCodes: { startsAt: ['booking.start_not_aligned'] },
    }), { status: 400 })));

    const result = await createBooking({ roomId: 'room', startsAt: '', endsAt: '' });

    expect(result.errors?.startsAt).toEqual(['Başlangıç 15 dakikalık aralığa uymalıdır.']);
    expect(result.errorCodes?.startsAt).toEqual(['booking.start_not_aligned']);
    expect(result.code).toBe('validation.failed');
  });

  it.each([401, 403])('turns an empty %s response into a form error', async status => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response(null, { status })));

    const result = await createBooking({ roomId: 'room', startsAt: '', endsAt: '' });

    expect(result.confirmation).toBeUndefined();
    expect(result.errors?.authorization).toHaveLength(1);
  });

  it.each([401, 403])('preserves a server-provided localized message for %s responses', async status => {
    setLanguage('tr');
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response(JSON.stringify({ detail: 'Oturum açmanız gerekiyor.' }), { status })));

    const result = await createBooking({ roomId: 'room', startsAt: '', endsAt: '' });

    expect(result.errors?.authorization).toEqual(['Oturum açmanız gerekiyor.']);
    setLanguage('en');
  });

  it('loads configured rooms instead of relying on a client-side room id', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response(JSON.stringify([
      { id: 'room-1', name: 'Board room', timeZone: 'Europe/Istanbul', workingPeriods: [] }
    ]), { status: 200 })));
    await expect(getRooms()).resolves.toMatchObject([{ id: 'room-1', timeZone: 'Europe/Istanbul' }]);
  });

  it('converts datetime-local values using the room timezone, not the browser timezone', () => {
    const utc = localInputToUtcIso('2030-01-01T12:00', 'Europe/Istanbul');
    expect(utc).toBe('2030-01-01T09:00:00.000Z');
    expect(utcToLocalInput(utc, 'Europe/Istanbul')).toBe('2030-01-01T12:00');
  });
});
