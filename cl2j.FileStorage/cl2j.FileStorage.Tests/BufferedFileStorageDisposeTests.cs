using cl2j.FileStorage.Extensions;
using Xunit;

namespace cl2j.FileStorage.Tests
{
    /// <summary>
    ///     <see cref="BufferedFileStorage"/> flushes from a timer and from <c>Dispose</c>. A flush
    ///     takes the buffer under its lock and writes it after releasing that lock, so a second
    ///     flush that starts during the write finds an empty buffer and returns at once.
    ///
    ///     <para>
    ///     When that second flush is the one in <c>Dispose</c>, the instance reports itself
    ///     disposed while the last lines are still on their way to the file: a logger shutting down
    ///     with the process loses them.
    ///     </para>
    /// </summary>
    public sealed class BufferedFileStorageDisposeTests
    {
        private static readonly TimeSpan Deadline = TimeSpan.FromSeconds(10);

        /// <summary>Holds the first append until the test releases it.</summary>
        private sealed class GatedProvider : InMemoryFileStorageProvider
        {
            private readonly TaskCompletionSource entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
            private readonly TaskCompletionSource released = new(TaskCreationOptions.RunContinuationsAsynchronously);
            private int appends;

            public Task Entered => entered.Task;

            public void Release() => released.TrySetResult();

            public override async Task AppendAsync(string name, Stream stream)
            {
                if (Interlocked.Increment(ref appends) == 1)
                {
                    entered.TrySetResult();
                    await released.Task;
                }

                await base.AppendAsync(name, stream);
            }
        }

        [Fact]
        public async Task Dispose_waits_for_a_flush_the_timer_has_already_started()
        {
            var provider = new GatedProvider();
            var storage = new BufferedFileStorage(provider, "log_{0:yyyyMMdd}_{1:00}.log", 1024 * 1024, TimeSpan.FromMilliseconds(20), clearFile: false);

            await storage.AppendAsync("last line");
            await provider.Entered.WaitAsync(Deadline);

            // The timer has taken the buffer and is held inside the write.
            var dispose = Task.Run(storage.Dispose);
            var returnedEarly = await Task.WhenAny(dispose, Task.Delay(TimeSpan.FromMilliseconds(500))) == dispose;

            provider.Release();
            await dispose.WaitAsync(Deadline);

            Assert.False(returnedEarly, "Dispose returned while the timer was still writing the buffer.");
            Assert.Equal("last line", provider.Text(storage.CurrentFileName));
        }
    }
}
