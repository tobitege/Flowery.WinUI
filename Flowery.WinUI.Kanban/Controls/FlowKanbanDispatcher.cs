using Microsoft.UI.Dispatching;

namespace Flowery.WinUI.Kanban.Controls;

/// <summary>
/// Runs work on a control's dispatcher queue from any thread.
/// </summary>
internal static class FlowKanbanDispatcher
{
    internal static void RunOrPost(DispatcherQueue? dispatcher, Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        if (dispatcher is null || dispatcher.HasThreadAccess)
        {
            action();
            return;
        }

        if (!dispatcher.TryEnqueue(() => action()))
        {
            action();
        }
    }

    internal static Task InvokeAsync(DispatcherQueue? dispatcher, Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        return InvokeAsync(dispatcher, () =>
        {
            action();
            return true;
        });
    }

    internal static Task<T> InvokeAsync<T>(DispatcherQueue? dispatcher, Func<T> function)
    {
        ArgumentNullException.ThrowIfNull(function);
        if (dispatcher is null || dispatcher.HasThreadAccess)
            return Task.FromResult(function());

        var completion = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        var enqueued = dispatcher.TryEnqueue(() =>
        {
            try
            {
                completion.TrySetResult(function());
            }
            catch (Exception ex)
            {
                completion.TrySetException(ex);
            }
        });

        if (!enqueued)
        {
            completion.TrySetException(new InvalidOperationException("The dispatcher queue is shutting down."));
        }

        return completion.Task;
    }
}
