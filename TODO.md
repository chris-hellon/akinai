# TODO

Running list of known work. Nothing here is urgent-broken — the app builds, tests pass,
and the invoice totals/rate work landed in `d34be04`, `86c970d`, `0a0a23f`.

## Blocked on a decision or an install

- [ ] **Upgrade off .NET 10 RC1.** SDK and every package are pinned to
      `10.0.100-rc.1` / `10.0.0-rc.1`, and RC1 is the only 10.x SDK installed on this
      machine. Needs the GA SDK installed first (a system-level install, not something
      a coding session should do unattended). Highest leverage item: it probably clears
      the vulnerability advisories below, and possibly the `dotnet ef` path quirk too.

- [ ] **Three high-severity advisories**, all transitive — no direct `PackageReference`
      exists for any of them, so they can only be resolved by moving the packages that
      pull them in:
      - `Microsoft.Build.Tasks.Core` 17.14.8 — GHSA-w3q9-fxm7-j8fq
      - `Microsoft.Build.Utilities.Core` 17.14.8 — GHSA-w3q9-fxm7-j8fq
      - `SQLitePCLRaw.lib.e_sqlite3` 2.1.11 — GHSA-2m69-gcr7-jv3q

- [ ] **Decide whether CLAUDE.md should be tracked.** It is gitignored
      (`.gitignore:206`, in a deliberate `# Claude Code` block) and has never been
      committed, so the maintained version exists only on this machine — a fresh clone
      gets nothing.

- [ ] **`GetProjectProfitabilityHandler` measures revenue on raw `Hours.Value`**, not
      `BillableHours`, so reported profitability does not match what was billed. Left
      alone deliberately during the rate work: whether profitability should track hours
      worked or hours billed is a reporting decision, not a bug.

- [ ] **Stale branch `claude/upbeat-gould-81088a`** (`e1722ab`, mid-July, 4 commits
      behind master). Worktree already pruned; the branch itself is still there.

## Differentiators (cross-border)

The payment rails are modelled thoroughly — IBAN, SWIFT, intermediary bank,
`PaymentPurposeCode`, VietQR. What is missing is the legal layer and the
did-the-money-actually-arrive layer, which is where cross-border billing actually hurts
and where general-purpose invoicing tools cannot follow.

- [ ] **Pre-flight payability check.** Validate IBAN (mod-97), BIC format, and whether the
      corridor requires a purpose-of-payment code (CN, IN, VN, AE and others do) before an
      invoice goes out. Every field already exists; nothing validates them. Prevents the
      most expensive failure in cross-border billing — a wire returned ten days later with
      fees deducted. No schema change.

- [ ] **Corridor compliance engine.** `(seller country, buyer country, B2B/B2C)` →
      required statements, required fields, tax treatment. EU B2B reverse charge needs both
      VAT numbers plus an explicit statement on the invoice; without it the client's
      accountant rejects it. Built as a compiled table in the spirit of `InvoiceLabels` —
      instant, free, and in the terms a local accountant expects.

- [ ] **Tax ID validation.** VIES for the EU (free), the UK VAT API, ABN for Australia.
      Validate when a client is saved and store the response and timestamp: that record is
      audit evidence, and being able to produce it is itself a selling point.

- [ ] **Reconciliation that understands cross-border money.** Three unrelated causes make
      the amount received differ from the amount invoiced, and every tool renders all three
      as "short payment": FX spread, correspondent bank fees (the SHA/OUR/BEN charge
      convention is not modelled even though the intermediary bank fields are), and
      withholding tax. The third is legitimate — in India, the Philippines, Indonesia,
      Japan and Brazil the payer withholds and remits, so the right response is to track a
      certificate, not chase the client. Extends the existing bank import matching.

- [ ] **Dunning in the client's language and business hours.** `OverdueInvoiceService`
      only flips a status today; it sends nothing. The translation engine and Claude are
      already wired. Tone needs calibrating per culture — a reminder pitched for a German
      client is actively damaging sent to a Japanese one.

- [ ] **Localized "how to pay me" walkthrough** per rail, not just account details.
      Already done for VietQR; generalise it.

- [ ] **Evidence pack.** `FirstViewedAtUtc`/`ViewCount` and `DigitalSignature` already
      exist. A hash-stamped delivery and view trail, exportable, is real leverage in a
      cross-jurisdiction dispute.

### Not worth building

More chart types, Slack, a mobile app, deeper time-tracking polish — competitors win those
on budget and none of them are why anyone would choose Akinai. Likewise no AI for its own
sake: the timesheet summariser earns its place by solving a real grouping problem.

## Worth doing

- [ ] **Extend handler test coverage.** The invoice-totals path now has six handler
      tests against a real SQLite database (`InvoiceTestContext`). The same fixture
      would serve estimates, recurring invoices, expenses and the PDF renderer, none of
      which have any coverage.

- [ ] **Tailwind is on v3.4.17**; v4 is out. Low priority, but the longer it waits the
      bigger the jump.

- [ ] **9 remaining nullable-reference warnings** (CS8604 ×5, CS8602 ×3, CS8601 ×1) in
      `PdfGeneratorService` (3), `IdentityComponentsEndpointRouteBuilderExtensions.cs:142`
      (3), `ExpenseSelector`, `TimeEntrySelector`. Each needs judging on whether null is
      actually reachable, so they were left rather than silenced.

## Housekeeping

- [ ] **Local connection strings still name the old database** —
      `Database=InvoiceSoftware` and `InvoiceSoftware.db` in the gitignored
      `appsettings.json`. Worth changing at the next local data reset rather than
      mid-flight.

## Done

- [x] Read invoice totals from the stored snapshot everywhere (`d34be04`)
- [x] Fix an entry's billing rate at the moment it is invoiced (`86c970d`)
- [x] Order invoice line items by date (`0a0a23f`)
- [x] Compiler warning cleanup, 47 -> 9: nullable `ICall` response types (24), dead
      fields (9), an un-awaited async call, an unused injected dependency
- [x] **Recurring invoice Generate Now / Pause / Resume did nothing** — `SecondaryButton`
      was never a component, so all three rendered as unknown HTML elements and their
      click handlers never bound. Component written, buttons work.
- [x] **Client portal pagination threw whenever a client had more than one page** —
      `TotalPages` was passed as a parameter but is a private computed property on
      `Pagination`, and the required `TotalItems` was never passed.
- [x] Handler-level tests for invoice totals, against a real SQLite database. Each was
      verified by mutation — reintroducing the original bug makes the matching test
      fail. Dropped the unused `EntityFrameworkCore.InMemory` package, which had pulled
      EF Core to a version its SQLite provider could not run against.
