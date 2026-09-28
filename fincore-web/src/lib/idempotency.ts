// Create once per logical operation and reuse for retries of that same operation.
export const createIdempotencyKey = () => crypto.randomUUID();
