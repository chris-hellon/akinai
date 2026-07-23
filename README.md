# Akinai 商い

Invoicing for people who bill across borders.

Your client receives the invoice in **their** language, priced in **their** currency, payable on a **rail they already use** — VietQR, bank transfer, PayPal, Wise or Revolut. You keep working in English.

Built with .NET 10 and Blazor Web App (Interactive Auto).

---

## What makes it different

Most invoicing tools assume you and your client share a language and a banking system. Akinai assumes you don't.

- **Documents in the client's language.** Fixed labels — Subtotal, Amount Due, Bill To — come from a translation table in code, so they render instantly, cost nothing, and use the term a local accountant expects. Only the text *you* wrote (job descriptions, notes) goes through machine translation.
- **Language pins to the invoice.** Once issued, an invoice keeps the language it was sent in. Changing your default later cannot rewrite a document your client already has.
- **The PDF is the web page.** Both are rendered from the same Blazor component — the PDF is printed from `/invoice/print/{token}` by headless Chromium. There is no second implementation to drift.
- **Cross-border payment details.** Correspondent bank, SWIFT/BIC, purpose-of-payment codes — the fields that stop a wire from Asia being returned.

Eight languages: English, Spanish, French, German, Chinese, Vietnamese, Russian, Korean.

---

## Features

**Invoicing** — invoices, estimates clients can accept and sign online, recurring billing, public share links, four visual templates, view tracking (know when your client opened it).

**Work tracking** — timesheets against projects and jobs, expenses, recurring expenses, a product/service catalogue.

**Money in** — multi-currency with live exchange rates, per-currency bank details, VietQR, PayPal/Wise/Revolut links, CSV bank-statement import matched against open invoices.

**Billing** — Free and Pro plans, editable from the admin area without a deploy. Entitlements are resolved server-side, never trusted from the UI.

**AI** — invoice text translated through Azure Translator, cached by content hash so the same phrase is paid for once, and capped per account per month.

**Security** — ASP.NET Core Identity, two-factor, passkeys/WebAuthn, external OAuth.

---

## Project structure

```
Akinai.sln
├── src/
│   ├── Akinai/                  # ASP.NET Core host, Identity UI, print routes
│   ├── Akinai.Client/           # Blazor WebAssembly — app pages and components
│   ├── Akinai.Domain/           # Entities, enums, value objects
│   ├── Akinai.Infrastructure/   # EF Core, handlers, services (PDF, translation, billing)
│   ├── Akinai.Shared/           # API contracts, DTOs, plan catalogue, invoice labels
│   └── fetch-fonts.sh           # Downloads PDF fonts at build time
└── tests/
    └── Akinai.Tests/            # xUnit
```

---

## Getting started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- SQL Server (or SQLite — see *Database provider* below)
- Node.js — only to rebuild the Tailwind stylesheet

### Run it

```bash
git clone <repository-url>
cd Akinai

# Configuration is gitignored; start from the example
cp src/Akinai/appsettings.example.json src/Akinai/appsettings.json

dotnet build Akinai.sln
dotnet run --project src/Akinai/Akinai.csproj
```

The schema is applied at startup — SQL Server runs EF migrations, SQLite is created from the model.

### Other commands

```bash
# Hot reload
dotnet watch --project src/Akinai/Akinai.csproj

# Tests
dotnet test Akinai.sln

# Rebuild Tailwind after editing wwwroot/css/input.css
cd src/Akinai && npm run css:build
```

### Migrations

Migrations live in Infrastructure, but the connection string lives in the host — so both projects are needed:

```bash
dotnet ef migrations add <Name> --project src/Akinai.Infrastructure --startup-project src/Akinai
dotnet ef database update      --project src/Akinai.Infrastructure --startup-project src/Akinai
```

Migrations are **SQL Server-specific**. Keep `DatabaseProvider` set to `SqlServer` when adding one.

---

## Configuration

All of the below live in `src/Akinai/appsettings.json`, which is gitignored — put real secrets there, not in the example file.

| Section | Needed for | Without it |
|---|---|---|
| `ConnectionStrings` | The database | Won't start |
| `Admin` | Seeds the first admin account | No admin user |
| `Translation` | Azure Translator key + region | Documents show English text under translated labels |
| `ExchangeRate` | Live FX rates | Falls back to hardcoded rates |
| `Smtp` | Sending invoices by email | Emails are logged, not sent |

**The Azure Translator region must match your resource** — a mismatch returns 401, and because translation fails soft you'll simply see untranslated text with no error.

### Database provider

SQL Server by default. Set `"DatabaseProvider": "Sqlite"` to use SQLite instead — handy for local work, but note SQLite has no native `decimal`, so money ordering and aggregation are approximate. Fine for development; weigh it for production.

### PDF rendering

PDFs are produced by printing the app's own invoice page with headless Chromium (PuppeteerSharp). Chromium downloads on first use (~150 MB) and is cached.

Fonts are **fetched at build time**, not committed — `src/fetch-fonts.sh` pulls Noto Sans and Noto Sans SC into `src/Akinai.Infrastructure/Fonts/`, which is gitignored. Without them the PDF falls back to Lato and non-Latin scripts won't render.

In a container, bake Chromium into the image rather than fetching at runtime.

---

## Architecture notes

**API layer** — [BlazorUtils.EasyApi](https://github.com/) gives a contract/handler pattern. Contracts in `Akinai.Shared/Api/`, handlers in `Akinai.Infrastructure/Handlers/`, called client-side through `ICall<TRequest, TResponse>`.

> Parameters need explicit `[RouteParam]`, `[QueryStringParam]` or `[BodyParam]` attributes. Without them, route placeholders aren't substituted and body values aren't serialised — and because server-side calls bypass HTTP, the bug only appears once the page is running on WebAssembly.

**Entitlements** — resolved from the plan catalogue, never from the UI. A lapsed or past-due subscription resolves to Free even if the stored plan says otherwise. The catalogue reads from the database and falls back to the compiled defaults in `Plans.cs` rather than failing open.

**Render mode** — Interactive Auto: the same component runs on Blazor Server until the WebAssembly runtime is cached, then in the browser. Anything writing to the response (cookies, headers) must be triggered from browser JS, not from a component.

### Key entities

| Entity | Purpose |
|---|---|
| `Client`, `Project`, `Job`, `TimeEntry` | Who you work for and what you did |
| `Invoice`, `InvoiceLineItem`, `Estimate` | The documents |
| `RecurringInvoice`, `RecurringExpense` | Scheduled billing |
| `Expense`, `Product` | Costs and catalogue |
| `BankTransaction` | Imported statement lines, matched to invoices |
| `BusinessProfile`, `CurrencyPaymentSettings` | Your details and per-currency payment info |
| `UserLanguagePreference`, `ClientLanguageSetting` | Document language defaults and per-client overrides |
| `PlanSettings`, `SubscriptionEvent`, `CheckoutSession` | Billing |
| `AiUsage`, `TranslationCacheEntry` | AI spend tracking and the translation cache |

---

## Status

Payments are **simulated**. `SimulatedBillingProvider` marks a checkout paid immediately — the flow, entitlements and audit trail are real, but no money moves. Going live means implementing `IBillingProvider` against Stripe and calling `CompleteCheckoutAsync` from a signature-verified webhook.

Estimates lag invoices: they still use the QuestPDF generator and aren't translated.

---

## License

Proprietary. All rights reserved.
