using Microsoft.Graph;
using Microsoft.Graph.Models;
using Microsoft.Graph.Models.ODataErrors;

namespace Pug.Application.Security.AzureADRoleProvider
{
	internal sealed class GraphAzureADGateway : IAzureADGateway
	{
		private const int PageSize = 999;

		private readonly GraphServiceClient _graphServiceClient;

		public GraphAzureADGateway( GraphServiceClient graphServiceClient )
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
								configuration.QueryParameters.Select = ["id", "appRoles"];
							} )
						.ConfigureAwait( false );

			ServicePrincipal? servicePrincipal = response?.Value?.FirstOrDefault();

			if( servicePrincipal?.Id == null )
				return null;

			Dictionary<Guid, string> appRoleValues = new();

			foreach( AppRole appRole in servicePrincipal.AppRoles ?? Enumerable.Empty<AppRole>() )
			{
				if( appRole.Id != null && !string.IsNullOrEmpty( appRole.Value ) )
					appRoleValues[appRole.Id.Value] = appRole.Value!;
			}

			return new ServicePrincipalInfo( servicePrincipal.Id, appRoleValues );
		}

		public async Task<PrincipalInfo?> ResolvePrincipalAsync( string principal )
		{
			try
			{
				User? user =
					await _graphServiceClient.Users[principal].GetAsync(
								configuration => configuration.QueryParameters.Select = ["id"])
							.ConfigureAwait( false );

				if( user?.Id != null )
					return new PrincipalInfo( user.Id, PrincipalType.User );
			}
			catch( ODataError error ) when( error.ResponseStatusCode is 404 or 400 )
			{
			}

			if( !Guid.TryParse( principal, out Guid _ ) )
				return null;

			try
			{
				ServicePrincipal? servicePrincipal =
					await _graphServiceClient.ServicePrincipals[principal].GetAsync(
								configuration => configuration.QueryParameters.Select = ["id"])
							.ConfigureAwait( false );

				if( servicePrincipal?.Id != null )
					return new PrincipalInfo( servicePrincipal.Id, PrincipalType.Application );
			}
			catch( ODataError error ) when( error.ResponseStatusCode == 404 )
			{
			}

			try
			{
				ServicePrincipal? servicePrincipal =
					await _graphServiceClient.ServicePrincipalsWithAppId( principal ).GetAsync(
								configuration => configuration.QueryParameters.Select = ["id"])
							.ConfigureAwait( false );

				if( servicePrincipal?.Id != null )
					return new PrincipalInfo( servicePrincipal.Id, PrincipalType.Application );
			}
			catch( ODataError error ) when( error.ResponseStatusCode == 404 )
			{
			}

			return null;
		}

		public Task<IReadOnlyCollection<string>> GetTransitiveGroupIdsAsync( PrincipalInfo principal )
		{
			return principal.Type == PrincipalType.User
						? GetUserTransitiveGroupIdsAsync( principal.ObjectId )
						: GetServicePrincipalTransitiveGroupIdsAsync( principal.ObjectId );
		}

		private async Task<IReadOnlyCollection<string>> GetUserTransitiveGroupIdsAsync( string userObjectId )
		{
			List<string> groupIds = [];

			GroupCollectionResponse? response =
				await _graphServiceClient.Users[userObjectId].TransitiveMemberOf.GraphGroup.GetAsync(
							configuration =>
							{
								configuration.QueryParameters.Select = ["id"];
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

		private async Task<IReadOnlyCollection<string>> GetServicePrincipalTransitiveGroupIdsAsync(
			string servicePrincipalObjectId )
		{
			List<string> groupIds = [];

			// query parameters and OData cast on this endpoint require advanced-query headers,
			// so fetch directory objects plainly and filter to groups locally
			DirectoryObjectCollectionResponse? response =
				await _graphServiceClient.ServicePrincipals[servicePrincipalObjectId].TransitiveMemberOf
						.GetAsync( configuration => configuration.QueryParameters.Top = PageSize )
						.ConfigureAwait( false );

			while( response != null )
			{
				foreach( DirectoryObject directoryObject in response.Value ?? Enumerable.Empty<DirectoryObject>() )
				{
					if( directoryObject is Group { Id: not null } group )
						groupIds.Add( group.Id );
				}

				if( string.IsNullOrEmpty( response.OdataNextLink ) )
					break;

				response =
					await _graphServiceClient.ServicePrincipals[servicePrincipalObjectId].TransitiveMemberOf
							.WithUrl( response.OdataNextLink )
							.GetAsync()
							.ConfigureAwait( false );
			}

			return groupIds;
		}

		public async Task<IReadOnlyCollection<AppRoleAssignmentInfo>> GetAppRoleAssignmentsAsync(
			string servicePrincipalObjectId )
		{
			List<AppRoleAssignmentInfo> assignments = [];

			AppRoleAssignmentCollectionResponse? response =
				await _graphServiceClient.ServicePrincipals[servicePrincipalObjectId].AppRoleAssignedTo.GetAsync(
							configuration => configuration.QueryParameters.Top = PageSize )
						.ConfigureAwait( false );

			while( response != null )
			{
				foreach( AppRoleAssignment assignment in response.Value ?? Enumerable.Empty<AppRoleAssignment>() )
				{
					if( assignment is { PrincipalId: not null, AppRoleId: not null } )
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