namespace Pug.Application.Security.AzureADRoleProvider
{
	public class AzureADRoleProviderOptions
	{
		/// <summary>
		/// Application (client) ID of the Entra ID app registration whose app roles define the application role set.
		/// </summary>
		public string ApplicationId { get; set; } = string.Empty;

		/// <summary>
		/// How long resolved roles, app role definitions and role assignments are cached in memory.
		/// Role or membership changes in Entra ID may take up to this duration to be reflected.
		/// Set to <see cref="TimeSpan.Zero"/> to disable caching.
		/// </summary>
		public TimeSpan CacheDuration { get; set; } = TimeSpan.FromMinutes( 5 );
	}
}