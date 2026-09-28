import { config } from '../app/config';

export function httpClient(path: string, options: RequestInit = {}) {
  return fetch(`${config.apiBaseUrl}${path}`, {
    ...options,
    signal: options.signal ?? AbortSignal.timeout(config.requestTimeoutMs),
  });
}
