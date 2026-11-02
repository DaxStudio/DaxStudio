using System;
using System.Runtime.InteropServices;
using DaxStudio.Common;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DaxStudio.Tests
{
    [TestClass]
    public class UnhandledExceptionTriageTests
    {
        private UnhandledExceptionTriage _triage;

        [TestInitialize]
        public void Setup()
        {
            _triage = new UnhandledExceptionTriage();
        }

        private static COMException ComException(int hresult)
        {
            return new COMException($"Test exception 0x{hresult:X8}", hresult);
        }

        // ----- WPF render thread failures -------------------------------------------------
        // These are NOT recoverable. WPF zombies the composition partition and never
        // reconnects it (MediaContext.NotifyPartitionIsZombie just throws, and
        // MediaSystem.ConnectTransport is only ever called once from Startup) so the process
        // can never render again.

        [DataTestMethod]
        [DataRow(UnhandledExceptionTriage.UCEERR_RENDERTHREADFAILURE)]
        [DataRow(UnhandledExceptionTriage.UCEERR_DISPLAYSTATEINVALID)]
        [DataRow(UnhandledExceptionTriage.UCEERR_NOTIFICATIONSDROPPED)]
        public void RenderThreadFailureIsFatalNotRecoverable(int hresult)
        {
            var decision = _triage.Triage(ComException(hresult));

            Assert.IsNotNull(decision);
            Assert.AreEqual(UnhandledExceptionAction.FatalRenderThreadFailure, decision.Action);
            Assert.IsFalse(decision.IsRecoverable, "the render partition is zombied - the process cannot continue");
        }

        [TestMethod]
        public void RenderThreadFailureIsNeverSilentlySwallowed()
        {
            // repeated occurrences must keep reporting as fatal - there is no threshold at which
            // it becomes safe to carry on
            for (var i = 0; i < 5; i++)
            {
                var decision = _triage.Triage(ComException(UnhandledExceptionTriage.UCEERR_RENDERTHREADFAILURE));
                Assert.AreEqual(UnhandledExceptionAction.FatalRenderThreadFailure, decision.Action);
                Assert.IsFalse(decision.IsRecoverable);
            }
        }

        [TestMethod]
        public void ZombiePartitionInvalidOperationExceptionIsFatal()
        {
            // the NotifyPartitionIsZombie flavour surfaces as an InvalidOperationException
            // rather than a COMException, but it is the same underlying failure
            var decision = _triage.Triage(new InvalidOperationException(UnhandledExceptionTriage.RenderThreadErrorMessage));

            Assert.IsNotNull(decision);
            Assert.AreEqual(UnhandledExceptionAction.FatalRenderThreadFailure, decision.Action);
            Assert.IsFalse(decision.IsRecoverable);
        }

        [TestMethod]
        public void RenderThreadFailureHresultMatchesTheCrashReport()
        {
            // Doctor Dump problem 1059964 reported UCEERR_RENDERTHREADFAILURE as 0x88980406
            Assert.AreEqual(unchecked((int)0x88980406), UnhandledExceptionTriage.UCEERR_RENDERTHREADFAILURE);
        }

        [TestMethod]
        public void IsRenderThreadFailureOnlyMatchesMilErrors()
        {
            Assert.IsTrue(UnhandledExceptionTriage.IsRenderThreadFailure(UnhandledExceptionTriage.UCEERR_RENDERTHREADFAILURE));
            Assert.IsTrue(UnhandledExceptionTriage.IsRenderThreadFailure(UnhandledExceptionTriage.UCEERR_DISPLAYSTATEINVALID));
            Assert.IsTrue(UnhandledExceptionTriage.IsRenderThreadFailure(UnhandledExceptionTriage.UCEERR_NOTIFICATIONSDROPPED));
            Assert.IsFalse(UnhandledExceptionTriage.IsRenderThreadFailure(UnhandledExceptionTriage.CLIPBRD_E_BAD_DATA));
            Assert.IsFalse(UnhandledExceptionTriage.IsRenderThreadFailure(0));
        }

        // ----- genuinely transient failures ------------------------------------------------

        [DataTestMethod]
        [DataRow(UnhandledExceptionTriage.CLIPBRD_E_BAD_DATA)]
        [DataRow(UnhandledExceptionTriage.CLIPBRD_E_CANT_OPEN)]
        [DataRow(UnhandledExceptionTriage.RPC_E_WRONG_THREAD)]
        public void ClipboardErrorsAreRecoverable(int hresult)
        {
            var decision = _triage.Triage(ComException(hresult));

            Assert.IsNotNull(decision);
            Assert.AreEqual(UnhandledExceptionAction.Recover, decision.Action);
            Assert.IsTrue(decision.IsRecoverable);
            Assert.IsFalse(string.IsNullOrEmpty(decision.UserMessage));
        }

        [TestMethod]
        public void DragDropInProgressIsRecoverable()
        {
            var decision = _triage.Triage(new COMException("A drag operation is already in progress"));

            Assert.IsNotNull(decision);
            Assert.IsTrue(decision.IsRecoverable);
            Assert.IsTrue(decision.UserMessage.Contains("Please retry"));
        }

        // ----- everything else must fall through to the existing crash path -----------------

        [TestMethod]
        public void UnknownComExceptionIsNotTriaged()
        {
            var decision = _triage.Triage(ComException(unchecked((int)0x80004005))); // E_FAIL

            Assert.IsNull(decision, "an unrecognized COM error must fall through to the fatal path");
        }

        [TestMethod]
        public void UnrelatedInvalidOperationExceptionIsNotTriaged()
        {
            var decision = _triage.Triage(new InvalidOperationException("something else entirely"));

            Assert.IsNull(decision);
        }

        [TestMethod]
        public void NullExceptionIsNotTriaged()
        {
            Assert.IsNull(_triage.Triage(null));
        }
    }
}
