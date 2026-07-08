using Azure.Core;
using Azure.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Graph;

namespace Pug.Application.Security.AzureADRoleProvider.DependencyInjection
{
	public static class IServiceCollectionExtensions
	{
		private static readonly string[] GraphScopes = { "https://graph.microsoft.com/.default" };

		/// <summary>
		/// Registers <see cref="PrincipalRoleProvider"/> using client-secret (app-only) authentication.
		/// Roles are the app roles of the app registration identified by <paramref name="clientId"/>,
		/// unless a different <see cref="AzureADRoleProviderOptions.ApplicationId"/> is specified via
		/// <paramref name="configure"/>.
		/// </summary>
		public static IServiceCollection AddAzureADRoleProvider(
			this IServiceCollection services, string tenantId, string clientId, string clientSecret,
			Action<AzureADRoleProviderOptions>? configure = null )
		{
			return services.AddAzureADRoleProvider(
					new ClientSecretCredential( tenantId, clientId, clientSecret ),
					clientId,
					configure
				);
		}

		/// <summary>
		/// Registers <see cref="PrincipalRoleProvider"/> using the specified app-only credential
		/// (e.g. <see cref="ClientSecretCredential"/>, ClientCertificateCredential or ManagedIdentityCredential).
		/// </summary>
		public static IServiceCollection AddAzureADRoleProvider(
			this IServiceCollection services, TokenCredential credential, string applicationId,
			Action<AzureADRoleProviderOptions>? configure = null )
		{
			return services.AddAzureADRoleProvider(
					new GraphServiceClient( credential, GraphScopes ),
					applicationId,
					configure
				);
		}

		/// <summary>
		/// Registers <see cref="PrincipalRoleProvider"/> using an existing <see cref="GraphServiceClient"/>.
		/// </summary>
		public static IServiceCollection AddAzureADRoleProvider(
			this IServiceCollection services, GraphServiceClient graphServiceClient, string applicationId,
			Action<AzureADRoleProviderOptions>? configure = null )
		{
			AzureADRoleProviderOptions options =
				new AzureADRoleProviderOptions { ApplicationId = applicationId };

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