using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Identity.Implementation;

public sealed class RuntimeRoleConnectionInterceptor(string roleName) : DbConnectionInterceptor
{
    private readonly string roleName = IsSafeIdentifier(roleName)
        ? roleName
        : throw new ArgumentException("The PostgreSQL runtime role is invalid.", nameof(roleName));

    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
    {
        using var command = connection.CreateCommand();
        command.CommandText = $"SET ROLE \"{roleName}\"";
        command.ExecuteNonQuery();
    }

    public override async Task ConnectionOpenedAsync(
        DbConnection connection,
        ConnectionEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = $"SET ROLE \"{roleName}\"";
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static bool IsSafeIdentifier(string value) =>
        value.Length is > 0 and <= 63
        && (char.IsAsciiLetter(value[0]) || value[0] == '_')
        && value.All(character => char.IsAsciiLetterOrDigit(character) || character == '_');
}
