using Pug.Application.Security.AzureADRoleProvider;

namespace Pug.Application.Security.AzureADRoleProvider.Tests
{
	internal sealed class FakeAzureADGateway : IAzureADGateway
	{
		public const string ApplicationId = "00000000-0000-0000-0000-0000000000aa";
		public const string ServicePrincipalObjectId = "10000000-0000-0000-0000-000000000001";

		public Dictionary<Guid, string> AppRoles { get; } = new();

		/// <summary>User object ID or UPN → object ID.</summary>
		public Dictionary<string, string> Users { get; } = new( StringComparer.OrdinalIgnoreCase );

		/// <summary>Service principal object ID or application (client) ID → object ID.</summary>
		public Dictionary<string, string> ServicePrincipals { get; } = new( StringComparer.OrdinalIgnoreCase );

		/// <summary>Principal object ID → transitive group object IDs.</summary>
		public Dictionary<string, List<string>> PrincipalGroups { get; } = new( StringComparer.OrdinalIgnoreCase );

		public List<AppRoleAssignmentInfo> Assignments { get; } = new();

		public int GetServicePrincipalCallCount { get; private set; }
		public int ResolvePrincipalCallCount { get; private set; }
		public int GetTransitiveGroupIdsCallCount { get; private set; }
		public int GetAppRoleAssignmentsCallCount { get; private set; }

		public Task<ServicePrincipalInfo?> GetServicePrincipalAsync( string applicationId )
		{
			GetServicePrincipalCallCount++;

			return Task.FromResult(
					applicationId == ApplicationId
						? new ServicePrincipalInfo( ServicePrincipalObjectId, AppRoles )
						: (ServicePrincipalInfo?)null
				);
		}

		public Task<PrincipalInfo?> ResolvePrincipalAsync( string principal )
		{
			ResolvePrincipalCallCount++;

			if( Users.TryGetValue( principal, out string? userObjectId ) )
				return Task.FromResult<PrincipalInfo?>( new PrincipalInfo( userObjectId, PrincipalType.User ) );

			if( ServicePrincipals.TryGetValue( principal, out string? servicePrincipalObjectId ) )
				return Task.FromResult<PrincipalInfo?>(
						new PrincipalInfo( servicePrincipalObjectId, PrincipalType.Application ) );

			return Task.FromResult<PrincipalInfo?>( null );
		}

		public Task<IReadOnlyCollection<string>> GetTransitiveGroupIdsAsync( PrincipalInfo principal )
		{
			GetTransitiveGroupIdsCallCount++;

			return Task.FromResult<IReadOnlyCollection<string>>(
					PrincipalGroups.TryGetValue( principal.ObjectId, out List<string>? groups )
						? groups
						: new List<string>()
				);
		}

		public Task<IReadOnlyCollection<AppRoleAssignmentInfo>> GetAppRoleAssignmentsAsync(
			string servicePrincipalObjectId )
		{
			GetAppRoleAssignmentsCallCount++;

			return Task.FromResult<IReadOnlyCollection<AppRoleAssignmentInfo>>( Assignments.ToList() );
		}
	}
}