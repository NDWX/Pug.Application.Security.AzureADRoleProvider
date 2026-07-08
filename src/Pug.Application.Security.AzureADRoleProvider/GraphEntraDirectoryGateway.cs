using Microsoft.Graph;
using Microsoft.Graph.Models;
using Microsoft.Graph.Models.ODataErrors;

namespace Pug.Application.Security.AzureADRoleProvider
{
	internal sealed class GraphEntraDirectoryGateway : IEntraDirectoryGateway
	{
		private const int PageSize = 999;

		private readonly GraphServiceClient _graphServiceClient;

		public GraphEntraDirectoryGateway( GraphServiceClient graphServiceClient )
		{
			_graphServiceClient = graphServiceClient ?? throw new ArgumentNullException( nameof(graphServiceClient) );
		}

		public async Task<ServicePrincipalInfo?> GetServicePrincipalAsync( string applicationId )
		{
			ServicePrincipalCollectionResponse? response =
				await _graphServiceClient.ServicePrincipals.GetAsync(
							configuration =>
							{
								configuration.QueryParameters.Filter =
									$"appId eq '{applicationId.Replace( "'", "''" )}'";
								configuration.QueryParameters.Select = new[] { "id", "appRoles" };
							} )
						.ConfigureAwait( false );

			ServicePrincipal? servicePrincipal = response?.Value?.FirstOrDefault();

			if( servicePrincipal?.Id == null )
				return null;

			Dictionary<Guid, string> appRoleValues = new Dictionary<Guid, string>();

			foreach( AppRole appRole in servicePrincipal.AppRoles ?? Enumerable.Empty<AppRole>() )
			{
				if( appRole.Id != null && !string.IsNullOrEmpty( appRole.Value ) )
					appRoleValues[appRole.Id.Value] = appRole.Value!;
			}

			return new ServicePrincipalInfo( servicePrincipal.Id, appRoleValues );
		}

		public async Task<string?> GetUserObjectIdAsync( string user )
		{
			try
			{
				User? graphUser =
					await _graphServiceClient.Users[user].GetAsync(
								configuration => configuration.QueryParameters.Select = new[] { "id" } )
							.ConfigureAwait( false );

				return graphUser?.Id;
			}
			catch( ODataError error ) when( error.ResponseStatusCode == 404 )
			{
				return null;
			}
		}

		public async Task<IReadOnlyCollection<string>> GetTransitiveGroupIdsAsync( string userObjectId )
		{
			List<string> groupIds = new List<string>();

			GroupCollectionResponse? response =
				await _graphServiceClient.Users[userObjectId].TransitiveMemberOf.GraphGroup.GetAsync(
							configuration =>
							{
								configuration.QueryParameters.Select = new[] { "id" };
								configuration.QueryParameters.Top = PageSize;
							} )
						.ConfigureAwait( false );

			while( response != null )
			{
				foreach( Group group in response.Value ?? Enumerable.Empty<Group>() )
				{
					if( group.Id != null )
						groupIds.Add( group.Id );
				}

				if( string.IsNullOrEmpty( response.OdataNextLink ) )
					break;

				response =
					await _graphServiceClient.Users[userObjectId].TransitiveMemberOf.GraphGroup
							.WithUrl( response.OdataNextLink )
							.GetAsync()
							.ConfigureAwait( false );
			}

			return groupIds;
		}

		public async Task<IReadOnlyCollection<AppRoleAssignmentInfo>> GetAppRoleAssignmentsAsync(
			string servicePrincipalObjectId )
		{
			List<AppRoleAssignmentInfo> assignments = new List<AppRoleAssignmentInfo>();

			AppRoleAssignmentCollectionResponse? response =
				await _graphServiceClient.ServicePrincipals[servicePrincipalObjectId].AppRoleAssignedTo.GetAsync(
							configuration => configuration.QueryParameters.Top = PageSize )
						.ConfigureAwait( false );

			while( response != null )
			{
				foreach( AppRoleAssignment assignment in response.Value ?? Enumerable.Empty<AppRoleAssignment>() )
				{
					if( assignment.PrincipalId != null && assignment.AppRoleId != null )
						assignments.Add(
							new AppRoleAssignmentInfo(
								assignment.PrincipalId.Value.ToString(), assignment.AppRoleId.Value ) );
				}

				if( string.IsNullOrEmpty( response.OdataNextLink ) )
					break;

				response =
					await _graphServiceClient.ServicePrincipals[servicePrincipalObjectId].AppRoleAssignedTo
							.WithUrl( response.OdataNextLink )
							.GetAsync()
							.ConfigureAwait( false );
			}

			return assignments;
		}
	}
}
