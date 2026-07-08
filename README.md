# Pug.Application.Security.AzureADRoleProvider

Microsoft Entra ID (formerly Azure AD) based implementation of
[Pug.Application.Security](https://github.com/NDWX/Pug.Application.Security)
`IPrincipalRoleProvider` and `IUserRoleProvider`.

A role is an **Entra app role** of a chosen app registration: `PrincipalRoleProvider` reports the app role
`value` strings assigned to a principal, whether the app role was assigned to the principal directly or to
any group the principal is a (transitive) member of — i.e. the same roles that would appear in the `roles`
claim of a token issued for that application.

A principal may be any actor known to the tenant:

- a **user**, identified by object ID or user principal name; or
- an **application, service, API or other resource** — anything represented by a service principal —
  identified by service principal object ID (the `oid` claim of an app-only token) or application (client)
  ID (the `appid`/`azp` claim).

Identifiers are resolved in that order (user, then service principal by object ID, then by application ID);
unknown principals are reported as having no roles.

Because Azure AD and Entra ID are the same service exposed through the same API (Microsoft Graph), this
single implementation applies to both.

## Authentication

The provider queries Microsoft Graph **app-only** (client credentials flow — client secret, certificate or
managed identity). No signed-in user, delegated permission or interactive flow is involved, so the provider
can be consumed by applications with or without user credentials, and can resolve roles for any user in the
tenant.

The client identity used to call Microsoft Graph requires the following Microsoft Graph **application**
permissions, with admin consent:

| Permission | Used for |
|---|---|
| `User.Read.All` | Resolving users by object ID or user principal name |
| `GroupMember.Read.All` | Reading users' transitive group memberships |
| `Application.Read.All` | Reading the application's service principal, app roles and role assignments; resolving application/service principals and their group memberships |

## Usage

```csharp
using Pug.Application.Security.AzureADRoleProvider;

// client secret credential; roles are the app roles of the same app registration
services.AddAzureADRoleProvider( tenantId, clientId, clientSecret );

// or with any app-only Azure.Identity TokenCredential (e.g. managed identity),
// naming the application whose app roles constitute the role set
services.AddAzureADRoleProvider( new ManagedIdentityCredential(), applicationId );

// or with a pre-configured GraphServiceClient
services.AddAzureADRoleProvider( graphServiceClient, applicationId );
```

Each overload registers a singleton `PrincipalRoleProvider` exposed as both `IPrincipalRoleProvider` and
(the obsolete) `IUserRoleProvider`. The provider may also be constructed directly:

```csharp
IPrincipalRoleProvider roleProvider =
	new PrincipalRoleProvider(
		graphServiceClient,
		new AzureADRoleProviderOptions { ApplicationId = applicationId } );

// user principal
bool authorized = await roleProvider.PrincipalIsInRoleAsync( "jane.doe@contoso.com", "Administrator" );

// application/service principal, by client ID or service principal object ID
bool trusted = await roleProvider.PrincipalIsInRoleAsync( callerClientId, "Integration" );
```

`PrincipalIsInRoles`/`UserIsInRoles` returns true only when the principal is in **all** specified roles.
Role name comparison is case-sensitive (ordinal), matching Entra app role `value` semantics.

## Caching

Resolved roles, app role definitions and role assignments are cached in memory for
`AzureADRoleProviderOptions.CacheDuration` (default 5 minutes), so role or membership changes in Entra ID
may take up to that duration to be observed. Set `CacheDuration` to `TimeSpan.Zero` to disable caching at
the cost of several Graph requests per role query.

## Verifying against a live tenant

1. Create (or reuse) an app registration; define one or more app roles (with `value`, e.g. `Administrator`)
   allowing *Users/Groups* and/or *Applications* as member types, and assign a test user (or one of their
   groups) to a role via *Enterprise applications → your app → Users and groups*.
2. Grant the Graph application permissions listed above to the client identity and provide admin consent.
3. Call `GetUserRolesAsync( "user@tenant.onmicrosoft.com" )` and confirm the assigned role values are
   returned.
4. For application principals: assign an *Applications*-typed app role to a client app's service principal
   (`New-MgServicePrincipalAppRoleAssignedTo`, or *App registrations → API permissions* for app roles
   exposed by another API followed by admin consent), then call
   `GetPrincipalRolesAsync( "<client-id-of-that-app>" )` and confirm the role value is returned.
