using Knowledge.Contracts;
using Xunit;

namespace Knowledge.Tests;

public sealed class KnowledgeContractAcceptanceTests
{
    [Fact]
    public void NoteContentCarriesSharedNotebookFields()
    {
        var content = new NoteContent(
            "Packliste",
            "## Vorbereitung\n\n- Bibeln einpacken",
            ["Leitung"],
            true,
            [new NoteLinkReference(NoteLinkTargetType.ScheduleEntry, Guid.NewGuid())]);

        Assert.Equal("Packliste", content.Title);
        Assert.True(content.IsPinned);
        Assert.Single(content.Links);
    }
}
