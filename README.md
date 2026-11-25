# AI-Powered Credit Risk Scoring API

> Capleon Portfolio · C# .NET 9 · Fintech + CQRS + AI

A production-grade REST API that scores a borrower's financial profile (0–100), assigns a risk tier, determines loan eligibility, and generates plain-English AI explanations of the score. Built with the CQRS pattern via MediatR and a deterministic weighted-formula scoring engine designed to be drop-in replaced by an ML.NET model.

---

## What it demonstrates

| Concern | Technique |
|---------|-----------|
| Architecture | CQRS with MediatR — Commands write, Queries read |
| Scoring model | Deterministic weighted formula (5 pillars, 100 pts) |
| AI layer | Template engine (simulated) — swap to OpenAI / Claude via config |
| Auth | JWT Bearer (Microsoft.AspNetCore.Authentication.JwtBearer), BCrypt-12 |
| Rate limiting | ASP.NET Core built-in `AddRateLimiter` — 100 req/min fixed window |
| Security | OWASP headers middleware, HTTPS redirect |
| Background jobs | `BackgroundService` — 6-hour stale profile recalculation + GC logging |
| GC tuning | Server GC via `runtimeconfig.template.json` + `GCSettings` at runtime |
| Database | SQLite (dev) / swap connection string for SQL Server or PostgreSQL |
| Docs | Swashbuckle — Swagger UI served at `/` (root) in dev |

---

## Project layout

```
credit-risk-scoring/
│
├── Program.cs                          # App builder: DI, JWT, Rate limiting, middleware pipeline
├── CreditRiskScoring.Api.csproj        # SDK.Web project file — ServerGC=true, deps
├── appsettings.json                    # JWT, DB provider, AI provider config
├── appsettings.Development.json        # Dev-only overrides (verbose logging)
├── runtimeconfig.template.json         # GC: server mode, 1 GB heap limit
│
├── Controllers/
│   ├── RiskController.cs       # POST /risk/score, /explain, /simulate  GET /risk/factors/{id}
│   └── AuthController.cs       # POST /auth/register, /auth/login  (in-memory user store)
│
├── Application/                        # Pure business logic — no DB or HTTP dependencies
│   ├── Commands/
│   │   ├── ScoreProfile/
│   │   │   ├── ScoreProfileCommand.cs          # IRequest<ScoreResponse>
│   │   │   └── ScoreProfileCommandHandler.cs   # Calls ScoringEngine, saves profile + factors
│   │   ├── ExplainScore/
│   │   │   ├── ExplainScoreCommand.cs
│   │   │   └── ExplainScoreCommandHandler.cs   # Calls AiExplanationService, persists text
│   │   └── SimulateScore/
│   │       ├── SimulateScoreCommand.cs
│   │       └── SimulateScoreCommandHandler.cs  # Scores a hypothetical profile, no DB write
│   └── Queries/
│       └── GetRiskFactors/
│           ├── GetRiskFactorsQuery.cs
│           └── GetRiskFactorsQueryHandler.cs   # Loads factors ordered by impact
│
├── Domain/
│   ├── Entities/
│   │   ├── RiskProfile.cs   # Financial inputs + computed score/tier/eligibility
│   │   └── RiskFactor.cs    # One row per scoring pillar (linked to RiskProfile)
│   └── Enums/
│       └── RiskTier.cs      # Excellent | LowRisk | MediumRisk | HighRisk
│
├── Infrastructure/
│   ├── Data/
│   │   └── AppDbContext.cs          # EF Core context, precision config, cascade delete
│   ├── Services/
│   │   ├── ScoringEngine.cs         # 5-pillar weighted formula (0–100)
│   │   └── AiExplanationService.cs  # Template engine today; LLM hook tomorrow
│   ├── BackgroundJobs/
│   │   └── RiskRecalculationJob.cs  # BackgroundService: recalc stale profiles + GC logging
│   └── Security/
│       └── JwtService.cs            # HS256 token generation
│
├── Middleware/
│   └── ExceptionHandlingMiddleware.cs   # Converts exceptions to structured JSON errors
│
└── DTOs/
    ├── Requests.cs    # ScoreRequest, SimulateRequest, ExplainRequest, LoginRequest, RegisterRequest
    └── Responses.cs   # ScoreResponse, SimulateResponse, ExplanationResponse, RiskFactorResponse
```

---

## Quick start (no database setup needed)

### Prerequisites
- .NET 9 SDK

```bash
# 1 — navigate to the folder
cd credit-risk-scoring

# 2 — run (uses SQLite automatically — creates creditrisk.db in the working dir)
dotnet run

# 3 — open Swagger UI (served at root in dev mode)
open http://localhost:5000
```

---

## Scoring model explained

The score is the sum of **five weighted pillars**:

```
Pillar                Max points   Key signal
─────────────────────────────────────────────────────────
1. Annual Income           30      < $20k → 2 pts  |  ≥ $100k → 30 pts
2. Debt-to-Income ratio    25      DTI > 50% → 0  |  DTI < 10% → 25 pts
3. Credit history length   20      < 1 yr → 1 pt  |  ≥ 7 yrs → 20 pts
4. Payment history         15      0 missed → 15  |  3+ missed → 0 pts
5. Assets / collateral     10      > $500k → 10   |  negligible → 1 pt
─────────────────────────────────────────────────────────
Total                     100
```

