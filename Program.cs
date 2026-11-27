using Avalonia.Threading;

var dispatcher = Dispatcher.UIThread;
var timeout = TimeSpan.FromSeconds(5);
using var workerReady = new ManualResetEventSlim();
using var startPost = new ManualResetEventSlim();
using var attemptingPost = new ManualResetEventSlim();
using var postReturned = new ManualResetEventSlim();

var worker = new Thread(() =>
{
    dispatcher.Post(static () => { });
    workerReady.Set();
    if (!startPost.Wait(timeout))
        return;
    attemptingPost.Set();
    dispatcher.Post(static () => { });
    postReturned.Set();
}) { IsBackground = true };
worker.Start();
if (!workerReady.Wait(timeout))
    throw new TimeoutException("The worker could not post before the test.");

var operation = dispatcher.InvokeAsync(() => 42);
var probe = operation.GetTask().ContinueWith(_ =>
{
    dispatcher.VerifyAccess();
    startPost.Set();
    if (!attemptingPost.Wait(timeout))
        throw new TimeoutException("The worker did not start the second post.");
    return postReturned.Wait(TimeSpan.FromSeconds(2));
}, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);

dispatcher.RunJobs();
var postedDuringCompletion = probe.GetAwaiter().GetResult();
if (!worker.Join(timeout) || !postReturned.IsSet)
    throw new TimeoutException("The worker did not finish after operation completion.");

Console.WriteLine($"Post returned during the completion continuation: {postedDuringCompletion}");
Console.WriteLine($"Post returned by the time the worker exited: {postReturned.IsSet}");
