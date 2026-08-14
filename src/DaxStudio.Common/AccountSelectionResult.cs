using Microsoft.Identity.Client;
using System.Collections.Generic;
using System.Linq;

namespace DaxStudio.Common
{
    /// <summary>
    /// The outcome of resolving an Entra account before any token is requested.
    /// </summary>
    public enum AccountSelectionStatus
    {
        /// <summary>
        /// Exactly one account was identified - either it matched the requested UPN or it was the
        /// only account in the DAX Studio token cache. A silent acquisition can be attempted.
        /// </summary>
        Matched,

        /// <summary>
        /// A UPN was requested but no cached or Windows account matches it. There is nothing to try
        /// silently; the only way forward is an interactive sign-in targeted with a login hint.
        /// <b>Never</b> substitute a different account here - see <c>OperatingSystemAccount</c> notes
        /// in <see cref="EntraIdHelper"/>.
        /// </summary>
        RequestedAccountNotFound,

        /// <summary>
        /// No UPN was requested and several accounts are cached, so the intended identity is
        /// genuinely unknown. Requires either a picker or an explicit UPN.
        /// </summary>
        Ambiguous,

        /// <summary>
        /// No UPN was requested and the cache is empty. This is the only situation in which
        /// falling back to the Windows (operating system) account is legitimate, because there is
        /// no other identity that could be silently substituted.
        /// </summary>
        NoCachedAccounts
    }

    /// <summary>
    /// The result of <see cref="EntraIdHelper.SelectAccountAsync"/>.
    /// </summary>
    public sealed class AccountSelectionResult
    {
        private AccountSelectionResult(AccountSelectionStatus status, IAccount account, IReadOnlyList<string> candidates)
        {
            Status = status;
            Account = account;
            Candidates = candidates ?? new string[0];
        }

        public AccountSelectionStatus Status { get; }

        /// <summary>
        /// The resolved account, or null unless <see cref="Status"/> is
        /// <see cref="AccountSelectionStatus.Matched"/>.
        /// </summary>
        public IAccount Account { get; }

        /// <summary>
        /// The usernames of the accounts that were considered. Used to build actionable error
        /// messages when selection is ambiguous or the requested account is missing.
        /// </summary>
        public IReadOnlyList<string> Candidates { get; }

        /// <summary>
        /// True when a silent token acquisition can be attempted with <see cref="Account"/>.
        /// </summary>
        public bool CanTrySilent => Status == AccountSelectionStatus.Matched;

        public static AccountSelectionResult Matched(IAccount account)
            => new AccountSelectionResult(AccountSelectionStatus.Matched, account, new[] { account?.Username });

        public static AccountSelectionResult RequestedAccountNotFound(IEnumerable<IAccount> candidates)
            => new AccountSelectionResult(AccountSelectionStatus.RequestedAccountNotFound, null, ToUsernames(candidates));

        public static AccountSelectionResult Ambiguous(IEnumerable<IAccount> candidates)
            => new AccountSelectionResult(AccountSelectionStatus.Ambiguous, null, ToUsernames(candidates));

        public static AccountSelectionResult NoCachedAccounts()
            => new AccountSelectionResult(AccountSelectionStatus.NoCachedAccounts, null, null);

        private static IReadOnlyList<string> ToUsernames(IEnumerable<IAccount> accounts)
            => (accounts ?? Enumerable.Empty<IAccount>())
                .Select(a => a.Username)
                .Where(u => !string.IsNullOrEmpty(u))
                .OrderBy(u => u, System.StringComparer.OrdinalIgnoreCase)
                .ToList();
    }
}
