
namespace Windows.Foundation
{
    using global::System;
    using global::System.Globalization;

    [StructLayout(LayoutKind.Sequential)]
    public struct Point : IFormattable
    {
        float _x;
        float _y;

        public Point(double x, double y)
        {
            _x = (float)x;
            _y = (float)y;
        }

        public double X
        {
            get { return _x; }
            set { _x = (float)value; }
        }

        public double Y
        {
            get { return _y; }
            set { _y = (float)value; }
        }

        public override string ToString()
        {
            return ConvertToString(null, null);
        }

        public string ToString(IFormatProvider provider)
        {
            return ConvertToString(null, provider);
        }

        string IFormattable.ToString(string format, IFormatProvider provider)
        {
            return ConvertToString(format, provider);
        }

        private string ConvertToString(string format, IFormatProvider provider)
        {
            char separator = GetNumericListSeparator(provider);
            return string.Format(provider, "{1:" + format + "}{0}{2:" + format + "}", separator, _x, _y);
        }

        static char GetNumericListSeparator(IFormatProvider provider)
        {
            // If the decimal separator is a comma use ';'
            char numericSeparator = ',';
            var numberFormat = NumberFormatInfo.GetInstance(provider);
            if ((numberFormat.NumberDecimalSeparator.Length > 0) && (numberFormat.NumberDecimalSeparator[0] == numericSeparator))
            {
                numericSeparator = ';';
            }

            return numericSeparator;
        }

        public static bool operator==(Point point1, Point point2)
        {
            return point1.X == point2.X && point1.Y == point2.Y;
        }

        public static bool operator!=(Point point1, Point point2)
        {
            return !(point1 == point2);
        }

        public override bool Equals(object o)
        {
            return o is Point && this == (Point)o;
        }

        public bool Equals(Point value)
        {
            return (this == value);
        }

        public override int GetHashCode()
        {
            return X.GetHashCode() ^ Y.GetHashCode();
        }
    }
}

namespace ABI.Windows.Foundation
{
    public static class Point
    {
        public static string GetGuidSignature()
        {
            return "struct(Windows.Foundation.Point;f4;f4)";
        }
    }
}

namespace ABI.System
{
    [StructLayout(LayoutKind.Sequential)]
    public struct TimeSpan
    {
        // NOTE: both 'Windows.Foundation.TimeSpan.Duration' and 'System.TimeSpan.Ticks' are in units of 100ns
        public long Duration;

        public struct Marshaler
        {
            public TimeSpan __abi;
        }

        public static Marshaler CreateMarshaler(global::System.TimeSpan value)
        {
            return new Marshaler{ __abi = new TimeSpan{ Duration = value.Ticks } };
        }

        public static TimeSpan GetAbi(Marshaler m) => m.__abi;

        public static global::System.TimeSpan FromAbi(TimeSpan value)
        {
            return global::System.TimeSpan.FromTicks(value.Duration);
        }

        public static unsafe void CopyAbi(Marshaler arg, IntPtr dest) =>
            *(TimeSpan*)dest.ToPointer() = GetAbi(arg);

        public static TimeSpan FromManaged(global::System.TimeSpan value)
        {
            return new TimeSpan { Duration = value.Ticks };
        }

        public static unsafe void CopyManaged(global::System.TimeSpan arg, IntPtr dest) =>
            *(TimeSpan*)dest.ToPointer() = FromManaged(arg);

        public static void DisposeMarshaler(Marshaler m) {}
        public static void DisposeAbi(TimeSpan abi) {}

        public static string GetGuidSignature()
        {
            return "struct(Windows.Foundation.TimeSpan;i8)";
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct DateTimeOffset
    {
        // NOTE: 'Windows.Foundation.DateTime.UniversalTime' is a FILETIME value (relative to 01/01/1601), however
        // 'System.DateTimeOffset.Ticks' is relative to 01/01/0001
        public long UniversalTime;

        public struct Marshaler
        {
            public DateTimeOffset __abi;
        }

        public static Marshaler CreateMarshaler(global::System.DateTimeOffset value)
        {
            return new Marshaler{ __abi = new DateTimeOffset{ UniversalTime = value.ToFileTime() } };
        }

        public static DateTimeOffset GetAbi(Marshaler m) => m.__abi;

        public static global::System.DateTimeOffset FromAbi(DateTimeOffset value)
        {
            return global::System.DateTimeOffset.FromFileTime(value.UniversalTime);
        }

        public static unsafe void CopyAbi(Marshaler arg, IntPtr dest) =>
            *(DateTimeOffset*)dest.ToPointer() = GetAbi(arg);

        public static DateTimeOffset FromManaged(global::System.DateTimeOffset value)
        {
            return new DateTimeOffset { UniversalTime = value.ToFileTime() };
        }

        public static unsafe void CopyManaged(global::System.DateTimeOffset arg, IntPtr dest) =>
            *(DateTimeOffset*)dest.ToPointer() = FromManaged(arg);

        public static void DisposeMarshaler(Marshaler m) {}
        public static void DisposeAbi(DateTimeOffset abi) {}

        public static string GetGuidSignature()
        {
            return "struct(Windows.Foundation.DateTime;i8)";
        }
    }
}

namespace System
{
    using global::System.Diagnostics;
    using global::System.Runtime.CompilerServices;
    using global::System.Runtime.InteropServices;
    using global::System.Threading;
    using global::System.Threading.Tasks;
    using global::Windows.Foundation;

