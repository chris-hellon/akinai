# TODO

Running list of known work. Nothing here is urgent-broken — the app builds, 329 tests
pass, and the cross-border compliance work is live end to end.

## Half-built: tested logic with nothing calling it

Both of these are real, tested libraries that are not yet features. The logic is done;
the persistence and the wiring are not.

- [ ] **Reconciliation — finish the edges.** The core is wired: matching a payment now
      classifies the difference and stores it, the bank import shows it in plain language,
      and a withholding certificate can be ticked off. What is left:
      - The FX rate used is the rate *now*, not the rate the invoice was booked at, because
        no rate is stored at issue and the exchange service has no historical lookup.
        Reasonable for a payment that just arrived, wrong for one reconciled months later.
      - The SHA/OUR/BEN charge convention is never captured, so a correspondent fee cannot
        be attributed to whoever undertook to pay it. A field on `CurrencyPaymentSettings`
        would close it.
      - No "certificates outstanding" view. The data is there per transaction; the year-end
        question — what am I still owed paperwork for — has no screen.
      - The bank-fee band assumes a major-currency receipt (8–60 units); a JPY or VND
        receipt lands as unexplained instead. Safe direction, still a gap.
      - The withholding rates are indicative. `CN/KR/TW/PK/TR/CO` most deserve an
        accountant's eye; Vietnam was deliberately left out of the table.

- [ ] **Dunning.** `DunningSchedule` and `DunningTemplates` cover a four-stage escalation
      in all 8 languages, with business-hours and weekend suppression in the client's
      timezone. Nothing sends. Needs a send-history table (the schedule is a pure function
      and assumes the caller records sends atomically), a background sender, and hooking
      the existing `SmtpEmailSender`.
      - Tone needs a native reader before anything goes out: Spanish is pitched at
        Peninsular Spanish, Chinese at mainland practice only, Vietnamese may be too soft
        to read as final, and Korean's seven-day deadline is unverified. Details are in
        comments in `DunningTemplates.cs`.

- [ ] **Evidence pack has no UI.** Delivery, document hash, view history and signature are
      recorded and `GET /api/invoices/{id}/evidence` returns them. Nothing displays or
      exports it.

## Blocked on a decision or an install

- [ ] **Upgrade off .NET 10 RC1.** SDK and every package are pinned to `10.0.100-rc.1` /
      `10.0.0-rc.1`, and RC1 is the only 10.x SDK installed. Needs the GA SDK installed
      first — a system-level install, not something a coding session should do unattended.
      Highest-leverage item left: it probably clears the advisories below too.

- [ ] **Three high-severity advisories**, all transitive — no direct `PackageReference`
      exists for any of them:
      - `Microsoft.Build.Tasks.Core` 17.14.8 — GHSA-w3q9-fxm7-j8fq
      - `Microsoft.Build.Utilities.Core` 17.14.8 — GHSA-w3q9-fxm7-j8fq
      - `SQLitePCLRaw.lib.e_sqlite3` 2.1.11 — GHSA-2m69-gcr7-jv3q

- [ ] **Decide whether CLAUDE.md should be tracked.** Gitignored at `.gitignore:206` in a
      deliberate `# Claude Code` block, never committed, so the maintained version exists
      only on this machine — a fresh clone gets nothing.

- [ ] **`GetProjectProfitabilityHandler` measures revenue on raw `Hours.Value`**, not
      `BillableHours`, so reported profitability does not match what was billed. Left alone
      deliberately: whether profitability tracks hours worked or hours billed is a reporting
      decision, not a bug.

- [ ] **Stale branch `claude/upbeat-gould-81088a`** (`e1722ab`, mid-July). Worktree pruned;
      the branch is still there.

## Worth doing

- [ ] **Online tax-ID validation.** Format and checksum validation is done offline; the
      live lookups are not. VIES for the EU (free) and the UK VAT API would confirm a number
      is not merely well-formed but real and currently registered. Store the response and
      timestamp — that record is audit evidence, and being able to produce it is itself a
      selling point.

- [ ] **A sweep for interrupted sends.** Sending is two-phase: the status is committed
      before the document is rendered, and rolled back if the email fails. A process that
      dies between the phases leaves `Status = Sent` with `SentAtUtc IS NULL`. Wrong but
      detectable, and it over-reports rather than losing a send. A background reconcile
      could clear it.

- [ ] **Extend handler test coverage.** `InvoiceTestContext` gives handlers a real SQLite
      database. It now serves invoice totals, compliance, pre-flight and evidence; the same
      fixture would serve estimates, recurring invoices, expenses and the PDF renderer, none
      of which have coverage.

- [ ] **Localized "how to pay me" walkthrough** per rail, not just account details. Done for
      VietQR; generalise it.

- [ ] **Nine nullable-reference warnings** (CS8604 ×5, CS8602 ×3, CS8601 ×1) across
      `PdfGeneratorService` (3), `IdentityComponentsEndpointRouteBuilderExtensions` (3),
      `ExternalLogin`, `TimeEntrySelector` and `ExpenseSelector`. Each needs judging on
      whether null is actually reachable, so none were silenced.

- [ ] **Tailwind is on v3.4.17**; v4 is out. The longer it waits the bigger the jump.

