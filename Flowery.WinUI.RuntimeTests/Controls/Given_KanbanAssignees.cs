using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Flowery.WinUI.Kanban.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Flowery.WinUI.RuntimeTests.Controls
{
    [TestClass]
    public class Given_KanbanAssignees
    {
        [TestMethod]
        public async Task When_AssigneeAdapterCannotResolveId_AssignmentIsPreserved()
        {
            var task = new FlowTask
            {
                Title = "Assigned task",
                Assignee = "External User",
                AssigneeId = "external:user-42"
            };
            var kanban = CreateKanban(task);

            kanban.AssigneeAdapter = new FlowKanbanAssigneeAdapter();
            await kanban.RefreshAssigneesAsync();

            Assert.AreEqual("external:user-42", task.AssigneeId);
            Assert.AreEqual("External User", task.Assignee);
            var unresolved = kanban.AssigneeFilterOptions.Single();
            Assert.AreEqual(task.AssigneeId, unresolved.Id);
            Assert.IsFalse(unresolved.IsResolved);
            StringAssert.Contains(unresolved.DisplayName, "External User");

            kanban.AssigneeAdapter = null;
            await kanban.RefreshAssigneesAsync();

            Assert.AreEqual("external:user-42", task.AssigneeId);
            Assert.AreEqual("External User", task.Assignee);
            unresolved = kanban.AssigneeFilterOptions.Single();
            Assert.IsFalse(unresolved.IsResolved);
        }

        [TestMethod]
        public async Task When_AssigneeAdapterResolvesOpaqueId_AssignmentIdIsPreserved()
        {
            const string assigneeId = " external:user-42 ";
            var task = new FlowTask { Title = "Assigned task", AssigneeId = assigneeId };
            var kanban = CreateKanban(task);

            kanban.AssigneeAdapter = new FlowKanbanAssigneeAdapter(new[]
            {
                new FlowKanbanAssignee(assigneeId, "External User", roles: new[] { "Reviewer" })
            });
            await kanban.RefreshAssigneesAsync();

            Assert.AreEqual(assigneeId, task.AssigneeId);
            Assert.AreEqual("External User", task.Assignee);
            CollectionAssert.AreEqual(new[] { "Reviewer" }, task.AssigneeRoles.ToArray());
            var option = kanban.AssigneeFilterOptions.Single();
            Assert.AreEqual(task.AssigneeId, option.Id);
            Assert.IsTrue(option.IsResolved);
        }

        [TestMethod]
        public async Task When_AssigneeIsMissingFromDirectory_ResolverCallbackProvidesMetadata()
        {
            var task = new FlowTask { Title = "Assigned task", AssigneeId = "user:42" };
            var kanban = CreateKanban(task);

            kanban.AssigneeAdapter = new FlowKanbanAssigneeAdapter(
                static (assigneeId, _) => Task.FromResult<FlowKanbanAssignee?>(
                    new FlowKanbanAssignee(assigneeId, "Resolved User", roles: new[] { "Team Lead" })));
            await kanban.RefreshAssigneesAsync();

            Assert.AreEqual("user:42", task.AssigneeId);
            Assert.AreEqual("Resolved User", task.Assignee);
            CollectionAssert.AreEqual(new[] { "Team Lead" }, task.AssigneeRoles.ToArray());
            var option = kanban.AssigneeFilterOptions.Single();
            Assert.AreEqual(task.AssigneeId, option.Id);
            Assert.IsTrue(option.IsResolved);
        }

        [TestMethod]
        public async Task When_AssigneeAdapterSuppliesAvatarAndRoles_TaskCardUsesTransientMetadata()
        {
            var avatar = new BitmapImage();
            var adapter = new FlowKanbanAssigneeAdapter(new[]
            {
                new FlowKanbanAssignee("assignee-1", "Alex Doe", avatar, new[] { "Administrator", "Reviewer", "administrator" })
            });
            var task = new FlowTask { Title = "Assigned", AssigneeId = "assignee-1" };
            var kanban = CreateKanban(task);
            kanban.AssigneeAdapter = adapter;

            await kanban.RefreshAssigneesAsync();

            Assert.AreEqual("Alex Doe", task.Assignee);
            Assert.AreSame(avatar, task.AssigneeAvatarSource);
            CollectionAssert.AreEqual(new[] { "Administrator", "Reviewer" }, task.AssigneeRoles.ToArray());

            var card = new FlowTaskCard { Task = task };
            Assert.IsTrue(card.HasAssigneeAvatar);
            Assert.AreSame(avatar, card.AssigneeAvatarSource);
            Assert.IsTrue(card.HasAssigneeRoles);
            Assert.AreEqual("Administrator, Reviewer", card.AssigneeRolesText);

            var json = JsonSerializer.Serialize(task, FlowKanbanJsonContext.Default.FlowTask);
            Assert.IsFalse(json.Contains(nameof(FlowTask.AssigneeAvatarSource), StringComparison.Ordinal));
            Assert.IsFalse(json.Contains(nameof(FlowTask.AssigneeRoles), StringComparison.Ordinal));
        }

        [TestMethod]
        public async Task When_AssigneeAdapterChangesOffUiThread_BoardRefreshesOnDispatcher()
        {
            var adapter = new FlowKanbanAssigneeAdapter(new[]
            {
                new FlowKanbanAssignee("external:first", "First User")
            });
            var kanban = new FlowKanban { AutoSaveAfterEdits = false, AssigneeAdapter = adapter };
            RuntimeTestHelpers.AttachToHost(kanban);
            await RuntimeTestHelpers.EnsureLoadedAsync(kanban);
            await kanban.RefreshAssigneesAsync();
            Assert.AreEqual("external:first", kanban.AssigneeFilterOptions.Single().Id);

            await Task.Run(() => adapter.SetAssignees(new[]
            {
                new FlowKanbanAssignee("external:second", "Second User")
            }));
            await RuntimeTestHelpers.WaitUntilAsync(
                () => kanban.AssigneeFilterOptions.SingleOrDefault()?.Id == "external:second",
                "the board shows the assignees set from another thread");
        }

        [TestMethod]
        public async Task When_AssigneeAdapterLoadsCompleteOutOfOrder_OldResultCannotOverwriteNewMetadata()
        {
            var task = new FlowTask { Title = "Assigned task", AssigneeId = "person-1" };
            var kanban = CreateKanban(task);
            var oldCompletion = new TaskCompletionSource<IReadOnlyList<FlowKanbanAssignee>>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            var newCompletion = new TaskCompletionSource<IReadOnlyList<FlowKanbanAssignee>>(
                TaskCreationOptions.RunContinuationsAsynchronously);

            kanban.AssigneeAdapter = new FlowKanbanAssigneeAdapter(_ => oldCompletion.Task);
            var oldLoad = kanban.RefreshAssigneesAsync();
            kanban.AssigneeAdapter = new FlowKanbanAssigneeAdapter(_ => newCompletion.Task);
            var newLoad = kanban.RefreshAssigneesAsync();

            newCompletion.SetResult(new[] { new FlowKanbanAssignee("person-1", "New User", roles: new[] { "New Role" }) });
            await newLoad;
            Assert.AreEqual("New User", task.Assignee);
            CollectionAssert.AreEqual(new[] { "New Role" }, task.AssigneeRoles.ToArray());

            oldCompletion.SetResult(new[] { new FlowKanbanAssignee("person-1", "Old User", roles: new[] { "Old Role" }) });
            await oldLoad;
            Assert.AreEqual("New User", task.Assignee);
            CollectionAssert.AreEqual(new[] { "New Role" }, task.AssigneeRoles.ToArray());
        }

        [TestMethod]
        public async Task When_AssigneeAdapterLoadThrows_FailureIsReportedAndMetadataIsRetained()
        {
            var task = new FlowTask { Title = "Assigned task", AssigneeId = "person-1" };
            var kanban = CreateKanban(task);
            kanban.AssigneeAdapter = new FlowKanbanAssigneeAdapter(new[]
            {
                new FlowKanbanAssignee("person-1", "Known User", roles: new[] { "Reviewer" })
            });
            FlowKanbanAssigneeAdapterFailedEventArgs? failure = null;
            kanban.AssigneeAdapterFailed += (_, args) => failure = args;

            await kanban.RefreshAssigneesAsync();
            kanban.AssigneeAdapter = new FlowKanbanAssigneeAdapter(
                static _ => Task.FromException<IReadOnlyList<FlowKanbanAssignee>>(
                    new InvalidOperationException("Assignee load failed.")));
            await kanban.RefreshAssigneesAsync();

            Assert.IsNotNull(failure);
            Assert.AreEqual(FlowKanbanAssigneeAdapterOperation.LoadAssignees, failure.Operation);
            Assert.IsInstanceOfType<InvalidOperationException>(failure.Exception);
            Assert.AreEqual("Known User", task.Assignee);
            CollectionAssert.AreEqual(new[] { "Reviewer" }, task.AssigneeRoles.ToArray());
        }

        [TestMethod]
        public async Task When_AssigneeAdapterCompletesAfterUnload_ControlMetadataIsNotChanged()
        {
            var completion = new TaskCompletionSource<IReadOnlyList<FlowKanbanAssignee>>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            var task = new FlowTask
            {
                Title = "Assigned task",
                Assignee = "Original User",
                AssigneeId = "person-1"
            };
            var kanban = CreateKanban(task);
            kanban.AssigneeAdapter = new FlowKanbanAssigneeAdapter(_ => completion.Task);
            RuntimeTestHelpers.AttachToHost(kanban);
            await RuntimeTestHelpers.EnsureLoadedAsync(kanban);

            var refresh = kanban.RefreshAssigneesAsync();
            // WinUI raises Unloaded after the removal, on a later tick; the board cancels its refresh there.
            var unloaded = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
            kanban.Unloaded += (_, _) => unloaded.TrySetResult(null);
            RuntimeTestHelpers.GetHostPanel().Children.Clear();
            await unloaded.Task.WaitAsync(TimeSpan.FromSeconds(5));

            completion.SetResult(new[] { new FlowKanbanAssignee("person-1", "Late User", roles: new[] { "Late Role" }) });
            await refresh;

            Assert.AreEqual("Original User", task.Assignee);
            Assert.AreEqual(0, task.AssigneeRoles.Count);
        }

        [TestMethod]
        public async Task When_CallbackAdapterLoadsCompleteOutOfOrder_ResolveUsesNewestSnapshot()
        {
            var first = new TaskCompletionSource<IReadOnlyList<FlowKanbanAssignee>>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            var second = new TaskCompletionSource<IReadOnlyList<FlowKanbanAssignee>>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            var calls = 0;
            var adapter = new FlowKanbanAssigneeAdapter(_ => ++calls == 1 ? first.Task : second.Task);

            var olderLoad = adapter.GetAssigneesAsync();
            var newerLoad = adapter.GetAssigneesAsync();

            second.SetResult(new[] { new FlowKanbanAssignee("person-1", "New User", roles: new[] { "New Role" }) });
            var newerSnapshot = await newerLoad;
            Assert.AreEqual("New User", newerSnapshot.Single().DisplayName);

            first.SetResult(new[] { new FlowKanbanAssignee("person-1", "Old User", roles: new[] { "Old Role" }) });
            await olderLoad;

            var resolved = await adapter.ResolveAssigneeAsync("person-1");
            Assert.IsNotNull(resolved);
            Assert.AreEqual("New User", resolved.DisplayName);
            CollectionAssert.AreEqual(new[] { "New Role" }, resolved.Roles.ToArray());
        }

        [TestMethod]
        public async Task When_ATaskIsReassigned_TheNewAssigneesMetadataReplacesTheOld()
        {
            var samAvatar = new BitmapImage();
            var task = new FlowTask { Title = "Assigned", AssigneeId = "sam" };
            var kanban = CreateKanban(task);
            kanban.AssigneeAdapter = new FlowKanbanAssigneeAdapter(new[]
            {
                new FlowKanbanAssignee("sam", "Sam", samAvatar, new[] { "Administrator" }),
                new FlowKanbanAssignee("dario", "Dario", roles: new[] { "Product owner" })
            });
            RuntimeTestHelpers.AttachToHost(kanban);
            await RuntimeTestHelpers.EnsureLoadedAsync(kanban);
            await kanban.RefreshAssigneesAsync();
            Assert.AreSame(samAvatar, task.AssigneeAvatarSource);

            // The task editor writes the new ID and name of a reassigned task.
            task.AssigneeId = "dario";
            task.Assignee = "Dario";

            await RuntimeTestHelpers.WaitUntilAsync(
                () => task.AssigneeAvatarSource is null && task.AssigneeRoles.Contains("Product owner"),
                "the card carries Dario's avatar and roles");
            Assert.AreEqual("Dario", task.Assignee);
        }

        [TestMethod]
        public void When_SettingsWereStoredPerUser_TheLocalUsersEntryBecomesTheSettings()
        {
            var storage = new InMemoryStateStorage();
            var previous = StateStorageProvider.Instance;
            StateStorageProvider.Configure(storage);
            try
            {
                storage.SaveLines("FlowKanban.LocalUserId", new[] { "11111111-2222-3333-4444-555555555555" });
                storage.SaveLines("kanban.settings", new[] { "{\"ConfirmColumnRemovals\":true,\"ColumnWidth\":280}" });
                storage.SaveLines("kanban.user.settings.local_11111111-2222-3333-4444-555555555555",
                    new[] { "{\"ConfirmColumnRemovals\":false,\"ColumnWidth\":333}" });
                storage.SaveLines("kanban.user.settings.github_octocat", new[] { "{\"ColumnWidth\":120}" });

                var kanban = new FlowKanban();
                Assert.IsTrue(kanban.TryLoadSettings(forceReload: true, out var error), error?.Message);

                Assert.IsFalse(kanban.ConfirmColumnRemovals);
                Assert.AreEqual(333, kanban.ColumnWidth);
                Assert.AreEqual(0, storage.GetKeys("kanban.user.settings").Count());
                StringAssert.Contains(string.Join("", storage.LoadLines("kanban.settings")), "333");
            }
            finally
            {
                StateStorageProvider.Configure(previous);
            }
        }

        private static FlowKanban CreateKanban(FlowTask task)
        {
            return new FlowKanban
            {
                AutoSaveAfterEdits = false,
                Board = new FlowKanbanData
                {
                    Columns = { new FlowKanbanColumnData { Title = "Todo", Tasks = { task } } }
                }
            };
        }
    }
}
