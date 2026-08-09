using Activity.Contracts;
using System.Globalization;
using Xunit;

namespace Activity.Tests;

public sealed class ActivityContractAcceptanceTests
{
    [Fact]
    public void JournalCommandContainsMetadataOnly()
    {
        var command = new RecordActivity(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            ActivityKind.Created,
            "Note",
            Guid.NewGuid(),
            "Packliste",
            DateTimeOffset.Parse("2026-08-07T12:00:00Z", CultureInfo.InvariantCulture));

        Assert.Equal(ActivityKind.Created, command.Kind);
        Assert.Equal("Packliste", command.Title);
    }
}
