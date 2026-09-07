using cl2j.Tooling;
using Xunit;

namespace cl2j.Tooling.Tests
{
    /// <summary>
    ///     The small helpers. <see cref="TypeUtils"/> is the one that matters most of them: both
    ///     cl2j.Database's generated reader and cl2j.Scripting write its output straight into
    ///     source code they then compile, so a name it gets wrong is a compile error a long way
    ///     from here.
    /// </summary>
    public class TypeUtilsTests
    {
        [Fact]
        public void A_plain_type_is_named_with_its_namespace()
        {
            Assert.Equal("System.String", TypeUtils.GetTypeName<string>());
        }

        [Fact]
        public void A_generic_type_is_named_the_way_it_is_written_in_source()
        {
            //`1 and the rest of the reflection spelling would not compile.
            Assert.Equal("System.Collections.Generic.List<System.String>", TypeUtils.GetTypeName<List<string>>());
        }

        [Fact]
        public void A_generic_type_of_several_arguments_lists_them_in_order()
        {
            Assert.Equal(
                "System.Collections.Generic.Dictionary<System.String, System.Int32>",
                TypeUtils.GetTypeName<Dictionary<string, int>>());
        }

        [Fact]
        public void A_generic_argument_that_is_itself_generic_loses_its_namespace()
        {
            //Characterisation, not endorsement. The outer type is fully qualified and the inner one
            //is not: GetCSharpRepresentation passes addNamespace: true, but that flag is only read
            //on the non-generic branch, so a generic argument comes back as a bare name.
            //
            //It compiles today only because the callers that write this into source also add
            //System.Collections.Generic to the usings — cl2j.Database's reader does. A generic
            //argument from a namespace nobody thought to add would be a compile error inside
            //generated code, which is the least pleasant place to read one.
            Assert.Equal(
                "System.Collections.Generic.List<List<System.Int32>>",
                TypeUtils.GetTypeName<List<List<int>>>());
        }

        [Fact]
        public void A_pretty_name_drops_the_namespace_and_the_arity_marker()
        {
            Assert.Equal("String", typeof(string).GetPrettyName());
            Assert.Equal("List<String>", typeof(List<string>).GetPrettyName());
            Assert.Equal("Dictionary<String, Int32>", typeof(Dictionary<string, int>).GetPrettyName());
        }
    }

    public class DateUtilsTests
    {
        private static readonly DateTimeOffset Moment = new(2026, 9, 7, 12, 0, 0, TimeSpan.Zero);

        [Fact]
        public void A_moment_inside_both_bounds_is_in_range()
        {
            Assert.True(Moment.InRange(Moment.AddDays(-1), Moment.AddDays(1)));
        }

        [Fact]
        public void A_moment_outside_either_bound_is_not()
        {
            Assert.False(Moment.InRange(Moment.AddDays(1), Moment.AddDays(2)));
            Assert.False(Moment.InRange(Moment.AddDays(-2), Moment.AddDays(-1)));
        }

        [Fact]
        public void A_bound_that_is_absent_does_not_constrain()
        {
            Assert.True(Moment.InRange(null, null));
            Assert.True(Moment.InRange(Moment.AddDays(-1), null));
            Assert.True(Moment.InRange(null, Moment.AddDays(1)));
        }
    }

    public class EnumUtilsTests
    {
        private enum Colour
        {
            Red,
            Green,
            Blue
        }

        [Fact]
        public void The_names_of_an_enum_are_listed_in_declaration_order()
        {
            Assert.Equal(["Red", "Green", "Blue"], EnumUtils.ToList<Colour>());
        }
    }

    public class CompressionUtilsTests
    {
        [Fact]
        public void What_was_compressed_decompresses_to_itself()
        {
            const string text = "Some text, with an accent — Renée — and punctuation.";

            Assert.Equal(text, CompressionUtils.Decompress(CompressionUtils.Compress(text)));
        }

        [Fact]
        public void Compressing_a_repetitive_string_makes_it_smaller()
        {
            var text = new string('a', 10_000);

            Assert.True(CompressionUtils.Compress(text).Length < 1_000);
        }

        [Fact]
        public void An_empty_string_survives_the_round_trip()
        {
            Assert.Equal(string.Empty, CompressionUtils.Decompress(CompressionUtils.Compress(string.Empty)));
        }
    }

    public class StreamUtilsTests
    {
        [Fact]
        public void A_string_becomes_a_stream_of_its_bytes_and_back()
        {
            using var stream = "hello".ToStream();

            Assert.NotNull(stream);
            Assert.Equal("hello"u8.ToArray(), stream.ToBytes());
        }

        [Fact]
        public void A_null_stream_has_no_bytes()
        {
            Assert.Null(StreamUtils.ToBytes(null));
        }

        [Fact]
        public void Copying_moves_every_byte()
        {
            using var source = new MemoryStream([1, 2, 3, 4, 5]);
            using var destination = new MemoryStream();

            StreamUtils.CopyTo(source, destination);

            Assert.Equal([1, 2, 3, 4, 5], destination.ToArray());
        }
    }

    public class ObservableTests
    {
        private sealed class Recorder : Observers.IObserver<string>
        {
            public List<string> Seen { get; } = [];

            public Task OnChangeAsync(string t)
            {
                Seen.Add(t);
                return Task.CompletedTask;
            }
        }

        private sealed class Thrower : Observers.IObserver<string>
        {
            public int Calls { get; private set; }

            public Task OnChangeAsync(string t)
            {
                Calls++;
                throw new InvalidOperationException("this observer is broken");
            }
        }

        [Fact]
        public async Task Every_subscriber_is_told()
        {
            var observable = new Observers.Observable<string>();
            var first = new Recorder();
            var second = new Recorder();
            observable.Subscribe(first);
            observable.Subscribe(second);

            await observable.NotifyAsync("event");

            Assert.Equal("event", Assert.Single(first.Seen));
            Assert.Equal("event", Assert.Single(second.Seen));
        }

        [Fact]
        public async Task The_same_subscriber_is_only_registered_once()
        {
            var observable = new Observers.Observable<string>();
            var recorder = new Recorder();

            Assert.True(observable.Subscribe(recorder));
            Assert.False(observable.Subscribe(recorder));

            await observable.NotifyAsync("event");

            Assert.Single(recorder.Seen);
        }

        [Fact]
        public async Task Notifying_with_no_subscriber_is_not_an_error()
        {
            await new Observers.Observable<string>().NotifyAsync("into the void");
        }

        [Fact]
        public async Task A_subscriber_that_throws_makes_the_others_hear_it_twice()
        {
            //Characterisation, not endorsement. NotifyAsync catches around the whole loop and then
            //runs the whole loop again, so a subscriber that already succeeded is notified a second
            //time — and the second attempt is swallowed whole, so the failure is never reported.
            //
            //An observer with a side effect therefore performs it twice whenever any other observer
            //fails, and nothing anywhere says so.
            var observable = new Observers.Observable<string>();
            var recorder = new Recorder();
            var thrower = new Thrower();
            observable.Subscribe(recorder);
            observable.Subscribe(thrower);

            await observable.NotifyAsync("event");

            Assert.Equal(2, recorder.Seen.Count);
            Assert.Equal(2, thrower.Calls);
        }
    }
}
