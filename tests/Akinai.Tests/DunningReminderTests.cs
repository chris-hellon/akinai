using Akinai.Domain.Entities;
using Akinai.Domain.Exceptions;
using Akinai.Shared.Geography;
using FluentAssertions;

namespace Akinai.Tests;

/// <summary>
/// The record of what was actually sent, and the offsets that decide when. The schedule
/// itself is covered in DunningTests; these cover the pieces the sender depends on.
/// </summary>
public class DunningReminderTests
{
    private static readonly DateTime Sent = new(2026, 10, 29, 9, 30, 0, DateTimeKind.Utc);

    [Fact]
    public void A_reminder_records_what_was_sent_and_where()
    {
        var reminder = DunningReminder.Record(
            Guid.NewGuid(), Guid.NewGuid(), "user-1",
            "FirmReminder", Sent, "client@example.com", "German");

        reminder.Stage.Should().Be("FirmReminder");
        reminder.ToEmail.Should().Be("client@example.com");
        reminder.Language.Should().Be("German");
        reminder.SentAtUtc.Should().Be(Sent);
    }

    [Fact]
    public void A_reminder_without_a_stage_is_refused()
    {
        // The schedule decides what to send next purely from what has already gone out,
        // so a row that does not say which rung it was is worse than no row.
        var record = () => DunningReminder.Record(
            Guid.NewGuid(), Guid.NewGuid(), "user-1", "", Sent, "client@example.com", "English");

        record.Should().Throw<DomainException>();
    }

    [Fact]
    public void A_reminder_without_a_recipient_is_refused()
    {
        var record = () => DunningReminder.Record(
            Guid.NewGuid(), Guid.NewGuid(), "user-1", "GentleReminder", Sent, "", "English");

        record.Should().Throw<DomainException>();
    }

    [Theory]
    [InlineData("GB", 0)]
    [InlineData("DE", 1)]
    [InlineData("VN", 7)]
    [InlineData("JP", 9)]
    [InlineData("CO", -5)]
    public void Single_zone_countries_get_their_own_working_day(string country, int expectedHours)
        => CountryTimeZones.OffsetFor(country).TotalHours.Should().Be(expectedHours);

    [Fact]
    public void India_keeps_its_half_hour()
        => CountryTimeZones.OffsetFor("IN").Should().Be(new TimeSpan(5, 30, 0));

    [Theory]
    [InlineData("US")]
    [InlineData("CA")]
    [InlineData("AU")]
    [InlineData("RU")]
    [InlineData("BR")]
    public void Countries_spanning_several_zones_are_not_guessed_at(string country)
    {
        // Picking one of their zones would be wrong for most of the country, so these
        // fall back to UTC — no worse than having no notion of the client's day.
        CountryTimeZones.IsKnown(country).Should().BeFalse();
        CountryTimeZones.OffsetFor(country).Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public void An_unknown_country_falls_back_to_UTC()
    {
        CountryTimeZones.OffsetFor(null).Should().Be(TimeSpan.Zero);
        CountryTimeZones.OffsetFor("ZZ").Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public void Business_profiles_do_not_chase_anyone_until_asked()
    {
        // Opt-in without exception: the first a user would know of an unasked-for default
        // is a client replying to an email they did not know had been sent.
        var profile = BusinessProfile.Create("user-1", "Acme", "me@example.com");

        profile.AutomaticRemindersEnabled.Should().BeFalse();

        profile.SetAutomaticReminders(true);
        profile.AutomaticRemindersEnabled.Should().BeTrue();
    }
}
