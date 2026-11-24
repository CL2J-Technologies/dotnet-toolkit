using Xunit;

namespace cl2j.Tooling.Tests
{
    public class SerializationUtilsTests
    {
        private sealed class Payload
        {
            public int Count { get; set; }
            public string? Name { get; set; }
        }

        [Fact]
        public void Data_is_deserialized_into_the_requested_type()
        {
            var payload = SerializationUtils.DeserializeData<Payload>("{\"Count\":3,\"Name\":\"abc\"}");

            Assert.Equal(3, payload.Count);
            Assert.Equal("abc", payload.Name);
        }

        [Fact]
        public void No_data_gives_an_empty_instance_rather_than_null()
        {
            var payload = SerializationUtils.DeserializeData<Payload>(null);

            Assert.NotNull(payload);
            Assert.Equal(0, payload.Count);
        }

        [Fact]
        public void A_json_null_gives_an_empty_instance_rather_than_null()
        {
            Assert.NotNull(SerializationUtils.DeserializeData<Payload>("null"));
        }
    }
}
