using FinCore.Application.Abstractions.Persistence;
using FinCore.Application.Abstractions.Identity;
using FinCore.Application.Exceptions;
using FinCore.Application.Idempotency;
using FinCore.Domain.Entities;
using FinCore.Domain.Exceptions;
using FinCore.Domain.ValueObjects;

namespace FinCore.Application.Features.Transfers.TransferMoney;

public sealed class TransferMoneyHandler(
    IWalletRepository walletRepository,
    ILedgerTransactionRepository ledgerRepository,
    IIdempotencyRepository idempotencyRepository,
    ICurrentOwner currentOwner)
{
    public async Task<TransferMoneyResult> HandleAsync(
        TransferMoneyCommand command, CancellationToken cancellationToken = default)
    {
        var ownerId = currentOwner.GetRequiredOwnerId();
        if (ownerId == Guid.Empty) throw new AuthenticationRequiredException();
        TransferRequestIdentity.ValidateKey(command.IdempotencyKey);
        if (command.SenderWalletId == Guid.Empty || command.ReceiverWalletId == Guid.Empty)
            throw new DomainException("Both wallets are required.");
        if (command.SenderWalletId == command.ReceiverWalletId)
            throw new DomainException("Cannot transfer to the same wallet.");

        var ownedWallet = await walletRepository.GetByIdAsync(command.SenderWalletId, cancellationToken)
            ?? throw new DomainException("Sender wallet was not found.");
        if (ownedWallet.OwnerId != ownerId) throw new WalletAccessDeniedException();
        var amount = new Money(command.Amount, command.Currency);
        var fingerprint = TransferRequestIdentity.Fingerprint(command, amount);
        await using var claim = await idempotencyRepository.ClaimAsync(ownerId, TransferRequestIdentity.Operation,
            command.IdempotencyKey, fingerprint, cancellationToken);
        if (!claim.IsNew)
        {
            if (claim.Record.RequestFingerprint != fingerprint) throw new IdempotencyConflictException();
            if (claim.Record.Status != "Completed") throw new IdempotencyInProgressException();
            return claim.Record.GetResult();
        }

        var sender = await walletRepository.GetTrackedByIdAsync(command.SenderWalletId, cancellationToken)
            ?? throw new DomainException("Sender wallet was not found.");
        if (sender.OwnerId != ownerId) throw new WalletAccessDeniedException();
        var receiver = await walletRepository.GetTrackedByIdAsync(command.ReceiverWalletId, cancellationToken)
            ?? throw new DomainException("Receiver wallet was not found.");

        sender.Debit(amount);
        receiver.Credit(amount);
        var transaction = LedgerTransaction.CreateTransfer(sender.Id, receiver.Id, amount);
        await ledgerRepository.AddAsync(transaction, cancellationToken);

        var result = new TransferMoneyResult(transaction.Id, transaction.Reference, amount.Amount, amount.Currency);
        claim.Record.Complete(result);
        // The key claim opened the transaction before any wallet mutation.
        await walletRepository.SaveChangesAsync(cancellationToken);
        await claim.CommitAsync(cancellationToken);
        return result;
    }
}