    public static class WindowsRuntimeSystemExtensions
    {
        public static Task AsTask(this IAsyncAction source, CancellationToken cancellationToken)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            // TODO: Handle the scenario where the 'IAsyncAction' is actually a task (i.e. originated from native code
            // but projected into an IAsyncAction)

            switch (source.Status)
            {
                case AsyncStatus.Completed:
                    return Task.CompletedTask;

                case AsyncStatus.Error:
                    return Task.FromException(Marshal.GetExceptionForHR(source.ErrorCode.Value)); // TODO: Restricted error info

                case AsyncStatus.Canceled:
                    return Task.FromCanceled(cancellationToken.IsCancellationRequested ? cancellationToken : new CancellationToken(true));
            }

            var bridge = new AsyncInfoToTaskBridge<VoidValueTypeParameter, VoidValueTypeParameter>(cancellationToken);
            source.Completed = new AsyncActionCompletedHandler(bridge.CompleteFromAsyncAction);
            bridge.RegisterForCancellation(source);
            return bridge.Task;
        }

        public static Task AsTask(this IAsyncAction source)
        {
            return AsTask(source, CancellationToken.None);
        }

        public static TaskAwaiter GetAwaiter(this IAsyncAction source)
        {
            return AsTask(source).GetAwaiter();
        }

        public static Task<TResult> AsTask<TResult>(this IAsyncOperation<TResult> source, CancellationToken cancellationToken)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            // TODO: Handle the scenario where the 'IAsyncOperation' is actually a task (i.e. originated from native code
            // but projected into an IAsyncOperation)

            switch (source.Status)
            {
                case AsyncStatus.Completed:
                    return Task.FromResult(source.GetResults());

                case AsyncStatus.Error:
                    return Task.FromException<TResult>(Marshal.GetExceptionForHR(source.ErrorCode.Value)); // TODO: Restricted error info

                case AsyncStatus.Canceled:
                    return Task.FromCanceled<TResult>(cancellationToken.IsCancellationRequested ? cancellationToken : new CancellationToken(true));
            }

            var bridge = new AsyncInfoToTaskBridge<TResult, VoidValueTypeParameter>(cancellationToken);
            source.Completed = new AsyncOperationCompletedHandler<TResult>(bridge.CompleteFromAsyncOperation);
            bridge.RegisterForCancellation(source);
            return bridge.Task;
        }

        public static Task<TResult> AsTask<TResult>(this IAsyncOperation<TResult> source)
        {
            return AsTask(source, CancellationToken.None);
        }

        public static TaskAwaiter<TResult> GetAwaiter<TResult>(this IAsyncOperation<TResult> source)
        {
            return AsTask(source).GetAwaiter();
        }

        public static Task AsTask<TProgress>(this IAsyncActionWithProgress<TProgress> source, CancellationToken cancellationToken, IProgress<TProgress> progress)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            // TODO: Handle the scenario where the 'IAsyncActionWithProgress' is actually a task (i.e. originated from native code
            // but projected into an IAsyncActionWithProgress)

            switch (source.Status)
            {
                case AsyncStatus.Completed:
                    return Task.CompletedTask;

                case AsyncStatus.Error:
                    return Task.FromException(Marshal.GetExceptionForHR(source.ErrorCode.Value)); // TODO: Restricted error info

                case AsyncStatus.Canceled:
                    return Task.FromCanceled(cancellationToken.IsCancellationRequested ? cancellationToken : new CancellationToken(true));
            }

            if (progress != null)
            {
                SetProgress(source, progress);
            }

            var bridge = new AsyncInfoToTaskBridge<VoidValueTypeParameter, TProgress>(cancellationToken);
            source.Completed = new AsyncActionWithProgressCompletedHandler<TProgress>(bridge.CompleteFromAsyncActionWithProgress);
            bridge.RegisterForCancellation(source);
            return bridge.Task;
        }

