import type { Session } from '../types/auth.types';
import { httpClient } from '../../../lib/httpClient';

export async function signIn(email: string, password: string): Promise<Session> {
  const response = await httpClient('/auth/login', {
    method: 'POST', headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ email: email.trim(), password }),
  });
  if (!response.ok) {
    if (response.status === 401) throw new Error('The email or password is incorrect. Please try again.');
    if (response.status === 429) throw new Error('Too many sign-in attempts. Please wait a minute and try again.');
    if (response.status === 400) throw new Error('Please check your email and password and try again.');
    throw new Error('We couldn’t sign you in right now. Please try again shortly.');
  }
  const token = await response.json();
  if (typeof token.accessToken !== 'string' || !token.accessToken || !Number.isFinite(token.expiresIn) || token.expiresIn <= 0) {
    throw new Error('The server returned an invalid session. Please try again.');
  }
  const expiresAt = Date.now() + token.expiresIn * 1000;
  const profile = await httpClient('/auth/me', {
    headers: { Authorization: `Bearer ${token.accessToken}` },
  });
  if (!profile.ok) throw new Error('We couldn’t load your account. Please sign in again.');
  const user = await profile.json();
  if (typeof user.id !== 'string' || typeof user.name !== 'string' || typeof user.email !== 'string') {
    throw new Error('The server returned an invalid account. Please try again.');
  }
  return { accessToken: token.accessToken, expiresAt, user };
}
