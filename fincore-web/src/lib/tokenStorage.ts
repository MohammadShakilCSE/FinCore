// Provider-scoped, memory-only storage. Never persists a bearer token to disk.
export function createTokenStorage() {
  let token: string | null = null;
  return {
    get: () => token,
    set: (value: string) => { token = value; },
    clear: () => { token = null; },
  };
}
