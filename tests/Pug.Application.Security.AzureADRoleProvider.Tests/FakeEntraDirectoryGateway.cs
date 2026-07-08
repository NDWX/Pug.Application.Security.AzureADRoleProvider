using Pug.Application.Security.AzureADRoleProvider;

namespace Pug.Application.Security.AzureADRoleProvider.Tests
{
	internal sealed class FakeEntraDirectoryGateway : IEntraDirectoryGateway
	{
		public const string ApplicationId = "00000000-0000-0000-0000-0000000000aa";
		public const string ServicePrincipalObjectId = "10000000-0000-0000-0000-000000000001";

		public Dictionary<Guid, string> AppRoles { get; } = new();

		/// <summary>User object ID or UPN → object ID.</summary>
		public Dictionary<string, string> Users { get; } = new( StringComparer.OrdinalIgnoreCase );

		/// <summary>User object ID → transitive group object IDs.</summary>
		public Dictionary<string, List<string>> UserGroups { get; } = new( StringComparer.OrdinalIgnoreCase );

		public List<AppRoleAssignmentInfo> Assignments { get; } = new();

		public int GetServicePrincipalCallCount { get; private set; }
		public int GetUserObjectIdCallCount { get; private set; }
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

		public Task<string?> GetUserObjectIdAsync( string user )
		{
			GetUserObjectIdCallCount++;

			return Task.FromResult( Users.TryGetValue( user, out string? objectId ) ? objectId : null );
		}

		public Task<IReadOnlyCollection<string>> GetTransitiveGroupIdsAsync( string userObjectId )
		{
			GetTransitiveGroupIdsCallCount++;

			return Task.FromResult<IReadOnlyCollection<string>>(
					UserGroups.TryGetValue( userObjectId, out List<string>? groups )
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
