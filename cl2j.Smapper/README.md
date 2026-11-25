`cl2j.Smapper` is a lightweight, reflection-based object-to-object mapper for .NET. It copies the properties that have the same name and the same type from one object to another, and it can deep-clone an object through JSON.

Use it for simple entity-to-DTO mapping when a full mapping library is more than you need. There is nothing to configure and nothing to register.

# Getting started

```powershell
Install-Package cl2j.Smapper
```

Target framework: `net10.0`.

```cs
using cl2j.Smapper;

public class Person
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public DateTime Born { get; set; }
}

public class PersonDto
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public DateTime Born { get; set; }
}

var dto = new Person { Id = 7, Name = "Ada", Born = new DateTime(1990, 5, 17) }.Map<PersonDto>();
```

# API

Both methods are extension methods in the static class `cl2j.Smapper.Smapper`.

## Map

```cs
public static TDestination Map<TDestination>(this object source, TDestination? input = null) where TDestination : class
```

- Without `input`, a `TDestination` is created with its parameterless constructor.
- With `input`, that instance is filled and returned, so existing values are overwritten:

```cs
var existing = new PersonDto();
new Person { Id = 3 }.Map(existing); // existing.Id == 3, and the same instance is returned
```

- The source type is its **runtime type**: `object source = new Person(); source.Map<PersonDto>()` works.
- The mapping plan for each source/destination pair is built once and cached, so repeated calls only pay for property reads and writes.

## Clone

```cs
public static TSource Clone<TSource>(this TSource source) where TSource : class
```

Returns a deep copy, made by serializing the object with `System.Text.Json` and deserializing it again. It throws `InvalidOperationException` if the deserialized value is `null` (for example when `source` is `null`).

```cs
var copy = person.Clone();
copy.Address!.City = "Laval"; // person.Address is unchanged
```

# Mapping rules

A destination property is filled when a source property matches **all** of these:

- same name (case-sensitive);
- exactly the same type (`int` is not mapped to `long`);
- the source property has a getter and the destination property has both a getter and a setter.

Behaviour confirmed by the test suite:

- Destination properties with no matching source property keep their value.
- A property whose type differs is skipped silently; the destination keeps its value.
- A destination property without a setter is never written.
- A `null` source value is copied over the destination value, including when you pass `input`.
- Only public instance properties are considered. Fields are ignored.

# Gotchas

- **The mapping is shallow.** A reference-type property such as `Address` is copied **by reference**: source and destination then share the same object. Use `Clone()` if you need a deep copy.
- **Collections are copied by reference too**, and only when the property types are identical (`List<int>` to `List<int>`, not `List<int>` to `IEnumerable<int>`). Properties typed as an interface are not mapped.
- **No conversion and no nested mapping.** A `Person.Address` of type `Address` does not map to a `PersonDto.Address` of type `AddressDto`.
- **The destination needs a public parameterless constructor** when you do not pass `input`.
- **`Map` on a `null` source throws** (`NullReferenceException`): it reads `source.GetType()`.
- `Clone` goes through `System.Text.Json`, so it copies public properties only and needs a type that `System.Text.Json` can round-trip.
- `cl2j.Smapper.ObjectExtensions` is public and exposes the two reflection helpers the mapper uses, `GetReadableProperties(Type)` and `GetReadableWritableProperties(Type)`. The earlier misspelled name, `GetReadaleWritableProperties(Type)`, still works but is deprecated (`[Obsolete]`) and will be removed in the next major version.
