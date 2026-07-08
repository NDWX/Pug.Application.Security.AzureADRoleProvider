# Pug.Application.Security.AzureADRoleProvider

Microsoft Entra ID (formerly Azure AD) based implementation of
[Pug.Application.Security](https://github.com/NDWX/Pug.Application.Security)
`IPrincipalRoleProvider` and `IUserRoleProvider`.

A role is an **Entra app role** of a chosen app registration: `EntraIdRoleProvider` reports the app role
`value` strings assigned to a user, whether the app role was assigned to the user directly or to any group
the user is a (transitive) member of — i.e. the same roles that would appear in the `roles` claim of a token
issued for that application.

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
| `Application.Read.All` | Reading the application's service principal, app roles and role assignments |

## Usage

```csharp
using Pug.Application.Security.AzureAD;

// client secret credential; roles are the app roles of the same app registration
services.AddEntraIdRoleProvider( tenantId, clientId, clientSecret );

// or with any app-only Azure.Identity TokenCredential (e.g. managed identity),
// naming the application whose app roles constitute the role set
services.AddEntraIdRoleProvider( new ManagedIdentityCredential(), applicationId );

// or with a pre-configured GraphServiceClient
services.AddEntraIdRoleProvider( graphServiceClient, applicationId );
```

Each overload registers a singleton `EntraIdRoleProvider` exposed as both `IPrincipalRoleProvider` and
(the obsolete) `IUserRoleProvider`. The provider may also be constructed directly:

```csharp
IPrincipalRoleProvider roleProvider =
	new EntraIdRoleProvider(
		graphServiceClient,
		new EntraIdRoleProviderOptions { ApplicationId = applicationId } );

bool authorized = await roleProvider.PrincipalIsInRoleAsync( "jane.doe@contoso.com", "Administrator" );
```

Users may be identified by object ID or user principal name. Unknown users are simply reported as having
no roles. `PrincipalIsInRoles`/`UserIsInRoles` returns true only when the user is in **all** specified
roles. Role name comparison is case-sensitive (ordinal), matching Entra app role `value` semantics.

## Caching

Resolved roles, app role definitions and role assignments are cached in memory for
`EntraIdRoleProviderOptions.CacheDuration` (default 5 minutes), so role or membership changes in Entra ID
may take up to that duration to be observed. Set `CacheDuration` to `TimeSpan.Zero` to disable caching at
the cost of several Graph requests per role query.

## Verifying against a live tenant

1. Create (or reuse) an app registration; define one or more app roles (with `value`, e.g. `Administrator`)
   allowing *Users/Groups* as member types, and assign a test user (or one of their groups) to a role via
   *Enterprise applications → your app → Users and groups*.
2. Grant the Graph application permissions listed above to the client identity and provide admin consent.
3. Call `GetUserRolesAsync( "user@tenant.onmicrosoft.com" )` and confirm the assigned role values are
   returned.
