# Ayso.Shared

Shared .NET 10 libraries for AYSO Region 42 and Area 10E soccer competition web apps.

## Intended consumers

- `Region42.ScoresStandings` — region regular-season scores & standings
- `Region42.PantherShootout` — Panther Shootout tournament
- Future `Area10E` app — area-level 14U scores & standings (3-1-0)

This repository is designed to be consumed as a **git submodule** (or sibling project reference). NuGet packaging is not required for the initial adoption path.

## Packages

| Project | Role |
|---|---|
| `Ayso.Shared.Domain` | Enums (`AgeGroup`, `Gender`), `AuditableEntity`, `DivisionKeyHelper`, `StorageDocument<T>`, write-lock and `IJsonObjectStore` contracts |
| `Ayso.Shared.Scoring` | Pure calculators: soccer **3-1-0**, AYSO **10-point** pool play, and `ScoreRules` validators (neutral DTOs — no app entities) |
| `Ayso.Shared.Storage` | `StorageOptions`, local/GCS JSON object stores, write locks under `{competitionSlug}/locks/` |
| `Ayso.Shared.AspNet` | Cookie auth helper, `DomainRequirement`, `EmailAllowlistRequirement`, security headers + CSP nonce, theme cookie middleware |

### Dependency graph

```
Domain
  ^
  |-- Scoring
  |-- Storage
  |-- AspNet
```

## Scoring notes

- **Soccer 3-1-0** (`SoccerPointsCalculator`) — win/draw/loss points, head-to-head, PPG when games-played differ. Callers apply volunteer points, region filters, and scrimmage exclusion before invoking.
- **AYSO 10-point** (`AysoTenPointCalculator`) — tournament/pool scoring with optional host volunteer deduction and KFTM override hooks.
- Area10E is expected to use **3-1-0 only**.

## Auth notes

- `DomainRequirement` — email ends with `@{allowedDomain}` (Region42 Google Workspace).
- `EmailAllowlistRequirement` — explicit email allowlist (Area10E multi-region admins).
- `AddAysoCookieAuthentication` registers the cookie scheme only; each host app adds Google, Microsoft, or other providers.

## Future submodule consumption

After this repo is published to GitHub, consumer apps can add it as a submodule, for example:

```bash
git submodule add <remote-url> externals/Ayso.Shared
```

Then reference projects:

```xml
<ProjectReference Include="..\..\externals\Ayso.Shared\src\Ayso.Shared.Domain\Ayso.Shared.Domain.csproj" />
```

Exact submodule path (`externals/`, `submodules/`, etc.) will be decided when wiring each consumer.

## Build & test

```bash
dotnet build Ayso.Shared.slnx
dotnet test Ayso.Shared.slnx
```

## Out of scope (for now)

- Migrating Region42 apps onto these packages
- Deleting duplicated code from those apps
- Scaffolding Area10E
- Publishing NuGet packages