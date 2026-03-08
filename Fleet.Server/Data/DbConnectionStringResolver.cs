using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace Fleet.Server.Data;

public static class DbConnectionStringResolver
{
    public static string? ResolveFleetDbLocalMigrationConnectionString(IConfiguration configuration)
    {
        var isLocalEnvironment = IsLocalEnvironment(configuration);
        var localMigrationConnection = ResolveFromCandidates(
            GetLocalMigrationCandidates(configuration),
            forceSupabaseSessionPooling: isLocalEnvironment);
        if (!string.IsNullOrWhiteSpace(localMigrationConnection))
        {
            return localMigrationConnection;
        }

        return ResolveFromCandidates(
            GetDefaultCandidates(configuration),
            forceSupabaseSessionPooling: isLocalEnvironment);
    }

    public static string? ResolveFleetDbConnectionString(IConfiguration configuration)
    {
        var isLocalEnvironment = IsLocalEnvironment(configuration);
        if (isLocalEnvironment)
        {
            var localMigrationConnection = ResolveFromCandidates(
                GetLocalMigrationCandidates(configuration),
                forceSupabaseSessionPooling: true);
            if (!string.IsNullOrWhiteSpace(localMigrationConnection))
            {
                return localMigrationConnection;
            }
        }

        return ResolveFromCandidates(
            GetDefaultCandidates(configuration),
            forceSupabaseSessionPooling: isLocalEnvironment);
    }

    private static bool IsLocalEnvironment(IConfiguration configuration)
    {
        var environmentName =
            Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ??
            Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ??
            configuration["ASPNETCORE_ENVIRONMENT"] ??
            configuration["DOTNET_ENVIRONMENT"] ??
            "Development";

        return string.Equals(environmentName, "Development", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(environmentName, "Local", StringComparison.OrdinalIgnoreCase);
    }

    private static string? ResolveFromCandidates(
        IEnumerable<string?> candidates,
        bool forceSupabaseSessionPooling)
    {
        foreach (var candidate in candidates)
        {
            var normalized = NormalizePostgresConnectionString(candidate);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                continue;
            }

            return forceSupabaseSessionPooling
                ? ForceSupabaseSessionPooling(normalized)
                : normalized;
        }

        return null;
    }

    private static IEnumerable<string?> GetLocalMigrationCandidates(IConfiguration configuration)
    {
        return new[]
        {
            configuration.GetConnectionString("fleetdb_migrations"),
            configuration.GetConnectionString("FleetDbMigrations"),
            configuration["ConnectionStrings:fleetdb_migrations"],
            configuration["ConnectionStrings:FleetDbMigrations"],
            configuration["FLEETDB_MIGRATIONS_CONNECTION_STRING"],
            configuration["DATABASE_MIGRATIONS_URL"],
        };
    }

    private static IEnumerable<string?> GetDefaultCandidates(IConfiguration configuration)
    {
        return new[]
        {
            configuration.GetConnectionString("fleetdb"),
            configuration.GetConnectionString("FleetDb"),
            configuration.GetConnectionString("Default"),
            configuration.GetConnectionString("DefaultConnection"),
            configuration["ConnectionStrings:fleetdb"],
            configuration["ConnectionStrings:FleetDb"],
            configuration["ConnectionStrings:Default"],
            configuration["ConnectionStrings:DefaultConnection"],
            configuration["ConnectionString"],
            configuration["Aspire:Npgsql:EntityFrameworkCore:PostgreSQL:ConnectionString"],
            configuration["Aspire:Npgsql:EntityFrameworkCore:PostgreSQL:FleetDbContext:ConnectionString"],
            configuration["DATABASE_URL"],
            configuration["POSTGRES_URL"],
            configuration["POSTGRES_CONNECTION_STRING"],
            configuration["FLEETDB_CONNECTION_STRING"],
        };
    }

    private static string? NormalizePostgresConnectionString(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var value = raw.Trim();
        if (Uri.TryCreate(value, UriKind.Absolute, out var uri))
        {
            if (!string.Equals(uri.Scheme, "postgres", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(uri.Scheme, "postgresql", StringComparison.OrdinalIgnoreCase))
            {
                return value;
            }

            return BuildNpgsqlConnectionString(uri);
        }

        if (TryNormalizeMalformedPostgresUrl(value, out var normalizedMalformed))
        {
            return normalizedMalformed;
        }

        return value;
    }

    private static string BuildNpgsqlConnectionString(Uri uri)
    {
        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.IsDefaultPort ? 5432 : uri.Port,
            Database = uri.AbsolutePath.Trim('/'),
        };

        if (!string.IsNullOrWhiteSpace(uri.UserInfo))
        {
            var userInfo = uri.UserInfo.Split(':', 2, StringSplitOptions.None);
            builder.Username = Uri.UnescapeDataString(userInfo[0]);
            if (userInfo.Length > 1)
            {
                builder.Password = Uri.UnescapeDataString(userInfo[1]);
            }
        }

        ApplyQueryStringOptions(builder, uri.Query);
        return builder.ConnectionString;
    }

