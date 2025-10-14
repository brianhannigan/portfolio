using System;
using System.Threading;
using System.Threading.Tasks;

namespace SurgicalVisualization.Helpers
{
    /// <summary>
    /// Runs work on a dedicated STA thread and returns the result as a Task.
    /// Needed for creating WPF Freezables (e.g., Model3D) off the UI thread safely.
    /// </summary>
    public static class StaTask
    {
        public static Task<T> Run<T>(Func<T> func, CancellationToken cancellationToken = default)
        {
            if (func == null) throw new ArgumentNullException(nameof(func));
            var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);

            Thread thread = new Thread(() =>
            {
                try
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        tcs.TrySetCanceled(cancellationToken);
                        return;
                    }
                    var result = func();
                    tcs.TrySetResult(result);
                }
                catch (OperationCanceledException oce)
                {
                    tcs.TrySetCanceled(oce.CancellationToken);
                }
                catch (Exception ex)
                {
                    tcs.TrySetException(ex);
                }
            });

            thread.IsBackground = true;
            thread.SetApartmentState(ApartmentState.STA); // <-- critical
            thread.Start();
            return tcs.Task;
        }
    }
}
