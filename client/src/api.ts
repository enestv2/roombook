import i18n, { currentLanguage } from './i18n';

export type BookingForm = { roomId: string; startsAt: string; endsAt: string };
export type Room = { id: string; name: string; timeZone: string; workingPeriods: { start: string; end: string }[] };
export type Alternative = { startsAtUtc: string; endsAtUtc: string };
export type BookingConfirmation = { id: string; roomId: string; startsAtLocal: string; endsAtLocal: string; timeZone: string };
export type Conflict = { roomId: string; requestedStartUtc: string; requestedEndUtc: string; conflictingStartUtc: string; conflictingEndUtc: string; alternatives: Alternative[]; timeZone: string; message: string; code?: string };
export type AccessToken = { tokenType: string; accessToken: string; expiresIn: number; refreshToken: string };
export type ApiError = { code?: string; errorCodes?: Record<string, string[]>; errors?: Record<string, string[]>; title?: string; detail?: string };
export type BookingResult = { confirmation?: BookingConfirmation; conflict?: Conflict; errors?: Record<string, string[]>; errorCodes?: Record<string, string[]>; code?: string };

const apiBaseUrl = (import.meta.env.VITE_API_BASE_URL ?? '').replace(/\/$/, '');
let accessToken: string | undefined;
let refreshToken: string | undefined;

export function setAccessToken(token: string | undefined): void {
  accessToken = token;
  if (!token) refreshToken = undefined;
}

function requestHeaders(contentType?: string): Record<string, string> {
  const headers: Record<string, string> = { 'Accept-Language': currentLanguage() };
  if (contentType) headers['Content-Type'] = contentType;
  if (accessToken) headers.Authorization = `Bearer ${accessToken}`;
  return headers;
}

function asApiError(payload: unknown): ApiError | undefined {
  return typeof payload === 'object' && payload !== null ? payload as ApiError : undefined;
}

export async function registerMember(email: string, password: string): Promise<void> {
  const response = await fetch(`${apiBaseUrl}/api/auth/register`, {
    method: 'POST', headers: requestHeaders('application/json'), credentials: 'include',
    body: JSON.stringify({ email, password }),
  });
  const payload = asApiError(await readJson(response));
  if (!response.ok) throw new Error(payload?.detail ?? payload?.title ?? i18n.t('errors.registrationFailed'));
}

export async function loginMember(email: string, password: string): Promise<AccessToken> {
  const response = await fetch(`${apiBaseUrl}/api/auth/login?useCookies=false`, {
    method: 'POST', headers: requestHeaders('application/json'), credentials: 'include',
    body: JSON.stringify({ email, password }),
  });
  const payload = await readJson(response);
  if (!response.ok || typeof payload !== 'object' || payload === null || !('accessToken' in payload))
    throw new Error(asApiError(payload)?.detail ?? asApiError(payload)?.title ?? i18n.t('errors.signInFailed'));
  const token = payload as AccessToken;
  accessToken = token.accessToken;
  refreshToken = token.refreshToken;
  return token;
}

export async function refreshMemberSession(): Promise<AccessToken | undefined> {
  if (!refreshToken) return undefined;
  const response = await fetch(`${apiBaseUrl}/api/auth/refresh`, {
    method: 'POST', headers: requestHeaders('application/json'), credentials: 'include',
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
  try { return await response.json(); } catch { return undefined; }
}

function errorMap(payload: unknown, status: number): { errors: Record<string, string[]>; errorCodes?: Record<string, string[]>; code?: string } {
  const error = asApiError(payload);
  if (error?.errors && typeof error.errors === 'object') {
    const result = Object.fromEntries(Object.entries(error.errors).flatMap(([field, messages]) => {
      if (!Array.isArray(messages)) return [];
      const strings = messages.filter((message): message is string => typeof message === 'string');
      return strings.length === 0 ? [] : [[field, strings]];
    }));
    if (Object.keys(result).length > 0) return { errors: result, errorCodes: error.errorCodes, code: error.code };
  }
  if (status === 401) return { errors: { authorization: [error?.detail ?? error?.title ?? i18n.t('errors.authorizationRequired')] }, code: error?.code };
  if (status === 403) return { errors: { authorization: [error?.detail ?? error?.title ?? i18n.t('errors.notAuthorized')] }, code: error?.code };
  return { errors: { form: [i18n.t('errors.genericBooking')] }, code: error?.code };
}

export async function getRooms(): Promise<Room[]> {
  const response = await fetch(`${apiBaseUrl}/api/rooms`, { headers: requestHeaders(), credentials: 'include' });
  if (!response.ok) {
    const payload = asApiError(await readJson(response));
    throw new Error(payload?.detail ?? payload?.title ?? i18n.t('rooms.loadFailed'));
  }
  return await response.json() as Room[];
}

export async function createBooking(input: BookingForm): Promise<BookingResult> {
  const headers = requestHeaders('application/json');
  const developmentMemberId = import.meta.env.VITE_DEVELOPMENT_MEMBER_ID;
  if (import.meta.env.DEV && developmentMemberId) headers['X-Development-Member-Id'] = developmentMemberId;
  const response = await fetch(`${apiBaseUrl}/api/bookings`, {
    method: 'POST', headers, credentials: 'include', body: JSON.stringify(input),
  });
  const payload = await readJson(response);
  if (response.status === 409 && typeof payload === 'object' && payload !== null)
    return { conflict: payload as Conflict };
  if (!response.ok) return errorMap(payload, response.status);
  if (typeof payload === 'object' && payload !== null) return { confirmation: payload as BookingConfirmation };
  return { errors: { form: [i18n.t('booking.invalidResponse')] } };
}
