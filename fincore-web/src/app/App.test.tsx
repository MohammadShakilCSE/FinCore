import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import App from './App';

async function fillAndSubmit() {
  const user = userEvent.setup();
  await user.type(screen.getByLabelText('Email address'), 'user@example.com');
  await user.type(screen.getByLabelText('Password'), 'my-password');
  await user.click(screen.getByRole('button', { name: 'Sign in' }));
}

describe('sign in', () => {
  it('authenticates, loads the account with the bearer token, and signs out', async () => {
    const fetch = vi.fn().mockResolvedValueOnce(new Response(JSON.stringify({ accessToken: 'token', expiresIn: 1800 })))
      .mockResolvedValueOnce(new Response(JSON.stringify({ id: '1', name: 'Sam', email: 'user@example.com' })));
    vi.stubGlobal('fetch', fetch);
    render(<App />); await fillAndSubmit();
    expect(await screen.findByText('Welcome, Sam.')).toBeInTheDocument();
    expect(fetch).toHaveBeenNthCalledWith(1, '/api/auth/login', expect.objectContaining({ method: 'POST', body: JSON.stringify({ email: 'user@example.com', password: 'my-password' }) }));
    expect(fetch).toHaveBeenNthCalledWith(2, '/api/auth/me', expect.objectContaining({ headers: { Authorization: 'Bearer token' } }));
    await userEvent.click(screen.getByRole('button', { name: /Sign out/ }));
    expect(screen.getByLabelText('Password')).toHaveValue('');
  });
  it.each([[401, /email or password is incorrect/], [429, /Too many sign-in attempts/], [500, /couldn’t sign you in/]])('handles HTTP %s', async (status, message) => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(new Response(null, { status: Number(status) })));
    render(<App />); await fillAndSubmit();
    expect(await screen.findByRole('alert')).toHaveTextContent(message);
    expect(screen.getByRole('button', { name: 'Sign in' })).toBeEnabled();
  });
  it('handles network failures', async () => {
    vi.stubGlobal('fetch', vi.fn().mockRejectedValue(new TypeError('Failed to fetch')));
    render(<App />); await fillAndSubmit();
    expect(await screen.findByRole('alert')).toHaveTextContent('Unable to reach FinCore');
  });
  it('does not sign in when the profile rejects the token', async () => {
    vi.stubGlobal('fetch', vi.fn()
      .mockResolvedValueOnce(new Response(JSON.stringify({ accessToken: 'token', expiresIn: 1800 })))
      .mockResolvedValueOnce(new Response(null, { status: 401 })));
    render(<App />); await fillAndSubmit();
    expect(await screen.findByRole('alert')).toHaveTextContent('Please sign in again');
    expect(screen.queryByRole('button', { name: /Sign out/ })).not.toBeInTheDocument();
  });
  it('prevents duplicate submissions while waiting for the server', async () => {
    const fetch = vi.fn().mockReturnValue(new Promise(() => {}));
    vi.stubGlobal('fetch', fetch);
    render(<App />); await fillAndSubmit();
    expect(screen.getByRole('button', { name: /Signing you in/ })).toBeDisabled();
    expect(screen.getByLabelText('Email address')).toBeDisabled();
    expect(fetch).toHaveBeenCalledTimes(1);
  });
  it('toggles password visibility without submitting', async () => {
    render(<App />);
    await userEvent.click(screen.getByRole('button', { name: 'Show password' }));
    expect(screen.getByLabelText('Password')).toHaveAttribute('type', 'text');
    await userEvent.click(screen.getByRole('button', { name: 'Hide password' }));
    expect(screen.getByLabelText('Password')).toHaveAttribute('type', 'password');
  });
});
