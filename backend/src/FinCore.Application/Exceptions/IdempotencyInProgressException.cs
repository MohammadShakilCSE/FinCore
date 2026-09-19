namespace FinCore.Application.Exceptions;

public sealed class IdempotencyInProgressException(Exception? innerException = null)
    : Exception("The attempt is still processing or its status is temporarily unavailable. Retry with the same key.", innerException);
