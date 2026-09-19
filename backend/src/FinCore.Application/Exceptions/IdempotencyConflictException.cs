namespace FinCore.Application.Exceptions;

public sealed class IdempotencyConflictException() : Exception("Idempotency key was already used for different request details.");
