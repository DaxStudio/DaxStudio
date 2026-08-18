using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;

namespace DaxStudio.Common
{
    /// <summary>
    /// Thrown when an Entra access token cannot be acquired without interaction and the caller has
    /// declared that it cannot service a prompt (dscmd <c>--non-interactive</c>), or when the
    /// account that was actually authenticated is not the one that was explicitly requested.
    /// <para>
    /// The message is deliberately actionable - when an overnight batch fails, naming the account,
    /// listing the cached candidates and suggesting the exact command to run is the single most
    /// useful thing that can be printed.
    /// </para>
    /// </summary>
    [Serializable]
    public class EntraAuthenticationException : Exception
    {
        public EntraAuthenticationException() { }

        public EntraAuthenticationException(string message) : base(message) { }

        public EntraAuthenticationException(string message, Exception innerException)
            : base(message, innerException) { }

        protected EntraAuthenticationException(SerializationInfo info, StreamingContext context)
            : base(info, context) { }

        /// <summary>
        /// Builds the message shown when interaction is required but not permitted.
        /// </summary>
        /// <param name="reason">Why interaction became necessary.</param>
        /// <param name="requestedUpn">The UPN the caller asked for, if any.</param>
        /// <param name="candidates">Usernames of the accounts found in the token cache.</param>
        /// <param name="serverName">The server being connected to, used to build the suggested command.</param>
        public static EntraAuthenticationException InteractionRequired(
            string reason,
            string requestedUpn,
            IReadOnlyList<string> candidates,
            string serverName)
        {
            var sb = new StringBuilder();
            sb.Append("Interactive sign-in is required but --non-interactive was specified. ");
            sb.Append(reason);

            if (candidates != null && candidates.Any())
            {
                sb.AppendLine();
                sb.AppendLine();
                sb.AppendLine("Accounts currently available to DAX Studio:");
                foreach (var candidate in candidates)
                {
                    sb.AppendLine($"  - {candidate}");
                }
            }

            sb.AppendLine();
            sb.AppendLine("To fix this, sign in once interactively so the account is cached. Either re-run");
            sb.AppendLine("this command without --non-interactive, or authenticate on its own:");
            sb.Append("  dscmd auth");
            if (!string.IsNullOrWhiteSpace(serverName)) sb.Append($" -s \"{serverName}\"");

            // Suggest the account to bootstrap: the one that was asked for, or the only sensible
            // candidate. With several candidates the user must choose, so -u is shown as a placeholder.
            if (!string.IsNullOrWhiteSpace(requestedUpn)) sb.Append($" -u \"{requestedUpn}\"");
            else if (candidates != null && candidates.Count > 1) sb.Append(" -u \"<account>\"");
            else if (candidates != null && candidates.Count == 1) sb.Append($" -u \"{candidates[0]}\"");

            sb.AppendLine();
            sb.Append("Then re-run this command, passing the same account via -u or the DSCMD_USER environment variable.");

            return new EntraAuthenticationException(sb.ToString());
        }

        /// <summary>
        /// Builds the message shown when the authenticated identity is not the one that was
        /// explicitly requested. This is the assertion that actually guarantees correctness -
        /// login hints are best-effort and an operator can pick a different account in the picker.
        /// </summary>
        public static EntraAuthenticationException IdentityMismatch(string requestedUpn, string actualUpn)
        {
            return new EntraAuthenticationException(
                $"Authentication returned a different account than the one requested. " +
                $"Requested '{requestedUpn}' but was signed in as '{actualUpn}'. " +
                "Aborting rather than running as an unintended identity, which could apply the wrong row-level security.");
        }
    }
}
