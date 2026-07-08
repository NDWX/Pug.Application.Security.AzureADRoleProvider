using Pug.Application.Security.AzureADRoleProvider;

namespace Pug.Application.Security.AzureADRoleProvider.Tests
{
	public class PrincipalRoleProviderTests
	{
		private static readonly Guid AdministratorRoleId = Guid.Parse( "20000000-0000-0000-0000-000000000001" );
		private static readonly Guid OperatorRoleId = Guid.Parse( "20000000-0000-0000-0000-000000000002" );
		private static readonly Guid AuditorRoleId = Guid.Parse( "20000000-0000-0000-0000-000000000003" );

		private const string UserObjectId = "30000000-0000-0000-0000-000000000001";
		private const string UserPrincipalName = "jane.doe@contoso.com";
		private const string GroupObjectId = "40000000-0000-0000-0000-000000000001";
		private const string UnassignedGroupObjectId = "40000000-0000-0000-0000-000000000002";
		private const string ClientApplicationObjectId = "50000000-0000-0000-0000-000000000001";
		private const string ClientApplicationClientId = "50000000-0000-0000-0000-0000000000aa";

		private static FakeAzureADGateway CreateGateway()
		{
			FakeAzureADGateway gateway = new FakeAzureADGateway();

			gateway.AppRoles[AdministratorRoleId] = "Administrator";
			gateway.AppRoles[OperatorRoleId] = "Operator";
			gateway.AppRoles[AuditorRoleId] = "Auditor";

			gateway.Users[UserObjectId] = UserObjectId;
			gateway.Users[UserPrincipalName] = UserObjectId;

			gateway.ServicePrincipals[ClientApplicationObjectId] = ClientApplicationObjectId;
			gateway.ServicePrincipals[ClientApplicationClientId] = ClientApplicationObjectId;

			gateway.PrincipalGroups[UserObjectId] = new List<string> { GroupObjectId, UnassignedGroupObjectId };
			gateway.PrincipalGroups[ClientApplicationObjectId] = new List<string> { GroupObjectId };

			// direct assignment to user
			gateway.Assignments.Add( new AppRoleAssignmentInfo( UserObjectId, AdministratorRoleId ) );
			// group assignment, inherited by both the user and the client application service principal
			gateway.Assignments.Add( new AppRoleAssignmentInfo( GroupObjectId, OperatorRoleId ) );
			// direct assignment to the client application service principal
			gateway.Assignments.Add( new AppRoleAssignmentInfo( ClientApplicationObjectId, AuditorRoleId ) );

			return gateway;
		}

		private static PrincipalRoleProvider CreateProvider(
			FakeAzureADGateway gateway, TimeSpan? cacheDuration = null )
		{
			return new PrincipalRoleProvider(
					gateway,
					new AzureADRoleProviderOptions
					{
						ApplicationId = FakeAzureADGateway.ApplicationId,
						CacheDuration = cacheDuration ?? TimeSpan.FromMinutes( 5 )
					}
				);
		}

		[Fact]
		public async Task GetPrincipalRolesReturnsDirectAndGroupInheritedRoles()
		{
			PrincipalRoleProvider provider = CreateProvider( CreateGateway() );

			IEnumerable<string> roles = await provider.GetPrincipalRolesAsync( UserObjectId );

			Assert.Equal(
					new[] { "Administrator", "Operator" },
					roles.OrderBy( role => role, StringComparer.Ordinal ) );
		}

		[Fact]
		public async Task UserMayBeIdentifiedByPrincipalName()
		{
			PrincipalRoleProvider provider = CreateProvider( CreateGateway() );

			Assert.True( await provider.PrincipalIsInRoleAsync( UserPrincipalName, "Administrator" ) );
		}

		[Fact]
		public async Task RolesNotAssignedToUserAreNotReported()
		{
			PrincipalRoleProvider provider = CreateProvider( CreateGateway() );

			Assert.False( await provider.PrincipalIsInRoleAsync( UserObjectId, "Auditor" ) );
		}

		[Fact]
		public async Task RoleComparisonIsCaseSensitive()
		{
			PrincipalRoleProvider provider = CreateProvider( CreateGateway() );

			Assert.False( await provider.PrincipalIsInRoleAsync( UserObjectId, "administrator" ) );
		}

		[Fact]
		public async Task PrincipalIsInRolesRequiresAllRoles()
		{
			PrincipalRoleProvider provider = CreateProvider( CreateGateway() );

			Assert.True(
					await provider.PrincipalIsInRolesAsync(
						UserObjectId, new[] { "Administrator", "Operator" } ) );

			Assert.False(
					await provider.PrincipalIsInRolesAsync(
						UserObjectId, new[] { "Administrator", "Auditor" } ) );
		}

		[Fact]
		public async Task PrincipalIsInRolesWithEmptyCollectionReturnsTrue()
		{
			PrincipalRoleProvider provider = CreateProvider( CreateGateway() );

			Assert.True( await provider.PrincipalIsInRolesAsync( UserObjectId, Array.Empty<string>() ) );
		}

