using System;
using System.Collections.Generic;
using System.Text;

namespace FinCore.Application.Features.Wallets
{
    public sealed record CreateWalletCommand(Guid OwnerId, string Currency);
}
