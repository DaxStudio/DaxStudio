using DaxStudio.Common;
using Microsoft.Identity.Client;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using System.Collections.Generic;
using System.Linq;

namespace DaxStudio.Tests
{
    /// <summary>
    /// Covers the account selection rules that keep dscmd quiet and deterministic on a machine with
    /// more than one Entra account.
    /// </summary>
    [TestClass]
    public class AccountSelectionTests
    {
        private static IAccount Account(string username, string homeAccountId = null)
        {
            var account = Substitute.For<IAccount>();
            account.Username.Returns(username);
            account.HomeAccountId.Returns(new AccountId(
                $"{homeAccountId ?? username}.tenant", homeAccountId ?? username, "tenant"));
            return account;
        }

        private static IReadOnlyList<IAccount> Accounts(params string[] usernames)
            => usernames.Select(u => Account(u)).ToList();

        #region No user id supplied

        [TestMethod]
        public void SingleCachedAccount_IsSelectedSilently_WithoutAUserId()
        {
            // The common case after one bootstrap sign-in: no -u needed, no prompt.
            var cached = Accounts("solo@contoso.com");

            var result = EntraIdHelper.SelectAccountFrom(cached, null, requestedUpn: null);

            Assert.AreEqual(AccountSelectionStatus.Matched, result.Status);
            Assert.IsTrue(result.CanTrySilent);
            Assert.AreEqual("solo@contoso.com", result.Account.Username);
        }

        [TestMethod]
        public void SeveralCachedAccounts_AreAmbiguous_WithoutAUserId()
        {
            var cached = Accounts("one@contoso.com", "two@contoso.com");

            var result = EntraIdHelper.SelectAccountFrom(cached, null, requestedUpn: null);

            Assert.AreEqual(AccountSelectionStatus.Ambiguous, result.Status);
            Assert.IsFalse(result.CanTrySilent, "An ambiguous identity must never be resolved by guessing");
            CollectionAssert.AreEquivalent(
                new[] { "one@contoso.com", "two@contoso.com" },
                result.Candidates.ToArray());
        }

        [TestMethod]
        public void EmptyCache_ReportsNoCachedAccounts_WithoutAUserId()
        {
            var result = EntraIdHelper.SelectAccountFrom(new List<IAccount>(), null, requestedUpn: null);

            Assert.AreEqual(AccountSelectionStatus.NoCachedAccounts, result.Status);
            Assert.IsNull(result.Account);
        }

        #endregion

        #region User id supplied

        [TestMethod]
        public void RequestedAccount_IsMatchedInTheCache_CaseInsensitively()
        {
            var cached = Accounts("one@contoso.com", "Two@Contoso.com");

            var result = EntraIdHelper.SelectAccountFrom(cached, null, "two@CONTOSO.com");

            Assert.AreEqual(AccountSelectionStatus.Matched, result.Status);
            Assert.AreEqual("Two@Contoso.com", result.Account.Username);
        }

        [TestMethod]
        public void RequestedAccount_IsMatchedInTheBrokerSet_WhenNotInTheCache()
        {
            // Zero-bootstrap SSO: the account is known to Windows but has never used DAX Studio.
            var cached = Accounts("one@contoso.com");
            var broker = Accounts("one@contoso.com", "windows@contoso.com");

            var result = EntraIdHelper.SelectAccountFrom(cached, broker, "windows@contoso.com");

            Assert.AreEqual(AccountSelectionStatus.Matched, result.Status);
            Assert.AreEqual("windows@contoso.com", result.Account.Username);
        }

        [TestMethod]
        public void RequestedAccount_IsSurroundedBySpaces_IsStillMatched()
        {
            var cached = Accounts("one@contoso.com");

            var result = EntraIdHelper.SelectAccountFrom(cached, null, "  one@contoso.com  ");

            Assert.AreEqual(AccountSelectionStatus.Matched, result.Status);
        }

        [TestMethod]
        public void RequestedAccountNotFound_NeverSubstitutesAnotherAccount()
        {
            // The whole point of the redesign. Previously an unmatched UPN fell through to the
            // Windows account and frequently authenticated SILENTLY as a different identity.
            var cached = Accounts("one@contoso.com", "two@contoso.com");
            var broker = Accounts("one@contoso.com", "two@contoso.com", "three@contoso.com");

            var result = EntraIdHelper.SelectAccountFrom(cached, broker, "absent@contoso.com");

            Assert.AreEqual(AccountSelectionStatus.RequestedAccountNotFound, result.Status);
            Assert.IsNull(result.Account, "A requested account that was not found must not resolve to any account");
            Assert.IsFalse(result.CanTrySilent, "There is nothing that can safely be tried silently");
        }

