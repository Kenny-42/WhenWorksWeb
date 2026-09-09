using Microsoft.EntityFrameworkCore;
using WhenWorksWeb.Models;
using WhenWorksWeb.Tests.TestData;

namespace WhenWorksWeb.Tests.Fixtures;

/// <summary>
/// Proves <see cref="SqliteDbContextFixture"/> actually enforces the same relational behavior production
/// (SQL Server) would — this is the whole reason for using real SQLite instead of the EF Core InMemory
/// provider. Each fact below verifies one specific behavior that InMemory would have silently let through.
/// </summary>
public class SqliteDbContextFixtureTests : SqliteDbContextFixture
{
    [Fact]
    public async Task Builders_ProduceEntitiesThatSaveSuccessfully()
    {
        var evt = new EventBuilder().Build();
        Db.Events.Add(evt);
        await Db.SaveChangesAsync();

        var participant = new ParticipantBuilder().ForEvent(evt).Build();
        Db.Participants.Add(participant);
        await Db.SaveChangesAsync();

        var eventDate = new EventDateBuilder().ForEvent(evt).Build();
        Db.EventDates.Add(eventDate);
        await Db.SaveChangesAsync();

        var message = new EventMessageBuilder().ForEvent(evt).FromParticipant(participant.Id).Build();
        Db.EventMessages.Add(message);
        await Db.SaveChangesAsync();

        Assert.True(evt.Id > 0);
        Assert.True(participant.Id > 0);
        Assert.True(eventDate.Id > 0);
        Assert.True(message.Id > 0);
    }

    [Fact]
    public async Task EventCode_UniqueIndex_IsCaseInsensitive()
    {
        // Proves the SQL_Latin1_General_CP1_CI_AS collation registered in the fixture actually behaves
        // case-insensitively, matching CODING_CONVENTIONS.md's documented intent for Event.Code.
        Db.Events.Add(new EventBuilder().WithCode("bcdfgh").Build());
        await Db.SaveChangesAsync();

        Db.Events.Add(new EventBuilder().WithCode("BCDFGH").Build());

        await Assert.ThrowsAsync<DbUpdateException>(() => Db.SaveChangesAsync());
    }

    [Fact]
    public async Task ParticipantDisplayName_CheckConstraint_RejectsUntrimmedValue()
    {
        // Proves the CK_Participants_DisplayName_Trimmed check constraint (SQL Server bracket-quoted
        // identifiers and LTRIM/RTRIM) actually translates to working SQLite DDL.
        var evt = new EventBuilder().Build();
        Db.Events.Add(evt);
        await Db.SaveChangesAsync();

        Db.Participants.Add(new ParticipantBuilder().ForEvent(evt).WithDisplayName(" Untrimmed ").Build());

        await Assert.ThrowsAsync<DbUpdateException>(() => Db.SaveChangesAsync());
    }

    [Fact]
    public async Task Participant_WithNonExistentEventId_ViolatesForeignKeyConstraint()
    {
        // Proves foreign key enforcement is actually on for this connection (SQLite defaults FK
        // enforcement OFF; EF Core's Sqlite provider turns it on, verified here rather than assumed).
        Db.Participants.Add(new ParticipantBuilder().ForEvent(eventId: 999_999).Build());

        await Assert.ThrowsAsync<DbUpdateException>(() => Db.SaveChangesAsync());
    }

    [Fact]
    public async Task EachTestMethod_GetsAnIndependentDatabase_FirstOfTwoIdenticalInserts()
    {
        // Paired with the test below: both insert an Event with the same code. If state leaked between
        // test methods (e.g. a shared connection/database), one of these two tests would fail with a
        // unique constraint violation. Both passing independently proves per-test isolation.
        Db.Events.Add(new EventBuilder().WithCode("BCDFGH").Build());
        await Db.SaveChangesAsync();

        Assert.Equal(1, await Db.Events.CountAsync());
    }

    [Fact]
    public async Task EachTestMethod_GetsAnIndependentDatabase_SecondOfTwoIdenticalInserts()
    {
        Db.Events.Add(new EventBuilder().WithCode("BCDFGH").Build());
        await Db.SaveChangesAsync();

        Assert.Equal(1, await Db.Events.CountAsync());
    }
}
