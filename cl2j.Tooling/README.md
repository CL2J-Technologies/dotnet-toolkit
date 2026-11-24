# cl2j.Tooling

General-purpose .NET utilities used across the cl2j toolkit and usable on their own: a refreshing cache base class,
localized values, an SMTP email service, a small JSON REST client, a set of exception types, and string, type,
stream and serialization helpers.

```powershell
dotnet add package cl2j.Tooling
```

Dependencies: `Newtonsoft.Json` and the `Microsoft.Extensions` configuration, options and logging abstractions.

| Area | Namespace | Main types |
| --- | --- | --- |
| [Caching](#caching) | `cl2j.Tooling` | `CacheableObject<T>`, `CacheLoader` |
| [Localized values](#localized-values) | `cl2j.Tooling` | `Localized<T>` |
| [Email](#email) | `cl2j.Tooling.Email` | `IEmailService`, `EmailResult`, `SmtpSettings`, `AddEmail` |
| [Exceptions](#exceptions) | `cl2j.Tooling.Exceptions` | `NotFoundException`, `ConflictException`, `ValidationException`... |
| [HTTP and JSON](#http-and-json) | `cl2j.Tooling` | `RestClient`, `HttpClientExtensions`, `SerializationUtils` |
| [Strings](#strings) | `cl2j.Tooling` | `StringUtils` |
| [Types, streams, and other helpers](#types-streams-and-other-helpers) | `cl2j.Tooling` | `TypeUtils`, `StreamUtils`, `CompressionUtils`, `DateUtils`, `EnumUtils` |
| [Observers](#observers) | `cl2j.Tooling.Observers` | `Observable<T>`, `IObserver<T>` |

# Caching

`CacheableObject<T>` is a base class for an in-memory list that is loaded from a slow source (a database, a file, a
web service) and refreshed on an interval. You implement `LoadCache()`; the base class calls it, stores the result, and
reloads it every `refreshInterval`.

```cs
public class CountryCache(ICountryRepository repository, ILogger<CountryCache> logger)
    : CacheableObject<Country>("countries", TimeSpan.FromMinutes(5), logger)
{
    protected override Task<List<Country>> LoadCache() => repository.LoadAllAsync();

    public async Task<IReadOnlyList<Country>> GetAllAsync() => await GetCache();
}
```

Protected members available to the subclass:

- `GetCache()` returns the cached list, waiting for the first load if it has not completed.
- `WaitForCache()` only waits for the first load.
- `AddCacheObject(item)` and `UpdateCacheObject(oldItem, newItem)` change the cached list in place, so a write is
  visible before the next refresh.
- `RefreshCache()` reloads immediately.

The first load starts on the first call to `GetCache`, `WaitForCache`, `AddCacheObject` or `UpdateCacheObject`, not
when the object is constructed. Waiting for it lasts at most one refresh interval. When a load throws, the error is
logged and the previous content stays in place. Dispose the object to stop the refresh timer.

`CacheLoader` is the engine underneath: give it a name, an interval and an async callback, and it runs the callback
on a timer. `WaitAsync()` returns `true` once a load has run to completion and `false` if that did not happen within
one interval. A callback that catches its own exceptions counts as completed, so track success yourself if you need
to tell the two apart.

# Localized values

`Localized<T>` is a dictionary of language code to value, with fallbacks on reads: the language asked for, then
`"en"`, then the first value present.

```cs
var title = Localized<string>.CreateFrEn(fr: "Bonjour", en: "Hello");

title["fr"];                     // "Bonjour"
title["es"];                     // "Hello": no "es", falls back to "en"
title.ContainsKey("es");         // false: ContainsKey does not fall back
title.Get("es", "fallback");     // "Hello"; "fallback" only when the instance is empty
title.GetOrDefault("es");        // "Hello"; default(T) only when the instance is empty
```

Also available: `Create(language, value)`, `Default(value)` (stored under `"*"`), `FromDict`, `FromDictNullable`,
`EmptyString()`, `Clone()`, and value-based `Equals` / `GetHashCode`. Reading from an empty instance through the
indexer throws `KeyNotFoundException`.

# Email

`AddEmail` binds the `SmtpSettings` section and registers `IEmailService`:

```json
"SmtpSettings": {
  "Server": "smtp.example.com",
  "Port": 587,
  "User": "mailer",
  "Password": "<secret>",
  "EnableSsl": true,
  "From": "noreply@example.com",
  "SystemTo": "ops@example.com",
  "ErrorTo": "dev@example.com"
}
```

```cs
services.AddEmail(configuration);   // cl2j.Tooling.Email.EmailExtensions

var result = await emailService.SendEmailAsync("Welcome", "<p>Hello!</p>", "user@example.com", isBodyHtml: true);
if (!result.Sent)
    logger.LogWarning(result.Failure, "welcome email not sent");
```

- Every send returns an `EmailResult`: `Sent`, and `Failure` (the exception) when it was not. A refused or unreachable
  server, a network error and an invalid address are reported this way; programming errors still throw.
- `toEmail` accepts several addresses separated by commas.
- `SendEmailAsync` has overloads that take an explicit `from`, and a `replyTo` for the `Reply-To` header.
- `SendSystemAsync(subject, details)` sends to `SystemTo`; `SendErrorAsync(exception, subject, details)` logs the
  exception and sends to `ErrorTo`. `From` (and `SystemTo` / `ErrorTo` for these two) must be set, otherwise a
  `ValidationException` is thrown.
- `EmailExtensions.IsEmailValid(string)` checks an address against a regular expression.

# Exceptions

Plain exception types, each with the usual three constructors (none, message, message + inner exception), meant to be
mapped to an HTTP status or an error response by the application:

`BadRequestException`, `ConflictException`, `ForbiddenException`, `NotFoundException`, `PropertyNotFoundException`,
`TooManyRequestsException`, `UnauthorizedException`, `UnhandledException`, `ValidationException`.

The other cl2j packages throw them: `NotFoundException` for a missing file storage provider or data store,
`ConflictException` for a duplicate key.

# HTTP and JSON

`RestClient` is a thin JSON client over `HttpClient` (Newtonsoft.Json). It sets `Accept: application/json`, sends
bodies as camelCase JSON without nulls, and calls `EnsureSuccessStatusCode` on responses.

```cs
using var client = new RestClient("https://api.example.com/", new() { ["X-Api-Key"] = "<key>" });
client.SetAuthorization("Bearer", token);
client.SetTimeout(TimeSpan.FromSeconds(10));

var product = await client.GetAsync<Product>("products/42");          // null on 404
var created = await client.PostAsync<NewProduct, Product>("products", new NewProduct { Name = "Pen" });
await client.PostAsync("products/42/archive", new { });               // no response body
var uploaded = await client.PostFiles<UploadResult>("files", [("a.txt", bytes)]);
```

A second constructor takes your own `HttpMessageHandler` (for `IHttpClientFactory` handlers or for tests). The handler
is not disposed with the client.

Related helpers:

- `HttpClientExtensions.ParseResponseAsync<T>()` deserializes a response body; `PrepareRequestBody()` builds the JSON
  `StringContent` described above.
- `SerializationUtils.DeserializeData<T>(string?)` returns a new `T` for `null` or a `null` result instead of `null`.

# Strings

`StringUtils` works on text that ends up in addresses, keys and search indexes:

```cs
"Déjà vu".NormalizeComponentForUri();            // "deja-vu"        one segment: separators become dashes
"Articles / Déjà vu".NormalizeForUri();          // "articles/deja-vu"   a path: slashes are kept
"Renée".RemoveDiacritics();                      // "Renee"          also transliterates, e.g. "Привет" -> "Privet"
StringUtils.ToInvariant("Renée");                // "renee"          lower case and no diacritics
"abcdefghij".Crop(5);                            // "abcde"
StringUtils.GenerateNormalizedKey("Déjà vu", "Hello world");   // "deja-vu.hello-world"
```

Others: `RemoveIfStartWith(prefix)`, `GenerateKeyFromArray(keys)` (joins with `.`), `ComputeInvariantValues` and
`ComputeNormalizedValues` (distinct normalized values of a list), and `SimpleRemoveHtml(html)` (drops everything
between `<` and `>`, it is not an HTML sanitizer).

# Types, streams, and other helpers

- `TypeUtils.GetPrettyName(type)`: `typeof(Dictionary<string,int>)` becomes `Dictionary<String, Int32>`.
  `TypeUtils.GetTypeName<T>()` returns the namespace-qualified name to write into generated source.
- `StreamUtils`: `string.ToStream()`, `Stream.ToBytes()`, `CopyTo(src, dest)`.
- `CompressionUtils`: `Compress(string)` to GZip bytes and `Decompress(byte[])` back (UTF-8).
- `DateUtils.InRange(time, start, end)`: `start` inclusive, `end` exclusive, either may be `null`.
- `EnumUtils.ToList<TEnum>()`: the enum's names as `string[]`.

# Observers

`Observable<T>` is a small async publish/subscribe helper. Implement `IObserver<T>.OnChangeAsync`, subscribe, and call
`NotifyAsync`:

```cs
var observable = new Observable<string>();
observable.Subscribe(myObserver);       // false if this observer is already subscribed
await observable.NotifyAsync("changed");
```

Every observer is notified once, even when another throws; failures are then raised together in an
`AggregateException`. The caches in `cl2j.DataStore` expose this interface to announce a refresh.

# More

The tests in `cl2j.Tooling.Tests` show each of the behaviors above.