		[Fact]
		public async Task ApplicationPrincipalGetsDirectAndGroupInheritedRoles()
		{
			PrincipalRoleProvider provider = CreateProvider( CreateGateway() );

			IEnumerable<string> roles = await provider.GetPrincipalRolesAsync( ClientApplicationObjectId );

			Assert.Equal(
					new[] { "Auditor", "Operator" },
					roles.OrderBy( role => role, StringComparer.Ordinal ) );
		}

		[Fact]
		public async Task ApplicationPrincipalMayBeIdentifiedByClientId()
		{
			PrincipalRoleProvider provider = CreateProvider( CreateGateway() );

			Assert.True( await provider.PrincipalIsInRoleAsync( ClientApplicationClientId, "Auditor" ) );
			Assert.True( await provider.PrincipalIsInRoleAsync( ClientApplicationClientId, "Operator" ) );
			Assert.False( await provider.PrincipalIsInRoleAsync( ClientApplicationClientId, "Administrator" ) );
		}

		[Fact]
		public async Task UnknownPrincipalIdentifierHasNoRoles()
		{
			PrincipalRoleProvider provider = CreateProvider( CreateGateway() );

			Assert.Empty( await provider.GetPrincipalRolesAsync( "99999999-0000-0000-0000-000000000000" ) );
		}

		[Fact]
		public async Task UnknownUserHasNoRoles()
		{
			PrincipalRoleProvider provider = CreateProvider( CreateGateway() );

			Assert.Empty( await provider.GetPrincipalRolesAsync( "unknown.user@contoso.com" ) );
			Assert.False( await provider.PrincipalIsInRoleAsync( "unknown.user@contoso.com", "Administrator" ) );
		}

		[Fact]
		public async Task RolesAreCachedWithinCacheDuration()
		{
			FakeAzureADGateway gateway = CreateGateway();
			PrincipalRoleProvider provider = CreateProvider( gateway );

			await provider.GetPrincipalRolesAsync( UserObjectId );
			await provider.GetPrincipalRolesAsync( UserObjectId );
			await provider.PrincipalIsInRoleAsync( UserObjectId, "Administrator" );

			Assert.Equal( 1, gateway.ResolvePrincipalCallCount );
			Assert.Equal( 1, gateway.GetTransitiveGroupIdsCallCount );
			Assert.Equal( 1, gateway.GetServicePrincipalCallCount );
			Assert.Equal( 1, gateway.GetAppRoleAssignmentsCallCount );
		}

		[Fact]
		public async Task ZeroCacheDurationDisablesCaching()
		{
			FakeAzureADGateway gateway = CreateGateway();
			PrincipalRoleProvider provider = CreateProvider( gateway, TimeSpan.Zero );

			await provider.GetPrincipalRolesAsync( UserObjectId );
			await provider.GetPrincipalRolesAsync( UserObjectId );

			Assert.Equal( 2, gateway.ResolvePrincipalCallCount );
			Assert.Equal( 2, gateway.GetAppRoleAssignmentsCallCount );
		}

		[Fact]
		public async Task UnknownApplicationCausesException()
		{
			FakeAzureADGateway gateway = CreateGateway();

			PrincipalRoleProvider provider =
				new PrincipalRoleProvider(
					gateway,
					new AzureADRoleProviderOptions
						{ ApplicationId = "99999999-9999-9999-9999-999999999999" } );

			await Assert.ThrowsAsync<InvalidOperationException>(
					() => provider.GetPrincipalRolesAsync( UserObjectId ) );
		}

		[Fact]
		public void SynchronousMembersReturnSameResults()
		{
			PrincipalRoleProvider provider = CreateProvider( CreateGateway() );

			Assert.True( provider.PrincipalIsInRole( UserObjectId, "Administrator" ) );
			Assert.True( provider.PrincipalIsInRoles( UserObjectId, new[] { "Administrator", "Operator" } ) );
			Assert.Equal(
					new[] { "Administrator", "Operator" },
					provider.GetPrincipalRoles( UserObjectId ).OrderBy( role => role, StringComparer.Ordinal ) );
		}

		[Fact]
		public void ObsoleteUserRoleProviderMembersDelegateToPrincipalMembers()
		{
			PrincipalRoleProvider provider = CreateProvider( CreateGateway() );

#pragma warning disable CS0618
			IUserRoleProvider userRoleProvider = provider;
#pragma warning restore CS0618

			Assert.True( userRoleProvider.UserIsInRole( UserPrincipalName, "Administrator" ) );
			Assert.False( userRoleProvider.UserIsInRoles( UserPrincipalName, new[] { "Auditor" } ) );
			Assert.Contains( "Operator", userRoleProvider.GetUserRoles( UserPrincipalName ) );
		}

		[Fact]
		public async Task FailedResolutionIsNotCached()
		{
			FakeAzureADGateway gateway = CreateGateway();

			PrincipalRoleProvider provider =
				new PrincipalRoleProvider(
					gateway,
					new AzureADRoleProviderOptions
						{ ApplicationId = "99999999-9999-9999-9999-999999999999" } );

			await Assert.ThrowsAsync<InvalidOperationException>(
					() => provider.GetPrincipalRolesAsync( UserObjectId ) );

			await Assert.ThrowsAsync<InvalidOperationException>(
					() => provider.GetPrincipalRolesAsync( UserObjectId ) );

			Assert.Equal( 2, gateway.GetServicePrincipalCallCount );
		}
	}
}