        private static void SetProgress<TProgress>(IAsyncActionWithProgress<TProgress> source, IProgress<TProgress> sink)
        {
            // This is separated out into a separate method so that we only pay the costs of compiler-generated closure if progress is non-null.
            source.Progress = new AsyncActionProgressHandler<TProgress>((_, info) => sink.Report(info));
        }

        public static Task AsTask<TProgress>(this IAsyncActionWithProgress<TProgress> source)
        {
            return AsTask(source, CancellationToken.None, null);
        }

        public static Task AsTask<TProgress>(this IAsyncActionWithProgress<TProgress> source, CancellationToken cancellationToken)
        {
            return AsTask(source, cancellationToken, null);
        }

        public static Task AsTask<TProgress>(this IAsyncActionWithProgress<TProgress> source, IProgress<TProgress> progress)
        {
            return AsTask(source, CancellationToken.None, progress);
        }

        public static TaskAwaiter GetAwaiter<TProgress>(this IAsyncActionWithProgress<TProgress> source)
        {
            return AsTask(source).GetAwaiter();
        }

        public static Task<TResult> AsTask<TResult, TProgress>(this IAsyncOperationWithProgress<TResult, TProgress> source, CancellationToken cancellationToken, IProgress<TProgress> progress)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            // TODO: Handle the scenario where the 'IAsyncOperationWithProgress' is actually a task (i.e. originated from native code
            // but projected into an IAsyncOperationWithProgress)

            switch (source.Status)
            {
                case AsyncStatus.Completed:
                    return Task.FromResult(source.GetResults());

                case AsyncStatus.Error:
                    return Task.FromException<TResult>(Marshal.GetExceptionForHR(source.ErrorCode.Value)); // TODO: Restricted error info

                case AsyncStatus.Canceled:
                    return Task.FromCanceled<TResult>(cancellationToken.IsCancellationRequested ? cancellationToken : new CancellationToken(true));
            }

            if (progress != null)
            {
                SetProgress(source, progress);
            }