        [TestMethod]
        public void RequestedAccountNotFound_ListsTheCandidatesSoTheErrorIsActionable()
        {
            var cached = Accounts("one@contoso.com");
            var broker = Accounts("one@contoso.com", "two@contoso.com");

            var result = EntraIdHelper.SelectAccountFrom(cached, broker, "absent@contoso.com");

            CollectionAssert.AreEquivalent(
                new[] { "one@contoso.com", "two@contoso.com" },
                result.Candidates.ToArray());
        }

        [TestMethod]
        public void RequestedAccount_TakesPrecedenceOverASingleCachedAccount()
        {
            // A supplied -u is an instruction, not a tiebreak. It must not be quietly ignored just
            // because there happens to be exactly one cached account.
            var cached = Accounts("solo@contoso.com");

            var result = EntraIdHelper.SelectAccountFrom(cached, new List<IAccount>(), "someone.else@contoso.com");

            Assert.AreEqual(AccountSelectionStatus.RequestedAccountNotFound, result.Status);
        }

        #endregion

        #region The two account sets must not be conflated

        [TestMethod]
        public void WindowsAccounts_DoNotMakeSelectionAmbiguous_WhenNoUserIdIsSupplied()
        {
            // This is the multi-account machine case. Broker accounts are deliberately NOT passed
            // in when no UPN was requested, so a single cached account still resolves silently even
            // though Windows knows about several other work/school accounts.
            var cached = Accounts("solo@contoso.com");

            var result = EntraIdHelper.SelectAccountFrom(cached, brokerAccounts: null, requestedUpn: null);

            Assert.AreEqual(AccountSelectionStatus.Matched, result.Status,
                "Windows accounts must not be counted when deciding whether there is exactly one obvious account");
        }

        #endregion

        #region Available account listing

        [TestMethod]
        public void AvailableAccounts_CachedAccountOverridesTheSameWindowsAccount()
        {
            var cached = Account("user@contoso.com", "account-1");
            var windows = Account("user@contoso.com", "account-1");

            var result = EntraIdHelper.MergeAvailableAccounts(new[] { cached }, new[] { windows });

            Assert.AreEqual(1, result.Count);
            Assert.AreEqual(EntraAccountSource.DaxStudioCache, result[0].Source);
        }

        [TestMethod]
        public void AvailableAccounts_PreservesTheSameUpnInDifferentTenants()
        {
            var firstTenant = Account("shared@contoso.com", "account-1");
            var secondTenant = Account("shared@contoso.com", "account-2");

            var result = EntraIdHelper.MergeAvailableAccounts(
                new[] { firstTenant },
                new[] { firstTenant, secondTenant });

            Assert.AreEqual(2, result.Count);
            Assert.AreEqual(EntraAccountSource.DaxStudioCache, result[0].Source);
            Assert.AreEqual(EntraAccountSource.Windows, result[1].Source);
        }

        [TestMethod]
        public void AvailableAccounts_LabelsWindowsOnlyAccounts()
        {
            var windows = Account("windows@contoso.com", "account-1");

            var result = EntraIdHelper.MergeAvailableAccounts(
                new List<IAccount>(),
                new[] { windows });

            Assert.AreEqual(1, result.Count);
            Assert.AreEqual("windows@contoso.com", result[0].Username);
            Assert.AreEqual(EntraAccountSource.Windows, result[0].Source);
        }

        [TestMethod]
        public void AvailableAccounts_HandlesMissingAccountMetadata()
        {
            var account = Substitute.For<IAccount>();

            var result = EntraIdHelper.MergeAvailableAccounts(
                new[] { account },
                new List<IAccount>());

            Assert.AreEqual(1, result.Count);
            Assert.AreEqual(string.Empty, result[0].Username);
            Assert.AreEqual(string.Empty, result[0].TenantId);
            Assert.AreEqual(string.Empty, result[0].HomeAccountId);
        }

        [TestMethod]
        public void AvailableAccounts_AreSortedByUsernameThenTenant()
        {
            var zed = Account("zed@contoso.com", "account-z");
            var alpha = Account("alpha@contoso.com", "account-a");

            var result = EntraIdHelper.MergeAvailableAccounts(
                new[] { zed, alpha },
                new List<IAccount>());

            CollectionAssert.AreEqual(
                new[] { "alpha@contoso.com", "zed@contoso.com" },
                result.Select(account => account.Username).ToArray());
        }

        #endregion

        #region Identity assertion