    private static string ForceSupabaseSessionPooling(string connectionString)
    {
        var builder = new NpgsqlConnectionStringBuilder(connectionString);
        var host = builder.Host ?? string.Empty;
        if (host.EndsWith(".pooler.supabase.com", StringComparison.OrdinalIgnoreCase))
        {
            builder.Port = 5432;
        }

        return builder.ConnectionString;
    }

    private static bool TryNormalizeMalformedPostgresUrl(string value, out string normalized)
    {
        normalized = string.Empty;
        var prefix = value.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase)
            ? "postgresql://"
            : value.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase)
                ? "postgres://"
                : null;

        if (prefix is null)
        {
            return false;
        }

        var remainder = value[prefix.Length..];
        var slashIndex = remainder.IndexOf('/');
        if (slashIndex <= 0)
        {
            return false;
        }

        var authority = remainder[..slashIndex];
        var pathAndQuery = remainder[(slashIndex + 1)..];
        var atIndex = authority.LastIndexOf('@');
        if (atIndex <= 0 || atIndex == authority.Length - 1)
        {
            return false;
        }

        var userInfo = authority[..atIndex];
        var hostPort = authority[(atIndex + 1)..];
        if (!TrySplitHostAndPort(hostPort, out var host, out var port))
        {
            return false;
        }

        var userSeparatorIndex = userInfo.IndexOf(':');
        var username = userSeparatorIndex >= 0 ? userInfo[..userSeparatorIndex] : userInfo;
        var password = userSeparatorIndex >= 0 ? userInfo[(userSeparatorIndex + 1)..] : string.Empty;

        var querySeparatorIndex = pathAndQuery.IndexOf('?');
        var database = querySeparatorIndex >= 0 ? pathAndQuery[..querySeparatorIndex] : pathAndQuery;
        var query = querySeparatorIndex >= 0 ? pathAndQuery[querySeparatorIndex..] : string.Empty;

        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = host,
            Port = port,
            Database = Uri.UnescapeDataString(database),
            Username = Uri.UnescapeDataString(username),
        };

        if (!string.IsNullOrEmpty(password))
        {
            builder.Password = Uri.UnescapeDataString(password);
        }

        ApplyQueryStringOptions(builder, query);
        normalized = builder.ConnectionString;
        return true;
    }

    private static bool TrySplitHostAndPort(string hostPort, out string host, out int port)
    {
        host = string.Empty;
        port = 5432;
        if (string.IsNullOrWhiteSpace(hostPort))
        {
            return false;
        }

        if (hostPort.StartsWith("[", StringComparison.Ordinal))
        {
            var endBracketIndex = hostPort.IndexOf(']');
            if (endBracketIndex <= 1)
            {
                return false;
            }

            host = hostPort[1..endBracketIndex];
            if (endBracketIndex + 1 >= hostPort.Length)
            {
                return true;
            }

            if (hostPort[endBracketIndex + 1] != ':')
            {
                return false;
            }

            return int.TryParse(hostPort[(endBracketIndex + 2)..], out port);
        }

        var lastColonIndex = hostPort.LastIndexOf(':');
        if (lastColonIndex > 0 && hostPort.IndexOf(':') == lastColonIndex)
        {
            host = hostPort[..lastColonIndex];
            return int.TryParse(hostPort[(lastColonIndex + 1)..], out port);
        }

        host = hostPort;
        return true;
    }

    private static void ApplyQueryStringOptions(NpgsqlConnectionStringBuilder builder, string queryString)
    {
        if (string.IsNullOrWhiteSpace(queryString))
        {
            return;
        }

        var queryValues = QueryHelpers.ParseQuery(queryString);
        foreach (var pair in queryValues)
        {
            if (pair.Value.Count == 0)
            {
                continue;
            }

            var key = pair.Key;
            var parsedValue = pair.Value[0];
            if (!string.IsNullOrWhiteSpace(key) && parsedValue is not null)
            {
                try
                {
                    builder[key] = parsedValue;
                }
                catch (ArgumentException)
                {
                    // Ignore unknown query keys from URL-style connection strings.
                }
            }
        }
    }
}
