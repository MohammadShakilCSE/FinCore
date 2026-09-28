export function getErrorMessage(failure: unknown): string {
  return failure instanceof Error && !['TypeError', 'TimeoutError', 'AbortError'].includes(failure.name)
    ? failure.message
    : 'Unable to reach FinCore. Check your connection and try again.';
}