        [TestMethod]
        public void EnforcedIdentity_MatchesCaseInsensitively()
        {
            Assert.IsTrue(EntraIdHelper.IsAcceptableIdentity("User@Contoso.com", "user@contoso.com", enforce: true));
        }

        [TestMethod]
        public void EnforcedIdentity_RejectsADifferentAccount()
        {
            // Login hints are best-effort and an operator can pick a different account in the
            // picker, so this assertion is what actually guarantees the right identity is used.
            Assert.IsFalse(EntraIdHelper.IsAcceptableIdentity("user@contoso.com", "someone.else@contoso.com", enforce: true));
        }

        [TestMethod]
        public void EnforcedIdentity_RejectsAMissingAccount()
        {
            Assert.IsFalse(EntraIdHelper.IsAcceptableIdentity("user@contoso.com", null, enforce: true));
        }

        [TestMethod]
        public void HintedIdentity_AllowsADifferentAccount()
        {
            // The desktop app passes the last used UPN as a hint only - the user remains free to
            // sign in as somebody else.
            Assert.IsTrue(EntraIdHelper.IsAcceptableIdentity("user@contoso.com", "someone.else@contoso.com", enforce: false));
        }

        #endregion

        #region Renewal account selection

        [TestMethod]
        public void Renewal_SelectsTheExactHomeAccountIdentifier_WhenUpnsMatch()
        {
            var first = Account("shared@contoso.com", "account-1");
            var expected = Account("shared@contoso.com", "account-2");

            var result = EntraIdHelper.SelectRenewalAccount(
                new[] { first, expected },
                expected.HomeAccountId.Identifier,
                expected.Username);

            Assert.AreSame(expected, result);
        }

        [TestMethod]
        public void Renewal_DoesNotSubstituteASameUpn_WhenTheHomeAccountIsMissing()
        {
            var otherTenant = Account("shared@contoso.com", "other-account");

            var result = EntraIdHelper.SelectRenewalAccount(
                new[] { otherTenant },
                "missing-account.tenant",
                "shared@contoso.com");

            Assert.IsNull(result);
        }

        [TestMethod]
        public void LegacyRenewalContext_FallsBackToTheUsername()
        {
            var expected = Account("user@contoso.com");

            var result = EntraIdHelper.SelectRenewalAccount(
                new[] { expected },
                accountIdentifier: null,
                username: "USER@CONTOSO.COM");

            Assert.AreSame(expected, result);
        }

        [TestMethod]
        public void ExistingTokenContexts_AllowInteractiveRenewalByDefault()
        {
            Assert.AreEqual(TokenRenewalMode.AllowInteractive, new AccessTokenContext().RenewalMode);
        }

        #endregion

        #region Error messages

        [TestMethod]
        public void NonInteractiveError_NamesTheAccount_ListsCandidates_AndSuggestsTheFix()
        {
            var ex = EntraAuthenticationException.InteractionRequired(
                "The account 'absent@contoso.com' is not signed in on this machine.",
                "absent@contoso.com",
                new[] { "one@contoso.com", "two@contoso.com" },
                "powerbi://api.powerbi.com/v1.0/myorg/ws");

            StringAssert.Contains(ex.Message, "absent@contoso.com");
            StringAssert.Contains(ex.Message, "one@contoso.com");
            StringAssert.Contains(ex.Message, "two@contoso.com");
            StringAssert.Contains(ex.Message, "dscmd auth");
            StringAssert.Contains(ex.Message, "--non-interactive");
            StringAssert.Contains(ex.Message, "powerbi://api.powerbi.com/v1.0/myorg/ws");
        }

        [TestMethod]
        public void AmbiguityDescription_TellsTheUserToSupplyAUserId()
        {
            var selection = AccountSelectionResult.Ambiguous(Accounts("one@contoso.com", "two@contoso.com"));

            var reason = EntraIdHelper.DescribeWhyInteractionIsNeeded(selection, new AuthenticationOptions());

            StringAssert.Contains(reason, "-u");
        }

        [TestMethod]
        public void ExpiredCachedAccountDescription_MentionsReAuthentication()
        {
            // Proves the case that cannot be inferred away: the account is known and unambiguous,
            // but Entra still requires a human. This is why --non-interactive has to exist.
            var selection = AccountSelectionResult.Matched(Account("one@contoso.com"));

            var reason = EntraIdHelper.DescribeWhyInteractionIsNeeded(
                selection,
                new AuthenticationOptions { RequestedUpn = "one@contoso.com" });

            StringAssert.Contains(reason, "one@contoso.com");
            StringAssert.Contains(reason, "expired");
        }

        #endregion
    }
}
