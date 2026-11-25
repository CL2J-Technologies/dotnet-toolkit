using cl2j.FileStorage.Extensions;
using cl2j.Tooling;
using Newtonsoft.Json;
using Xunit;

namespace cl2j.FileStorage.Tests
{
    /// <summary>
    ///     What a <c>$type</c> property in a stored JSON file is allowed to do. Reading used to run
    ///     with <c>TypeNameHandling.Auto</c> and no binder, so anyone who could write to the storage
    ///     chose which .NET type the reader instantiated. It now runs with Auto and a binder that
    ///     refuses every type not on a list, empty by default: a stored <c>$type</c> or a
    ///     polymorphic write fails loudly instead of being followed, or silently dropped.
    /// </summary>
    public sealed class JsonTypeNameTests
    {
        public class Shape
        {
            public string? Name { get; set; }
        }

        public sealed class Circle : Shape
        {
            public int Radius { get; set; }
        }

        public sealed class Square : Shape
        {
            public int Side { get; set; }
        }

        public sealed class Holder
        {
            public Shape? Shape { get; set; }
            public List<Shape>? Shapes { get; set; }
            public object? Payload { get; set; }
        }

        public sealed class ForcedHolder
        {
            [JsonProperty(TypeNameHandling = TypeNameHandling.All)]
            public object? Payload { get; set; }
        }

        /// <summary>Stands for any type an attacker would want constructed: it records that it was.</summary>
        public sealed class Gadget
        {
            private static int created;

            public static int Created => Volatile.Read(ref created);

            public Gadget() => Interlocked.Increment(ref created);

            public string? Command { get; set; }
        }

        private static readonly string GadgetType = $"{typeof(Gadget).FullName}, {typeof(Gadget).Assembly.GetName().Name}";
        private static readonly string CircleType = $"{typeof(Circle).FullName}, {typeof(Circle).Assembly.GetName().Name}";

        private readonly InMemoryFileStorageProvider provider = new();

        private static JsonStorageOptions AllowShapes() => new() { AllowedTypes = [typeof(Circle), typeof(Square)] };

        [Fact]
        public void Writing_a_polymorphic_member_is_refused_unless_the_caller_opted_in()
        {
            //Quietly writing it without $type would lose the derived properties on the next read.
            var failure = Assert.Throws<JsonSerializationException>(() =>
                JsonExtensions.SerializeJsonObject(new Holder { Shape = new Circle { Radius = 3 } }, indented: false));

            Assert.Contains(nameof(Circle), failure.Message, StringComparison.Ordinal);
        }

        [Fact]
        public async Task Writing_a_polymorphic_member_to_a_file_is_refused_and_writes_nothing()
        {
            await Assert.ThrowsAsync<JsonSerializationException>(() =>
                provider.WriteJsonObjectAsync("holder.json", new Holder { Shape = new Circle { Radius = 3 } }));

            Assert.False(await provider.ExistsAsync("holder.json"));
        }

        [Fact]
        public void An_ordinary_object_is_written_without_a_type()
        {
            var json = JsonExtensions.SerializeJsonObject(new Holder { Shape = new Shape { Name = "s" }, Payload = "text" }, indented: false);

            Assert.DoesNotContain("$type", json, StringComparison.Ordinal);
        }

        public sealed class InterfaceHolder
        {
            public IList<string>? Names { get; set; }
            public IDictionary<string, int>? Counts { get; set; }
        }

        [Fact]
        public async Task A_member_declared_as_an_interface_collection_is_not_polymorphic()
        {
            //Characterisation: Auto writes no $type for a collection member declared as an interface,
            //so the most ordinary models neither need the list nor meet the refusal.
            var holder = new InterfaceHolder { Names = new List<string> { "a" }, Counts = new Dictionary<string, int> { ["a"] = 1 } };

            await provider.WriteJsonObjectAsync("holder.json", holder);
            var read = await provider.ReadJsonObjectAsync<InterfaceHolder>("holder.json");

            Assert.DoesNotContain("$type", await provider.ReadTextAsync("holder.json"), StringComparison.Ordinal);
            Assert.Equal("a", Assert.Single(read.Names!));
            Assert.Equal(1, read.Counts!["a"]);
        }

