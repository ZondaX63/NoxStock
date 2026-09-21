using System;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Uow;

namespace NoxStock.Documents;

// A command is first durably reserved in its own transaction.  Posting then runs
// in a second transaction, so an uncommitted winner can never be mistaken for a
// successful replay by another request.
public class CommercialCommandCoordinator : ITransientDependency
{
    private const int ReplayPollCount = 3;
    private static readonly TimeSpan LeaseDuration = TimeSpan.FromMinutes(5);
    private readonly IRepository<CommercialCommand, Guid> _commandRepository;
    private readonly IUnitOfWorkManager _unitOfWorkManager;

    public CommercialCommandCoordinator(
        IRepository<CommercialCommand, Guid> commandRepository,
        IUnitOfWorkManager unitOfWorkManager)
    {
        _commandRepository = commandRepository;
        _unitOfWorkManager = unitOfWorkManager;
    }

    public async Task<T> ExecuteAsync<T>(
        Guid? tenantId,
        CommercialCommandType type,
        string? operationKey,
        Guid? documentId,
        Func<Task<T>> execute,
        Func<Guid, Task<T>> replay,
        Func<T, Guid>? resultDocumentId = null)
    {
        if (operationKey is null)
        {
            return await ExecuteInTransactionAsync(execute);
        }

        var reservation = await ReserveOrReadAsync(tenantId, type, operationKey, documentId);
        if (!reservation.Reserved)
        {
            return await ResolveReplayAsync(reservation.Command, documentId, replay);
        }

        try
        {
            using var uow = _unitOfWorkManager.Begin(requiresNew: true, isTransactional: true);
            var command = await _commandRepository.GetAsync(reservation.Command.Id);
            if (command.Status != CommercialCommandStatus.Processing)
            {
                return await ResolveReplayAsync(command, documentId, replay);
            }
            command.ExecutionToken = Guid.NewGuid();
            command.LeaseExpiresAt = DateTime.UtcNow.Add(LeaseDuration);
            command.AttemptCount++;
            await _commandRepository.UpdateAsync(command);
            var result = await execute();
            if (resultDocumentId is not null)
            {
                command.DocumentId = resultDocumentId(result);
            }
            command.Status = CommercialCommandStatus.Succeeded;
            command.CompletedAt = DateTime.UtcNow;
            command.FailureCode = null;
            await uow.CompleteAsync();
            return result;
        }
        catch (Exception exception)
        {
            await MarkFailedAsync(reservation.Command.Id, (exception as BusinessException)?.Code);
            throw;
        }
    }

    private async Task<T> ExecuteInTransactionAsync<T>(Func<Task<T>> execute)
    {
        using var uow = _unitOfWorkManager.Begin(requiresNew: true, isTransactional: true);
        var result = await execute();
        await uow.CompleteAsync();
        return result;
    }

    private async Task<(bool Reserved, CommercialCommand Command)> ReserveOrReadAsync(
        Guid? tenantId, CommercialCommandType type, string operationKey, Guid? documentId)
    {
        try
        {
            using var uow = _unitOfWorkManager.Begin(requiresNew: true, isTransactional: true);
            var existing = await _commandRepository.FirstOrDefaultAsync(x =>
                x.Type == type && x.OperationKey == operationKey);
            if (existing is not null)
            {
                await uow.CompleteAsync();
                return (false, existing);
            }

            var command = new CommercialCommand(Guid.NewGuid(), tenantId, type, operationKey)
            {
                DocumentId = documentId
            };
            await _commandRepository.InsertAsync(command);
            await uow.CompleteAsync();
            return (true, command);
        }
        catch (Exception exception) when (IsUniqueConstraintException(exception))
        {
            // The competing reservation may still be uncommitted.  ResolveReplay
            // uses fresh UoWs and bounded polling instead of treating this race as
            // a completed command.
            var command = await GetCommandAsync(type, operationKey);
            return (false, command ?? throw new BusinessException("NoxStock:CommandInProgress"));
        }
    }

    private async Task<T> ResolveReplayAsync<T>(
        CommercialCommand command, Guid? expectedDocumentId, Func<Guid, Task<T>> replay)
    {
        if (expectedDocumentId.HasValue && command.DocumentId.HasValue && command.DocumentId != expectedDocumentId)
        {
            throw new BusinessException("NoxStock:OperationKeyAlreadyUsed");
        }

        for (var attempt = 0; attempt < ReplayPollCount; attempt++)
        {
            var current = await GetCommandAsync(command.Type, command.OperationKey);
            if (current is null)
            {
                break;
            }

            if (current.Status == CommercialCommandStatus.Succeeded && current.DocumentId.HasValue)
            {
                return await replay(current.DocumentId.Value);
            }

            if (current.Status == CommercialCommandStatus.Failed)
            {
                throw new BusinessException("NoxStock:CommandFailed");
            }

            if (current.Status == CommercialCommandStatus.Expired)
            {
                throw new BusinessException("NoxStock:CommandExpired");
            }

            await Task.Delay(100 * (attempt + 1));
        }

        throw new BusinessException("NoxStock:CommandInProgress");
    }

    private async Task<CommercialCommand?> GetCommandAsync(CommercialCommandType type, string operationKey)
    {
        using var uow = _unitOfWorkManager.Begin(requiresNew: true, isTransactional: false);
        var command = await _commandRepository.FirstOrDefaultAsync(x => x.Type == type && x.OperationKey == operationKey);
        await uow.CompleteAsync();
        return command;
    }

    private async Task MarkFailedAsync(Guid commandId, string? failureCode)
    {
        using var uow = _unitOfWorkManager.Begin(requiresNew: true, isTransactional: true);
        var command = await _commandRepository.FindAsync(commandId);
        if (command is not null && command.Status == CommercialCommandStatus.Processing && command.ExecutionToken.HasValue)
        {
            command.Status = CommercialCommandStatus.Failed;
            command.CompletedAt = DateTime.UtcNow;
            command.FailureCode = failureCode?.Length <= 128 ? failureCode : "NoxStock:CommandFailed";
            await _commandRepository.UpdateAsync(command);
        }
        await uow.CompleteAsync();
    }

    private static bool IsUniqueConstraintException(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException!)
        {
            if (current.GetType().Name is "PostgresException" or "SqliteException" &&
                current.Message.Contains("unique", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }
}
