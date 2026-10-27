namespace Flowery.WinUI.Kanban.Controls
{
    public partial class FlowKanban
    {
        private IFlowKanbanAssigneeAdapter? _trackedAssigneeAdapter;
        private long _assigneeRefreshVersion;

        // The assignee state below is only read and written on the UI thread.
        // One cancellation source per refresh generation: a new full refresh, an adapter change and an unload
        // cancel the running refresh and every pending single-ID resolve. A plain source without a timer and
        // without linked parents holds nothing that needs disposing, so a late Cancel can never hit a disposed one.
        private CancellationTokenSource? _assigneeCancellation;
        private List<FlowKanbanAssignee> _assignees = new();
        private Dictionary<string, FlowKanbanAssignee> _assigneesById = new(StringComparer.Ordinal);
        private HashSet<string> _unresolvableAssigneeIds = new(StringComparer.Ordinal);
        private readonly HashSet<string> _pendingAssigneeIds = new(StringComparer.Ordinal);
        private bool _isApplyingAssigneeMetadata;
        private bool _isAssigneeFilterRebuildPending;

        public static readonly DependencyProperty AssigneeAdapterProperty =
            DependencyProperty.Register(
                nameof(AssigneeAdapter),
                typeof(IFlowKanbanAssigneeAdapter),
                typeof(FlowKanban),
                new PropertyMetadata(null, OnAssigneeAdapterChanged));

        /// <summary>
        /// Supplies the assignees of the board: stable IDs, display names, avatars and roles.
        /// The host application owns sign-in, external accounts and tokens.
        /// </summary>
        public IFlowKanbanAssigneeAdapter? AssigneeAdapter
        {
            get => (IFlowKanbanAssigneeAdapter?)GetValue(AssigneeAdapterProperty);
            set => SetValue(AssigneeAdapterProperty, value);
        }

        /// <summary>
        /// Reports an assignee adapter call that failed. Saved assignments stay unchanged.
        /// </summary>
        public event EventHandler<FlowKanbanAssigneeAdapterFailedEventArgs>? AssigneeAdapterFailed;

        /// <summary>
        /// Returns the assignees that the adapter supplied or resolved, sorted by name, as the choices for
        /// <see cref="FlowTaskEditorDialog.ShowAsyncWithAssignees"/>. IDs that only saved tasks carry are not included.
        /// </summary>
        public IReadOnlyList<FlowTaskAssigneeOption> GetAssigneeOptions()
        {
            return _assignees
                .Select(assignee => new FlowTaskAssigneeOption(assignee.Id, assignee.DisplayName))
                .OrderBy(option => option.DisplayName, StringComparer.CurrentCultureIgnoreCase)
                .ToArray();
        }

        private static void OnAssigneeAdapterChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not FlowKanban kanban)
                return;

            kanban.CancelAssigneeRefresh();
            kanban.DetachAssigneeAdapter();
            if (!kanban.IsLoaded)
            {
                // The next load refreshes. Until then the filter keeps only the assignees of the saved tasks.
                kanban.ReplaceAssigneeSnapshot(Array.Empty<FlowKanbanAssignee>(), Array.Empty<string>());
                kanban.UpdateAssigneeFilterOptions();
                return;
            }

            kanban.AttachAssigneeAdapter(e.NewValue as IFlowKanbanAssigneeAdapter);
            _ = ObserveAssigneeRefreshAsync(kanban.RefreshAssigneesAsync());
        }

        private void AttachAssigneeAdapter(IFlowKanbanAssigneeAdapter? adapter)
        {
            if (ReferenceEquals(_trackedAssigneeAdapter, adapter))
                return;

            DetachAssigneeAdapter();
            if (adapter is null)
                return;

            _trackedAssigneeAdapter = adapter;
            _trackedAssigneeAdapter.AssigneesChanged += OnAssigneesChanged;
        }

        private void DetachAssigneeAdapter()
        {
            if (_trackedAssigneeAdapter is null)
                return;

            _trackedAssigneeAdapter.AssigneesChanged -= OnAssigneesChanged;
            _trackedAssigneeAdapter = null;
        }

        private void OnAssigneesChanged(object? sender, EventArgs e)
        {
            var adapter = _trackedAssigneeAdapter;
            if (adapter is null)
                return;

            FlowKanbanDispatcher.RunOrPost(DispatcherQueue, () =>
            {
                if (!IsLoaded || !ReferenceEquals(_trackedAssigneeAdapter, adapter))
                    return;

                _ = ObserveAssigneeRefreshAsync(RefreshAssigneesAsync());
            });
        }

        /// <summary>
        /// Reloads the assignees from <see cref="AssigneeAdapter"/> and applies names, avatars and roles
        /// to the tasks and the assignee filter. A newer reload replaces an older unfinished one.
        /// The board reloads on load, on a board change, on an adapter change and on
        /// <see cref="IFlowKanbanAssigneeAdapter.AssigneesChanged"/>; added, moved and reassigned tasks
        /// use the last reload and only resolve IDs the adapter has not been asked about.
        /// </summary>
        public async Task RefreshAssigneesAsync(CancellationToken cancellationToken = default)
        {
            var (adapter, version, generationToken) = await FlowKanbanDispatcher
                .InvokeAsync(DispatcherQueue, BeginAssigneeRefresh)
                .ConfigureAwait(false);

            // Only this method cancels or disposes the linked source, so disposing it here cannot race.
            using var linked = cancellationToken.CanBeCanceled
                ? CancellationTokenSource.CreateLinkedTokenSource(generationToken, cancellationToken)
                : null;
            var token = linked?.Token ?? generationToken;

            try
            {
                IReadOnlyList<FlowKanbanAssignee> suppliedAssignees = Array.Empty<FlowKanbanAssignee>();
                if (adapter is not null)
                {
                    try
                    {
                        suppliedAssignees = await adapter.GetAssigneesAsync(token).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) when (token.IsCancellationRequested)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        await ReportAssigneeAdapterFailureAsync(
                                adapter,
                                version,
                                FlowKanbanAssigneeAdapterOperation.LoadAssignees,
                                ex)
                            .ConfigureAwait(false);
                        return;
                    }

                    token.ThrowIfCancellationRequested();
                }

                var resolvedAssignees = NormalizeResolvedAssignees(suppliedAssignees);
                var unresolvableIds = new List<string>();
                if (adapter is not null)
                {
                    var unresolvedIds = await FlowKanbanDispatcher.InvokeAsync(DispatcherQueue, () =>
                            CollectUnresolvedAssigneeIds(resolvedAssignees))
                        .ConfigureAwait(false);

                    foreach (var assigneeId in unresolvedIds)
                    {
                        token.ThrowIfCancellationRequested();
                        if (!IsCurrentAssigneeRefresh(version))
                            return;

                        var (resolved, assignee) = await TryResolveAssigneeAsync(adapter, version, assigneeId, token)
                            .ConfigureAwait(false);
                        if (!resolved)
                            continue;

                        if (assignee is null)
                            unresolvableIds.Add(assigneeId);
                        else
                            resolvedAssignees.Add(assignee);
                    }
                }

                await FlowKanbanDispatcher.InvokeAsync(DispatcherQueue, () =>
                {
                    token.ThrowIfCancellationRequested();
                    if (!IsCurrentAssigneeRefresh(version)
                        || !ReferenceEquals(adapter, AssigneeAdapter))
                    {
                        return;
                    }

                    ReplaceAssigneeSnapshot(resolvedAssignees, unresolvableIds);
                    ApplyAssigneeSnapshot(EnumerateBoardTasks());
                    UpdateAssigneeFilterOptions();
                }).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                if (cancellationToken.IsCancellationRequested)
                    throw;
            }
        }

        // Runs on the UI thread: the adapter, the version and the generation are taken together, so a refresh
        // started from another thread cannot pair a newer version with an adapter that was already replaced.
        private (IFlowKanbanAssigneeAdapter? Adapter, long Version, CancellationToken Token) BeginAssigneeRefresh()
        {
            var version = Interlocked.Increment(ref _assigneeRefreshVersion);
            var previous = _assigneeCancellation;
            _assigneeCancellation = new CancellationTokenSource();
            previous?.Cancel();
            _pendingAssigneeIds.Clear();
            return (AssigneeAdapter, version, _assigneeCancellation.Token);
        }

        // Resolves one ID. Returns resolved = false after a reported failure; the assignee is null when the
        // adapter does not know the ID.
        private async Task<(bool Resolved, FlowKanbanAssignee? Assignee)> TryResolveAssigneeAsync(
            IFlowKanbanAssigneeAdapter adapter,
            long version,
            string assigneeId,
            CancellationToken token)
        {
            FlowKanbanAssignee? assignee;
            try
            {
                assignee = await adapter.ResolveAssigneeAsync(assigneeId, token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                await ReportAssigneeAdapterFailureAsync(
                        adapter,
                        version,
                        FlowKanbanAssigneeAdapterOperation.ResolveAssignee,
                        ex,
                        assigneeId)
                    .ConfigureAwait(false);
                return (false, null);
            }

            if (assignee is not null && !string.Equals(assignee.Id, assigneeId, StringComparison.Ordinal))
            {
                await ReportAssigneeAdapterFailureAsync(
                        adapter,
                        version,
                        FlowKanbanAssigneeAdapterOperation.ResolveAssignee,
                        new InvalidOperationException(
                            $"The assignee callback returned ID '{assignee.Id}' for requested ID '{assigneeId}'."),
                        assigneeId)
                    .ConfigureAwait(false);
                return (false, null);
            }

            return (true, assignee);
        }

        /// <summary>
        /// Applies the last reloaded assignees to tasks that joined the board or changed their assignee.
        /// The adapter's list is not reloaded; IDs it has not been asked about are resolved one call each.
        /// </summary>
        private void ApplyKnownAssignees(IEnumerable<FlowTask> tasks)
        {
            var canResolve = AssigneeAdapter is not null && _assigneeCancellation is not null;
            List<string>? missingIds = null;

            _isApplyingAssigneeMetadata = true;
            try
            {
                foreach (var task in tasks)
                {
                    if (ApplyAssigneeMetadata(task) || !canResolve)
                        continue;

                    var assigneeId = task.AssigneeId;
                    if (string.IsNullOrWhiteSpace(assigneeId)
                        || _unresolvableAssigneeIds.Contains(assigneeId)
                        || !_pendingAssigneeIds.Add(assigneeId))
                    {
                        continue;
                    }

                    (missingIds ??= new List<string>()).Add(assigneeId);
                }
            }
            finally
            {
                _isApplyingAssigneeMetadata = false;
            }

            ScheduleAssigneeFilterRebuild();
            if (missingIds is not null && AssigneeAdapter is { } adapter && _assigneeCancellation is { } generation)
            {
                _ = ObserveAssigneeRefreshAsync(ResolveMissingAssigneesAsync(
                    adapter,
                    missingIds,
                    Volatile.Read(ref _assigneeRefreshVersion),
                    generation.Token));
            }
        }

        private async Task ResolveMissingAssigneesAsync(
            IFlowKanbanAssigneeAdapter adapter,
            IReadOnlyList<string> assigneeIds,
            long version,
            CancellationToken token)
        {
            foreach (var assigneeId in assigneeIds)
            {
                token.ThrowIfCancellationRequested();
                var (resolved, assignee) = await TryResolveAssigneeAsync(adapter, version, assigneeId, token)
                    .ConfigureAwait(false);

                await FlowKanbanDispatcher.InvokeAsync(DispatcherQueue, () =>
                {
                    if (token.IsCancellationRequested
                        || !IsCurrentAssigneeRefresh(version)
                        || !ReferenceEquals(adapter, AssigneeAdapter))
                    {
                        return;
                    }

                    _pendingAssigneeIds.Remove(assigneeId);
                    if (!resolved)
                        return;

                    if (assignee is null)
                    {
                        _unresolvableAssigneeIds.Add(assigneeId);
                        return;
                    }

                    if (_assigneesById.TryAdd(assignee.Id, assignee))
                        _assignees.Add(assignee);

                    ApplyAssigneeSnapshot(EnumerateBoardTasks()
                        .Where(task => string.Equals(task.AssigneeId, assigneeId, StringComparison.Ordinal)));
                    ScheduleAssigneeFilterRebuild();
                }).ConfigureAwait(false);
            }
        }

        // FlowKanbanAssignee is immutable and normalizes itself, so only duplicate IDs are dropped.
        private static List<FlowKanbanAssignee> NormalizeResolvedAssignees(
            IEnumerable<FlowKanbanAssignee>? assignees)
        {
            var normalized = new List<FlowKanbanAssignee>();
            var seenIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var assignee in assignees ?? Array.Empty<FlowKanbanAssignee>())
            {
                if (assignee is not null && seenIds.Add(assignee.Id))
                    normalized.Add(assignee);
            }

            return normalized;
        }

        private string[] CollectUnresolvedAssigneeIds(
            IReadOnlyList<FlowKanbanAssignee> resolvedAssignees)
        {
            var resolvedIds = resolvedAssignees
                .Select(assignee => assignee.Id)
                .ToHashSet(StringComparer.Ordinal);
            var unresolvedIds = new List<string>();
            var seenIds = new HashSet<string>(StringComparer.Ordinal);

            foreach (var task in EnumerateBoardTasks())
            {
                var assigneeId = task.AssigneeId;
                if (string.IsNullOrWhiteSpace(assigneeId)
                    || resolvedIds.Contains(assigneeId)
                    || !seenIds.Add(assigneeId))
                {
                    continue;
                }

                unresolvedIds.Add(assigneeId);
            }

            return unresolvedIds.ToArray();
        }

        private IEnumerable<FlowTask> EnumerateBoardTasks()
        {
            return Board?.Columns.SelectMany(column => column.Tasks) ?? Enumerable.Empty<FlowTask>();
        }

        private void ReplaceAssigneeSnapshot(
            IReadOnlyList<FlowKanbanAssignee> assignees,
            IEnumerable<string> unresolvableIds)
        {
            _assignees = new List<FlowKanbanAssignee>(assignees);
            _assigneesById = _assignees.ToDictionary(assignee => assignee.Id, StringComparer.Ordinal);
            _unresolvableAssigneeIds = new HashSet<string>(unresolvableIds, StringComparer.Ordinal);
        }

        // Names, avatars and roles from the adapter are display data, not an edit by the user, so the
        // task changes raise no CardEdited and start no auto-save (see OnTaskPropertyChanged).
        private void ApplyAssigneeSnapshot(IEnumerable<FlowTask> tasks)
        {
            _isApplyingAssigneeMetadata = true;
            try
            {
                foreach (var task in tasks)
                    ApplyAssigneeMetadata(task);
            }
            finally
            {
                _isApplyingAssigneeMetadata = false;
            }
        }

        // Returns true when the task's assignee is in the snapshot.
        private bool ApplyAssigneeMetadata(FlowTask task)
        {
            var assigneeId = task.AssigneeId;
            if (!string.IsNullOrWhiteSpace(assigneeId)
                && _assigneesById.TryGetValue(assigneeId, out var assignee))
            {
                if (!string.Equals(task.Assignee, assignee.DisplayName, StringComparison.Ordinal))
                    task.Assignee = assignee.DisplayName;
                task.AssigneeAvatarSource = assignee.AvatarSource;
                if (!task.AssigneeRoles.SequenceEqual(assignee.Roles, StringComparer.Ordinal))
                    task.AssigneeRoles = assignee.Roles;
                return true;
            }

            task.AssigneeAvatarSource = null;
            if (task.AssigneeRoles.Count > 0)
                task.AssigneeRoles = Array.Empty<string>();
            return false;
        }

        // Coalesces filter rebuilds: a card move (remove and add) or a batch of new tasks rebuilds once.
        private void ScheduleAssigneeFilterRebuild()
        {
            if (_isAssigneeFilterRebuildPending)
                return;

            if (DispatcherQueue is not { } dispatcher)
            {
                UpdateAssigneeFilterOptions();
                return;
            }

            _isAssigneeFilterRebuildPending = true;
            if (!dispatcher.TryEnqueue(() =>
                {
                    _isAssigneeFilterRebuildPending = false;
                    UpdateAssigneeFilterOptions();
                }))
            {
                _isAssigneeFilterRebuildPending = false;
            }
        }

        private async Task ReportAssigneeAdapterFailureAsync(
            IFlowKanbanAssigneeAdapter adapter,
            long version,
            FlowKanbanAssigneeAdapterOperation operation,
            Exception exception,
            string? assigneeId = null)
        {
            await FlowKanbanDispatcher.InvokeAsync(DispatcherQueue, () =>
            {
                if (!IsCurrentAssigneeRefresh(version)
                    || !ReferenceEquals(adapter, AssigneeAdapter))
                {
                    return;
                }

                AssigneeAdapterFailed?.Invoke(
                    this,
                    new FlowKanbanAssigneeAdapterFailedEventArgs(operation, exception, assigneeId));
            }).ConfigureAwait(false);
        }

        private bool IsCurrentAssigneeRefresh(long version)
        {
            return version == Volatile.Read(ref _assigneeRefreshVersion);
        }

        // Runs on the UI thread (adapter change, unload).
        private void CancelAssigneeRefresh()
        {
            Interlocked.Increment(ref _assigneeRefreshVersion);
            var previous = _assigneeCancellation;
            _assigneeCancellation = null;
            previous?.Cancel();
            _pendingAssigneeIds.Clear();
        }

        private static async Task ObserveAssigneeRefreshAsync(Task refreshTask)
        {
            try
            {
                await refreshTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
            catch
            {
                // Adapter failures are reported through AssigneeAdapterFailed.
            }
        }
    }
}
