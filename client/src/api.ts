export type BookingForm = { roomId: string; startsAt: string; endsAt: string };
export type Room = { id: string; name: string; timeZone: string; workingPeriods: { start: string; end: string }[] };
export type Alternative = { startsAtUtc: string; endsAtUtc: string };
export type BookingConfirmation = { id: string; roomId: string; startsAtLocal: string; endsAtLocal: string; timeZone: string };
export type Conflict = { roomId: string; requestedStartUtc: string; requestedEndUtc: string; conflictingStartUtc: string; conflictingEndUtc: string; alternatives: Alternative[]; timeZone: string; message: string };
export type AccessToken = { tokenType: string; accessToken: string; expiresIn: number; refreshToken: string };

const apiBaseUrl = (import.meta.env.VITE_API_BASE_URL ?? '').replace(/\/$/, '');
let accessToken: string | undefined;
let refreshToken: string | undefined;

export function setAccessToken(token: string | undefined): void {
  accessToken = token;
  if (!token) refreshToken = undefined;
}

export async function registerMember(email: string, password: string): Promise<void> {
  const response = await fetch(`${apiBaseUrl}/api/auth/register`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    credentials: 'include',
    body: JSON.stringify({ email, password }),
  });
  if (!response.ok) throw new Error('Member registration failed.');
}

export async function loginMember(email: string, password: string): Promise<AccessToken> {
  const response = await fetch(`${apiBaseUrl}/api/auth/login?useCookies=false`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    credentials: 'include',
    body: JSON.stringify({ email, password }),
  });
  const payload = await readJson(response);
  if (!response.ok || typeof payload !== 'object' || payload === null || !('accessToken' in payload))
    throw new Error('Member sign-in failed.');
  const token = payload as AccessToken;
  accessToken = token.accessToken;
  refreshToken = token.refreshToken;
  return token;
}

export async function refreshMemberSession(): Promise<AccessToken | undefined> {
  if (!refreshToken) return undefined;
  const response = await fetch(`${apiBaseUrl}/api/auth/refresh`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    credentials: 'include',
    body: JSON.stringify({ refreshToken }),
  });
  const payload = await readJson(response);
  if (!response.ok || typeof payload !== 'object' || payload === null || !('accessToken' in payload)) {
    accessToken = undefined;
    refreshToken = undefined;
    return undefined;
  }
  const token = payload as AccessToken;
  accessToken = token.accessToken;
  refreshToken = token.refreshToken;
  return token;
}

async function readJson(response: Response): Promise<unknown> {
  try {
    return await response.json();
  } catch {
    return undefined;
  }
}

function errorMap(payload: unknown, status: number): Record<string, string[]> {
  if (typeof payload === 'object' && payload !== null && 'errors' in payload) {
    const errors = (payload as { errors?: unknown }).errors;
    if (typeof errors === 'object' && errors !== null) {
      const result = Object.fromEntries(Object.entries(errors).flatMap(([field, messages]) => {
        if (!Array.isArray(messages)) return [];
        const strings = messages.filter((message): message is string => typeof message === 'string');
        return strings.length === 0 ? [] : [[field, strings]];
      }));
      if (Object.keys(result).length > 0) return result;
    }
  }
  if (status === 401) return { authorization: ['You must be signed in to book a room.'] };
  if (status === 403) return { authorization: ['You are not authorized to book a room.'] };
  return { form: ['Booking could not be created.'] };
}

export async function getRooms(): Promise<Room[]> {
  const headers: Record<string, string> = {};
  if (accessToken) headers.Authorization = `Bearer ${accessToken}`;
  const response = await fetch(`${apiBaseUrl}/api/rooms`, { headers, credentials: 'include' });
  if (!response.ok) throw new Error('Rooms could not be loaded.');
  return await response.json() as Room[];
}

export async function createBooking(input: BookingForm): Promise<{ confirmation?: BookingConfirmation; conflict?: Conflict; errors?: Record<string, string[]> }> {
  const headers: Record<string, string> = { 'Content-Type': 'application/json' };
  const developmentMemberId = import.meta.env.VITE_DEVELOPMENT_MEMBER_ID;
  if (import.meta.env.DEV && developmentMemberId) headers['X-Development-Member-Id'] = developmentMemberId;
  if (accessToken) headers.Authorization = `Bearer ${accessToken}`;
  const response = await fetch(`${apiBaseUrl}/api/bookings`, {
    method: 'POST', headers, credentials: 'include', body: JSON.stringify(input)
  });
  const payload = await readJson(response);
  if (response.status === 409 && typeof payload === 'object' && payload !== null)
    return { conflict: payload as Conflict };
  if (!response.ok) return { errors: errorMap(payload, response.status) };
  if (typeof payload === 'object' && payload !== null) return { confirmation: payload as BookingConfirmation };
  return { errors: { form: ['The booking response was invalid.'] } };
}
