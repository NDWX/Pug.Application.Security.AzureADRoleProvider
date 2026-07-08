using Azure.Core;
using Azure.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Graph;

namespace Pug.Application.Security.AzureADRoleProvider
{
	public static class IServiceCollectionExtensions
	{
		private static readonly string[] GraphScopes = { "https://graph.microsoft.com/.default" };

		/// <summary>
		/// Registers <see cref="PrincipalRoleProvider"/> using client-secret (app-only) authentication.
		/// Roles are the app roles of the app registration identified by <paramref name="clientId"/>,
		/// unless a different <see cref="EntraIdRoleProviderOptions.ApplicationId"/> is specified via
		/// <paramref name="configure"/>.
		/// </summary>
		public static IServiceCollection AddEntraIdRoleProvider(
			this IServiceCollection services, string tenantId, string clientId, string clientSecret,
			Action<EntraIdRoleProviderOptions>? configure = null )
		{
			return services.AddEntraIdRoleProvider(
					new ClientSecretCredential( tenantId, clientId, clientSecret ),
					clientId,
					configure
				);
		}

		/// <summary>
		/// Registers <see cref="PrincipalRoleProvider"/> using the specified app-only credential
		/// (e.g. <see cref="ClientSecretCredential"/>, ClientCertificateCredential or ManagedIdentityCredential).
		/// </summary>
		public static IServiceCollection AddEntraIdRoleProvider(
			this IServiceCollection services, TokenCredential credential, string applicationId,
			Action<EntraIdRoleProviderOptions>? configure = null )
		{
			return services.AddEntraIdRoleProvider(
					new GraphServiceClient( credential, GraphScopes ),
					applicationId,
					configure
				);
		}

		/// <summary>
		/// Registers <see cref="PrincipalRoleProvider"/> using an existing <see cref="GraphServiceClient"/>.
		/// </summary>
		public static IServiceCollection AddEntraIdRoleProvider(
			this IServiceCollection services, GraphServiceClient graphServiceClient, string applicationId,
			Action<EntraIdRoleProviderOptions>? configure = null )
		{
			EntraIdRoleProviderOptions options =
				new EntraIdRoleProviderOptions { ApplicationId = applicationId };

			configure?.Invoke( options );

			PrincipalRoleProvider roleProvider = new PrincipalRoleProvider( graphServiceClient, options );

			services.AddSingleton( roleProvider );
			services.AddSingleton<IPrincipalRoleProvider>( roleProvider );
#pragma warning disable CS0618 // IUserRoleProvider is obsolete but intentionally supported
			services.AddSingleton<IUserRoleProvider>( roleProvider );
#pragma warning restore CS0618

			return services;
		}
	}
}