namespace FinCore.Application.Exceptions;

public sealed class WalletAccessDeniedException() : Exception("You are not authorized to transfer from this wallet.");
