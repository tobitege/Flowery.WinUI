namespace Flowery.WinUI.Kanban.Controls
{
    public partial class FlowKanban
    {
        private IFlowKanbanAssigneeAdapter? _trackedAssigneeAdapter;
        private CancellationTokenSource? _assigneeRefreshCancellation;
        private long _assigneeRefreshVersion;

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

        private static void OnAssigneeAdapterChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not FlowKanban kanban)
                return;

            kanban.CancelAssigneeRefresh();
            kanban.DetachAssigneeAdapter();
            if (e.NewValue is null || !kanban.IsLoaded)
            {
                // No refresh will rebuild the filter, so it keeps only the assignees of the saved tasks.
                kanban.UpdateAssigneeFilterOptions(Array.Empty<FlowTaskAssigneeOption>());
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
        /// </summary>
        public async Task RefreshAssigneesAsync(CancellationToken cancellationToken = default)
        {
            // The adapter and the version are taken together on the UI thread, so a refresh started from
            // another thread cannot pair a newer version with an adapter that has already been replaced.
            var (adapter, version) = await FlowKanbanDispatcher.InvokeAsync(DispatcherQueue, () =>
                    (AssigneeAdapter, Interlocked.Increment(ref _assigneeRefreshVersion)))
                .ConfigureAwait(false);
            var cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var previousCancellation = Interlocked.Exchange(ref _assigneeRefreshCancellation, cancellation);
            previousCancellation?.Cancel();

            try
            {
                if (adapter is null)
                {
                    await ApplyResolvedAssigneesAsync(
                            adapter,
                            version,
                            Array.Empty<FlowKanbanAssignee>(),
                            cancellation.Token)
                        .ConfigureAwait(false);
                    return;
                }

                IReadOnlyList<FlowKanbanAssignee> suppliedAssignees;
                try
                {
                    suppliedAssignees = await adapter.GetAssigneesAsync(cancellation.Token)
                        .ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
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

                cancellation.Token.ThrowIfCancellationRequested();
                if (!IsCurrentAssigneeRefresh(version))
                    return;

                var resolvedAssignees = NormalizeResolvedAssignees(suppliedAssignees);
                var unresolvedIds = await FlowKanbanDispatcher.InvokeAsync(DispatcherQueue, () =>
                        CollectUnresolvedAssigneeIds(resolvedAssignees))
                    .ConfigureAwait(false);

                foreach (var assigneeId in unresolvedIds)
                {
                    cancellation.Token.ThrowIfCancellationRequested();
                    if (!IsCurrentAssigneeRefresh(version))
                        return;

                    FlowKanbanAssignee? resolvedAssignee;
                    try
                    {
                        resolvedAssignee = await adapter.ResolveAssigneeAsync(assigneeId, cancellation.Token)
                            .ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
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
                        continue;
                    }

                    if (resolvedAssignee is not null
                        && !string.Equals(resolvedAssignee.Id, assigneeId, StringComparison.Ordinal))
                    {
                        await ReportAssigneeAdapterFailureAsync(
                                adapter,
                                version,
                                FlowKanbanAssigneeAdapterOperation.ResolveAssignee,
                                new InvalidOperationException(
                                    $"The assignee callback returned ID '{resolvedAssignee.Id}' " +
                                    $"for requested ID '{assigneeId}'."),
                                assigneeId)
                            .ConfigureAwait(false);
                        continue;
                    }

                    if (resolvedAssignee is not null)
                    {
                        resolvedAssignees.Add(resolvedAssignee);
                    }
                }

                await ApplyResolvedAssigneesAsync(
                        adapter,
                        version,
                        resolvedAssignees,
                        cancellation.Token)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
            {
                if (cancellationToken.IsCancellationRequested)
                    throw;
            }
            finally
            {
                // Not disposed: a cancel from the UI thread may still reach this source, and a linked source
                // without a timer holds nothing that needs disposing.
                Interlocked.CompareExchange(ref _assigneeRefreshCancellation, null, cancellation);
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

            foreach (var column in Board?.Columns ?? [])
            {
                foreach (var task in column.Tasks)
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
            }

            return unresolvedIds.ToArray();
        }

        private async Task ApplyResolvedAssigneesAsync(
            IFlowKanbanAssigneeAdapter? adapter,
            long version,
            IReadOnlyList<FlowKanbanAssignee> resolvedAssignees,
            CancellationToken cancellationToken)
        {
            await FlowKanbanDispatcher.InvokeAsync(DispatcherQueue, () =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!IsCurrentAssigneeRefresh(version)
                    || !ReferenceEquals(adapter, AssigneeAdapter))
                {
                    return;
                }

                ApplyResolvedAssignees(resolvedAssignees);
            }).ConfigureAwait(false);
        }

        private void ApplyResolvedAssignees(IReadOnlyList<FlowKanbanAssignee> resolvedAssignees)
        {
            var assigneesById = resolvedAssignees
                .ToDictionary(assignee => assignee.Id, StringComparer.Ordinal);

            foreach (var column in Board?.Columns ?? [])
            {
                foreach (var task in column.Tasks)
                {
                    var assigneeId = task.AssigneeId;
                    if (!string.IsNullOrWhiteSpace(assigneeId)
                        && assigneesById.TryGetValue(assigneeId, out var assignee))
                    {
                        if (!string.Equals(task.Assignee, assignee.DisplayName, StringComparison.Ordinal))
                            task.Assignee = assignee.DisplayName;
                        task.AssigneeAvatarSource = assignee.AvatarSource;
                        if (!task.AssigneeRoles.SequenceEqual(assignee.Roles, StringComparer.Ordinal))
                            task.AssigneeRoles = assignee.Roles;
                    }
                    else
                    {
                        task.AssigneeAvatarSource = null;
                        if (task.AssigneeRoles.Count > 0)
                            task.AssigneeRoles = Array.Empty<string>();
                    }
                }
            }

            var filterOptions = resolvedAssignees
                .Select(assignee => new FlowTaskAssigneeOption(assignee.Id, assignee.DisplayName))
                .ToArray();
            UpdateAssigneeFilterOptions(filterOptions);
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

        private void CancelAssigneeRefresh()
        {
            Interlocked.Increment(ref _assigneeRefreshVersion);
            Interlocked.Exchange(ref _assigneeRefreshCancellation, null)?.Cancel();
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
