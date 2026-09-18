namespace FinCore.Domain.Common;

public abstract record DomainEvent(DateTimeOffset OccurredOn);
