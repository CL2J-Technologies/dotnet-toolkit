using cl2j.Tooling;
using Xunit;

namespace cl2j.Tooling.Tests
{
    /// <summary>
    ///     The largest file in the package and the one whose output ends up in URLs, so a change
    ///     here changes addresses that are already published and linked to.
    /// </summary>
    public class StringUtilsTests
    {
        [Theory]
        [InlineData("Hello world", "hello-world")]
        [InlineData("Déjà vu", "deja-vu")]
        [InlineData("C# 12 & .NET", "c-12-net")]
        [InlineData("already-normalized", "already-normalized")]
        [InlineData("Trailing spaces   ", "trailing-spaces-")]
        public void A_uri_keeps_letters_and_digits_and_collapses_the_rest_to_one_dash(string input, string expected)
        {
            Assert.Equal(expected, input.NormalizeForUri());
        }

        [Fact]
        public void A_uri_keeps_its_slashes_and_drops_a_dash_that_would_sit_before_one()
        {
            //The whole reason NormalizeForUri exists apart from NormalizeComponentForUri: it is for
            //a path, so the separators survive.
            Assert.Equal("articles/deja-vu", "Articles / Déjà vu".NormalizeForUri());
        }

        [Fact]
        public void A_uri_does_not_end_on_a_slash()
        {
            Assert.Equal("articles", "Articles/".NormalizeForUri());
        }

        [Theory]
        [InlineData("Hello world", "hello-world")]
        [InlineData("Déjà vu", "deja-vu")]
        [InlineData("Trailing spaces   ", "trailing-spaces")]
        [InlineData("a/b", "a-b")]
        public void A_component_has_no_separators_and_no_trailing_dash(string input, string expected)
        {
            //A component is one segment, so a slash is punctuation like any other and a dash left
            //at the end would show up in the address.
            Assert.Equal(expected, input.NormalizeComponentForUri());
        }

        [Theory]
        [InlineData("Renée", "Renee")]
        [InlineData("Привет", "Privet")]
        [InlineData("nothing to remove", "nothing to remove")]
        public void Diacritics_and_non_latin_letters_are_transliterated(string input, string expected)
        {
            Assert.Equal(expected, input.RemoveDiacritics());
        }

        [Theory]
        [InlineData("Ærøskøbing", "Aroskobing")]   //AE truncated to A
        [InlineData("я", "y")]                     //ya truncated to y
        [InlineData("Я", "Y")]                     //Ya truncated to Y
        [InlineData("ю", "y")]                     //yu truncated to y
        public void A_transliteration_of_more_than_one_letter_keeps_only_its_first(string input, string expected)
        {
            //Characterisation, not endorsement. The table maps "Æ" to "AE" and "я" to "ya", but the
            //char overload returns entry.Value[0] — one character — and the string overload calls
            //it per character. So the second letter of every multi-character transliteration is
            //dropped, silently.
            //
            //Held in place rather than fixed because this output ends up in URLs: correcting it
            //changes the address of anything already published whose title contains one of these.
            //See the issue raised alongside these tests.
            Assert.Equal(expected, input.RemoveDiacritics());
        }

        [Fact]
        public void Cropping_shortens_only_what_is_too_long()
        {
            Assert.Equal("abcde", "abcdefghij".Crop(5));
            Assert.Equal("abc", "abc".Crop(5));
            Assert.Equal(string.Empty, "abc".Crop(0));
        }

        [Fact]
        public void Cropping_null_is_refused_rather_than_returning_null()
        {
            Assert.Throws<ArgumentNullException>(() => StringUtils.Crop(null!, 5));
        }

        [Fact]
        public void An_invariant_value_is_lower_case_and_without_diacritics()
        {
            Assert.Equal("renee", StringUtils.ToInvariant("Renée"));
        }

        [Fact]
        public void A_prefix_is_removed_only_when_it_is_there()
        {
            Assert.Equal("bar", "foobar".RemoveIfStartWith("foo"));
            Assert.Equal("foobar", "foobar".RemoveIfStartWith("baz"));
        }

        [Fact]
        public void A_key_joins_its_parts_with_dots()
        {
            Assert.Equal("a.b.c", StringUtils.GenerateKeyFromArray("a", "b", "c"));
        }

        [Fact]
        public void A_normalized_key_normalizes_each_part_first()
        {
            Assert.Equal("deja-vu.hello-world", StringUtils.GenerateNormalizedKey("Déjà vu", "Hello world"));
        }

        [Fact]
        public void Invariant_values_are_deduplicated()
        {
            var set = StringUtils.ComputeInvariantValues(x => x, new List<string> { "Renée", "renee", "RENEE", string.Empty });

            Assert.Equal("renee", Assert.Single(set));
        }

        [Fact]
        public void Normalized_values_are_deduplicated()
        {
            var set = StringUtils.ComputeNormalizedValues(x => x, new[] { "Hello world", "hello  world", string.Empty });

            Assert.Equal("hello-world", Assert.Single(set));
        }

        [Theory]
        [InlineData("<p>Hello</p>", "Hello")]
        [InlineData("<a href=\"x\">link</a> and text", "link and text")]
        [InlineData("no markup", "no markup")]
        [InlineData("", "")]
        public void Markup_is_stripped_and_the_text_between_it_is_kept(string html, string expected)
        {
            Assert.Equal(expected, StringUtils.SimpleRemoveHtml(html));
        }

        [Fact]
        public void Stripping_markup_is_not_a_sanitiser()
        {
            //Characterisation, and worth being explicit about: it drops everything between angle
            //brackets, which is not the same as making a string safe to render. A lone "<" eats
            //the rest of the input.
            Assert.Equal("before ", StringUtils.SimpleRemoveHtml("before < after"));
        }
    }
}
