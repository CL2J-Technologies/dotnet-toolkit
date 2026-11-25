using cl2j.Tooling;
using Xunit;

namespace cl2j.Tooling.Tests
{
    /// <summary>
    ///     A dictionary keyed by language that answers even when the language it is asked for is
    ///     not there. That fallback is the whole point of the type and the part a caller cannot
    ///     guess, so it is what these tests are about.
    ///
    ///     <para>
    ///     TourDuChapeau stores its article titles, slugs, summaries and bodies in this.
    ///     </para>
    /// </summary>
    public class LocalizedTests
    {
        private static Localized<string> Localized(params (string Language, string Value)[] values)
        {
            var localized = new Localized<string>();
            foreach (var (language, value) in values)
                localized.Add(language, value);
            return localized;
        }

        [Fact]
        public void The_language_asked_for_is_the_one_returned()
        {
            var localized = Localized(("en", "Hello"), ("fr", "Bonjour"));

            Assert.Equal("Bonjour", localized["fr"]);
        }

        [Fact]
        public void A_missing_language_falls_back_to_english()
        {
            var localized = Localized(("en", "Hello"), ("es", "Hola"));

            Assert.Equal("Hello", localized["fr"]);
        }

        [Fact]
        public void With_no_english_either_the_first_value_answers()
        {
            var localized = Localized(("es", "Hola"), ("de", "Hallo"));

            Assert.Equal("Hola", localized["fr"]);
        }

        [Fact]
        public void An_empty_one_has_nothing_to_fall_back_to()
        {
            var localized = new Localized<string>();

            Assert.False(localized.TryGetValue("fr", out _));
            Assert.Throws<KeyNotFoundException>(() => localized["fr"]);
        }

        [Fact]
        public void ContainsKey_answers_about_the_language_itself_and_does_not_fall_back()
        {
            //The asymmetry worth knowing about: ContainsKey("fr") is false while localized["fr"]
            //returns the English text. A caller testing ContainsKey before reading gets a
            //different answer than one that just reads.
            var localized = Localized(("en", "Hello"));

            Assert.False(localized.ContainsKey("fr"));
            Assert.Equal("Hello", localized["fr"]);
        }

        [Fact]
        public void Get_falls_back_to_the_value_it_is_given_only_when_there_is_nothing_at_all()
        {
            Assert.Equal("Hello", Localized(("en", "Hello")).Get("fr", "fallback"));
            Assert.Equal("Hola", Localized(("es", "Hola")).Get("fr", "fallback"));
            Assert.Equal("fallback", new Localized<string>().Get("fr", "fallback"));
        }

        [Fact]
        public void GetOrDefault_returns_default_only_when_there_is_nothing_at_all()
        {
            Assert.Equal("Hello", Localized(("en", "Hello")).GetOrDefault("fr"));
            Assert.Null(new Localized<string>().GetOrDefault("fr"));
        }

        [Fact]
        public void It_behaves_as_a_dictionary_for_everything_else()
        {
            var localized = Localized(("en", "Hello"), ("fr", "Bonjour"));

            Assert.Equal(2, localized.Count);
            Assert.Equal(["en", "fr"], localized.Keys);
            Assert.True(localized.Remove("fr"));
            Assert.Single(localized);

            localized.Clear();
            Assert.Empty(localized);
        }

        [Fact]
        public void A_value_can_be_replaced_through_the_indexer()
        {
            var localized = Localized(("en", "Hello"));

            localized["en"] = "Hi";

            Assert.Equal("Hi", localized["en"]);
        }

        [Fact]
        public void Adding_the_same_language_twice_is_refused()
        {
            var localized = Localized(("en", "Hello"));

            Assert.Throws<ArgumentException>(() => localized.Add("en", "Hi"));
        }

        [Fact]
        public void A_value_type_falls_back_the_same_way()
        {
            //TryGetValue's "take the first" branch tests `first != null`, which behaves differently
            //for a value type: default(int) is 0, not null, so the branch is reachable in a way it
            //is not for a string.
            var localized = new Localized<int>();
            localized.Add("es", 42);

            Assert.Equal(42, localized["fr"]);
        }
    }
}
