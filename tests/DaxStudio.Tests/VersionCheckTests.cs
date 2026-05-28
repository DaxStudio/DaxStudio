using DaxStudio.UI.Model;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;
using System;

namespace DaxStudio.Tests
{
    [TestClass]
    public class VersionCheckTests
    {
        private const string StableUrl = "https://daxstudio.org/";
        private const string PreviewUrl = "https://github.com/daxstudio/daxstudio/releases";

        private static JObject BuildJson(string stableVersion, string previewVersion)
        {
            var jobj = new JObject
            {
                ["Version"] = stableVersion,
                ["DownloadUrl"] = StableUrl
            };
            if (previewVersion != null)
            {
                jobj["PreRelease"] = new JObject
                {
                    ["Version"] = previewVersion,
                    ["DownloadUrl"] = PreviewUrl
                };
            }
            return jobj;
        }

        // --- Opted-out (stable user, ShowPreReleaseNotifications=false) ---

        [TestMethod]
        public void OptedOut_NoPreReleaseBlock_UsesStable()
        {
            var jobj = BuildJson("3.2.0.1000", previewVersion: null);

            var result = VersionCheck.SelectServerVersion(jobj, isPreviewBuild: false, showPreReleaseNotifications: false);

            Assert.AreEqual(Version.Parse("3.2.0.1000"), result.EffectiveVersion);
            Assert.AreEqual("Production", result.ServerVersionType);
            Assert.AreEqual(new Uri(StableUrl), result.EffectiveDownloadUrl);
        }

        [TestMethod]
        public void OptedOut_PreReleaseNewerThanStable_StillUsesStable()
        {
            var jobj = BuildJson("3.2.0.1000", "3.2.0.1100");

            var result = VersionCheck.SelectServerVersion(jobj, isPreviewBuild: false, showPreReleaseNotifications: false);

            Assert.AreEqual(Version.Parse("3.2.0.1000"), result.EffectiveVersion);
            Assert.AreEqual("Production", result.ServerVersionType);
            Assert.AreEqual(new Uri(StableUrl), result.EffectiveDownloadUrl);
        }

        // --- Opted-in via ShowPreReleaseNotifications (stable build, user opt-in) ---

        [TestMethod]
        public void OptedIn_PreReleaseNewerThanStable_UsesPreview()
        {
            var jobj = BuildJson("3.2.0.1000", "3.2.0.1100");

            var result = VersionCheck.SelectServerVersion(jobj, isPreviewBuild: false, showPreReleaseNotifications: true);

            Assert.AreEqual(Version.Parse("3.2.0.1100"), result.EffectiveVersion);
            Assert.AreEqual("Preview", result.ServerVersionType);
            Assert.AreEqual(new Uri(PreviewUrl), result.EffectiveDownloadUrl);
        }

        [TestMethod]
        public void OptedIn_StableNewerThanPreRelease_UsesStable()
        {
            var jobj = BuildJson("3.2.0.1200", "3.2.0.1100");

            var result = VersionCheck.SelectServerVersion(jobj, isPreviewBuild: false, showPreReleaseNotifications: true);

            Assert.AreEqual(Version.Parse("3.2.0.1200"), result.EffectiveVersion);
            Assert.AreEqual("Production", result.ServerVersionType);
            Assert.AreEqual(new Uri(StableUrl), result.EffectiveDownloadUrl);
        }

        [TestMethod]
        public void OptedIn_StableEqualsPreRelease_UsesStable()
        {
            var jobj = BuildJson("3.2.0.1100", "3.2.0.1100");

            var result = VersionCheck.SelectServerVersion(jobj, isPreviewBuild: false, showPreReleaseNotifications: true);

            Assert.AreEqual(Version.Parse("3.2.0.1100"), result.EffectiveVersion);
            Assert.AreEqual("Production", result.ServerVersionType);
        }

        // --- Auto-opted-in via IsPreviewBuild (user is running a PREVIEW build) ---

        [TestMethod]
        public void IsPreviewBuild_PreReleaseNewerThanStable_UsesPreview()
        {
            var jobj = BuildJson("3.2.0.1000", "3.2.0.1100");

            var result = VersionCheck.SelectServerVersion(jobj, isPreviewBuild: true, showPreReleaseNotifications: false);

            Assert.AreEqual(Version.Parse("3.2.0.1100"), result.EffectiveVersion);
            Assert.AreEqual("Preview", result.ServerVersionType);
            Assert.AreEqual(new Uri(PreviewUrl), result.EffectiveDownloadUrl);
        }

        [TestMethod]
        public void IsPreviewBuild_StableNewerThanPreRelease_UsesStable()
        {
            // A preview-build user with installed v1.1-pre should be offered a newer stable v1.2.
            var jobj = BuildJson("3.2.0.1200", "3.2.0.1100");

            var result = VersionCheck.SelectServerVersion(jobj, isPreviewBuild: true, showPreReleaseNotifications: false);

            Assert.AreEqual(Version.Parse("3.2.0.1200"), result.EffectiveVersion);
            Assert.AreEqual("Production", result.ServerVersionType);
            Assert.AreEqual(new Uri(StableUrl), result.EffectiveDownloadUrl);
        }

        [TestMethod]
        public void IsPreviewBuild_NoPreReleaseBlock_UsesStable()
        {
            var jobj = BuildJson("3.2.0.1200", previewVersion: null);

            var result = VersionCheck.SelectServerVersion(jobj, isPreviewBuild: true, showPreReleaseNotifications: false);

            Assert.AreEqual(Version.Parse("3.2.0.1200"), result.EffectiveVersion);
            Assert.AreEqual("Production", result.ServerVersionType);
        }

        // --- Edge cases ---

        [TestMethod]
        public void EmptyPreReleaseVersion_IsIgnored()
        {
            var jobj = new JObject
            {
                ["Version"] = "3.2.0.1000",
                ["DownloadUrl"] = StableUrl,
                ["PreRelease"] = new JObject
                {
                    ["Version"] = "",
                    ["DownloadUrl"] = PreviewUrl
                }
            };

            var result = VersionCheck.SelectServerVersion(jobj, isPreviewBuild: true, showPreReleaseNotifications: true);

            Assert.AreEqual(Version.Parse("3.2.0.1000"), result.EffectiveVersion);
            Assert.AreEqual("Production", result.ServerVersionType);
        }

        [TestMethod]
        public void NullJson_Throws()
        {
            Assert.ThrowsExactly<ArgumentNullException>(() =>
                VersionCheck.SelectServerVersion(null, isPreviewBuild: false, showPreReleaseNotifications: false));
        }

        [TestMethod]
        public void PreviewBlockExists_ButOptedOut_ProductionFieldsArePopulated()
        {
            var jobj = BuildJson("3.2.0.1000", "3.2.0.1100");

            var result = VersionCheck.SelectServerVersion(jobj, isPreviewBuild: false, showPreReleaseNotifications: false);

            Assert.AreEqual(Version.Parse("3.2.0.1000"), result.ProductionVersion);
            Assert.AreEqual(Version.Parse("3.2.0.1100"), result.PreReleaseVersion);
            Assert.AreEqual(new Uri(StableUrl), result.ProductionDownloadUrl);
            Assert.AreEqual(new Uri(PreviewUrl), result.PreReleaseDownloadUrl);
        }
    }
}
