# Pug.Application.Security.AzureADRoleProvider

Microsoft Entra ID (formerly Azure AD) implementation of the
[Pug.Application.Security](https://github.com/NDWX/Pug.Application.Security)
`IPrincipalRoleProvider` and `IUserRoleProvider` interfaces.

A role is an **Entra app role** of a chosen app registration: the provider reports the app role `value`
strings assigned to a principal — whether assigned to the principal directly or to any group the
principal is a (transitive) member of — i.e. the same roles that would appear in the `roles` claim of a
token issued for that application.

A principal may be any actor known to the tenant:

- a **user**, identified by object ID or user principal name; or
- an **application, service, API or other resource** — anything represented by a service principal —
  identified by service principal object ID or application (client) ID.

Identifiers are resolved in that order; unknown principals are reported as having no roles.

## Authentication

Microsoft Graph is queried **app-only** (client credentials — client secret, certificate or managed
identity). No signed-in user, delegated permission or interactive flow is involved, so the provider can
be consumed by applications with or without user credentials.

The client identity requires the following Microsoft Graph **application** permissions, with admin
consent: `User.Read.All`, `GroupMember.Read.All`, `Application.Read.All`.

## Usage

```csharp
using Pug.Application.Security.AzureADRoleProvider;

IPrincipalRoleProvider roleProvider =
	new PrincipalRoleProvider(
		graphServiceClient,
		new AzureADRoleProviderOptions { ApplicationId = applicationId } );

// user principal, by object ID or user principal name
bool authorized = await roleProvider.PrincipalIsInRoleAsync( "jane.doe@contoso.com", "Administrator" );

// application/service principal, by client ID or service principal object ID
bool trusted = await roleProvider.PrincipalIsInRoleAsync( callerClientId, "Integration" );
```

`PrincipalIsInRoles` returns true only when the principal is in **all** specified roles. Role name
comparison is case-sensitive (ordinal), matching Entra app role `value` semantics.

Resolved roles, app role definitions and role assignments are cached in memory for
`AzureADRoleProviderOptions.CacheDuration` (default 5 minutes); set it to `TimeSpan.Zero` to disable
caching.

For ASP.NET Core / `IServiceCollection` registration, use the companion package
[`Pug.Application.Security.AzureADRoleProvider.DependencyInjection`](https://www.nuget.org/packages/Pug.Application.Security.AzureADRoleProvider.DependencyInjection).
