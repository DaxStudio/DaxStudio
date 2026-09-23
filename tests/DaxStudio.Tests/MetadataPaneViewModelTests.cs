using System;
using System.Reflection;
using System.Threading.Tasks;
using ADOTabular;
using Caliburn.Micro;
using DaxStudio.Interfaces;
using DaxStudio.UI.Interfaces;
using DaxStudio.UI.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;

namespace DaxStudio.Tests
{
    [TestClass]
    public class MetadataPaneViewModelTests
    {
        private static Task WaitForMetadataAsync(MetadataPaneViewModel pane)
        {
            return (Task)typeof(MetadataPaneViewModel).GetMethod("WaitForMetadataLoadAsync",
                BindingFlags.Instance | BindingFlags.NonPublic).Invoke(pane, null);
        }

        private static ADOTabularModel CreateModel(string name)
        {
            return new ADOTabularModel(null, null, name, name, string.Empty, string.Empty);
        }

        [TestMethod]
        public async Task LoadingStatusRemainsUntilModelLoadClearsBusyState()
        {
            var provider = Substitute.For<IMetadataProvider>();
            var loading = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            provider.SetSelectedModelAsync(Arg.Any<ADOTabularModel>()).Returns(loading.Task);
            var pane = new MetadataPaneViewModel(provider, Substitute.For<IEventAggregator>(), null,
                Substitute.For<IGlobalOptions>());
            var document = Substitute.For<IHaveStatusBar>();

            async Task ConnectAsync()
            {
                using (var status = new DaxStudio.Core.Model.StatusBarMessage(document, "Loading model metadata..."))
                {
                    pane.SelectedModel = CreateModel("Model");
                    await WaitForMetadataAsync(pane);
                    Assert.IsFalse(pane.IsBusy);
                }
            }

            var connecting = ConnectAsync();
            Assert.IsTrue(pane.IsBusy);
            Assert.IsFalse(connecting.IsCompleted);
            document.DidNotReceive().SetStatusBarMessage("Ready");

            loading.SetResult(true);
            await connecting;

            Assert.IsFalse(pane.IsBusy);
            document.Received(1).SetStatusBarMessage("Ready");
        }

        [TestMethod]
        public async Task FailedModelLoadClearsBusyStateBeforeCompletion()
        {
            var provider = Substitute.For<IMetadataProvider>();
            var loading = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            provider.SetSelectedModelAsync(Arg.Any<ADOTabularModel>()).Returns(loading.Task);
            var pane = new MetadataPaneViewModel(provider, Substitute.For<IEventAggregator>(), null,
                Substitute.For<IGlobalOptions>());
            pane.SelectedModel = CreateModel("Model");
            var waiting = WaitForMetadataAsync(pane);

            loading.SetException(new InvalidOperationException("Loading failed."));
            await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => waiting);

            Assert.IsFalse(pane.IsBusy);
        }

        [TestMethod]
        public async Task MetadataWaitIncludesSelectionsStartedWhileWaiting()
        {
            var provider = Substitute.For<IMetadataProvider>();
            var firstLoad = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var secondLoad = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var firstModel = CreateModel("First");
            var secondModel = CreateModel("Second");
            provider.SetSelectedModelAsync(firstModel).Returns(firstLoad.Task);
            provider.SetSelectedModelAsync(secondModel).Returns(secondLoad.Task);
            var pane = new MetadataPaneViewModel(provider, Substitute.For<IEventAggregator>(), null,
                Substitute.For<IGlobalOptions>());
            var databaseLoad = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            typeof(MetadataPaneViewModel).GetField("_databaseSelectionTask",
                BindingFlags.Instance | BindingFlags.NonPublic).SetValue(pane, databaseLoad.Task);

            pane.SelectedModel = firstModel;
            var waiting = WaitForMetadataAsync(pane);
            firstLoad.SetResult(true);
            Assert.IsFalse(waiting.IsCompleted);
            pane.SelectedModel = secondModel;
            databaseLoad.SetResult(true);
            Assert.IsFalse(waiting.IsCompleted);
            secondLoad.SetResult(true);
            await waiting;

            Assert.IsFalse(pane.IsBusy);
        }
    }
}