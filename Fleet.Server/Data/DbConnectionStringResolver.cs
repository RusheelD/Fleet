using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace Fleet.Server.Data;

public static class DbConnectionStringResolver
{
    public static string? ResolveFleetDbConnectionString(IConfiguration configuration)
    {
        var candidates = new[]
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

        foreach (var candidate in candidates)
        {
            var normalized = NormalizePostgresConnectionString(candidate);
            if (!string.IsNullOrWhiteSpace(normalized))
            {
                return normalized;
            }
        }

        return null;
    }

    private static string? NormalizePostgresConnectionString(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var value = raw.Trim();
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri))
        {
            return value;
        }

        if (!string.Equals(uri.Scheme, "postgres", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(uri.Scheme, "postgresql", StringComparison.OrdinalIgnoreCase))
        {
            return value;
        }

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

        var queryValues = QueryHelpers.ParseQuery(uri.Query);
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

        return builder.ConnectionString;
    }
}
