using Microsoft.Graph;

namespace Pug.Application.Security.AzureADRoleProvider
{
	/// <summary>
	/// <see cref="IPrincipalRoleProvider"/> and <see cref="IUserRoleProvider"/> implementation backed by
	/// Microsoft Entra ID (formerly Azure AD). Roles are the app role assignments ('roles' claim) of the
	/// application identified by <see cref="EntraIdRoleProviderOptions.ApplicationId"/>, including app roles
	/// assigned through group membership. Microsoft Graph is queried app-only (client credentials); no
	/// user authentication is required.
	/// </summary>
#pragma warning disable CS0618 // IUserRoleProvider is obsolete but intentionally supported
	public class PrincipalRoleProvider : IPrincipalRoleProvider, IUserRoleProvider
#pragma warning restore CS0618
	{
		private readonly IEntraDirectoryGateway _directoryGateway;
		private readonly EntraIdRoleProviderOptions _options;
		private readonly TtlCache<ServicePrincipalInfo> _servicePrincipalCache;
		private readonly TtlCache<IReadOnlyCollection<AppRoleAssignmentInfo>> _assignmentsCache;
		private readonly TtlCache<IReadOnlyCollection<string>> _rolesCache;

		public PrincipalRoleProvider( GraphServiceClient graphServiceClient, EntraIdRoleProviderOptions options )
			: this(
				new GraphEntraDirectoryGateway(
					graphServiceClient ?? throw new ArgumentNullException( nameof(graphServiceClient) ) ),
				options )
		{
		}

		internal PrincipalRoleProvider( IEntraDirectoryGateway directoryGateway, EntraIdRoleProviderOptions options )
		{
			if( options == null )
				throw new ArgumentNullException( nameof(options) );

			if( string.IsNullOrWhiteSpace( options.ApplicationId ) )
				throw new ArgumentException(
					$"{nameof(EntraIdRoleProviderOptions.ApplicationId)} must be specified", nameof(options) );

			_directoryGateway = directoryGateway;
			_options = options;
			_servicePrincipalCache = new TtlCache<ServicePrincipalInfo>( options.CacheDuration );
			_assignmentsCache = new TtlCache<IReadOnlyCollection<AppRoleAssignmentInfo>>( options.CacheDuration );
			_rolesCache = new TtlCache<IReadOnlyCollection<string>>( options.CacheDuration );
		}

		private Task<IReadOnlyCollection<string>> GetRolesAsync( string principal )
		{
			if( string.IsNullOrEmpty( principal ) )
				throw new ArgumentException( "Principal must be specified", nameof(principal) );

			return _rolesCache.GetOrAddAsync( principal, ResolveRolesAsync );
		}

		private async Task<IReadOnlyCollection<string>> ResolveRolesAsync( string principal )
		{
			ServicePrincipalInfo servicePrincipal =
				await _servicePrincipalCache.GetOrAddAsync( _options.ApplicationId, GetServicePrincipalAsync )
						.ConfigureAwait( false );

			string? userObjectId =
				await _directoryGateway.GetUserObjectIdAsync( principal ).ConfigureAwait( false );

			if( userObjectId == null )
				return Array.Empty<string>();

			IReadOnlyCollection<string> groupIds =
				await _directoryGateway.GetTransitiveGroupIdsAsync( userObjectId ).ConfigureAwait( false );

			IReadOnlyCollection<AppRoleAssignmentInfo> assignments =
				await _assignmentsCache.GetOrAddAsync(
							servicePrincipal.ObjectId, _directoryGateway.GetAppRoleAssignmentsAsync )
						.ConfigureAwait( false );

			HashSet<string> principalObjectIds =
				new HashSet<string>( groupIds, StringComparer.OrdinalIgnoreCase ) { userObjectId };

			HashSet<string> roles = new HashSet<string>( StringComparer.Ordinal );

			foreach( AppRoleAssignmentInfo assignment in assignments )
			{
				if( principalObjectIds.Contains( assignment.PrincipalId ) &&
					servicePrincipal.AppRoleValues.TryGetValue( assignment.AppRoleId, out string? roleValue ) )
					roles.Add( roleValue );
			}

			return roles;
		}

		private async Task<ServicePrincipalInfo> GetServicePrincipalAsync( string applicationId )
		{
			ServicePrincipalInfo? servicePrincipal =
				await _directoryGateway.GetServicePrincipalAsync( applicationId ).ConfigureAwait( false );

			if( servicePrincipal == null )
				throw new InvalidOperationException(
					$"Service principal of application '{applicationId}' was not found in the tenant" );

			return servicePrincipal;
		}

		public bool PrincipalIsInRole( string principal, string role )
		{
			return PrincipalIsInRoleAsync( principal, role ).ConfigureAwait( false ).GetAwaiter().GetResult();
		}

		public async Task<bool> PrincipalIsInRoleAsync( string principal, string role )
		{
			IReadOnlyCollection<string> roles = await GetRolesAsync( principal ).ConfigureAwait( false );

			return roles.Contains( role );
		}

		public bool PrincipalIsInRoles( string principal, ICollection<string> roles )
		{
			return PrincipalIsInRolesAsync( principal, roles ).ConfigureAwait( false ).GetAwaiter().GetResult();
		}

		public async Task<bool> PrincipalIsInRolesAsync( string principal, ICollection<string> roles )
		{
			IReadOnlyCollection<string> principalRoles = await GetRolesAsync( principal ).ConfigureAwait( false );

			foreach( string role in roles )
			{
				if( !principalRoles.Contains( role ) )
					return false;
			}

			return true;
		}

		public IEnumerable<string> GetPrincipalRoles( string principal )
		{
			return GetPrincipalRolesAsync( principal ).ConfigureAwait( false ).GetAwaiter().GetResult();
		}

		public async Task<IEnumerable<string>> GetPrincipalRolesAsync( string principal )
		{
			return await GetRolesAsync( principal ).ConfigureAwait( false );
		}

		public bool UserIsInRole( string user, string role )
		{
			return PrincipalIsInRole( user, role );
		}

		public Task<bool> UserIsInRoleAsync( string user, string role )
		{
			return PrincipalIsInRoleAsync( user, role );
		}

		public bool UserIsInRoles( string user, ICollection<string> roles )
		{
			return PrincipalIsInRoles( user, roles );
		}

		public Task<bool> UserIsInRolesAsync( string user, ICollection<string> roles )
		{
			return PrincipalIsInRolesAsync( user, roles );
		}

		public IEnumerable<string> GetUserRoles( string user )
		{
			return GetPrincipalRoles( user );
		}

		public Task<IEnumerable<string>> GetUserRolesAsync( string user )
		{
			return GetPrincipalRolesAsync( user );
		}
	}
}