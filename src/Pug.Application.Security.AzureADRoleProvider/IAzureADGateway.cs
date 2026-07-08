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
		/// Object ID of the user, group or service principal the app role is assigned to.
		/// </summary>
		public string PrincipalId { get; }

		public Guid AppRoleId { get; }
	}

	internal enum PrincipalType
	{
		User,

		/// <summary>
		/// Application, service, API or any other resource represented by a service principal in the tenant.
		/// </summary>
		Application
	}

	internal sealed class PrincipalInfo
	{
		public PrincipalInfo( string objectId, PrincipalType type )
		{
			ObjectId = objectId;
			Type = type;
		}

		public string ObjectId { get; }

		public PrincipalType Type { get; }
	}

	/// <summary>
	/// Thin abstraction over Microsoft Graph directory queries, allowing role resolution logic to be tested
	/// without a live tenant.
	/// </summary>
	internal interface IAzureADGateway
	{
		/// <returns>Service principal of the specified application within the tenant, or null when not found.</returns>
		Task<ServicePrincipalInfo?> GetServicePrincipalAsync( string applicationId );

		/// <param name="principal">
		/// User object ID or principal name, or service principal object ID or application (client) ID.
		/// </param>
		/// <returns>Object ID and type of the principal, or null when no matching principal exists.</returns>
		Task<PrincipalInfo?> ResolvePrincipalAsync( string principal );

		Task<IReadOnlyCollection<string>> GetTransitiveGroupIdsAsync( PrincipalInfo principal );

		/// <returns>
		/// All app role assignments (to users, groups and service principals) of the specified service principal.
		/// </returns>
		Task<IReadOnlyCollection<AppRoleAssignmentInfo>> GetAppRoleAssignmentsAsync( string servicePrincipalObjectId );
	}
}