        [Fact]
        public async Task A_file_without_a_type_round_trips_by_default()
        {
            var holder = new Holder { Shape = new Shape { Name = "s" }, Shapes = [new Shape { Name = "a" }], Payload = "text" };

            await provider.WriteJsonObjectAsync("holder.json", holder);
            var read = await provider.ReadJsonObjectAsync<Holder>("holder.json");

            Assert.Equal("s", read.Shape!.Name);
            Assert.Equal("a", Assert.Single(read.Shapes!).Name);
            Assert.Equal("text", read.Payload);
        }

        [Fact]
        public void Writing_emits_a_type_for_a_polymorphic_member_when_the_caller_opted_in()
        {
            var json = JsonExtensions.SerializeJsonObject(new Holder { Shape = new Circle { Radius = 3 } }, indented: false, AllowShapes());

            Assert.Contains("\"$type\":\"" + CircleType + "\"", json, StringComparison.Ordinal);
        }

        [Fact]
        public async Task A_hostile_type_is_refused_by_default()
        {
            var created = Gadget.Created;
            await provider.WriteTextAsync("hostile.json", $"{{\"payload\":{{\"$type\":\"{GadgetType}\",\"command\":\"calc\"}}}}");

            await Assert.ThrowsAsync<JsonSerializationException>(() => provider.ReadJsonObjectAsync<Holder>("hostile.json"));

            Assert.Equal(created, Gadget.Created);
        }

        [Fact]
        public async Task A_hostile_type_is_refused_by_default_in_a_compressed_file()
        {
            var created = Gadget.Created;
            await provider.WriteBytesAsync("hostile.json.gz", CompressionUtils.Compress($"{{\"payload\":{{\"$type\":\"{GadgetType}\"}}}}"));

            await Assert.ThrowsAsync<JsonSerializationException>(() => provider.ReadJsonObjectAsync<Holder>("hostile.json.gz"));

            Assert.Equal(created, Gadget.Created);
        }

        [Fact]
        public async Task A_hostile_type_is_refused_when_an_allow_list_does_not_contain_it()
        {
            var created = Gadget.Created;
            await provider.WriteTextAsync("hostile.json", $"{{\"payload\":{{\"$type\":\"{GadgetType}\",\"command\":\"calc\"}}}}");

            await Assert.ThrowsAsync<JsonSerializationException>(() => provider.ReadJsonObjectAsync<Holder>("hostile.json", AllowShapes()));

            Assert.Equal(created, Gadget.Created);
        }

        [Fact]
        public async Task A_hostile_generic_type_is_refused_when_an_allow_list_does_not_contain_it()
        {
            var created = Gadget.Created;
            var generic = "System.Collections.Generic.List`1[[" + typeof(Gadget).FullName + ", " + typeof(Gadget).Assembly.GetName().Name + "]], System.Private.CoreLib";
            await provider.WriteTextAsync("hostile.json", $"{{\"payload\":{{\"$type\":\"{generic}\",\"$values\":[{{}}]}}}}");

            await Assert.ThrowsAsync<JsonSerializationException>(() => provider.ReadJsonObjectAsync<Holder>("hostile.json", AllowShapes()));

            Assert.Equal(created, Gadget.Created);
        }

        [Fact]
        public async Task A_hostile_type_is_refused_even_where_an_attribute_asks_for_type_names()
        {
            //A member marked [JsonProperty(TypeNameHandling = All)] turns type names back on for
            //itself whatever the settings say, so the allow-list has to hold there too.
            var created = Gadget.Created;
            await provider.WriteTextAsync("hostile.json", $"{{\"payload\":{{\"$type\":\"{GadgetType}\"}}}}");

            await Assert.ThrowsAsync<JsonSerializationException>(() => provider.ReadJsonObjectAsync<ForcedHolder>("hostile.json"));

            Assert.Equal(created, Gadget.Created);
        }

        [Fact]
        public async Task A_hostile_type_is_refused_when_an_empty_allow_list_is_given()
        {
            var created = Gadget.Created;
            await provider.WriteTextAsync("hostile.json", $"{{\"payload\":{{\"$type\":\"{GadgetType}\"}}}}");

            await Assert.ThrowsAsync<JsonSerializationException>(() => provider.ReadJsonObjectAsync<Holder>("hostile.json", new JsonStorageOptions()));

            Assert.Equal(created, Gadget.Created);
        }

