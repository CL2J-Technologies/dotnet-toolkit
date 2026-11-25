using System.Security.Claims;
using Xunit;

namespace cl2j.WebTooling.Tests
{
    public class ClaimsExtensionsTests
    {
        private static ClaimsPrincipal PrincipalWith(params Claim[] claims) => new(new ClaimsIdentity(claims, "test"));

        [Fact]
        public void GetClaimValue_returns_the_value_of_the_claim()
        {
            var principal = PrincipalWith(new Claim("email", "ada@example.com"), new Claim("role", "admin"));

            Assert.Equal("ada@example.com", principal.GetClaimValue("email"));
        }

        [Fact]
        public void GetClaimValue_returns_null_when_the_claim_is_missing()
        {
            Assert.Null(PrincipalWith(new Claim("role", "admin")).GetClaimValue("email"));
        }

        [Fact]
        public void GetClaimValue_returns_the_first_of_several_claims_of_the_same_type()
        {
            var principal = PrincipalWith(new Claim("role", "admin"), new Claim("role", "editor"));

            Assert.Equal("admin", principal.GetClaimValue("role"));
        }

        [Fact]
        public void GetClaimValue_returns_null_for_a_principal_without_claims()
        {
            Assert.Null(new ClaimsPrincipal().GetClaimValue("email"));
        }

        [Fact]
        public void GetClaimValue_matches_the_claim_type_exactly()
        {
            Assert.Null(PrincipalWith(new Claim("Email", "ada@example.com")).GetClaimValue("email"));
        }
    }
}
