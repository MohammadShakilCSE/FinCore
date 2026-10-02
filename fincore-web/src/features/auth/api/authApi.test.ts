import { describe, expect, it, vi } from 'vitest';
import { signIn } from './authApi';

describe('signIn response validation', () => {
  it.each([null, [], 'token', {}, { accessToken: 'token', expiresIn: '1800' }])(
    'rejects an invalid session payload: %j', async (payload) => {
      const fetch = vi.fn().mockResolvedValue(new Response(JSON.stringify(payload)));
      vi.stubGlobal('fetch', fetch);

      await expect(signIn('user@example.com', 'password')).rejects.toThrow('The server returned an invalid session.');
      expect(fetch).toHaveBeenCalledTimes(1);
    },
  );

  it.each([null, [], 'user', {}, { id: 1, name: 'Sam', email: 'user@example.com' }])(
    'rejects an invalid account payload: %j', async (payload) => {
      vi.stubGlobal('fetch', vi.fn()
        .mockResolvedValueOnce(new Response(JSON.stringify({ accessToken: 'token', expiresIn: 1800 })))
        .mockResolvedValueOnce(new Response(JSON.stringify(payload))));

      await expect(signIn('user@example.com', 'password')).rejects.toThrow('The server returned an invalid account.');
    },
  );
});
