`cl2j.WebTooling` is a set of small ASP.NET Core extension methods: reading claims, describing and locating the current request, picking a language, and posting uploaded files with an `HttpClient`.

Use it in a web application that wants these one-liners instead of rewriting them in every project.

# Getting started

```powershell
Install-Package cl2j.WebTooling
```

Target framework: `net10.0`. The package uses the `Microsoft.AspNetCore.App` shared framework, so it is meant for ASP.NET Core applications (or libraries consumed by them). It depends on `cl2j.Tooling`. There is no configuration and no DI registration: add `using cl2j.WebTooling;` and call the extension methods.

```cs
using cl2j.WebTooling;

app.Use(async (context, next) =>
{
    logger.LogInformation("{Request} from {Ip} ({Agent})", context.GetUrl(), context.GetIp(), context.GetUserAgent());
    await next();
});
```

# Claims

```cs
string? email = User.GetClaimValue("email");
```

`ClaimsPrincipal.GetClaimValue(claimType)` returns the value of the **first** claim of that type, or `null`. The type must match exactly.

# Describing the request

| Method | Result |
|---|---|
| `HttpRequest.GetInfo()` | `"POST https://example.com:8443/a/b?x=1"` (method, scheme, host, path, query) |
| `HttpContext.GetUrl()` | Same string as `GetInfo()`, from the context |
| `HttpContext.GetIp()` | Remote IP address as a string, or `string.Empty` when unknown |
| `HttpContext.GetUserAgent()` | The `User-Agent` header, or `string.Empty` |
| `HttpContext.GetQueryString()` | The query string including the `?`, or `string.Empty` |
| `HttpRequest.GetCurrentUrl()` | `scheme://host/path?query`, without the method |
| `HttpRequest.FormatCanonicalUrl(path)` | `scheme://host` + the given path; `string.Empty` for a null or empty path |

`GetIp()` reads the connection's remote address. Behind a reverse proxy this is the proxy's address unless forwarded headers are configured in your application.

# Routing

```cs
string? slug = context.GetRouteValueString("slug"); // also available on HttpRequest
var tokens = context.GetDataTokens();               // empty dictionary when the endpoint has none
```

`GetRouteValueString` returns `null` when the key is missing **or the value is not a string** (a route value of `42` as an `int` gives `null`).

# Language

```cs
string browser = request.GetBrowserLanguage(); // "fr" or "en"
string page = request.GetRequestLanguage();    // "en" under /en..., otherwise "fr"
```

- `GetBrowserLanguage()` reads `Accept-Language`, ordered by quality, and returns the first of `"fr"` or `"en"` found. It falls back to `"en"`. The supported list is hard-coded; a regional tag such as `fr-CA` does not match.
- `GetRequestLanguage()` returns `"en"` if the path starts with `/en` (case-insensitive), `"fr"` otherwise.

Known limitation: `GetRequestLanguage()` has no segment boundary, so a path such as `/entreprise` is reported as English. A skipped test in the repository documents this.

# Posting uploaded files

```cs
public async Task<UploadResult?> Forward(IFormFileCollection files)
    => await httpClient.PostFiles<UploadResult>("https://other.example.com/upload", files);
```

`HttpClient.PostFiles<TOut>(url, IFormFileCollection files)` sends every file as one `multipart/form-data` request, each part named `file` and carrying the original file name. It calls `EnsureSuccessStatusCode()` (an error status throws `HttpRequestException`), then parses the JSON response into `TOut`. An empty response body gives `default`.

Gotchas:

- Each file is fully read into memory before sending.
- The response is parsed with Newtonsoft.Json (through `cl2j.Tooling`), not `System.Text.Json`, so use property names and attributes that Newtonsoft understands.
- An empty file collection still sends a request, with an empty multipart body.
