using System.Security.Claims;

namespace cl2j.WebTooling
{
    public static class ClaimsExtensions
    {
        public static string? GetClaimValue(this ClaimsPrincipal principal, string claimType)
        {
            string? value = null;

            var claim = principal.Claims.FirstOrDefault(c => c.Type == claimType);
            if (claim != null)
                value = claim.Value;

            return value;
        }
    }
}
