namespace FinCore.Application.Exceptions;

public sealed class ConcurrencyConflictException : Exception
{
    public ConcurrencyConflictException(Exception innerException)
        : base("The wallet was changed by another request.", innerException)
    {
    }
}
