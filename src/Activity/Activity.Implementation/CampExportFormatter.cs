using System.Text;
using Activity.Contracts;
using Identity.Contracts;

namespace Activity.Implementation;

public sealed class CampExportFormatter(ITenantAccessControl accessControl) : ICampExportFormatter
{
    private const int MaximumColumns = 100;
    private const int MaximumRows = 100_000;
    private const int MaximumCellLength = 50_000;

    public async Task<CsvDocument> FormatAsync(
        CampCsvRequest request,
        CancellationToken cancellationToken)
    {
        var decision = await accessControl.AuthorizeCampAsync(
            new CampAccessRequest(
                request.ActorId,
                request.OrganizationId,
                request.CampId,
                CampAction.Export),
            cancellationToken);
        if (!decision.Allowed)
        {
            throw Rule("access_denied", "Für den Export fehlt die Berechtigung.");
        }

        ValidateShape(request);
        var csv = new StringBuilder();
        AppendRow(csv, request.GermanHeaders);
        foreach (var row in request.Rows)
        {
            cancellationToken.ThrowIfCancellationRequested();
            AppendRow(csv, row);
        }

        var content = Encoding.UTF8.GetBytes(csv.ToString());
        var bytes = new byte[Encoding.UTF8.Preamble.Length + content.Length];
        Encoding.UTF8.Preamble.CopyTo(bytes);
        content.CopyTo(bytes, Encoding.UTF8.Preamble.Length);
        return new CsvDocument(bytes, "text/csv; charset=utf-8");
    }

    private static void ValidateShape(CampCsvRequest request)
    {
        if (request.GermanHeaders.Count is < 1 or > MaximumColumns)
        {
            throw Rule("invalid_csv_columns", $"Der Export benötigt zwischen 1 und {MaximumColumns} Spalten.");
        }

        if (request.Rows.Count > MaximumRows)
        {
            throw Rule("too_many_csv_rows", $"Der Export darf höchstens {MaximumRows} Zeilen enthalten.");
        }

        if (request.GermanHeaders.Any(string.IsNullOrWhiteSpace))
        {
            throw Rule("csv_header_required", "Jede Exportspalte benötigt eine deutsche Überschrift.");
        }

        if (request.Rows.Any(row => row.Count != request.GermanHeaders.Count))
        {
            throw Rule("csv_column_mismatch", "Alle Exportzeilen müssen zur Anzahl der Überschriften passen.");
        }
    }

    private static void AppendRow(StringBuilder csv, IReadOnlyList<string?> cells)
    {
        for (var index = 0; index < cells.Count; index++)
        {
            if (index > 0)
            {
                csv.Append(',');
            }

            AppendCell(csv, cells[index] ?? string.Empty);
        }

        csv.Append("\r\n");
    }

    private static void AppendCell(StringBuilder csv, string value)
    {
        if (value.Length > MaximumCellLength)
        {
            throw Rule("csv_cell_too_long", $"Eine Exportzelle darf höchstens {MaximumCellLength} Zeichen enthalten.");
        }

        var protectedValue = StartsFormula(value) ? $"'{value}" : value;
        if (protectedValue.IndexOfAny([',', '"', '\r', '\n']) < 0)
        {
            csv.Append(protectedValue);
            return;
        }

        csv.Append('"');
        csv.Append(protectedValue.Replace("\"", "\"\"", StringComparison.Ordinal));
        csv.Append('"');
    }

    private static bool StartsFormula(string value) =>
        value.Length > 0 && value[0] is '=' or '+' or '-' or '@' or '\t' or '\r' or '\n';

    private static ActivityRuleException Rule(string code, string message) => new(code, message);
}
