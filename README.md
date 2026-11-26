# Ayso.Shared

Shared .NET 10 libraries for AYSO Region 42 and Area 10E soccer competition web apps.

## Intended consumers

- `Region42.ScoresStandings` — region regular-season scores & standings
- `Region42.PantherShootout` — Panther Shootout tournament
- `Area10E.ScoresStandings` — area-level 14U scores & standings (3-1-0)

This repository is designed to be consumed as a **git submodule** (or sibling project reference). NuGet packaging is not required for the initial adoption path.

## Packages

| Project | Role |
|---|---|
| `Ayso.Shared.Domain` | Enums (`AgeGroup`, `Gender`), `AuditableEntity`, `DivisionKeyHelper`, `StorageDocument<T>`, write-lock, `IJsonObjectStore`, and `IBlobObjectStore` contracts |
| `Ayso.Shared.Scoring` | Pure calculators: soccer **3-1-0**, AYSO **10-point** pool play, and `ScoreRules` validators (neutral DTOs — no app entities) |
| `Ayso.Shared.Storage` | `StorageOptions`, local/GCS JSON object stores, local/GCS binary blob stores, write locks under `{competitionSlug}/locks/` |
| `Ayso.Shared.AspNet` | Cookie auth helper, `DomainRequirement`, `EmailAllowlistRequirement`, security headers + configurable CSP/COOP (`SecurityHeadersOptions`), CSP nonce, theme cookie middleware |

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
- `Area10E.ScoresStandings` uses **3-1-0 only**.

## Storage notes

- **JSON document storage (`IJsonObjectStore`)** — `LocalFileJsonObjectStore` (atomic `.tmp` rename + `.generation` sidecar) and `GcsJsonObjectStore` (`IfGenerationMatch` optimistic concurrency + transient rate-limit retries), exposing paired sync and async overloads (`Get` / `GetAsync`, `Save` / `SaveAsync`, `Exists` / `ExistsAsync`, `Delete` / `DeleteAsync`).
- **Binary blob storage (`IBlobObjectStore`)** — `LocalFileBlobObjectStore` and `GcsBlobObjectStore` share the same `StorageOptions` (`Provider`, `BucketName`, `LocalRoot`) for binary assets such as scorecard photos, exposing paired sync and async overloads (`Upload` / `UploadAsync`, `OpenRead` / `OpenReadAsync`, `Exists` / `ExistsAsync`, `Delete` / `DeleteAsync`).
- **Write locks (`IStorageWriteLock`)** — `LocalFileStorageWriteLock` and `GcsStorageWriteLock` coordinate coarse-grained administrative operations under `{competitionSlug}/locks/{lockName}.json`, exposing paired sync and async overloads (`IsLocked` / `IsLockedAsync`, `TryAcquire` / `TryAcquireAsync`) with `IStorageWriteLockHandle` implementing both `IDisposable` and `IAsyncDisposable`.

## Auth & security headers notes

- `DomainRequirement` — email ends with `@{allowedDomain}` (Region42 Google Workspace).
- `EmailAllowlistRequirement` — explicit email allowlist.
- `AddAysoCookieAuthentication` registers the `ayso.auth` cookie scheme; each host app wires its own external identity provider (e.g., Google OAuth or Google Cloud Identity Platform).
- `UseSecurityHeaders()` applies strict OWASP defaults and a per-request `'strict-dynamic'` CSP nonce. Host apps needing Identity Platform / Firebase Auth popups or client-side `blob:` image previews can pass `SecurityHeadersOptions` without relaxing defaults for other apps:

```csharp
app.UseSecurityHeaders(options =>
{
  options.AdditionalConnectSources.Add("https://identitytoolkit.googleapis.com");
  options.AdditionalConnectSources.Add("https://securetoken.googleapis.com");
  options.AdditionalFrameSources.Add("https://*.firebaseapp.com");
  options.AdditionalImageSources.Add("blob:");
  options.CrossOriginOpenerPolicyResolver = context =>
    context.Request.Path.StartsWithSegments("/Account")
      ? "same-origin-allow-popups"
      : null;
});
```

## Submodule consumption

Consumer apps add this repository as a submodule, for example:

```bash
git submodule add <remote-url> externals/Ayso.Shared
```

Then reference projects:

```xml
<ProjectReference Include="..\..\externals\Ayso.Shared\src\Ayso.Shared.Domain\Ayso.Shared.Domain.csproj" />
```

## Build & test

```bash
dotnet build Ayso.Shared.slnx
dotnet test Ayso.Shared.slnx
```

## Out of scope (for now)

- Migrating Region42 apps onto these packages
- Deleting duplicated code from those apps
- Publishing NuGet packages