**Risk tiers:**

| Score | Tier | Loan eligible |
|-------|------|---------------|
| 81–100 | Excellent | Yes |
| 61–80 | Low Risk | Yes |
| 41–60 | Medium Risk | No |
| 0–40 | High Risk | No |

---

## API walkthrough

### Step 1 — Register

```bash
curl -X POST http://localhost:5000/api/v1/auth/register \
  -H "Content-Type: application/json" \
  -d '{"username":"bob","email":"bob@example.com","password":"secure456"}'
```

### Step 2 — Login (copy the token)

```bash
curl -X POST http://localhost:5000/api/v1/auth/login \
  -H "Content-Type: application/json" \
  -d '{"username":"bob","password":"secure456"}'
```

### Step 3 — Score a financial profile

```bash
TOKEN="eyJ..."

curl -X POST http://localhost:5000/api/v1/risk/score \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "annualIncome": 75000,
    "totalDebt": 12000,
    "monthlyExpenses": 2500,
    "assets": 45000,
    "creditHistoryMonths": 84,
    "missedPayments": 0,
    "employmentStatus": "Employed",
    "loanPurpose": "Home renovation",
    "userId": "bob-001"
  }'
```

Response includes `profileId` — save it for the next calls.

### Step 4 — Get an AI explanation

```bash
curl -X POST http://localhost:5000/api/v1/risk/explain \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"profileId": "<profileId from step 3>"}'
```

### Step 5 — List risk factors

```bash
curl http://localhost:5000/api/v1/risk/factors/<profileId> \
  -H "Authorization: Bearer $TOKEN"
```

### Step 6 — Simulate "What if my income increases 20%?"

```bash
curl -X POST http://localhost:5000/api/v1/risk/simulate \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "profileId": "<profileId>",
    "newAnnualIncome": 90000,
    "newTotalDebt": null,
    "newMissedPayments": null
  }'
```

Response: original score, simulated score, delta, and whether loan eligibility changed.

---

## CQRS pattern with MediatR

```
HTTP Request
     │
  Controller
     │ mediator.Send(new ScoreProfileCommand(request))
     ▼
  IRequestHandler<ScoreProfileCommand, ScoreResponse>
     │   (ScoreProfileCommandHandler)
     │
     ├── ScoringEngine.Calculate(profile)   ← pure logic, no I/O
     ├── AppDbContext.SaveChangesAsync()    ← persistence
     └── return ScoreResponse              ← mapped DTO
```

Commands (write) and Queries (read) are kept in separate namespaces with their own handler classes. This means you can:
- Scale reads and writes independently
- Test handlers in isolation without an HTTP context
- Add cross-cutting concerns (logging, validation, caching) via MediatR pipeline behaviours

---

## Switching to a real AI explanation

1. Set `Ai:Provider` in `appsettings.json`:

```json
{ "Ai": { "Provider": "claude", "Claude": { "ApiKey": "sk-ant-..." } } }
```

2. In `AiExplanationService.cs`, wire the `ExplainViaLlm` method:

```csharp
// Example Claude API call
var client = new HttpClient();
client.DefaultRequestHeaders.Add("x-api-key", _config["Ai:Claude:ApiKey"]);
var prompt = $"""
    A borrower has a credit score of {profile.Score}/100.
    Factors: {JsonSerializer.Serialize(factors.Select(f => new { f.Name, f.Description }))}
    Write a 3-sentence plain-English explanation suitable for a compliance document.
    """;
// POST to https://api.anthropic.com/v1/messages ...
```

---

## Production deployment

```bash
# 1 — publish self-contained binary
dotnet publish -c Release -o publish

# 2 — set secrets (never commit these)
export Jwt__Secret=$(openssl rand -base64 48)
export ConnectionStrings__Default="Host=db.example.com;Database=creditrisk;Username=app;Password=..."

# 3 — override DB for PostgreSQL
export Database__Provider=postgres

# 4 — run
cd publish && dotnet CreditRiskScoring.Api.dll
```

### GC tuning explained

**`runtimeconfig.template.json`** (embedded in the build):

```json
{
  "System.GC.Server": true,        // multi-threaded server GC (vs workstation GC)
  "System.GC.Concurrent": true,    // background GC — minimises stop-the-world pauses
  "System.GC.HeapHardLimit": 1073741824  // 1 GB max heap (prevents runaway memory)
}
```

The `RiskRecalculationJob` logs Gen0/1/2 collection counts and heap size every 6 hours:

```
[GC] Heap used: 42MB | Gen0: 12 | Gen1: 3 | Gen2: 1 | Compacted: False
```

**Additional runtime env vars for fine-tuning:**

| Variable | Effect |
|----------|--------|
| `DOTNET_GCServer=1` | Force server GC even if auto-detection fails |
| `DOTNET_GCConserve=1` | Sacrifice throughput to reduce memory footprint |
| `DOTNET_GCHeapHardLimit=536870912` | Override heap limit (bytes) at runtime |

---

## Running tests

```bash
dotnet test
```

The in-memory EF Core provider is used for unit tests — no external database needed.

---

*Capleon International Concept Limited — Building Tomorrow's Solution Today*
