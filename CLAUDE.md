# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Commands

```sh
dotnet build Pug.Application.Security.AzureADRoleProvider.sln            # all TFMs: netstandard2.0, net8.0, net10.0
dotnet test                                                              # all tests (xUnit, net10.0)
dotnet test --filter "FullyQualifiedName~PrincipalRoleProviderTests.ApplicationPrincipalMayBeIdentifiedByClientId"   # single test
```

Builds must stay at **0 warnings**. Live verification against a real Entra tenant is manual only (see README "Verifying against a live tenant"); unit tests never touch the network.

## What this library is

Microsoft Entra ID (formerly Azure AD — same service, same Microsoft Graph API) implementation of
`IPrincipalRoleProvider` and the obsolete-but-intentionally-supported `IUserRoleProvider` from
[Pug.Application.Security](https://github.com/NDWX/Pug.Application.Security), consumed as the NuGet
package `Pug.Application.Security.Abstractions` (not a project reference; sibling repos live in
`~/dev/pug/`, e.g. `Pug.Groups` is the reference implementation for conventions and semantics).

## Architecture

Single flow, three layers, all in `src/Pug.Application.Security.AzureADRoleProvider/`
(namespace `Pug.Application.Security.AzureADRoleProvider`):

- **`PrincipalRoleProvider`** — public entry point implementing both role-provider interfaces. Sync
  members block on the async ones. All role queries funnel into `ResolveRolesAsync`, which computes the
  principal's effective app-role set once and caches it.
- **`IAzureADGateway`** (internal) — seam isolating all Microsoft Graph traffic so role logic is
  unit-testable; tests substitute `FakeAzureADGateway`. New Graph calls belong behind this
  interface, implemented in `GraphAzureADGateway`, which is itself not unit-tested (kept thin).
  Widening it requires updating the fake (tests use `InternalsVisibleTo`).
- **`TtlCache`** (internal) — per-key async cache with absolute expiry; faulted tasks are evicted so
  errors are never cached. `TimeSpan.Zero` disables caching.

The DI project (`...DependencyInjection`, net8.0/net10.0 only) registers one singleton exposed as both
interfaces; it is where `Azure.Identity` lives. The core project depends only on `Microsoft.Graph`.

### Role-resolution model (the part that requires context)

A "role" is an **Entra app role** (`value` string) of the app registration named by
`AzureADRoleProviderOptions.ApplicationId` — what would appear in a token's `roles` claim. Effective
roles = intersection of the resource service principal's `appRoleAssignedTo` list with
{principal object ID} ∪ {its transitive group IDs}, mapped through the app-role definitions.

A principal may be **any actor type** (this is why `IUserRoleProvider` was superseded, per Andrian):
resolution order is user (object ID or UPN) → service principal by object ID → service principal by
application/client ID. Unknown principals yield **no roles, never an exception**; an unknown
`ApplicationId` (resource app) throws `InvalidOperationException`.

Fixed semantics — do not change without direction:
- `PrincipalIsInRoles`/`UserIsInRoles` requires **ALL** listed roles (matches `Pug.Groups`); empty list → true.
- Role comparison is **ordinal, case-sensitive**; object-ID comparison is case-insensitive.
- Graph access is **app-only** (client credentials); never introduce delegated permissions or user
  sign-in. Required application permissions: `User.Read.All`, `GroupMember.Read.All`,
  `Application.Read.All` — keep README's table in sync if calls change.

### Graph gotchas already accounted for

- `/servicePrincipals/{id}/transitiveMemberOf` rejects `$select`/OData cast without advanced-query
  headers — fetch plainly and filter to `Group` client-side (users' endpoint supports the cast).
- All collection endpoints are paged via `OdataNextLink` + `.WithUrl(...)` loops, `$top=999`.
- Graph 404s surface as `ODataError` with `ResponseStatusCode`; principal resolution also swallows 400
  (malformed identifier on `/users/{...}`).

## Conventions

- Pug house style: tabs, Allman braces, block (non-file-scoped) namespaces, explicit types over `var`.
- Solution file stays in the repo root; shippable projects under `src/`, tests under `tests/`.
- netstandard2.0 is a target: no `init` accessors/records in the core project; per-TFM
  `Microsoft.Extensions.DependencyInjection.Abstractions` pins in the DI csproj (net8.0 pin must be
  ≥ 8.0.2 to avoid NU1605 via Azure.Identity).
- Naming: class `PrincipalRoleProvider`, options `AzureADRoleProviderOptions` — Andrian chose these;
  keep them. `#pragma warning disable CS0618` wraps intentional `IUserRoleProvider` usage.