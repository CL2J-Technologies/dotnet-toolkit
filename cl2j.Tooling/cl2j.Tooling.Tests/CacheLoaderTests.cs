using System.Diagnostics;
using cl2j.Tooling;
using Microsoft.Extensions.Logging;
using Xunit;

namespace cl2j.Tooling.Tests
{
    /// <summary>
    ///     <see cref="CacheLoader"/> is what every cache in cl2j.DataStore is built on, and it was
    ///     untested. Its behaviour under failure and under contention is what those caches inherit,
    ///     so it is written down here rather than rediscovered there.
    ///
    ///     <para>
    ///     Several of these hold a load open to watch what happens around it, so they live in one
    ///     class: xUnit runs the tests of a class one at a time.
    ///     </para>
    /// </summary>
    public class CacheLoaderTests
    {
        //Long enough that the loader never refreshes during a test, short enough that a test
        //which has gone wrong fails in seconds: WaitAsync gives up after one interval.
        private static readonly TimeSpan NoRefresh = TimeSpan.FromSeconds(5);

        [Fact]
        public async Task It_loads_once_without_being_asked()
        {
            //The timer's due time is zero, so the first load starts in the constructor rather than
            //on the first read.
            var loads = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            using var loader = new CacheLoader("eager", NoRefresh, () =>
            {
                loads.TrySetResult();
                return Task.CompletedTask;
            }, new RecordingLogger());

            await loads.Task;

            Assert.True(await loader.WaitAsync());
        }

        [Fact]
        public async Task Waiting_after_it_has_loaded_returns_immediately()
        {
            using var loader = new CacheLoader("loaded", NoRefresh, () => Task.CompletedTask, new RecordingLogger());
            await loader.WaitAsync();

            var start = DateTime.UtcNow;
            Assert.True(await loader.WaitAsync());
            Assert.True(DateTime.UtcNow - start < TimeSpan.FromMilliseconds(90));
        }

        [Fact]
        public async Task A_load_that_throws_is_logged_and_leaves_it_unloaded()
        {
            //A callback that lets its exception out leaves the loader unloaded, and WaitAsync says
            //so. A callback that catches its own — as the cl2j.DataStore caches do — returns
            //normally and is indistinguishable from a success here, which is exactly why those
            //caches track their own first load rather than trusting this.
            var logger = new RecordingLogger();
            using var loader = new CacheLoader("broken", TimeSpan.FromMilliseconds(200),
                () => throw new InvalidOperationException("the source is unavailable"), logger);

            Assert.False(await loader.WaitAsync());
            Assert.Contains(logger.Snapshot(), e => e.Level == LogLevel.Error && e.Exception is not null);
        }

        [Fact]
        public async Task It_gives_up_waiting_after_one_refresh_interval()
        {
            //The bound on how long a caller can block: one interval. A loader configured to refresh
            //hourly therefore has a caller willing to block for an hour.
            var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var inside = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            using var loader = new CacheLoader("slow", TimeSpan.FromMilliseconds(150), async () =>
            {
                inside.TrySetResult();
                await release.Task;
            }, new RecordingLogger());

            await inside.Task;
            try
            {
                Assert.False(await loader.WaitAsync());
            }
            finally
            {
                //Let the load finish, so the loader disposes cleanly.
                release.SetResult();
            }
        }

        [Fact]
        public async Task It_reloads_on_the_interval()
        {
            var loads = 0;
            using var loader = new CacheLoader("repeating", TimeSpan.FromMilliseconds(100), () =>
            {
                Interlocked.Increment(ref loads);
                return Task.CompletedTask;
            }, new RecordingLogger());

            await loader.WaitAsync();
            await Task.Delay(TimeSpan.FromMilliseconds(500));

            Assert.True(Volatile.Read(ref loads) >= 3, $"expected several reloads, saw {Volatile.Read(ref loads)}");
        }

        [Fact]
        public async Task Disposing_it_stops_the_reloading()
        {
            var loads = 0;
            var loader = new CacheLoader("disposable", TimeSpan.FromMilliseconds(100), () =>
            {
                Interlocked.Increment(ref loads);
                return Task.CompletedTask;
            }, new RecordingLogger());

            await loader.WaitAsync();
            loader.Dispose();

            //Let anything already in flight finish before taking the reading.
            await Task.Delay(TimeSpan.FromMilliseconds(100));
            var afterDispose = Volatile.Read(ref loads);
            await Task.Delay(TimeSpan.FromMilliseconds(400));

            Assert.Equal(afterDispose, Volatile.Read(ref loads));
        }

        [Fact]
        public async Task One_loader_refreshing_does_not_hold_up_another()
        {
            //The semaphore used to be `private static readonly` on a class with no type parameters,
            //so there was exactly one for the whole process — held across each refresh's I/O. Every
            //cache refresh in an application was serialised against every other: ten stores on a
            //five-minute refresh queued behind each other, and one slow source stalled all of them.
            //It is one per loader now, which is the only thing it ever needed to guard. See #35.
            var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var slowIsInside = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            using var slow = new CacheLoader("slow", NoRefresh, async () =>
            {
                slowIsInside.TrySetResult();
                await release.Task;
            }, new RecordingLogger());

            await slowIsInside.Task;

            //A second loader, built while the first is still inside its load and holding its lock.
            using var unrelated = new CacheLoader("unrelated", NoRefresh, () => Task.CompletedTask, new RecordingLogger());

            Assert.True(await unrelated.WaitAsync());

            release.SetResult();
            Assert.True(await slow.WaitAsync());
        }

        [Fact]
        public async Task Waiting_ends_when_the_load_does_rather_than_on_the_next_poll()
        {
            //WaitAsync used to spin on Thread.Sleep(100), which cost two things: a thread pool
            //thread held for the whole wait, and up to a tenth of a second of latency after the
            //load had already finished. It waits on the load itself now. See #35.
            var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            using var loader = new CacheLoader("gated", NoRefresh, async () => await release.Task, new RecordingLogger());

            var waiting = loader.WaitAsync();
            var started = Stopwatch.StartNew();
            release.SetResult();

            Assert.True(await waiting);
            Assert.True(started.ElapsedMilliseconds < 60, $"waited {started.ElapsedMilliseconds}ms after the load finished");
        }

        [Fact]
        public async Task Disposing_it_while_a_load_is_in_flight_is_not_an_error()
        {
            //RefreshAsync is `async void`, so anything escaping it takes the process down rather
            //than failing a call. Disposing the loader disposes its semaphore, which a refresh
            //already queued on would otherwise walk into.
            var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var inside = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            var loader = new CacheLoader("disposed-mid-flight", TimeSpan.FromMilliseconds(50), async () =>
            {
                inside.TrySetResult();
                await release.Task;
            }, new RecordingLogger());

            await inside.Task;
            loader.Dispose();
            release.SetResult();

            //Give any refresh that was queued behind the first one a chance to reach the disposed
            //semaphore. Nothing should escape.
            await Task.Delay(TimeSpan.FromMilliseconds(200));
        }
    }
}
