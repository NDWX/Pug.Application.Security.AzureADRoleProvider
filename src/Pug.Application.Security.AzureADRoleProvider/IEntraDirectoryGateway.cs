namespace Pug.Application.Security.AzureADRoleProvider
{
	internal sealed class ServicePrincipalInfo
	{
		public ServicePrincipalInfo( string objectId, IReadOnlyDictionary<Guid, string> appRoleValues )
		{
			ObjectId = objectId;
			AppRoleValues = appRoleValues;
		}

		public string ObjectId { get; }

		/// <summary>
		/// App role definitions of the service principal, keyed by app role ID. Values are the role 'value' strings.
		/// </summary>
		public IReadOnlyDictionary<Guid, string> AppRoleValues { get; }
	}

	internal readonly struct AppRoleAssignmentInfo
	{
		public AppRoleAssignmentInfo( string principalId, Guid appRoleId )
		{
			PrincipalId = principalId;
			AppRoleId = appRoleId;
		}

		/// <summary>
		/// Object ID of the user or group the app role is assigned to.
		/// </summary>
		public string PrincipalId { get; }

		public Guid AppRoleId { get; }
	}

	/// <summary>
	/// Thin abstraction over Microsoft Graph directory queries, allowing role resolution logic to be tested
	/// without a live tenant.
	/// </summary>
	internal interface IEntraDirectoryGateway
	{
		/// <returns>Service principal of the specified application within the tenant, or null when not found.</returns>
		Task<ServicePrincipalInfo?> GetServicePrincipalAsync( string applicationId );

		/// <param name="user">User object ID or user principal name.</param>
		/// <returns>Object ID of the user, or null when the user does not exist.</returns>
		Task<string?> GetUserObjectIdAsync( string user );

		Task<IReadOnlyCollection<string>> GetTransitiveGroupIdsAsync( string userObjectId );

		/// <returns>All app role assignments (to users and groups) of the specified service principal.</returns>
		Task<IReadOnlyCollection<AppRoleAssignmentInfo>> GetAppRoleAssignmentsAsync( string servicePrincipalObjectId );
	}
}
