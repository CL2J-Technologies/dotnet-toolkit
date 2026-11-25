using Xunit;

namespace cl2j.Tooling.Tests
{
    /// <summary>
    ///     The factories, equality, cloning and collection plumbing of <see cref="Localized{T}"/>.
    ///     The language fallback has its own class, <see cref="LocalizedTests"/>.
    /// </summary>
    public class LocalizedMembersTests
    {
        private static Localized<string> EnFr(string en, string fr) => Localized<string>.CreateFrEn(fr, en);

        [Fact]
        public void Create_holds_the_one_language_it_is_given()
        {
            var localized = Localized<string>.Create("es", "Hola");

            Assert.Equal("Hola", localized["es"]);
            Assert.Single(localized);
        }

        [Fact]
        public void CreateFrEn_puts_each_text_under_its_own_language()
        {
            var localized = Localized<string>.CreateFrEn("Bonjour", "Hello");

            Assert.Equal("Bonjour", localized["fr"]);
            Assert.Equal("Hello", localized["en"]);
        }

        [Fact]
        public void Default_answers_for_any_language()
        {
            var localized = Localized<string>.Default("Hello");

            Assert.Equal("Hello", localized["fr"]);
            Assert.Equal("Hello", localized["de"]);
        }

        [Fact]
        public void EmptyString_answers_with_an_empty_string()
        {
            Assert.Equal(string.Empty, Localized<string>.EmptyString()["fr"]);
        }

        [Fact]
        public void FromDict_copies_every_entry()
        {
            var localized = Localized<int>.FromDict(new Dictionary<string, int> { ["en"] = 1, ["fr"] = 2 });

            Assert.Equal(1, localized["en"]);
            Assert.Equal(2, localized["fr"]);
            Assert.Equal(2, localized.Count);
        }

        [Fact]
        public void FromDictNullable_leaves_out_the_languages_without_a_value()
        {
            var localized = Localized<string>.FromDictNullable(new Dictionary<string, string?>
            {
                ["en"] = "Hello",
                ["fr"] = null,
            });

            Assert.Single(localized);
            Assert.False(localized.ContainsKey("fr"));
        }

        [Fact]
        public void ToString_lists_english_first_then_the_other_languages()
        {
            var localized = new Localized<string> { { "fr", "Bonjour" }, { "en", "Hello" } };

            Assert.Equal("en=Hello, fr=Bonjour", localized.ToString());
        }

        [Fact]
        public void ToString_works_without_an_english_entry()
        {
            Assert.Equal("*=Hello", Localized<string>.Default("Hello").ToString());
        }

        [Fact]
        public void A_clone_is_equal_but_independent()
        {
            var original = EnFr("Hello", "Bonjour");

            var clone = original.Clone();
            Assert.True(original.Equals(clone));

            clone["en"] = "Hi";

            Assert.Equal("Hello", original["en"]);
            Assert.False(original.Equals(clone));
        }

        [Fact]
        public void Equal_when_the_same_languages_hold_the_same_values()
        {
            Assert.True(EnFr("Hello", "Bonjour").Equals(EnFr("Hello", "Bonjour")));
            Assert.Equal(EnFr("Hello", "Bonjour").GetHashCode(), EnFr("Hello", "Bonjour").GetHashCode());
        }

        [Fact]
        public void Not_equal_when_a_value_differs()
        {
            Assert.False(EnFr("Hello", "Bonjour").Equals(EnFr("Hello", "Salut")));
        }

        [Fact]
        public void Not_equal_when_the_number_of_languages_differs()
        {
            var one = Localized<string>.Create("en", "Hello");

            Assert.False(one.Equals(EnFr("Hello", "Bonjour")));
            Assert.False(EnFr("Hello", "Bonjour").Equals(one));
        }

        [Fact]
        public void Not_equal_to_something_that_is_not_a_Localized()
        {
            Assert.False(EnFr("Hello", "Bonjour").Equals("en=Hello"));
            Assert.False(EnFr("Hello", "Bonjour").Equals(null));
        }

        [Fact]
        public void A_null_value_is_only_equal_to_a_null_value()
        {
            var withNull = new Localized<string?> { { "en", null } };
            var otherNull = new Localized<string?> { { "en", null } };
            var withValue = new Localized<string?> { { "en", "Hello" } };

            Assert.True(withNull.Equals(otherNull));
            Assert.False(withNull.Equals(withValue));
            Assert.False(withValue.Equals(withNull));
            Assert.Equal(withNull.GetHashCode(), otherNull.GetHashCode());
        }

        [Fact]
        public void The_hash_code_does_not_depend_on_insertion_order()
        {
            var a = new Localized<string> { { "en", "Hello" }, { "fr", "Bonjour" } };
            var b = new Localized<string> { { "fr", "Bonjour" }, { "en", "Hello" } };

            Assert.Equal(a.GetHashCode(), b.GetHashCode());
        }

        [Fact]
        public void Different_languages_are_not_equal()
        {
            var spanish = Localized<string>.Create("es", "Hello");
            var french = Localized<string>.Create("fr", "Hello");

            Assert.False(spanish.Equals(french));
        }

        [Fact]
        public void The_key_value_pair_members_work_on_the_entries()
        {
            var localized = new Localized<string>();
            var pair = new KeyValuePair<string, string>("fr", "Bonjour");

            localized.Add(pair);

            Assert.Contains(pair, localized);
            Assert.DoesNotContain(new KeyValuePair<string, string>("fr", "Salut"), localized);
            Assert.Equal(["Bonjour"], localized.Values);
            Assert.False(localized.IsReadOnly);

            var copy = new KeyValuePair<string, string>[2];
            localized.CopyTo(copy, 1);
            Assert.Equal(pair, copy[1]);

            Assert.True(localized.Remove(pair));
            Assert.Empty(localized);
        }

        [Fact]
        public void It_enumerates_its_entries()
        {
            var localized = EnFr("Hello", "Bonjour");

            Assert.Equal(["en", "fr"], localized.Select(kvp => kvp.Key).OrderBy(k => k));
            Assert.Equal(2, ((System.Collections.IEnumerable)localized).Cast<object>().Count());
        }
    }
}
