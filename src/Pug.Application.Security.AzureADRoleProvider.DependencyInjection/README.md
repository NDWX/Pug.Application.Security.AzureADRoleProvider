# Pug.Application.Security.AzureADRoleProvider.DependencyInjection

`IServiceCollection` registration extensions for
[`Pug.Application.Security.AzureADRoleProvider`](https://www.nuget.org/packages/Pug.Application.Security.AzureADRoleProvider)
— the Microsoft Entra ID (formerly Azure AD) implementation of the
[Pug.Application.Security](https://github.com/NDWX/Pug.Application.Security)
`IPrincipalRoleProvider` and `IUserRoleProvider` interfaces.

Each overload registers a single `PrincipalRoleProvider` as a singleton, exposed as both
`IPrincipalRoleProvider` and (the obsolete) `IUserRoleProvider`. Microsoft Graph is queried **app-only**
(client credentials); no signed-in user is required.

## Usage

```csharp
using Pug.Application.Security.AzureADRoleProvider.DependencyInjection;

// client secret credential; roles are the app roles of the same app registration
services.AddAzureADRoleProvider( tenantId, clientId, clientSecret );

// or with any app-only Azure.Identity TokenCredential (e.g. managed identity),
// naming the application whose app roles constitute the role set
services.AddAzureADRoleProvider( new ManagedIdentityCredential(), applicationId );

// or with a pre-configured GraphServiceClient
services.AddAzureADRoleProvider( graphServiceClient, applicationId );
```

An optional `Action<AzureADRoleProviderOptions>` argument on each overload configures the resolved
`ApplicationId` (whose app roles constitute the role set) and `CacheDuration` (default 5 minutes).

See the [core package](https://www.nuget.org/packages/Pug.Application.Security.AzureADRoleProvider) for
the role-resolution model, supported principal types and required Microsoft Graph permissions.
