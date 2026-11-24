using System.Text;
using cl2j.FileStorage.Extensions;
using Newtonsoft.Json;
using Xunit;

namespace cl2j.FileStorage.Tests
{
    public sealed class JsonExtensionsTests
    {
        public enum Color
        {
            Red,
            Green
        }

        public class Item
        {
            public string? Name { get; set; }
            public int Count { get; set; }
            public Color Color { get; set; }
            public Dictionary<string, int>? Scores { get; set; }
        }

        private readonly InMemoryFileStorageProvider provider = new();

        [Fact]
        public async Task A_written_object_is_read_back_unchanged()
        {
            var item = new Item { Name = "pen", Count = 3, Color = Color.Green, Scores = new() { ["Alice"] = 5 } };

            await provider.WriteJsonObjectAsync("item.json", item);
            var read = await provider.ReadJsonObjectAsync<Item>("item.json");

            Assert.Equal("pen", read.Name);
            Assert.Equal(3, read.Count);
            Assert.Equal(Color.Green, read.Color);
            Assert.Equal(5, read.Scores!["Alice"]);
        }

        [Fact]
        public void Properties_are_camel_cased_but_dictionary_keys_are_not()
        {
            var json = JsonExtensions.SerializeJsonObject(new Item { Name = "pen", Scores = new() { ["Alice"] = 5 } }, indented: false);

            Assert.Equal("{\"name\":\"pen\",\"scores\":{\"Alice\":5}}", json);
        }

        [Fact]
        public void Enums_are_written_as_strings()
        {
            var json = JsonExtensions.SerializeJsonObject(new Item { Color = Color.Green }, indented: false);

            Assert.Contains("\"color\":\"Green\"", json);
        }

        [Fact]
        public void Nulls_and_default_values_are_left_out()
        {
            var json = JsonExtensions.SerializeJsonObject(new Item(), indented: false);

            Assert.Equal("{}", json);
        }

        [Fact]
        public void Indented_output_spans_several_lines_and_compact_output_does_not()
        {
            var item = new Item { Name = "pen" };

            Assert.Contains('\n', JsonExtensions.SerializeJsonObject(item, indented: true));
            Assert.DoesNotContain('\n', JsonExtensions.SerializeJsonObject(item, indented: false));
        }

        [Fact]
        public async Task WriteJsonObjectAsync_indents_when_asked()
        {
            await provider.WriteJsonObjectAsync("item.json", new Item { Name = "pen" }, indented: true);

            Assert.Contains('\n', await provider.ReadTextAsync("item.json"));
        }

        [Fact]
        public void Dates_are_written_in_utc()
        {
            var json = JsonExtensions.SerializeJsonObject(new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc), indented: false);

            Assert.Equal("\"2026-01-02T03:04:05Z\"", json);
        }

        [Fact]
        public async Task A_missing_file_reads_as_a_new_instance()
        {
            var read = await provider.ReadJsonObjectAsync<Item>("missing.json");

            Assert.Equal(0, read.Count);
            Assert.Null(read.Name);
        }

        [Fact]
        public async Task A_blank_file_reads_as_a_new_instance()
        {
            await provider.WriteTextAsync("blank.json", "  \n ");

            var read = await provider.ReadJsonObjectAsync<Item>("blank.json");

            Assert.Null(read.Name);
        }

        [Fact]
        public async Task A_missing_compressed_file_reads_as_a_new_instance()
        {
            var read = await provider.ReadJsonObjectAsync<Item>("missing.json.gz");

            Assert.Null(read.Name);
        }

        [Fact]
        public async Task A_utf8_byte_order_mark_is_ignored()
        {
            var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes("{\"name\":\"pen\"}")).ToArray();
            await provider.WriteBytesAsync("bom.json", bytes);

            var read = await provider.ReadJsonObjectAsync<Item>("bom.json");

            Assert.Equal("pen", read.Name);
        }

        [Fact]
        public async Task The_encoding_is_used_to_write_and_read_the_text()
        {
            await provider.WriteJsonObjectAsync("latin.json", new Item { Name = "café" }, encoding: Encoding.Latin1);

            Assert.Equal(Encoding.Latin1.GetBytes("{\"name\":\"café\"}"), provider.Files["latin.json"]);
            Assert.Equal("café", (await provider.ReadJsonObjectAsync<Item>("latin.json", Encoding.Latin1)).Name);
        }

        [Fact]
        public async Task A_json_null_cannot_be_deserialized()
        {
            await provider.WriteTextAsync("null.json", "null");

            await Assert.ThrowsAsync<JsonException>(() => provider.ReadJsonObjectAsync<Item>("null.json"));
        }

        [Fact]
        public async Task A_gz_file_is_stored_compressed_and_read_back_decompressed()
        {
            var item = new Item { Name = "pen", Count = 3 };

            await provider.WriteJsonObjectAsync("item.json.gz", item);

            var stored = provider.Files["item.json.gz"];
            Assert.Equal(new byte[] { 0x1f, 0x8b }, stored[..2]);
            var read = await provider.ReadJsonObjectAsync<Item>("item.json.gz");
            Assert.Equal("pen", read.Name);
            Assert.Equal(3, read.Count);
        }
    }
}
