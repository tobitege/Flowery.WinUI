using Flowery.Services;

namespace Flowery.WinUI.Gallery
{
    /// <summary>
    /// Removes the credentials that the Gallery's GitHub and OpenID Connect demo providers stored in the
    /// Windows Credential Manager up to 1.10. The providers were removed in 1.11, so nothing reads or deletes
    /// these entries any more, and a GitHub token would otherwise stay on the machine.
    /// </summary>
    public static class LegacyKanbanCredentials
    {
        private const string RemovedStateKey = "kanban.legacy-credentials-removed";

        /// <summary>The resources and keys the removed providers wrote through <see cref="ISecureStorage"/>.</summary>
        internal static readonly (string Resource, string Key)[] Entries =
        [
            ("Flowery.GitHub", "AccessToken"),
            ("Flowery.OAuth.oidc-demo", "HasToken"),
            ("Flowery.OAuth.oidc-demo", "AccessToken"),
            ("Flowery.OAuth.oidc-demo", "RefreshToken"),
            ("Flowery.OAuth.oidc-demo", "UserRawId"),
            ("Flowery.OAuth.oidc-demo", "UserDisplayName"),
            ("Flowery.OAuth.oidc-demo", "UserEmail"),
            ("Flowery.OAuth.oidc-demo", "UserAvatarUrl")
        ];

        /// <summary>
        /// Removes the entries once. A marker in the state storage keeps later starts from querying the vault again;
        /// it is only written when every entry is gone, so a vault that failed silently is tried again next start.
        /// </summary>
        public static void RemoveOnce(ISecureStorage secureStorage, IStateStorage stateStorage)
        {
            ArgumentNullException.ThrowIfNull(secureStorage);
            ArgumentNullException.ThrowIfNull(stateStorage);
            if (stateStorage.LoadLines(RemovedStateKey).Count > 0)
                return;

            foreach (var (resource, key) in Entries)
                secureStorage.RemoveValue(resource, key);

            if (Entries.Any(entry => secureStorage.GetValue(entry.Resource, entry.Key) is not null))
                return;

            stateStorage.SaveLines(RemovedStateKey, (string[])["1"]);
        }
    }
}
