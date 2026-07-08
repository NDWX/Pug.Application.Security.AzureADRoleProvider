using Pug.Application.Security.AzureADRoleProvider;

namespace Pug.Application.Security.AzureADRoleProvider.Tests
{
	public class TtlCacheTests
	{
		[Fact]
		public async Task ValueIsReusedUntilExpiry()
		{
			DateTime now = new DateTime( 2026, 1, 1, 0, 0, 0, DateTimeKind.Utc );
			int factoryCalls = 0;

			TtlCache<int> cache = new TtlCache<int>( TimeSpan.FromMinutes( 5 ), () => now );

			Assert.Equal( 1, await cache.GetOrAddAsync( "key", _ => Task.FromResult( ++factoryCalls ) ) );

			now = now.AddMinutes( 4 );

			Assert.Equal( 1, await cache.GetOrAddAsync( "key", _ => Task.FromResult( ++factoryCalls ) ) );
			Assert.Equal( 1, factoryCalls );
		}

		[Fact]
		public async Task ValueIsRefreshedAfterExpiry()
		{
			DateTime now = new DateTime( 2026, 1, 1, 0, 0, 0, DateTimeKind.Utc );
			int factoryCalls = 0;

			TtlCache<int> cache = new TtlCache<int>( TimeSpan.FromMinutes( 5 ), () => now );

			await cache.GetOrAddAsync( "key", _ => Task.FromResult( ++factoryCalls ) );

			now = now.AddMinutes( 6 );

			Assert.Equal( 2, await cache.GetOrAddAsync( "key", _ => Task.FromResult( ++factoryCalls ) ) );
			Assert.Equal( 2, factoryCalls );
		}

		[Fact]
		public async Task EntriesAreCachedPerKey()
		{
			TtlCache<string> cache = new TtlCache<string>( TimeSpan.FromMinutes( 5 ) );

			Assert.Equal( "first", await cache.GetOrAddAsync( "first", key => Task.FromResult( key ) ) );
			Assert.Equal( "second", await cache.GetOrAddAsync( "second", key => Task.FromResult( key ) ) );
		}

		[Fact]
		public async Task FaultedTaskIsEvicted()
		{
			TtlCache<int> cache = new TtlCache<int>( TimeSpan.FromMinutes( 5 ) );
			int factoryCalls = 0;

			await Assert.ThrowsAsync<InvalidOperationException>(
					() => cache.GetOrAddAsync(
						"key",
						_ =>
						{
							factoryCalls++;

							return Task.FromException<int>( new InvalidOperationException() );
						} ) );

			Assert.Equal( 42, await cache.GetOrAddAsync( "key", _ => Task.FromResult( 42 ) ) );
			Assert.Equal( 1, factoryCalls );
		}

		[Fact]
		public async Task ZeroTimeToLiveBypassesCache()
		{
			TtlCache<int> cache = new TtlCache<int>( TimeSpan.Zero );
			int factoryCalls = 0;

			await cache.GetOrAddAsync( "key", _ => Task.FromResult( ++factoryCalls ) );

			Assert.Equal( 2, await cache.GetOrAddAsync( "key", _ => Task.FromResult( ++factoryCalls ) ) );
		}
	}
}
