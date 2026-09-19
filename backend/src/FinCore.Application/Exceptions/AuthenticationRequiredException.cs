namespace FinCore.Application.Exceptions;

public sealed class AuthenticationRequiredException() : Exception("Authenticated owner identity is required.");
