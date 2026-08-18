using System;

namespace DaxStudio.Common
{
    /// <summary>
    /// Describes how a caller wants an Entra account to be selected and whether the caller is
    /// able to service an interactive sign-in prompt.
    /// <para>
    /// This is what keeps the desktop app and dscmd on a single acquisition path while giving them
    /// different policies. The desktop app supplies the last used UPN as a non-binding <b>hint</b>
    /// and can always prompt; dscmd supplies an explicit <c>-u|--userid</c> as a binding
    /// <b>assertion</b> and may be forbidden from prompting.
    /// </para>
    /// </summary>
    public sealed class AuthenticationOptions
    {
        /// <summary>
        /// The user principal name used to select the MSAL account. May be null or empty, in which
        /// case selection falls back to the "exactly one cached account" rule.
        /// </summary>
        public string RequestedUpn { get; set; }

        /// <summary>
        /// When true the resolved account MUST match <see cref="RequestedUpn"/>; a mismatch is an
        /// error. When false the UPN is only a hint - the user remains free to pick a different
        /// account in the interactive picker (desktop behaviour).
        /// </summary>
        public bool EnforceRequestedUpn { get; set; }

        /// <summary>
        /// When false, any situation that would require an interactive prompt fails with an
        /// actionable error instead. This is a failure policy, not a separate code path.
        /// </summary>
        public bool AllowInteractivePrompt { get; set; } = true;

        /// <summary>
        /// Owner window for the WAM sign-in dialog. Ignored when prompting is not allowed.
        /// </summary>
        public IntPtr? OwnerWindowHandle { get; set; }

        /// <summary>
        /// True when a UPN was supplied by the caller and so should drive account selection.
        /// </summary>
        public bool HasRequestedUpn => !string.IsNullOrWhiteSpace(RequestedUpn);

        /// <summary>
        /// Options for the desktop app: <paramref name="lastUsedUpn"/> is a hint only and
        /// interaction is always permitted.
        /// </summary>
        public static AuthenticationOptions ForInteractiveUser(string lastUsedUpn, IntPtr? hwnd)
        {
            return new AuthenticationOptions
            {
                RequestedUpn = lastUsedUpn,
                EnforceRequestedUpn = false,
                AllowInteractivePrompt = true,
                OwnerWindowHandle = hwnd
            };
        }
    }
}