### Not worth building

More chart types, Slack, a mobile app, deeper time-tracking polish — competitors win those
on budget and none of them are why anyone would choose Akinai. Likewise no AI for its own
sake: the timesheet summariser earns its place by solving a real grouping problem.

## Housekeeping

- [ ] **Local connection strings still name the old database** — `Database=InvoiceSoftware`
      and `InvoiceSoftware.db` in the gitignored `appsettings.json`. Worth changing at the
      next local data reset rather than mid-flight.

## Done

### Invoice totals and rates

- [x] Read invoice totals from the stored snapshot everywhere (`d34be04`)
- [x] Fix an entry's billing rate at the moment it is invoiced (`86c970d`) — the rate is
      frozen when an entry is linked to an invoice, so a later change to a job, project or
      client rate can no longer restate an invoice already sent
- [x] Order invoice line items by date (`0a0a23f`)
- [x] Handler tests for invoice totals against a real SQLite database (`6546734`), each
      mutation-verified. Dropped the unused `EntityFrameworkCore.InMemory` package, which
      had pulled EF Core to a version its SQLite provider could not run against.

### Reconciliation

- [x] **A payment that differs from its invoice is now explained rather than assumed.**
      Matching classifies the difference — FX spread, correspondent fee, tax withheld at
      source, or a genuine shortfall — and stores it on the transaction. Only the last is
      ever presented as owed. A 10% deduction from India reads "paid in full, 10% withheld
      at source, certificate outstanding" instead of looking like an underpayment, which
      would otherwise mean chasing a client for tax they already remitted on your behalf.
- [x] Withholding certificates can be ticked off from the bank import screen, guarded so a
      certificate cannot be recorded against a payment that had no tax withheld.
- [x] Unmatching clears the analysis: it described a match that no longer exists, and left
      behind it would be stale and believed.

### Cross-border compliance

- [x] **Corridor compliance engine** (`60e5857`, `f7f0b27`). `(seller country, buyer
      country, business/consumer)` → tax treatment and required wording, compiled in the
      spirit of `InvoiceLabels`. Gets the post-Brexit case right: a UK seller billing the EU
      is making an export, not an intra-EU reverse charge. It advises and generates wording;
      it never alters a tax rate.
- [x] Country captured as an ISO code on clients and the business profile, alongside the
      free text rather than replacing it, with a conservative backfill. Client VAT number
      and business/consumer status added.
- [x] Required wording prints on the document — invoice view, public client copy, and the
      print page the PDF is taken from.
- [x] **Pre-flight check** before sending: IBAN mod-97, BIC format, purpose-of-payment
      corridors, VAT-number shape, and every corridor finding. Three severity tiers, and
      Send is never disabled — the rules can be wrong about a corridor, and being unable to
      invoice is worse than sending something imperfect.
- [x] **Offline VAT number validation** for all 27 EU formats plus GB and AU, with real
      checksums for GB (HMRC mod-97, both generations), AU (ABN mod-89) and DE (ISO 7064).
      The other 26 are structure-only on purpose — a validator that wrongly rejects a real
      VAT number is worse than one that only checks shape.
- [x] Purpose-of-payment keyed on the **payer's** country, not the seller's. It is an
      outbound exchange control, so it is the country the money leaves that demands it.

### Documents and delivery

- [x] **Two PDF renderers unified behind `InvoicePdfComposition`** — print the page, fall
      back to rebuilding it. Both the download and the send path go through it, so they
      cannot diverge again; they already did once, when a corridor-required tax statement
      was added to the print page and the emailed copy went out without it.
- [x] **Sending is transactional two-phase.** The status is committed before the document
      is rendered, because the print page loads the invoice through a different context and
      would otherwise stamp the client's copy "Draft". `AbandonSend` rolls it back if the
      render or email fails, guarded on `SentAtUtc` being null so it can never un-send an
      invoice that actually reached someone.
- [x] **Evidence pack**: SHA-256 of the exact PDF sent, with recipient and timestamp,
      written only after the email succeeds.

### Bugs found and fixed along the way

- [x] **Recurring invoice Generate Now / Pause / Resume did nothing** — `SecondaryButton`
      was never a component, so all three rendered as unknown HTML elements and their click
      handlers never bound.
- [x] **Client portal pagination threw whenever a client had more than one page** —
      `TotalPages` was passed as a parameter but is a private computed property on
      `Pagination`, and the required `TotalItems` was never passed.
- [x] **The emailed total omitted product line items**, so any invoice carrying products was
      emailed an amount lower than the invoice itself stated.
- [x] **The client portal derived balances from raw hours**, missing quarter-hour billing
      rounding, expenses and product lines.
- [x] **`IsBusiness` would have defaulted to false on every existing client**, silently
      suppressing reverse charge. EF generates `defaultValue: false` for a bool; the EF
      configuration now sets it true.
- [x] **A cross-currency receipt with no rate reported as settled** — it shared a cause with
      a verified FX conversion, so an unverified receipt counted as paid in full on no
      evidence. Now its own `RateUnknown` cause, excluded from `IsSettled`.
- [x] Compiler warnings 56 → 9 unique: nullable `ICall` response types (24), dead fields (9), an
      un-awaited async call, an unused injected dependency.