            var bridge = new AsyncInfoToTaskBridge<TResult, TProgress>(cancellationToken);
            source.Completed = new AsyncOperationWithProgressCompletedHandler<TResult, TProgress>(bridge.CompleteFromAsyncOperationWithProgress);
            bridge.RegisterForCancellation(source);
            return bridge.Task;
        }

        private static void SetProgress<TResult, TProgress>(IAsyncOperationWithProgress<TResult, TProgress> source, IProgress<TProgress> sink)
        {
            // This is separated out into a separate method so that we only pay the costs of compiler-generated closure if progress is non-null.
            source.Progress = new AsyncOperationProgressHandler<TResult, TProgress>((_, info) => sink.Report(info));
        }

        public static Task<TResult> AsTask<TResult, TProgress>(this IAsyncOperationWithProgress<TResult, TProgress> source)
        {
            return AsTask(source, CancellationToken.None, null);
        }


        public static Task<TResult> AsTask<TResult, TProgress>(this IAsyncOperationWithProgress<TResult, TProgress> source, CancellationToken cancellationToken)
        {
            return AsTask(source, cancellationToken, null);
        }

        public static Task<TResult> AsTask<TResult, TProgress>(this IAsyncOperationWithProgress<TResult, TProgress> source, IProgress<TProgress> progress)
        {
            return AsTask(source, CancellationToken.None, progress);
        }

        public static TaskAwaiter<TResult> GetAwaiter<TResult, TProgress>(this IAsyncOperationWithProgress<TResult, TProgress> source)
        {
            return AsTask(source).GetAwaiter();
        }
    }

    // Marker type since generic parameters cannot be 'void'
    struct VoidValueTypeParameter { }

    sealed class AsyncInfoToTaskBridge<TResult, TProgress> : TaskCompletionSource<TResult>
    {
        private readonly CancellationToken _ct;
        private CancellationTokenRegistration _ctr;
        private bool _completing;

        internal AsyncInfoToTaskBridge(CancellationToken cancellationToken)
        {
            // TODO: AsyncCausality?
            _ct = cancellationToken;
        }

        internal void RegisterForCancellation(IAsyncInfo asyncInfo)
        {
            Debug.Assert(asyncInfo != null);

            try
            {
                if (_ct.CanBeCanceled && !_completing)
                {
                    var ctr = _ct.Register(ai => ((IAsyncInfo)ai).Cancel(), asyncInfo);
                    bool disposeOfCtr = false;
                    lock (this)
                    {
                        if (_completing)
                        {
                            disposeOfCtr = true;
                        }
                        else
                        {
                            _ctr = ctr;
                        }
                    }

                    if (disposeOfCtr)
                    {
                        ctr.Dispose();
                    }
                }
            }
            catch (Exception ex)
            {
                if (!base.Task.IsFaulted)
                {
                    Debug.Fail($"Expected base task to already be faulted but found it in state {base.Task.Status}");
                    base.TrySetException(ex);
                }
            }
        }

        internal void CompleteFromAsyncAction(IAsyncAction asyncInfo, AsyncStatus asyncStatus)
        {
            Complete(asyncInfo, null, asyncStatus);
        }

        internal void CompleteFromAsyncActionWithProgress(IAsyncActionWithProgress<TProgress> asyncInfo, AsyncStatus asyncStatus)
        {
            Complete(asyncInfo, null, asyncStatus);
        }

        internal void CompleteFromAsyncOperation(IAsyncOperation<TResult> asyncInfo, AsyncStatus asyncStatus)
        {
            Complete(asyncInfo, ai => ((IAsyncOperation<TResult>)ai).GetResults(), asyncStatus);
        }

        internal void CompleteFromAsyncOperationWithProgress(IAsyncOperationWithProgress<TResult, TProgress> asyncInfo, AsyncStatus asyncStatus)
        {
            Complete(asyncInfo, ai => ((IAsyncOperationWithProgress<TResult, TProgress>)ai).GetResults(), asyncStatus);
        }

        private void Complete(IAsyncInfo asyncInfo, Func<IAsyncInfo, TResult> getResultsFunction, AsyncStatus asyncStatus)
        {
            if (asyncInfo == null)
            {
               throw new ArgumentNullException(nameof(asyncInfo));
            }

            // TODO: AsyncCausality?

            try
            {
                Debug.Assert(asyncInfo.Status == asyncStatus, "asyncInfo.Status does not match asyncStatus; are we dealing with a faulty IAsyncInfo implementation?");
                if (Task.IsCompleted)
                {
                    Debug.Fail("Expected the task to not yet be completed.");
                    throw new InvalidOperationException("The asynchronous operation could not be completed.");
                }

                // Clean up our registration with the cancellation token, noting that we're now in the process of cleaning up.
                CancellationTokenRegistration ctr;
                lock (this)
                {
                    _completing = true;
                    ctr = _ctr;
                    _ctr = default;
                }
                ctr.Dispose();

                try
                {
                    if (asyncStatus != AsyncStatus.Completed && asyncStatus != AsyncStatus.Canceled && asyncStatus != AsyncStatus.Error)
                    {
                        Debug.Fail("The async operation should be in a terminal state.");
                        throw new InvalidOperationException("The asynchronous operation could not be completed.");
                    }

                    TResult result = default(TResult);
                    Exception error = null;
                    if (asyncStatus == AsyncStatus.Error)
                    {
                        var hr = asyncInfo.ErrorCode;

                        // Defend against a faulty IAsyncInfo implementation
                        if (hr.Value >= 0)
                        {
                            Debug.Fail("IAsyncInfo.Status == Error, but ErrorCode returns a null Exception (implying S_OK).");
                            error = new InvalidOperationException("The asynchronous operation could not be completed.");
                        }
                        else
                        {
                            error = Marshal.GetExceptionForHR(hr.Value); // TODO: Restricted error info
                        }
                    }
                    else if (asyncStatus == AsyncStatus.Completed && getResultsFunction != null)
                    {
                        try
                        {
                            result = getResultsFunction(asyncInfo);
                        }
                        catch (Exception resultsEx)
                        {
                            // According to the WinRT team, this can happen in some egde cases, such as marshalling errors in GetResults.
                            error = resultsEx;
                            asyncStatus = AsyncStatus.Error;
                        }
                    }

                    // Complete the task based on the previously retrieved results:
                    bool success = false;
                    switch (asyncStatus)
                    {
                        case AsyncStatus.Completed:
                            // TODO: AsyncCausality?
                            success = base.TrySetResult(result);
                            break;

                        case AsyncStatus.Error:
                            Debug.Assert(error != null, "The error should have been retrieved previously.");
                            success = base.TrySetException(error);
                            break;

                        case AsyncStatus.Canceled:
                            success = base.TrySetCanceled(_ct.IsCancellationRequested ? _ct : new CancellationToken(true));
                            break;
                    }

                    Debug.Assert(success, "Expected the outcome to be successfully transfered to the task.");
                }
                catch (Exception exc)
                {
                    Debug.Fail($"Unexpected exception in Complete: {exc}");

                    // TODO: AsyncCausality

                    if (!base.TrySetException(exc))
                    {
                        Debug.Fail("The task was already completed and thus the exception couldn't be stored.");
                        throw;
                    }
                }
            }
            finally
            {
                // TODO:
                // We may be called on an STA thread which we don't own, so make sure that the RCW is released right
                // away. Otherwise, if we leave it up to the finalizer, the apartment may already be gone.
                // if (Marshal.IsComObject(asyncInfo))
                //     Marshal.ReleaseComObject(asyncInfo);
            }
        }
    }
}