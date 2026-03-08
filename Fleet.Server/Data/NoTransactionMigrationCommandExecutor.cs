using System.Data;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace Fleet.Server.Data;

/// <summary>
/// Design-time migration executor that avoids opening explicit transactions.
/// Some hosted PostgreSQL poolers can fail on transaction-scoped DDL when running dotnet-ef.
/// </summary>
internal sealed class NoTransactionMigrationCommandExecutor : IMigrationCommandExecutor
{
    public void ExecuteNonQuery(IEnumerable<MigrationCommand> migrationCommands, IRelationalConnection connection)
    {
        var commandList = migrationCommands as IReadOnlyList<MigrationCommand> ?? migrationCommands.ToList();
        ExecuteNonQuery(commandList, connection, new MigrationExecutionState(), beginTransaction: false, isolationLevel: null);
    }

    public int ExecuteNonQuery(
        IReadOnlyList<MigrationCommand> migrationCommands,
        IRelationalConnection connection,
        MigrationExecutionState executionState,
        bool beginTransaction,
        IsolationLevel? isolationLevel)
    {
        using var commandConnection = new NpgsqlConnection(connection.ConnectionString);
        commandConnection.Open();

        try
        {
            var affected = 0;
            for (var i = 0; i < migrationCommands.Count; i++)
            {
                using var command = commandConnection.CreateCommand();
                command.CommandText = migrationCommands[i].CommandText;
                command.CommandType = CommandType.Text;
                if (connection.CommandTimeout is int timeoutSeconds)
                {
                    command.CommandTimeout = timeoutSeconds;
                }

                affected += command.ExecuteNonQuery();
                executionState.LastCommittedCommandIndex = i;
                executionState.AnyOperationPerformed = true;
            }

            return affected;
        }
        finally
        {
            commandConnection.Close();
        }
    }

    public async Task ExecuteNonQueryAsync(
        IEnumerable<MigrationCommand> migrationCommands,
        IRelationalConnection connection,
        CancellationToken cancellationToken = default)
    {
        var commandList = migrationCommands as IReadOnlyList<MigrationCommand> ?? migrationCommands.ToList();
        _ = await ExecuteNonQueryAsync(
            commandList,
            connection,
            new MigrationExecutionState(),
            beginTransaction: false,
            isolationLevel: null,
            cancellationToken);
    }

    public async Task<int> ExecuteNonQueryAsync(
        IReadOnlyList<MigrationCommand> migrationCommands,
        IRelationalConnection connection,
        MigrationExecutionState executionState,
        bool beginTransaction,
        IsolationLevel? isolationLevel,
        CancellationToken cancellationToken = default)
    {
        await using var commandConnection = new NpgsqlConnection(connection.ConnectionString);
        await commandConnection.OpenAsync(cancellationToken);

        try
        {
            var affected = 0;
            for (var i = 0; i < migrationCommands.Count; i++)
            {
                await using var command = commandConnection.CreateCommand();
                command.CommandText = migrationCommands[i].CommandText;
                command.CommandType = CommandType.Text;
                if (connection.CommandTimeout is int timeoutSeconds)
                {
                    command.CommandTimeout = timeoutSeconds;
                }

                affected += await command.ExecuteNonQueryAsync(cancellationToken);
                executionState.LastCommittedCommandIndex = i;
                executionState.AnyOperationPerformed = true;
            }

            return affected;
        }
        finally
        {
            await commandConnection.CloseAsync();
        }
    }
}
