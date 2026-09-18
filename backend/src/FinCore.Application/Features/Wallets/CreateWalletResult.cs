using System;
using System.Collections.Generic;
using System.Text;

namespace FinCore.Application.Features.Wallets
{
    public sealed record CreateWalletResult(
     Guid WalletId,
     Guid OwnerId,
     string Currency,
     decimal Balance,
     string Status,
     DateTime CreatedAt);
}
