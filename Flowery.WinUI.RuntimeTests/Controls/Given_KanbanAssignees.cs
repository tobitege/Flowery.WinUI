using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Flowery.WinUI.Gallery;
using Flowery.WinUI.Gallery.Examples;
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
            Assert.IsFalse(card.IsAssigneeAvatarPlaceholder);
            Assert.AreSame(avatar, card.AssigneeAvatarSource);
            Assert.IsTrue(card.HasAssigneeRoles);
            Assert.AreEqual("Administrator, Reviewer", card.AssigneeRolesText);

            var initialsCard = new FlowTaskCard { Task = new FlowTask { Title = "Initials", Assignee = "Bo Lee" } };
            Assert.IsTrue(initialsCard.HasAssignee);
            Assert.IsTrue(initialsCard.IsAssigneeAvatarPlaceholder);

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

        [TestMethod]
        public async Task When_TasksMoveOrJoin_TheAdapterListIsNotReloaded()
        {
            var adapter = new CountingAdapter(new[]
            {
                new FlowKanbanAssignee("sam", "Sam", roles: new[] { "Administrator" }),
                new FlowKanbanAssignee("dario", "Dario", roles: new[] { "Product owner" })
            });
            var moved = new FlowTask { Title = "Moved", AssigneeId = "sam" };
            var todo = new FlowKanbanColumnData { Title = "Todo", Tasks = { moved } };
            var done = new FlowKanbanColumnData { Title = "Done" };
            var kanban = new FlowKanban
            {
                AutoSaveAfterEdits = false,
                Board = new FlowKanbanData { Columns = { todo, done } }
            };
            kanban.AssigneeAdapter = adapter;
            RuntimeTestHelpers.AttachToHost(kanban);
            await RuntimeTestHelpers.EnsureLoadedAsync(kanban);
            await kanban.RefreshAssigneesAsync();
            var loads = adapter.LoadCount;

            todo.Tasks.Remove(moved);
            done.Tasks.Add(moved);
            var joined = new FlowTask { Title = "Joined", AssigneeId = "dario" };
            done.Tasks.Add(joined);

            Assert.AreEqual(loads, adapter.LoadCount, "A move or a new task reloaded the adapter's list.");
            Assert.AreEqual(0, adapter.ResolveCounts.Count, "Known IDs were resolved again.");
            CollectionAssert.AreEqual(new[] { "Administrator" }, moved.AssigneeRoles.ToArray());
            Assert.AreEqual("Dario", joined.Assignee);
            CollectionAssert.AreEqual(new[] { "Product owner" }, joined.AssigneeRoles.ToArray());
        }

        [TestMethod]
        public async Task When_ATaskWithAnUnknownIdJoins_OnlyThatIdIsResolvedOnce()
        {
            var adapter = new CountingAdapter(
                new[] { new FlowKanbanAssignee("sam", "Sam") },
                new[] { new FlowKanbanAssignee("ext-1", "External", roles: new[] { "Guest" }) });
            var todo = new FlowKanbanColumnData { Title = "Todo" };
            var done = new FlowKanbanColumnData { Title = "Done" };
            var kanban = new FlowKanban
            {
                AutoSaveAfterEdits = false,
                Board = new FlowKanbanData { Columns = { todo, done } }
            };
            kanban.AssigneeAdapter = adapter;
            RuntimeTestHelpers.AttachToHost(kanban);
            await RuntimeTestHelpers.EnsureLoadedAsync(kanban);
            await kanban.RefreshAssigneesAsync();
            var loads = adapter.LoadCount;

            var external = new FlowTask { Title = "External", AssigneeId = "ext-1" };
            var ghost = new FlowTask { Title = "Ghost", Assignee = "Gone", AssigneeId = "ghost" };
            todo.Tasks.Add(external);
            todo.Tasks.Add(ghost);
            await RuntimeTestHelpers.WaitUntilAsync(
                () => external.AssigneeRoles.Contains("Guest") && adapter.ResolveCount("ghost") == 1,
                "the two new IDs are resolved");

            todo.Tasks.Remove(external);
            done.Tasks.Add(external);
            todo.Tasks.Remove(ghost);
            done.Tasks.Add(ghost);
            await RuntimeTestHelpers.WaitUntilAsync(
                () => kanban.AssigneeFilterOptions.Any(option => option.Id == "ghost"),
                "the filter is rebuilt after the moves");

            Assert.AreEqual(loads, adapter.LoadCount);
            Assert.AreEqual(1, adapter.ResolveCount("ext-1"), "A resolved ID was resolved again after a move.");
            Assert.AreEqual(1, adapter.ResolveCount("ghost"), "An unknown ID was asked for again after a move.");
            Assert.IsTrue(kanban.GetAssigneeOptions().Any(option => option.Id == "ext-1"));
        }

        [TestMethod]
        public async Task When_TheAdapterRenamesAnAssignee_NoCardEditedIsRaised()
        {
            var task = new FlowTask { Title = "Assigned", Assignee = "Old Name", AssigneeId = "sam" };
            var kanban = CreateKanban(task);
            kanban.AssigneeAdapter = new FlowKanbanAssigneeAdapter(new[] { new FlowKanbanAssignee("sam", "Sam") });
            var edits = 0;
            kanban.CardEdited += (_, _) => edits++;

            await kanban.RefreshAssigneesAsync();

            Assert.AreEqual("Sam", task.Assignee);
            Assert.AreEqual(0, edits, "A name from the adapter was reported as a card edit.");

            // A real edit is still reported, so the handler did not just stop listening.
            task.Title = "Renamed by the user";
            Assert.AreEqual(1, edits);
        }

        [TestMethod]
        public async Task When_NoAdapterIsSet_UnresolvedAssigneesAreNotMarkedUnavailable()
        {
            var task = new FlowTask { Title = "Assigned", Assignee = "External User", AssigneeId = "x-1" };
            var kanban = CreateKanban(task);

            await kanban.RefreshAssigneesAsync();
            var plain = kanban.AssigneeFilterOptions.Single();
            Assert.AreEqual("External User", plain.DisplayName);
            Assert.IsFalse(plain.IsResolved);

            kanban.AssigneeAdapter = new FlowKanbanAssigneeAdapter();
            await kanban.RefreshAssigneesAsync();
            var marked = kanban.AssigneeFilterOptions.Single();
            Assert.AreNotEqual("External User", marked.DisplayName);
            StringAssert.StartsWith(marked.DisplayName, "External User (");
        }

        [TestMethod]
        public async Task When_GetAssigneeOptionsIsCalled_ItReturnsTheAdaptersAssigneesSortedWithoutUnresolvedIds()
        {
            var task = new FlowTask { Title = "Assigned", Assignee = "Former", AssigneeId = "former" };
            var kanban = CreateKanban(task);
            kanban.AssigneeAdapter = new FlowKanbanAssigneeAdapter(new[]
            {
                new FlowKanbanAssignee("zoe", "Zoe"),
                new FlowKanbanAssignee("adam", "adam")
            });

            await kanban.RefreshAssigneesAsync();

            var options = kanban.GetAssigneeOptions();
            CollectionAssert.AreEqual(new[] { "adam", "zoe" }, options.Select(option => option.Id).ToArray());
            Assert.IsTrue(options.All(option => option.IsResolved));
            Assert.IsTrue(kanban.AssigneeFilterOptions.Any(option => option.Id == "former"),
                "The filter keeps the unresolved ID that the editor choices leave out.");
        }

        [TestMethod]
        public void When_ABoardIsLoaded_AssigneeIdsAreKeptExactlyOrDropped()
        {
            var longestId = new string('x', 200);
            var ids = new[] { " spaced id ", longestId, longestId + "x", "bad\u0007id", "plain" };
            var column = new FlowKanbanColumnData { Title = "Todo" };
            foreach (var id in ids)
                column.Tasks.Add(new FlowTask { Title = "Task", AssigneeId = id });
            var json = JsonSerializer.Serialize(
                new FlowKanbanData { Columns = { column } },
                FlowKanbanJsonContext.Default.FlowKanbanData);

            Assert.IsTrue(FlowKanbanBoardSanitizer.TryLoadFromJson(json, out var loaded));

            var loadedIds = loaded!.Columns.Single().Tasks.Select(task => task.AssigneeId).ToArray();
            CollectionAssert.AreEqual(new string?[] { " spaced id ", longestId, null, null, "plain" }, loadedIds);
        }

        [TestMethod]
        public void When_TheDemoBoardIsBuilt_EveryAssignedTaskCarriesADemoId()
        {
            var board = FlowKanban.BuildDefaultBoard(includeDemoTasks: true);
            var assigned = board.Columns.SelectMany(column => column.Tasks)
                .Where(task => !string.IsNullOrWhiteSpace(task.Assignee))
                .ToArray();

            Assert.IsTrue(assigned.Length > 0);
            foreach (var task in assigned)
                Assert.AreEqual($"demo-{task.Assignee!.ToLowerInvariant()}", task.AssigneeId, task.Title);
        }

        [TestMethod]
        public void When_TheGalleryBuildsItsSampleBoard_EveryTaskHasAnAssigneeTheAdapterKnows()
        {
            var assignees = FlowKanbanExample.CreateAssignees().ToDictionary(assignee => assignee.Id);
            var tasks = FlowKanbanExample.CreateSampleKanbanBoard().Columns.SelectMany(column => column.Tasks).ToArray();

            Assert.IsTrue(tasks.All(task => task.AssigneeId is { } id && assignees.ContainsKey(id)));
            Assert.IsNotNull(assignees["demo-sam"].AvatarSource);
            Assert.IsTrue(assignees.Values.All(assignee => assignee.Roles.Count > 0));
        }

        [TestMethod]
        public void When_TheGalleryLoadsABoardFromBefore111_LegacyDemoIdsAreMapped()
        {
            var board = new FlowKanbanData
            {
                Columns =
                {
                    new FlowKanbanColumnData
                    {
                        Title = "Todo",
                        Tasks =
                        {
                            new FlowTask { Title = "Legacy demo", AssigneeId = "local:demo-sam" },
                            new FlowTask { Title = "Legacy machine user", AssigneeId = "local:3f2a" },
                            new FlowTask { Title = "Current", AssigneeId = "demo-max" }
                        }
                    }
                }
            };

            FlowKanbanExample.MigrateLegacyDemoAssigneeIds(board);

            CollectionAssert.AreEqual(
                new[] { "demo-sam", "local:3f2a", "demo-max" },
                board.Columns.Single().Tasks.Select(task => task.AssigneeId).ToArray());
        }

        // Positive control for the next test: while a refresh runs, the host token reaches the adapter call.
        [TestMethod]
        public async Task When_TheHostTokenIsCancelledDuringARefresh_TheAdapterCallIsCancelled()
        {
            var pending = new TaskCompletionSource<IReadOnlyList<FlowKanbanAssignee>>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            var received = CancellationToken.None;
            var kanban = CreateKanban(new FlowTask { Title = "Assigned", AssigneeId = "sam" });
            kanban.AssigneeAdapter = new FlowKanbanAssigneeAdapter(token =>
            {
                received = token;
                return pending.Task.WaitAsync(token);
            });
            using var host = new CancellationTokenSource();

            var refresh = kanban.RefreshAssigneesAsync(host.Token);
            host.Cancel();

            var cancelled = false;
            try
            {
                await refresh;
            }
            catch (OperationCanceledException)
            {
                cancelled = true;
            }

            Assert.IsTrue(cancelled, "The refresh did not report the cancelled host token.");
            Assert.IsTrue(received.IsCancellationRequested, "The host token did not reach the adapter call.");
        }

        // A linked token source registers a callback on the host token until it is disposed. If a finished
        // refresh left it registered, cancelling the host token later would still cancel the token the adapter
        // received, and every refresh would add one more registration to a long-lived host token.
        [TestMethod]
        public async Task When_ARefreshWithAHostTokenHasFinished_TheHostTokenNoLongerReachesTheAdapterToken()
        {
            var received = new List<CancellationToken>();
            var kanban = CreateKanban(new FlowTask { Title = "Assigned", AssigneeId = "sam" });
            kanban.AssigneeAdapter = new FlowKanbanAssigneeAdapter(token =>
            {
                received.Add(token);
                return Task.FromResult<IReadOnlyList<FlowKanbanAssignee>>(
                    new[] { new FlowKanbanAssignee("sam", "Sam") });
            });
            using var host = new CancellationTokenSource();

            for (var i = 0; i < 3; i++)
                await kanban.RefreshAssigneesAsync(host.Token);
            host.Cancel();

            Assert.AreEqual(3, received.Count);
            Assert.IsTrue(received.All(token => token.CanBeCanceled), "The adapter did not get a linked token.");
            Assert.IsFalse(
                received.Any(token => token.IsCancellationRequested),
                "A finished refresh left its linked token source registered on the host token.");
        }

        [TestMethod]
        public void When_StateOfTheRemovedUserProvidersExists_TheFirstSettingsLoadDeletesIt()
        {
            var storage = new InMemoryStateStorage();
            var previous = StateStorageProvider.Instance;
            StateStorageProvider.Configure(storage);
            try
            {
                storage.SaveLines("FlowKanban.LocalUserId", new[] { "11111111-2222-3333-4444-555555555555" });
                storage.SaveLines("FlowKanban.IdentityLinks", new[] { "[]" });
                storage.SaveLines("kanban.admin.global", new[] { "local:11111111-2222-3333-4444-555555555555" });
                storage.SaveLines("kanban.board.keep", new[] { "{}" });

                Assert.IsTrue(new FlowKanban().TryLoadSettings(forceReload: true, out var error), error?.Message);

                Assert.AreEqual(0, storage.LoadLines("FlowKanban.LocalUserId").Count);
                Assert.AreEqual(0, storage.LoadLines("FlowKanban.IdentityLinks").Count);
                Assert.AreEqual(0, storage.LoadLines("kanban.admin.global").Count);
                Assert.AreEqual(1, storage.LoadLines("kanban.board.keep").Count, "A board entry was deleted.");
            }
            finally
            {
                StateStorageProvider.Configure(previous);
            }
        }

        [TestMethod]
        public void When_TheGalleryStarts_TheLegacyProviderCredentialsAreRemovedOnce()
        {
            var vault = new DictionarySecureStorage();
            foreach (var (resource, key) in LegacyKanbanCredentials.Entries)
                vault.SetValue(resource, key, "secret");
            vault.SetValue("Other.App", "AccessToken", "keep");
            var state = new InMemoryStateStorage();

            LegacyKanbanCredentials.RemoveOnce(vault, state);

            Assert.IsNull(vault.GetValue("Flowery.GitHub", "AccessToken"), "The GitHub token is still stored.");
            Assert.IsTrue(LegacyKanbanCredentials.Entries.All(entry => vault.GetValue(entry.Resource, entry.Key) is null));
            Assert.AreEqual("keep", vault.GetValue("Other.App", "AccessToken"), "Another app's credential was removed.");

            // Later starts do not touch the vault again.
            vault.SetValue("Flowery.GitHub", "AccessToken", "new");
            LegacyKanbanCredentials.RemoveOnce(vault, state);
            Assert.AreEqual("new", vault.GetValue("Flowery.GitHub", "AccessToken"));
        }

        [TestMethod]
        public void When_TheVaultFailsToRemoveACredential_TheNextStartTriesAgain()
        {
            var vault = new DictionarySecureStorage { FailRemovals = true };
            vault.SetValue("Flowery.GitHub", "AccessToken", "secret");
            var state = new InMemoryStateStorage();

            LegacyKanbanCredentials.RemoveOnce(vault, state);
            Assert.AreEqual("secret", vault.GetValue("Flowery.GitHub", "AccessToken"));

            vault.FailRemovals = false;
            LegacyKanbanCredentials.RemoveOnce(vault, state);
            Assert.IsNull(vault.GetValue("Flowery.GitHub", "AccessToken"),
                "A start after a silent vault failure skipped the removal.");
        }

        [TestMethod]
        public async Task When_NamesNeedCultureOrder_TheFilterAndTheEditorListPeopleAlike()
        {
            var kanban = CreateKanban(new FlowTask { Title = "Unassigned" });
            kanban.AssigneeAdapter = new FlowKanbanAssigneeAdapter(new[]
            {
                new FlowKanbanAssignee("zoe", "Zoe"),
                new FlowKanbanAssignee("aerger", "Ärger"),
                new FlowKanbanAssignee("adam", "adam")
            });

            await kanban.RefreshAssigneesAsync();

            var editorOrder = kanban.GetAssigneeOptions().Select(option => option.Id).ToArray();
            var filterOrder = kanban.AssigneeFilterOptions.Select(option => option.Id).ToArray();
            CollectionAssert.AreEqual(editorOrder, filterOrder);
            Assert.AreEqual("zoe", filterOrder[^1], "A name with an umlaut was sorted after Z.");
        }

        // The roles are the avatar's tooltip in the card template; a card without roles has no empty tooltip.
        [TestMethod]
        public async Task When_ACardShowsAnAssigneeWithRoles_TheAvatarCarriesThemAsTooltip()
        {
            var task = new FlowTask { Title = "Assigned", Assignee = "Sam", AssigneeId = "sam" };
            var kanban = CreateKanban(task);
            kanban.AssigneeAdapter = new FlowKanbanAssigneeAdapter(new[]
            {
                new FlowKanbanAssignee("sam", "Sam", roles: new[] { "Administrator", "Reviewer" })
            });
            await kanban.RefreshAssigneesAsync();
            var card = new FlowTaskCard { Task = task };
            RuntimeTestHelpers.AttachToHost(card);
            await RuntimeTestHelpers.EnsureLoadedAsync(card);

            var avatar = Descendants<Flowery.Controls.DaisyAvatar>(card)
                .Single(element => element.Name == "PART_AssigneeAvatar");
            Assert.AreEqual("Administrator, Reviewer", ToolTipService.GetToolTip(avatar));
            Assert.IsTrue(avatar.IsPlaceholder, "The initials stay visible while there is no avatar image.");

            task.AssigneeRoles = Array.Empty<string>();
            Assert.IsNull(ToolTipService.GetToolTip(avatar), "A card without roles kept a tooltip.");
        }

        private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
        {
            var count = VisualTreeHelper.GetChildrenCount(root);
            for (var i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(root, i);
                if (child is T match)
                    yield return match;

                foreach (var descendant in Descendants<T>(child))
                    yield return descendant;
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

        private sealed class DictionarySecureStorage : ISecureStorage
        {
            private readonly Dictionary<(string, string), string> _values = new();

            // Like PasswordVaultSecureStorage, a failed removal is swallowed.
            public bool FailRemovals { get; set; }

            public void SetValue(string resource, string key, string value) => _values[(resource, key)] = value;

            public string? GetValue(string resource, string key) => _values.GetValueOrDefault((resource, key));

            public void RemoveValue(string resource, string key)
            {
                if (!FailRemovals)
                    _values.Remove((resource, key));
            }
        }

        // Counts the adapter calls, so a test can prove that board changes do not reload the list.
        private sealed class CountingAdapter : IFlowKanbanAssigneeAdapter
        {
            private readonly Dictionary<string, FlowKanbanAssignee> _listed;
            private readonly Dictionary<string, FlowKanbanAssignee> _resolvable;

            public CountingAdapter(
                IEnumerable<FlowKanbanAssignee> listed,
                IEnumerable<FlowKanbanAssignee>? resolvable = null)
            {
                _listed = listed.ToDictionary(assignee => assignee.Id, StringComparer.Ordinal);
                _resolvable = (resolvable ?? Array.Empty<FlowKanbanAssignee>())
                    .ToDictionary(assignee => assignee.Id, StringComparer.Ordinal);
            }

            public int LoadCount { get; private set; }

            public Dictionary<string, int> ResolveCounts { get; } = new(StringComparer.Ordinal);

            public event EventHandler? AssigneesChanged { add { } remove { } }

            public int ResolveCount(string assigneeId) => ResolveCounts.GetValueOrDefault(assigneeId);

            public Task<IReadOnlyList<FlowKanbanAssignee>> GetAssigneesAsync(CancellationToken cancellationToken = default)
            {
                LoadCount++;
                return Task.FromResult<IReadOnlyList<FlowKanbanAssignee>>(_listed.Values.ToArray());
            }

            public Task<FlowKanbanAssignee?> ResolveAssigneeAsync(string assigneeId, CancellationToken cancellationToken = default)
            {
                ResolveCounts[assigneeId] = ResolveCount(assigneeId) + 1;
                return Task.FromResult(_listed.GetValueOrDefault(assigneeId) ?? _resolvable.GetValueOrDefault(assigneeId));
            }
        }
    }
}
