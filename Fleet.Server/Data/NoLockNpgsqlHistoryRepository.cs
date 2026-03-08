using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using Npgsql.EntityFrameworkCore.PostgreSQL.Migrations.Internal;

namespace Fleet.Server.Data;

internal sealed class NoLockNpgsqlHistoryRepository(HistoryRepositoryDependencies dependencies)
    : NpgsqlHistoryRepository(dependencies)
{
    private readonly IRelationalConnection _connection = dependencies.Connection;

    public override IMigrationsDatabaseLock AcquireDatabaseLock()
        => new NoOpMigrationsDatabaseLock(this);

    public override Task<IMigrationsDatabaseLock> AcquireDatabaseLockAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IMigrationsDatabaseLock>(new NoOpMigrationsDatabaseLock(this));

    public override IReadOnlyList<HistoryRow> GetAppliedMigrations()
    {
        EnsureHistoryTableExists();
        using var historyConnection = new NpgsqlConnection(_connection.ConnectionString);
        historyConnection.Open();

        try
        {
            using var command = historyConnection.CreateCommand();
            command.CommandText = """
                SELECT "MigrationId", "ProductVersion"
                FROM "__EFMigrationsHistory"
                ORDER BY "MigrationId";
                """;
            if (_connection.CommandTimeout is int timeoutSeconds)
            {
                command.CommandTimeout = timeoutSeconds;
            }

            using var reader = command.ExecuteReader();
            var rows = new List<HistoryRow>();
            while (reader.Read())
            {
                rows.Add(new HistoryRow(reader.GetString(0), reader.GetString(1)));
            }

            return rows;
        }
        finally
        {
            historyConnection.Close();
        }
    }

    public override async Task<IReadOnlyList<HistoryRow>> GetAppliedMigrationsAsync(CancellationToken cancellationToken = default)
    {
        await EnsureHistoryTableExistsAsync(cancellationToken);
        await using var historyConnection = new NpgsqlConnection(_connection.ConnectionString);
        await historyConnection.OpenAsync(cancellationToken);

        try
        {
            await using var command = historyConnection.CreateCommand();
            command.CommandText = """
                SELECT "MigrationId", "ProductVersion"
                FROM "__EFMigrationsHistory"
                ORDER BY "MigrationId";
                """;
            if (_connection.CommandTimeout is int timeoutSeconds)
            {
                command.CommandTimeout = timeoutSeconds;
            }

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            var rows = new List<HistoryRow>();
            while (await reader.ReadAsync(cancellationToken))
            {
                rows.Add(new HistoryRow(reader.GetString(0), reader.GetString(1)));
            }

            return rows;
        }
        finally
        {
            await historyConnection.CloseAsync();
        }
    }

    private void EnsureHistoryTableExists()
    {
        using var historyConnection = new NpgsqlConnection(_connection.ConnectionString);
        historyConnection.Open();

        try
        {
            using var command = historyConnection.CreateCommand();
            command.CommandText = """
                CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
                    "MigrationId" character varying(150) NOT NULL,
                    "ProductVersion" character varying(32) NOT NULL,
                    CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
                );
                """;
            if (_connection.CommandTimeout is int timeoutSeconds)
            {
                command.CommandTimeout = timeoutSeconds;
            }

            command.ExecuteNonQuery();
        }
        finally
        {
            historyConnection.Close();
        }
    }

    private async Task EnsureHistoryTableExistsAsync(CancellationToken cancellationToken)
    {
        await using var historyConnection = new NpgsqlConnection(_connection.ConnectionString);
        await historyConnection.OpenAsync(cancellationToken);

        try
        {
            await using var command = historyConnection.CreateCommand();
            command.CommandText = """
                CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
                    "MigrationId" character varying(150) NOT NULL,
                    "ProductVersion" character varying(32) NOT NULL,
                    CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
                );
                """;
            if (_connection.CommandTimeout is int timeoutSeconds)
            {
                command.CommandTimeout = timeoutSeconds;
            }

            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        finally
        {
            await historyConnection.CloseAsync();
        }
    }

    private sealed class NoOpMigrationsDatabaseLock(IHistoryRepository historyRepository) : IMigrationsDatabaseLock
    {
        public IHistoryRepository HistoryRepository => historyRepository;

        public void Dispose()
        {
        }

        public ValueTask DisposeAsync()
            => ValueTask.CompletedTask;

        public IMigrationsDatabaseLock ReacquireIfNeeded(bool connectionReopened, bool? transactionRestarted)
            => this;

        public Task<IMigrationsDatabaseLock> ReacquireIfNeededAsync(
            bool connectionReopened,
            bool? transactionRestarted,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IMigrationsDatabaseLock>(this);
    }
}
