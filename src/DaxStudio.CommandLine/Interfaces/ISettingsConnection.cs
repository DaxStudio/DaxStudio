namespace DaxStudio.CommandLine.Interfaces
{
    internal interface ISettingsConnection
    {
        string FullConnectionString { get; }

        /// <summary>
        /// The user id from -u|--userid or the DSCMD_USER environment variable. Used to select the
        /// Entra account deterministically, so concurrent dscmd processes cannot drift onto
        /// different identities.
        /// </summary>
        string ResolvedUserID { get; }

        /// <summary>
        /// True when this process must never block on an interactive sign-in prompt.
        /// </summary>
        bool IsNonInteractive { get; }
    }
}
