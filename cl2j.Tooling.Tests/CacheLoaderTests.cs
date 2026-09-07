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
    ///     One class on purpose: the semaphore inside CacheLoader is static on a non-generic type,
    ///     so it is shared by every loader in the process, and tests that hold it must not run
    ///     beside each other. xUnit runs the tests of a class one at a time.
    ///     </para>
    /// </summary>
    public class CacheLoaderTests
    {
        //Long enough that the loader never refreshes during a test, short enough that a test which
        //has gone wrong fails in seconds instead of blocking a thread for ten minutes: WaitAsync
        //spins on Thread.Sleep for up to one interval.
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
            //And this is what the caller then sees: WaitAsync returns false. Every caller in
            //cl2j.DataStore ignores that return and serves an empty cache, so an unreadable source
            //and an empty source are indistinguishable from the outside.
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
                //Held on a process-wide semaphore, so it has to be given back before the next test.
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
        public async Task One_loader_refreshing_holds_up_every_other_loader_in_the_process()
        {
            //Characterisation, not endorsement. The semaphore is `private static readonly` on a
            //class with no type parameters, so there is exactly one for the whole process. Every
            //cache refresh in an application is therefore serialised against every other, each one
            //holding the lock for as long as its own I/O takes.
            var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var slowIsInside = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            using var slow = new CacheLoader("slow", NoRefresh, async () =>
            {
                slowIsInside.TrySetResult();
                await release.Task;
            }, new RecordingLogger());

            await slowIsInside.Task;

            var unrelatedRan = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            using var unrelated = new CacheLoader("unrelated", NoRefresh, () =>
            {
                unrelatedRan.TrySetResult();
                return Task.CompletedTask;
            }, new RecordingLogger());

            var first = await Task.WhenAny(unrelatedRan.Task, Task.Delay(TimeSpan.FromMilliseconds(400)));
            Assert.NotSame(unrelatedRan.Task, first);

            release.SetResult();

            await unrelatedRan.Task;
            Assert.True(await unrelated.WaitAsync());
        }
    }
}