        [Fact]
        public async Task An_allowed_polymorphic_type_round_trips()
        {
            var holder = new Holder
            {
                Shape = new Circle { Name = "c", Radius = 3 },
                Shapes = [new Square { Name = "s", Side = 4 }, new Circle { Name = "d", Radius = 5 }],
                Payload = new Square { Side = 9 }
            };

            await provider.WriteJsonObjectAsync("holder.json", holder, AllowShapes());
            var read = await provider.ReadJsonObjectAsync<Holder>("holder.json", AllowShapes());

            Assert.Equal(3, Assert.IsType<Circle>(read.Shape).Radius);
            Assert.Equal(2, read.Shapes!.Count);
            Assert.Equal(4, Assert.IsType<Square>(read.Shapes[0]).Side);
            Assert.Equal(5, Assert.IsType<Circle>(read.Shapes[1]).Radius);
            Assert.Equal(9, Assert.IsType<Square>(read.Payload).Side);
        }

        [Fact]
        public async Task An_allowed_polymorphic_type_round_trips_in_a_compressed_file()
        {
            await provider.WriteJsonObjectAsync("holder.json.gz", new Holder { Shape = new Circle { Radius = 3 } }, AllowShapes());

            var read = await provider.ReadJsonObjectAsync<Holder>("holder.json.gz", AllowShapes());

            Assert.Equal(3, Assert.IsType<Circle>(read.Shape).Radius);
        }

        [Fact]
        public async Task An_allowed_generic_type_round_trips()
        {
            var options = new JsonStorageOptions { AllowedTypes = [typeof(List<Shape>)] };

            await provider.WriteJsonObjectAsync("holder.json", new Holder { Payload = new List<Shape> { new() { Name = "a" } } }, options);
            var read = await provider.ReadJsonObjectAsync<Holder>("holder.json", options);

            Assert.Equal("a", Assert.Single(Assert.IsType<List<Shape>>(read.Payload)).Name);
        }

        [Fact]
        public async Task A_type_that_is_not_on_the_allow_list_is_refused_even_when_a_sibling_is()
        {
            await provider.WriteTextAsync("holder.json", $"{{\"shape\":{{\"$type\":\"{CircleType}\",\"radius\":3}}}}");

            await Assert.ThrowsAsync<JsonSerializationException>(() =>
                provider.ReadJsonObjectAsync<Holder>("holder.json", new JsonStorageOptions { AllowedTypes = [typeof(Square)] }));
        }

        [Fact]
        public async Task A_file_written_with_a_legitimate_derived_type_is_refused_by_default()
        {
            //The behaviour change, pinned: a $type already on disk is not followed unless the type is
            //listed. Reading it as the declared type would drop the derived properties without a word,
            //and the next write would remove them from the file.
            await provider.WriteTextAsync("holder.json", $"{{\"shape\":{{\"$type\":\"{CircleType}\",\"name\":\"c\",\"radius\":3}}}}");

            var failure = await Assert.ThrowsAsync<JsonSerializationException>(() => provider.ReadJsonObjectAsync<Holder>("holder.json"));

            Assert.Contains(nameof(Circle), failure.Message, StringComparison.Ordinal);
        }

        [Fact]
        public async Task A_type_that_cannot_be_read_back_is_not_written_when_the_caller_opted_in()
        {
            //Writing a $type the allow-list would refuse on the way back produces a file nobody can
            //read, so it is refused at write time, where the message can name the type.
            var options = new JsonStorageOptions { AllowedTypes = [typeof(Square)] };

            var failure = await Assert.ThrowsAsync<JsonSerializationException>(() =>
                provider.WriteJsonObjectAsync("holder.json", new Holder { Shape = new Circle() }, options));

            Assert.Contains(nameof(Circle), failure.Message, StringComparison.Ordinal);
            Assert.False(await provider.ExistsAsync("holder.json"));
        }

        [Fact]
        public async Task An_allowed_type_from_a_file_written_with_a_full_assembly_name_is_accepted()
        {
            //Other writers, and older versions of Json.NET, write the assembly with its version.
            var full = $"{typeof(Circle).FullName}, {typeof(Circle).Assembly.FullName}";
            await provider.WriteTextAsync("holder.json", $"{{\"shape\":{{\"$type\":\"{full}\",\"radius\":3}}}}");

            var read = await provider.ReadJsonObjectAsync<Holder>("holder.json", AllowShapes());

            Assert.Equal(3, Assert.IsType<Circle>(read.Shape).Radius);
        }
    }
}
