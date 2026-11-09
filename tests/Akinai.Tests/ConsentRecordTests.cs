using Akinai.Domain.Entities;
using Akinai.Domain.Exceptions;
using Akinai.Shared.Legal;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Akinai.Tests;

/// <summary>
/// Consent has to be explicit, separable and demonstrable. The first two are properties of
/// the registration form; the third is this table, and it is the one that can rot quietly —
/// a record missing the document version, or overwritten on withdrawal, still looks like a
/// record right up until somebody asks it a question.
/// </summary>
public class ConsentRecordTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static readonly DateTime When = new(2026, 11, 9, 10, 30, 0, DateTimeKind.Utc);

    [Fact]
    public void A_record_captures_what_was_agreed_and_which_version()
    {
        var record = ConsentRecord.Record(
            "user-1", ConsentPurpose.TermsAndPrivacy, granted: true,
            LegalDocuments.CurrentVersion, When, source: "registration", ipAddress: "203.0.113.9");

        record.UserId.Should().Be("user-1");
        record.Purpose.Should().Be("TermsAndPrivacy");
        record.Granted.Should().BeTrue();
        record.DocumentVersion.Should().Be(LegalDocuments.CurrentVersion);
        record.RecordedAtUtc.Should().Be(When);
        record.Source.Should().Be("registration");
        record.IpAddress.Should().Be("203.0.113.9");
    }

    [Fact]
    public void A_declined_optional_consent_is_recorded_rather_than_omitted()
    {
        // "They said no" is a position to be able to evidence. An absent row cannot be
        // told apart from one that was never written, which is the same problem the old
        // small-print line had.
        var record = ConsentRecord.Record(
            "user-1", ConsentPurpose.ProductEmails, granted: false,
            LegalDocuments.CurrentVersion, When, source: "registration");

        record.Granted.Should().BeFalse();
        record.Purpose.Should().Be("ProductEmails");
    }

    [Fact]
    public void A_record_without_a_document_version_is_refused()
    {
        // Without it the row says somebody agreed to "the terms" without saying which,
        // and the terms change.
        var make = () => ConsentRecord.Record(
            "user-1", ConsentPurpose.TermsAndPrivacy, true, "", When, "registration");

        make.Should().Throw<DomainException>();
    }

    [Fact]
    public void A_record_without_a_user_is_refused()
    {
        var make = () => ConsentRecord.Record(
            "", ConsentPurpose.TermsAndPrivacy, true, LegalDocuments.CurrentVersion, When, "registration");

        make.Should().Throw<DomainException>();
    }

    [Fact]
    public void A_record_without_a_source_is_refused()
    {
        var make = () => ConsentRecord.Record(
            "user-1", ConsentPurpose.TermsAndPrivacy, true, LegalDocuments.CurrentVersion, When, "");

        make.Should().Throw<DomainException>();
    }

    [Fact]
    public void The_address_is_optional()
    {
        // Behind a proxy there may be none, and a missing header must never be a reason to
        // refuse a registration — it corroborates the consent, it is not the consent.
        var record = ConsentRecord.Record(
            "user-1", ConsentPurpose.TermsAndPrivacy, true,
            LegalDocuments.CurrentVersion, When, "registration", ipAddress: null);

        record.IpAddress.Should().BeNull();
    }

    [Fact]
    public async Task Withdrawal_appends_rather_than_overwriting()
    {
        using var ctx = new InvoiceTestContext();
        await ctx.SeedAsync();

        await using (var db = ctx.NewContext())
        {
            db.ConsentRecords.Add(ConsentRecord.Record(
                InvoiceTestContext.UserId, ConsentPurpose.ProductEmails, granted: true,
                "2026-11-09", When, "registration"));
            db.ConsentRecords.Add(ConsentRecord.Record(
                InvoiceTestContext.UserId, ConsentPurpose.ProductEmails, granted: false,
                "2026-11-09", When.AddMonths(3), "settings"));
            await db.SaveChangesAsync(Ct);
        }

        await using var check = ctx.NewContext();
        var history = await check.ConsentRecords
            .Where(c => c.Purpose == "ProductEmails")
            .OrderBy(c => c.RecordedAtUtc)
            .ToListAsync(Ct);

        // Both rows survive. What matters in a dispute is what was true on the day a thing
        // was sent, not what is true now — so the grant that authorised an earlier email
        // has to outlive its withdrawal.
        history.Should().HaveCount(2);
        history[0].Granted.Should().BeTrue();
        history[1].Granted.Should().BeFalse();
        history[1].Source.Should().Be("settings");
    }

    [Fact]
    public async Task The_current_position_is_the_most_recent_row_for_that_purpose()
    {
        using var ctx = new InvoiceTestContext();
        await ctx.SeedAsync();

        await using (var db = ctx.NewContext())
        {
            db.ConsentRecords.Add(ConsentRecord.Record(
                InvoiceTestContext.UserId, ConsentPurpose.ProductEmails, true, "2026-11-09", When, "registration"));
            db.ConsentRecords.Add(ConsentRecord.Record(
                InvoiceTestContext.UserId, ConsentPurpose.ProductEmails, false, "2026-11-09", When.AddMonths(3), "settings"));
            db.ConsentRecords.Add(ConsentRecord.Record(
                InvoiceTestContext.UserId, ConsentPurpose.TermsAndPrivacy, true, "2026-11-09", When, "registration"));
            await db.SaveChangesAsync(Ct);
        }

        await using var check = ctx.NewContext();
        var latest = await check.ConsentRecords
            .Where(c => c.UserId == InvoiceTestContext.UserId && c.Purpose == "ProductEmails")
            .OrderByDescending(c => c.RecordedAtUtc)
            .FirstAsync(Ct);

        latest.Granted.Should().BeFalse("the most recent row for a purpose is the position");

        // And the other purpose is untouched by it — that is what separable means.
        var terms = await check.ConsentRecords
            .Where(c => c.Purpose == "TermsAndPrivacy")
            .OrderByDescending(c => c.RecordedAtUtc)
            .FirstAsync(Ct);

        terms.Granted.Should().BeTrue();
    }

    [Fact]
    public void Every_purpose_is_stored_by_name_not_by_number()
    {
        // Stored as text so renumbering the enum cannot re-point historical rows at a
        // different purpose — a row that silently changes meaning is worse than no row.
        foreach (var purpose in Enum.GetValues<ConsentPurpose>())
        {
            var record = ConsentRecord.Record(
                "user-1", purpose, true, LegalDocuments.CurrentVersion, When, "registration");

            record.Purpose.Should().Be(purpose.ToString());
            record.Purpose.Should().NotMatchRegex("^[0-9]+$");
        }
    }

    [Fact]
    public void The_published_document_version_is_a_readable_date()
    {
        // Dated rather than numbered so a stored version is legible without a lookup.
        LegalDocuments.CurrentVersion.Should().MatchRegex(@"^\d{4}-\d{2}-\d{2}$");
    }
}
