using System.Text;
using Activity.Contracts;
using Activity.Implementation;
using Xunit;

namespace Activity.Tests;

public sealed class ActivityCsvTests
{
    private static readonly Guid ActorId = Guid.Parse("10000000-0000-0000-0000-000000000004");
    private static readonly Guid OrganizationId = Guid.Parse("20000000-0000-0000-0000-000000000001");
    private static readonly Guid CampId = Guid.Parse("30000000-0000-0000-0000-000000000001");

    [Fact]
    public async Task FormatProducesUtf8CsvWithGermanHeadersAndRfcQuoting()
    {
        var subject = new CampExportFormatter(new AllowActivityAccess());

        var result = await subject.FormatAsync(
            new CampCsvRequest(
                ActorId,
                OrganizationId,
                CampId,
                ["Titel", "Ort", "Notiz"],
                [["Müsli", "Berlin, Mitte", "Er sagte \"Hallo\"\r\nWeiter"]]),
            TestContext.Current.CancellationToken);

        var bytes = result.Content.ToArray();
        Assert.True(bytes.AsSpan().StartsWith(Encoding.UTF8.Preamble));
        Assert.Equal(
            "Titel,Ort,Notiz\r\nMüsli,\"Berlin, Mitte\",\"Er sagte \"\"Hallo\"\"\r\nWeiter\"\r\n",
            Encoding.UTF8.GetString(bytes.AsSpan(Encoding.UTF8.Preamble.Length)));
        Assert.Equal("text/csv; charset=utf-8", result.MediaType);
    }

    [Fact]
    public async Task FormatNeutralizesSpreadsheetFormulaPrefixes()
    {
        var subject = new CampExportFormatter(new AllowActivityAccess());

        var result = await subject.FormatAsync(
            new CampCsvRequest(
                ActorId,
                OrganizationId,
                CampId,
                ["Gleich", "Plus", "Minus", "At", "Tab", "CR"],
                [["=1+1", "+1", "-2", "@cmd", "\tBefehl", "\rBefehl"]]),
            TestContext.Current.CancellationToken);

        var csv = Encoding.UTF8.GetString(result.Content.Span[Encoding.UTF8.Preamble.Length..]);
        Assert.Equal(
            "Gleich,Plus,Minus,At,Tab,CR\r\n'=1+1,'+1,'-2,'@cmd,'\tBefehl,\"'\rBefehl\"\r\n",
            csv);
    }

    [Fact]
    public async Task DeniedActorCannotExport()
    {
        var subject = new CampExportFormatter(new DenyActivityAccess());

        var exception = await Assert.ThrowsAsync<ActivityRuleException>(() => subject.FormatAsync(
            new CampCsvRequest(ActorId, OrganizationId, CampId, ["Titel"], [["Geheim"]]),
            TestContext.Current.CancellationToken));

        Assert.Equal("access_denied", exception.ErrorCode);
    }
}
