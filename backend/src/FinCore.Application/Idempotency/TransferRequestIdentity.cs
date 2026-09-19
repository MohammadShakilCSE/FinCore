using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using FinCore.Application.Features.Transfers.TransferMoney;
using FinCore.Domain.Exceptions;
using FinCore.Domain.ValueObjects;

namespace FinCore.Application.Idempotency;

public static class TransferRequestIdentity
{
    public const string Operation = "WalletTransfer:v1";

    public static void ValidateKey(string key)
    {
        if (string.IsNullOrEmpty(key) || key.Length > 128 || key.Any(c => c < '!' || c > '~'))
            throw new DomainException("Idempotency key must contain 1 to 128 printable ASCII characters without spaces.");
    }

    public static string Fingerprint(TransferMoneyCommand command, Money amount)
    {
        var canonical = string.Join('\n', Operation, command.SenderWalletId.ToString("D"),
            command.ReceiverWalletId.ToString("D"),
            amount.Amount.ToString("0.############################", CultureInfo.InvariantCulture), amount.Currency);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }
}
