using System.Collections.Concurrent;

namespace Pug.Application.Security.AzureADRoleProvider
{
	/// <summary>
	/// Minimal thread-safe cache of asynchronously produced values with absolute per-entry expiry.
	/// Faulted value tasks are evicted so errors are never cached.
	/// </summary>
	internal sealed class TtlCache<TValue>
	{
		private sealed class Entry
		{
			public Entry( Lazy<Task<TValue>> valueTask, DateTime expiry )
			{
				ValueTask = valueTask;
				Expiry = expiry;
			}

			public Lazy<Task<TValue>> ValueTask { get; }

			public DateTime Expiry { get; }
		}

		private readonly TimeSpan _timeToLive;
		private readonly Func<DateTime> _clock;
		private readonly ConcurrentDictionary<string, Entry> _entries = new ConcurrentDictionary<string, Entry>();

		public TtlCache( TimeSpan timeToLive, Func<DateTime>? clock = null )
		{
			_timeToLive = timeToLive;
			_clock = clock ?? ( () => DateTime.UtcNow );
		}

		public async Task<TValue> GetOrAddAsync( string key, Func<string, Task<TValue>> valueFactory )
		{
			if( _timeToLive <= TimeSpan.Zero )
				return await valueFactory( key ).ConfigureAwait( false );

			DateTime now = _clock();

			Entry entry =
				_entries.GetOrAdd(
					key,
					k => new Entry( new Lazy<Task<TValue>>( () => valueFactory( k ) ), now.Add( _timeToLive ) ) );

			if( entry.Expiry <= now )
			{
				Entry replacement =
					new Entry( new Lazy<Task<TValue>>( () => valueFactory( key ) ), now.Add( _timeToLive ) );

				entry = _entries.TryUpdate( key, replacement, entry )
							? replacement
							: _entries.GetOrAdd( key, replacement );
			}

			try
			{
				return await entry.ValueTask.Value.ConfigureAwait( false );
			}
			catch
			{
				( (ICollection<KeyValuePair<string, Entry>>)_entries )
					.Remove( new KeyValuePair<string, Entry>( key, entry ) );

				throw;
			}
		}
	}
}