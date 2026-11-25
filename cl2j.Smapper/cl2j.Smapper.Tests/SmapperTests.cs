using System.Reflection;
using Xunit;

namespace cl2j.Smapper.Tests
{
    public class SmapperTests
    {
        private sealed class Address
        {
            public string City { get; set; } = "";
        }

        private sealed class Person
        {
            public int Id { get; set; }
            public string? Name { get; set; }
            public DateTime Born { get; set; }
            public Address? Address { get; set; }
            public long Wide { get; set; }
            public string ReadOnly { get; } = "source-only";
        }

        private sealed class PersonDto
        {
            public int Id { get; set; }
            public string? Name { get; set; }
            public DateTime Born { get; set; }
            public Address? Address { get; set; }
            public int Wide { get; set; }
            public string ReadOnly { get; } = "destination-default";
            public string Unmatched { get; set; } = "untouched";
        }

        [Fact]
        public void Map_copies_properties_with_the_same_name_and_type()
        {
            var born = new DateTime(1990, 5, 17);
            var source = new Person { Id = 7, Name = "Ada", Born = born };

            var dto = source.Map<PersonDto>();

            Assert.Equal(7, dto.Id);
            Assert.Equal("Ada", dto.Name);
            Assert.Equal(born, dto.Born);
        }

        [Fact]
        public void Map_leaves_destination_properties_that_have_no_source_untouched()
        {
            var dto = new Person { Id = 1 }.Map<PersonDto>();

            Assert.Equal("untouched", dto.Unmatched);
        }

        [Fact]
        public void Map_skips_a_property_whose_type_differs_from_the_source()
        {
            var dto = new Person { Wide = 42 }.Map<PersonDto>();

            Assert.Equal(0, dto.Wide);
        }

        [Fact]
        public void Map_does_not_write_to_a_destination_property_without_a_setter()
        {
            var dto = new Person().Map<PersonDto>();

            Assert.Equal("destination-default", dto.ReadOnly);
        }

        [Fact]
        public void Map_copies_a_reference_property_by_reference()
        {
            var address = new Address { City = "Montreal" };

            var dto = new Person { Address = address }.Map<PersonDto>();

            Assert.Same(address, dto.Address);
        }

        [Fact]
        public void Map_copies_a_null_value_over_the_destination_default()
        {
            var input = new PersonDto { Name = "previous" };

            new Person { Name = null }.Map(input);

            Assert.Null(input.Name);
        }

        [Fact]
        public void Map_fills_the_instance_it_is_given_and_returns_it()
        {
            var input = new PersonDto();

            var result = new Person { Id = 3 }.Map(input);

            Assert.Same(input, result);
            Assert.Equal(3, input.Id);
        }

        [Fact]
        public void Map_uses_the_runtime_type_of_the_source()
        {
            object source = new Person { Id = 9 };

            var dto = source.Map<PersonDto>();

            Assert.Equal(9, dto.Id);
        }

        [Fact]
        public void Map_gives_the_same_result_when_repeated()
        {
            var first = new Person { Id = 1, Name = "a" }.Map<PersonDto>();
            var second = new Person { Id = 2, Name = "b" }.Map<PersonDto>();

            Assert.Equal((1, "a"), (first.Id, first.Name));
            Assert.Equal((2, "b"), (second.Id, second.Name));
        }

        [Fact]
        public void Clone_returns_an_equal_but_distinct_object()
        {
            var source = new Person { Id = 5, Name = "Grace", Address = new Address { City = "Quebec" } };

            var clone = source.Clone();

            Assert.NotSame(source, clone);
            Assert.Equal(5, clone.Id);
            Assert.Equal("Grace", clone.Name);
        }

        [Fact]
        public void Clone_is_deep()
        {
            var source = new Person { Address = new Address { City = "Quebec" } };

            var clone = source.Clone();
            clone.Address!.City = "Laval";

            Assert.Equal("Quebec", source.Address.City);
        }

        [Fact]
        public void Clone_throws_when_the_serialized_form_is_null()
        {
            Person? source = null;

            Assert.Throws<InvalidOperationException>(() => source!.Clone());
        }
    }

    public class ObjectExtensionsTests
    {
        private sealed class Sample
        {
            public int ReadWrite { get; set; }
            public int ReadOnly { get; } = 1;
            public int WriteOnly { set => writeOnly = value; }
            private int writeOnly;
        }

        private static string[] Names(IEnumerable<PropertyInfo> properties) => properties.Select(p => p.Name).Order().ToArray();

        [Fact]
        public void GetReadableProperties_returns_every_property_with_a_getter()
        {
            Assert.Equal(["ReadOnly", "ReadWrite"], Names(ObjectExtensions.GetReadableProperties(typeof(Sample))));
        }

        [Fact]
        public void GetReadaleWritableProperties_returns_only_properties_with_both_accessors()
        {
            Assert.Equal(["ReadWrite"], Names(ObjectExtensions.GetReadaleWritableProperties(typeof(Sample))));
        }
    }
}
