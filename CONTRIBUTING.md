# Contributing to Latiprimere

Thanks for your interest in Latiprimere. This document covers how to contribute and the coding conventions every
change must follow. Consistent code is easier to review, debug and maintain, especially in a library where the same
behaviour has to be implemented once per platform.

> Questions or suggestions about these guidelines? Open an issue or raise it in your pull request.

---

## Table of Contents

1. [How to Contribute](#how-to-contribute)
2. [Golden Rules](#golden-rules)
3. [Formatting](#formatting)
   - [File and Class Layout](#file-and-class-layout)
   - [Indicating Scope](#indicating-scope)
   - [Indentation & Braces](#indentation--braces)
   - [Whitespace](#whitespace)
   - [Long Lines of Code](#long-lines-of-code)
   - [Multi-Line Argument and Parameter Lists](#multi-line-argument-and-parameter-lists)
4. [Commenting](#commenting)
   - [XML Documentation Comments](#xml-documentation-comments)
   - [End-of-Line Comments](#end-of-line-comments)
   - [Single Line Comments](#single-line-comments)
   - [TODO Comments](#todo-comments)
   - [C-Style Comments](#c-style-comments)
5. [Capitalisation & Naming](#capitalisation--naming)
6. [Programming Standards](#programming-standards)
   - [Namespaces](#namespaces)
   - [Classes & Structures](#classes--structures)
   - [Interfaces](#interfaces)
   - [Constants](#constants)
   - [Enumerations](#enumerations)
   - [Variables, Fields & Parameters](#variables-fields--parameters)
   - [Explicit Typing (var is Prohibited)](#explicit-typing-var-is-prohibited)
   - [Properties](#properties)
   - [Methods](#methods)
   - [Event Handlers](#event-handlers)
   - [Error Handling](#error-handling)
   - [Async / Await](#async--await)
   - [LINQ](#linq)
   - [Nullable Reference Types](#nullable-reference-types)
   - [Pattern Matching and Record Types](#pattern-matching-and-record-types)
   - [Object and Collection Initialisation](#object-and-collection-initialisation)
   - [Representing Boolean Values](#representing-boolean-values)
   - [Strings and Localisation](#strings-and-localisation)
7. [SOLID Design Principles](#solid-design-principles)
8. [Architecture](#architecture)
   - [Packages and Layers](#packages-and-layers)
   - [Dependency Rule](#dependency-rule)
   - [No UI Types in Core Packages](#no-ui-types-in-core-packages)
9. [Platform-Specific Code](#platform-specific-code)
10. [Unit Tests](#unit-tests)
    - [Class Naming](#class-naming)
    - [Class Location](#class-location)
    - [Method Naming](#method-naming)
    - [Arrange / Act / Assert](#arrange--act--assert)
    - [Supporting Files](#supporting-files)
    - [Integration Tests and Real Printers](#integration-tests-and-real-printers)
11. [Appendix A: Naming Parts & Pairs](#appendix-a-naming-parts--pairs)

---

## How to Contribute

1. **Discuss first.** For anything beyond a small fix, open an issue describing the problem or proposal before
   writing code. Significant design choices are recorded as Architecture Decision Records in
   [docs/decisions/](docs/decisions/README.md).
2. **Branch from `main`** and keep each pull request focused on one change.
3. **Build and test locally** before opening a pull request:
   ```bash
   dotnet build
   dotnet test
   ```
   The build treats warnings as errors, and `.editorconfig` plus the .NET analyzers enforce much of this guide.
4. **Include tests.** See [Unit Tests](#unit-tests). Tests are part of the definition of done.
5. **Update the docs** when behaviour, scope or plans change: [docs/progress.md](docs/progress.md) for progress,
   [docs/plan.md](docs/plan.md) for milestones.
6. **Reporting printer-specific bugs:** include the OS and version, the printer make and model, how it is connected
   (USB, network, shared queue), and, where possible, an IPP attribute dump or the Windows driver name.

**Before your first pull request is merged, sign the [Contributor Licence Agreement](CLA.md).** The CLA Assistant
bot asks you to on the pull request. Latiprimere has a free Community Licence and a paid Commercial Licence
([LICENSE](LICENSE)), and the agreement lets the owner license your contribution under both. You keep the
copyright in your work.

---

## Golden Rules

- **Follow the style of existing code.** Consistency within the codebase comes first. When further guidance is
  needed, consult these guidelines and the .NET Framework Design Guidelines.
- **Make code as simple and readable as possible.** Assume that someone else will be reading your code.
- **Prefer small, cohesive classes and methods** over large monolithic ones.
- **Write shared code once.** When the same formatting, conversion or set-up code is needed in more than one place,
  put it in one extension method or helper class and call that. Examples: resource messages are formatted with
  their generated `Format…` methods (see [Strings and Localisation](#strings-and-localisation)), and test projects
  build common objects with a shared builder (see [Supporting Files](#supporting-files)).
- **Write the comments first.** When writing a new method, write comments for each step the method will perform
  before coding a single statement.
- **Use liberal, meaningful comments** within each class, method, and code block to document the *purpose* of the code.
- **Mark incomplete code with `// TODO:` comments.** When working with many classes at once, it is easy to lose a
  train of thought.
- **Never hard-code "magic" values** (strings or numbers). Define constants, static read-only fields or enumerations.
  This applies especially to native interop: Win32 flags, IPP attribute names and status codes all get named constants.
- **Prefer `while` and `foreach`** over other looping constructs where applicable. They are logically simpler and
  easier to debug.
- **Prefer string interpolation** (`$"Hello {name}"`) for simple string composition. Use `StringBuilder` only for
  high-volume string construction in loops.
- **Always dispose `IDisposable` objects** created within a method. Use a `using` declaration
  (`using FileStream stream = new FileStream(...);`) as the preferred pattern.
- **Never write diagnostic output to the console or UI from library code.** Use `ILogger` from
  `Microsoft.Extensions.Logging.Abstractions`.
- **Discuss exceptions to these guidelines** in an issue or the pull request before proceeding.

---

## Formatting

### File and Class Layout

- **One type per file.** Every class, record, struct, interface, enum and delegate has its own file, including
  small private helper types: don't nest them in another class, make them `internal` in their own file instead.
- **The file name matches the type name**, so `PrinterCatalog` is in `PrinterCatalog.cs`. A generic type's name has
  characters a file name can't contain, so its type parameters go in braces: `Cache<TKey, TValue>` is in
  `Cache{TKey,TValue}.cs`.
- The build enforces both rules for top-level types (StyleCop SA1402 and SA1649). It doesn't check nested types, so
  reviewers do.
- Use the folder structure to reflect the namespace structure.
- Order members by accessibility: public, internal, protected, private, then native interop declarations.
- **Do not use `#region`.** A class that needs regions to stay navigable is too large and should be split
  (see [Single Responsibility Principle](#single-responsibility-principle-srp)).

---

### Indicating Scope

Always indicate scope when accessing static and instance members. This makes the intended use of each member
unambiguous and invokes IntelliSense, reducing the risk of typographic errors.

```csharp
int defaultPort = IppConstants.DefaultPort;
string printerName = this.PrinterName;
this.currentStatus = this.ParseJobStatus(response, this.jobId);
```

**Guidelines:**
- Include the `this` keyword before all instance fields, properties, and methods.
- Include the type name before all static fields, constants, and methods.

---

### Indentation & Braces

Indent with **tabs** to show the relative scope of execution. Braces are placed on their own line, aligned with the
statement that opens the scope.

**Never omit braces**, even where the compiler permits it (e.g. a single-line `if` body).

```csharp
private PrintJobState MapWindowsJobStatus(uint statusFlags)
{
    // Treat any error-style flag as an aborted job
    if ((statusFlags & WinSpoolConstants.JobStatusError) != 0)
    {
        this.logger.LogWarning("Spooler reported an error for job {JobId}.", this.jobId);

        return PrintJobState.Aborted;
    }

    // Map the remaining flags in order of precedence
    PrintJobState state = statusFlags switch
    {
        _ when (statusFlags & WinSpoolConstants.JobStatusPrinted)  != 0 => PrintJobState.Completed,
        _ when (statusFlags & WinSpoolConstants.JobStatusPrinting) != 0 => PrintJobState.Processing,
        _ when (statusFlags & WinSpoolConstants.JobStatusDeleted)  != 0 => PrintJobState.Cancelled,
        _ => PrintJobState.Pending
    };

    return state;
}
```

---

### Whitespace

Liberal use of whitespace is encouraged. It improves readability and helps during debugging and code reviews.

**Guidelines:**
- Use blank lines to separate logical blocks of code, in the same way a writer uses paragraphs.
- Keep logic statements clean without excessive brackets.
- **Leave a blank line before:**
  - a `return` (or `yield return`) statement;
  - a statement with a block: `if`, `while`, `do`, `for`, `foreach`, `switch`, `try`, `using`, `lock`, `fixed`,
    a local function, or a bare `{ }` block;
  - any statement that spans three or more lines.
- **The exception** is a statement that comes first in its block, straight after the opening brace (or after a
  `case` label). The line before it is already as good as blank.
- A comment that introduces a statement stays with it: the blank line goes above the comment.

```csharp
// ✅ Correct
foreach (PrinterInfo printer in printers)
{
    if (printer.IsDefault)
    {
        return printer;
    }

    string name = printer.Name;

    // Network printers are listed by their announced name
    if (name.StartsWith(NetworkPrefix, StringComparison.Ordinal))
    {
        names.Add(name);
    }
}

return null;
```

---

### Long Lines of Code

Comments and statements that exceed **144 characters** should be broken up and indented for readability. Tabs
count as 4 characters. No analyzer enforces this, so `.editorconfig` sets `max_line_length` and `guidelines` to
show a guide at column 144 in editors that support them; in VS Code, add `"editor.rulers": [144]`.

```csharp
[LibraryImport(WinSpoolConstants.LibraryName, EntryPoint = "OpenPrinterW", StringMarshalling = StringMarshalling.Utf16)]
[return: MarshalAs(UnmanagedType.Bool)]
internal static partial bool OpenPrinter
(
    string     printerName,
    out IntPtr printerHandle,
    IntPtr     printerDefaults
);
```

**Guidelines:**
- Don't break a line that fits. Break it where the parts stay readable: between arguments, or before each `.` of a
  fluent chain.
- Group related parameters on the same line where appropriate.
- When breaking comments across lines, match the indentation of the code being commented.

---

### Multi-Line Argument and Parameter Lists

When a method call, constructor call or method declaration drops its arguments or parameters onto further lines,
treat its parentheses like braces:
- The opening `(` goes on its own line, aligned with the line that names the method.
- The arguments or parameters go on the lines after it, indented one tab further.
- The closing `)` goes on its own line, aligned with the `(`, followed by whatever completes the statement.

```csharp
// ✅ Correct
CupsEndpoint endpoint = CupsEndpoint.Resolve
(
    options.Server,
    Environment.GetEnvironmentVariable(CupsEndpoint.ServerEnvironmentVariable),
    File.Exists,
    platform
);

PrinterDescriptionAttributes? attributes = await IppPrinterQueries
    .GetAttributesAsync
    (
        this.context,
        printerUri,
        IppRequestedAttributes.NetworkPrinterAttributes,
        cancellationToken
    )
    .ConfigureAwait(false);

// ✅ Correct: a nested call that wraps closes with its outer call
services.TryAddEnumerable(ServiceDescriptor.Singleton<IPrintSystem, IppNetworkPrintSystem>
(
    serviceProvider => (IppNetworkPrintSystem)IppNetworkPrintSystemFactory.Create(options))
);

// ❌ Incorrect
CupsEndpoint endpoint = CupsEndpoint.Resolve(
    options.Server,
    Environment.GetEnvironmentVariable(CupsEndpoint.ServerEnvironmentVariable),
    File.Exists,
    platform);
```

A list that opens on the same line as its first argument and simply carries on, such as a lambda with a block body
(`.Select(printer => new PrinterInfo { ... })`), is left as it is.

---

## Commenting

### XML Documentation Comments

Use triple-slash `///` comments to document every type and member, whatever its accessibility. Public API
documentation is published with the NuGet packages, so it must be complete: describe parameters, return values,
platform differences, and every exception a member can throw.

Two exceptions, both enforced by the build:
- **Private fields** are documented with an [end-of-line comment](#end-of-line-comments) instead.
- **Test methods** are documented by their GivenWhenThen names. Builders, fakes and fixtures in test projects are
  documented like production code.

---

### End-of-Line Comments

Use end-of-line comments only with variable and field declarations, to document the purpose of the variable.

```csharp
private int pollIntervalMilliseconds = 500; // Delay between job-status polls
```

---

### Single Line Comments

Use single-line comments above each significant block of code within a method. Comments always begin with two
slashes followed by a space: `// `.

---

### TODO Comments

Use `// TODO:` to mark code that needs further work before release. Search for them before opening a pull request,
and link each one that remains to an issue: `// TODO: #42 Support IPPS client certificates.`

---

### C-Style Comments

Use `/* ... */` only to temporarily disable blocks of code during development. Code must **not** be committed with
active `/* ... */` blocks. Delete code that is no longer needed; source control keeps the history.

---

## Capitalisation & Naming

Follow the [.NET Naming Guidelines](https://learn.microsoft.com/en-us/dotnet/standard/design-guidelines/naming-guidelines)
using three capitalisation styles: **Pascal**, **Camel**, and **UPPER**.

| Identifier Type | Capitalisation Style | Example(s) |
|---|---|---|
| Abbreviations (4 letters or fewer) | UPPER | `IPP`, `PDF`, `DPI` |
| `Id` (short for identifier) | Pascal, as a word | `Id`, `PrinterId`, `jobId` |
| Namespaces | Pascal | `Latiprimere.Windows` |
| Classes & Structs | Pascal | `PrinterCatalog` |
| Constants & Enums | Pascal | `DuplexMode` |
| Interfaces | Pascal (prefixed with `I`) | `IPrinterCatalog` |
| Enum Values | Pascal | `DuplexMode.LongEdge` |
| Properties | Pascal | `MediaSize` |
| Methods | Pascal | `SubmitAsync()` |
| Private and protected fields | Camel | `jobId`, `httpClient` |
| Method parameters | Camel | `GetPrinterAsync(string printerId)` |
| Local variables | Camel | `pageCount` |

- **Pascal casing:** first letter of the identifier and of each concatenated word are capitalised.
- **Camel casing:** first letter is lowercase; first letter of each subsequent word is capitalised.
- **UPPER casing:** used only for abbreviations and acronyms of four letters or fewer. In camel-cased identifiers
  the abbreviation keeps its case after the first word (`sourceURI`), and is lowercase when it begins one
  (`ippClient`, `pdfStream`).
- **`Id` is a word, not an acronym.** Write it `Id` (`PrinterId`, `jobId`, `DeletedJobIds`), never `ID`, as the
  .NET design guidelines and most .NET libraries do, so it reads the same as in the libraries used alongside
  Latiprimere ([ADR 0018](docs/decisions/0018-api-review-0-1.md)). In prose and messages it stays "ID".
- **No field prefixes.** Do not use `_` or `m` prefixes; the `this.` qualifier already marks fields.
- **Native interop types** use PascalCase like everything else (`DocInfo1` for `DOC_INFO_1W`, `JobStatusFlags` for
  the `JOB_STATUS_*` values), with an XML comment naming the native declaration they mirror. They live only in
  `internal` types in an `Interop` folder.

---

## Programming Standards

### Namespaces

Template: `Latiprimere.PackageName.SubsystemName`

```csharp
Latiprimere.Windows.Interop
Latiprimere.Cups
Latiprimere.Rendering
```

Use Pascal casing for all segments. Project and folder names match the namespace root of the project
(`src/Latiprimere.Cups/`), and subfolders match the remaining segments.

Two exceptions keep applications down to a couple of `using`s ([ADR 0018](docs/decisions/0018-api-review-0-1.md)):

- **`Latiprimere.Abstractions` uses the `Latiprimere` namespace** (its `RootNamespace`), like
  `Microsoft.Extensions.Logging.Abstractions`. Its `Rendering/` folder, for adapter authors, is
  `Latiprimere.Rendering`.
- **Dependency injection extensions use the `Microsoft.Extensions.DependencyInjection` namespace**, in each
  package's `DependencyInjection/` folder. Each class is named after its package so they don't collide:
  `LatiprimereServiceCollectionExtensions`, `LatiprimereCupsServiceCollectionExtensions`, and so on.

---

### Classes & Structures

Template: `Noun + Qualifier(s)`

```csharp
WindowsPrinterCatalog : IPrinterCatalog
IppPrintJob : IPrintJob
```

**Guidelines:**
- Use Pascal casing.
- Assign default field values on the line where each field is declared.
- Seal classes that are not designed for inheritance.
- Keep types `internal` unless they are part of the intended public API. Every public type is a long-term commitment.

---

### Interfaces

Template: Noun, Noun Phrase, or Adjective describing the behaviour.

```csharp
IPrinterCatalog
IPrintJob
IDocumentRasterizer
```

Prefix all interface names with `I` and use Pascal casing.

---

### Constants

Template: `Adjective(s) + Noun + Qualifier(s)`

```csharp
public static readonly TimeSpan DefaultStatusPollInterval = TimeSpan.FromMilliseconds(500);
internal const int DefaultIppPort = 631;
```

**Guidelines:**
- Use Pascal casing.
- **Prefer `static readonly` over `const` for public values.** `const` values are compiled into the calling assembly,
  so changing one breaks consumers until they recompile.
- `const` is fine for `internal` and `private` values, and is required for attribute arguments and native interop.

---

### Enumerations

Template: `Adjective(s) + Noun + Qualifier(s)`

```csharp
/// <summary>
/// Enumerates the colour modes a printer may support.
/// </summary>
[Flags]
public enum ColorModes
{
    None       = 0,
    Monochrome = 1 << 0,
    Grayscale  = 1 << 1,
    Color      = 1 << 2,
}
```

**Guidelines:**
- Use Pascal casing.
- Use `[Flags]` only when the enumeration represents a bit field, and use bit-shift literals (`1 << 0`) for its values.

---

### Variables, Fields & Parameters

Template: `Adjective(s) + Noun + Qualifier(s)`

```csharp
int   remainingCopyCount = 2;
float horizontalOffset   = 0.0f;
```

**Guidelines:**
- Use Camel casing.
- Declare each variable and field on a separate line to allow end-of-line comments.
- Assign initial values wherever possible.
- Avoid meaningless names like `i`, `j`, `k`, or `temp`. Use `pageIndex` instead of `i`; `swapValue` instead of `temp`.
- Use positive names for booleans: `isOnline` rather than `notOffline`.

---

### Explicit Typing (`var` is Prohibited)

Variables must be explicitly typed. **`var` is prohibited**, except for anonymous types in LINQ projections where
the compiler requires it.

```csharp
// ✅ Correct
int copyCount = 0;
foreach (PrinterInfo printer in printers) { ... }

// ❌ Incorrect
var copyCount = 0;
foreach (var printer in printers) { ... }
```

Target-typed `new()` is also avoided, so the type is always visible at the start of the declaration.

---

### Properties

Template: `Adjective(s) + Noun + Qualifier(s)`

```csharp
public string PrinterName
{
    get
    {
        return this.printerName;
    }
}
```

**Guidelines:**
- Use the common prefixes for inspection properties (see [Appendix A](#appendix-a-naming-parts--pairs)).
- Prefer `init`-only properties and records for data models; see [Pattern Matching and Record Types](#pattern-matching-and-record-types).
- Auto-properties (`public string Name { get; init; }`) are fine where no logic is needed.

---

### Methods

Template: `Verb + Adjective(s) + Noun + Qualifier(s)`

```csharp
private async Task<PrintJobStatus> FetchJobStatusAsync
(
    int               jobId,
    CancellationToken cancellationToken
)
```

**Guidelines:**
- Prefer returning a record or named tuple over `ref`/`out` parameters. Native interop declarations are the exception.
- **Early returns** are encouraged for guard clauses. Outside of guard clauses, minimise exit points.
- Keep methods to **20–30 lines**. Extract blocks into their own methods when a method grows beyond this.
- Extract repeated blocks of code into a method.
- **Use full method and property bodies.** Do not use expression-bodied members (`=>`).

  ```csharp
  // ❌ Incorrect
  public bool IsTerminal() => this.State is PrintJobState.Completed or PrintJobState.Cancelled;

  // ✅ Correct
  public bool IsTerminal()
  {
      return this.State is PrintJobState.Completed or PrintJobState.Cancelled;
  }
  ```
- **Don't store a lambda in a field, property or local variable to stand in for a method.** Write a method, or a
  local function inside a method, with a full brace body, and pass it by name. Short lambdas passed straight to a
  call, such as `.Select(printer => printer.Name)`, are fine. So is the delegate a test's Act step hands to
  `Assert.Throws` (see [Arrange / Act / Assert](#arrange--act--assert)).

  ```csharp
  // ❌ Incorrect
  private static readonly Func<string, bool> SocketAlwaysExists = path => true;

  // ✅ Correct
  private static bool SocketAlwaysExists(string path)
  {
      return true;
  }
  ```

---

### Event Handlers

Template: `ObjectName_EventName`

```csharp
private void StatusTimer_Elapsed(object? sender, ElapsedEventArgs e)
```

---

### Error Handling

Use exceptions for exceptional cases only, not for routine program flow.

**Guidelines:**
- Pass a descriptive message into the constructor when throwing an exception. Messages are grammatically correct
  sentences ending with a period, and come from resources (see [Strings and Localisation](#strings-and-localisation)).
- Document every exception a member can throw, and what causes it, in its `///` comments.
- Use built-in exceptions where they fit:
  - `ArgumentException`, `ArgumentNullException`, `ArgumentOutOfRangeException`: invalid arguments.
  - `InvalidOperationException`: the call is invalid for the object's current state.
  - `NotSupportedException`: the operation is not supported by this printer or platform.
  - `PlatformNotSupportedException`: the API is not available on the current operating system.
- Use Latiprimere's own exception types for printing failures (for example, a `PrintJobException` carrying the
  platform error code), so callers can handle them consistently across platforms.
- **Always preserve native error detail.** Include the Win32 error code, IPP status code or CUPS error in the
  exception so issues can be diagnosed.
- Use `ArgumentNullException.ThrowIfNull(argument)` for null-argument guards.
- **Never swallow exceptions with an empty `catch` block.** At minimum, log the exception before continuing.
- **Rethrow with `throw;`**, never `throw ex;`, to preserve the call stack.

---

### Async / Await

All I/O-bound and long-running operations must be asynchronous.

**Guidelines:**
- **Always `await` tasks.** Never block with `.Result` or `.Wait()`.
- **Append `Async` to all async method names.**
- **Avoid `async void`.** All async methods return `Task`, `Task<T>`, `ValueTask`, `ValueTask<T>` or
  `IAsyncEnumerable<T>`.
- **Accept a `CancellationToken`** as the last parameter of every public async method, and pass it through. On
  public interfaces and their implementations it defaults to `default`, so callers without one can leave it out.
- **Use `ConfigureAwait(false)`** on every `await` in library code. Test projects are exempt: xUnit recommends
  against it in test methods.
- Blocking native calls (for example `WritePrinter`) must not be presented as async unless they genuinely run off the
  calling thread. Document which methods block.

---

### LINQ

**Guidelines:**
- Use **method syntax** consistently.
- **Never produce side effects inside LINQ queries.** Queries are pure transformations or filters.
- **Be mindful of deferred execution.** Materialise with `.ToList()` or `.ToArray()` when the result is iterated more
  than once or must be captured immediately.
- Avoid LINQ in hot paths such as page rendering loops.

---

### Nullable Reference Types

Nullable reference types are enabled in every project (`<Nullable>enable</Nullable>`).

**Guidelines:**
- Use `string?` and `string` deliberately. A non-nullable reference must never be assigned `null`.
- **Do not suppress warnings with the null-forgiving operator (`!`)** unless the value is verified non-null and the
  compiler cannot tell. Each use must have a comment explaining why it is safe.

---

### Pattern Matching and Record Types

**Guidelines:**
- **Prefer `switch` expressions** over `switch` statements when mapping a value to a result.
- **Use `is` patterns for type checks:** `if (job is IppPrintJob ippJob) { ... }`.
- **Use `record` types** for immutable data models (`PrinterInfo`, `PrintOptions`, `PrintCapabilities`).
  - Use `class` for mutable entities with identity and behaviour (`IppPrintJob`).
  - Use `struct` or `readonly record struct` for small value types (`MediaSize`).
- **Use `init`-only setters** for properties that should only be assigned during initialisation.

---

### Object and Collection Initialisation

Use object and collection initialisers when properties or items are set immediately after construction. An object
is then never observed in a partially-initialised state.

```csharp
// ❌ Without initialiser
PrintOptions options = new PrintOptions();
options.Copies = 2;
options.Duplex = DuplexMode.LongEdge;

// ✅ With initialiser
PrintOptions options = new PrintOptions
{
    Copies = 2,
    Duplex = DuplexMode.LongEdge
};
```

---

### Representing Boolean Values

Always use `bool` for true/false values in managed code, never a `string` or `char`. Convert native representations
(Win32 `BOOL`, IPP `boolean`) at the interop boundary.

---

### Strings and Localisation

- **Exception messages and any other text that can reach an end user** are held in `.resx` resources and retrieved
  through the generated resource class. English is required; other languages are welcome from native speakers.
  Do not add machine translations.
- **Resource classes come from a source generator.** A project with `.resx` files references
  `Microsoft.CodeAnalysis.ResxSourceGenerator` with `PrivateAssets="all"`. Put the files in a `Resources` folder.
  `Resources/ExceptionMessages.resx` then becomes the class `<Project>.Resources.ExceptionMessages`, visible in the
  editor without a build. Don't commit generated code ([ADR 0014](docs/decisions/0014-resx-source-generator.md)).
- **Format messages with their generated methods.** Each message with placeholders also gets a `Format…` method that
  formats it in the current culture. Call `ExceptionMessages.FormatPrinterNotFoundWithId(printerId)`, not
  `string.Format(CultureInfo.CurrentCulture, ExceptionMessages.PrinterNotFoundWithId, printerId)`. The root
  `Directory.Build.props` turns this on for every `.resx` file.
- **Protocol and platform strings** (IPP attribute names, MIME types, Win32 datatypes such as `RAW`) are not
  user-facing. Define them as named constants.
- **Log messages** use message templates (`"Job {JobId} completed."`), not interpolation, so structured logging works.

---

## SOLID Design Principles

All new code must follow the SOLID principles.

### Single Responsibility Principle (SRP)

*A class should have one, and only one, reason to change.*

Separate protocol handling, platform interop, mapping and public API surface into distinct classes.

```csharp
// ❌ Incorrect — one class doing too much
public class CupsPrinter { } // builds IPP requests, sends HTTP, parses attributes, maps capabilities

// ✅ Correct — responsibilities separated
internal sealed class IppRequestFactory     { } // builds requests only
internal sealed class IppTransport          { } // sends and receives only
internal sealed class IppCapabilityMapper   { } // maps attributes to PrintCapabilities only
```

---

### Open/Closed Principle (OCP)

*Classes should be open for extension but closed for modification.*

Design around abstractions so new behaviour (a new platform adapter, a new document format, a new rasteriser) can be
added without editing existing, tested code. Avoid `switch`/`if` chains that dispatch on type or platform; register
implementations instead.

```csharp
// ❌ Incorrect — every new format requires editing this method
public byte[] PrepareDocument(PrintContent content)
{
    if (content.Format == DocumentFormat.Pdf)   { ... }
    if (content.Format == DocumentFormat.Image) { ... }
}

// ✅ Correct — new formats are added as new implementations
public interface IDocumentPreparer
{
    bool CanPrepare(DocumentFormat format);

    Task<Stream> PrepareAsync(PrintContent content, CancellationToken cancellationToken);
}
```

---

### Liskov Substitution Principle (LSP)

*Derived types must be substitutable for their base types without altering correctness.*

- **Never throw `NotImplementedException`** from a published interface member. Use it only in stub code during active
  development, paired with a `// TODO:` linked to an issue.
- If a platform cannot support an operation, model that through capabilities or a separate interface rather than
  throwing `NotSupportedException` from a member every implementation is expected to support.

```csharp
// ✅ Hierarchy reflects actual capability
public interface IPrintJob
{
    Task<PrintJobStatus> GetStatusAsync(CancellationToken cancellationToken);
}

public interface ICancellablePrintJob : IPrintJob
{
    Task CancelAsync(CancellationToken cancellationToken);
}
```

---

### Interface Segregation Principle (ISP)

*Clients should not be forced to depend on interfaces they do not use.*

Prefer many small, focused interfaces. If an implementation needs to throw `NotSupportedException` or
`NotImplementedException` on any member, split the interface. Interfaces may compose smaller ones through inheritance.

---

### Dependency Inversion Principle (DIP)

*High-level modules should not depend on low-level modules. Both should depend on abstractions.*

Services are registered with `Microsoft.Extensions.DependencyInjection` and receive their dependencies through the
constructor. Latiprimere must also be usable without a DI container, so each package provides a simple factory for
callers who don't use one.

- **Never instantiate a service dependency with `new` inside a class body.**
- **Constructor parameters representing dependencies must be interface types.**
- **Do not introduce static service dependencies.** Static helpers are acceptable only for pure functions with no
  I/O (for example, unit conversion).

```csharp
// ❌ Incorrect — hard-coded concrete dependency
internal sealed class CupsPrinterCatalog
{
    private readonly IppTransport transport = new IppTransport();
}

// ✅ Correct — dependency injected as an abstraction
internal sealed class CupsPrinterCatalog : IPrinterCatalog
{
    private readonly IIppTransport transport; // Sends IPP requests to the CUPS scheduler

    public CupsPrinterCatalog(IIppTransport transport)
    {
        this.transport = transport;
    }
}
```

---

## Architecture

Latiprimere is split into packages with a strict, one-way dependency hierarchy. The full rationale is in
[docs/plan.md](docs/plan.md) and [docs/decisions/](docs/decisions/README.md).

### Packages and Layers

```
┌──────────────────────────────────────────────────────────────┐
│  Consumers — applications, samples, future UI add-ons        │
└──────────────────────────────┬───────────────────────────────┘
                               │ references ↓
┌──────────────────────────────▼───────────────────────────────┐
│  Latiprimere — services, platform selection, DI registration  │
└──────────────────────────────┬───────────────────────────────┘
                               │ references ↓
┌──────────────────────────────▼───────────────────────────────┐
│  Platform adapters (Latiprimere.*)                            │
│  .Windows · .Cups · .Ipp · .Rendering.Pdfium                 │
└──────────────────────────────┬───────────────────────────────┘
                               │ references ↓
┌──────────────────────────────▼───────────────────────────────┐
│  Latiprimere.Abstractions — interfaces, records, enums,       │
│  exception types. No implementations, no dependencies.       │
└──────────────────────────────────────────────────────────────┘
```

### Dependency Rule

- **`Latiprimere.Abstractions`** references nothing within the solution, and no third-party packages.
- **Platform adapters** reference `Latiprimere.Abstractions`, their own third-party dependencies, and
  `Microsoft.Extensions.Logging.Abstractions` and `Microsoft.Extensions.DependencyInjection.Abstractions`.
  Adapters don't reference each other, with one exception: **`Latiprimere.Ipp`** is both the direct IPP adapter and the
  shared IPP transport, so `Latiprimere.Cups` references it for its protocol code ([ADR 0006](docs/decisions/0006-ipp-and-cups-adapter-design.md)),
  and `Latiprimere.Windows` references it to send PDFs straight to a queue's IPP printer
  ([ADR 0027](docs/decisions/0027-direct-ipp-for-windows-queues.md)). Nothing references `Latiprimere.Cups` or
  `Latiprimere.Windows`, and `Latiprimere.Ipp` references no other adapter. Shared code goes in `Latiprimere.Ipp`'s
  internals (`InternalsVisibleTo`), not in a public API.
- **`Latiprimere`** references the abstractions and the adapters it selects between.
- No package references a higher layer. Third-party types (for example, SharpIppNext or PDFium types) must never
  appear in a public API.

### No UI Types in Core Packages

UI framework types (WinForms, WPF, Avalonia, MAUI) are **prohibited** in every package listed above, including in
interface signatures. Print dialogs and previews will live in separate, optional add-on packages.

---

## Platform-Specific Code

- Annotate platform-specific types and members with `[SupportedOSPlatform("windows")]`, `("linux")` or `("macos")`,
  and guard calls with `OperatingSystem.IsWindows()` (etc.) so the platform analyzer catches mistakes.
- Use `LibraryImport` (source-generated marshalling), never `DllImport`. Keep all native declarations in an
  `internal` `Interop` folder per package.
- Wrap native handles in `SafeHandle` subclasses; never pass raw `IntPtr` handles around managed code.
- Code must be trim-safe and Native AOT compatible: no reflection-based serialisation, no dynamic code generation.
  `IsAotCompatible` turns on the analyzers for `src/`, but they can't see into dependencies. CI also publishes the CLI
  sample and the end-to-end harness with Native AOT, and runs the native harness's scenarios listed at the top of
  `tools/aot-check/check-aot.sh`; run the script yourself after adding a dependency or a new code path through one,
  and add a scenario to its list when the new path has one.
  It needs the platform's native toolchain, as the script's header says. `tools/test-harness/run.ps1 -Aot` (or
  `run.sh --aot`) runs every scenario natively.
- Convert native errors into Latiprimere exceptions at the interop boundary, keeping the native code in the exception.

---

## Unit Tests

Unit tests are part of the definition of done for any change that adds or modifies behaviour.

> **Coverage goal:** every public method has at least one happy-path test. Cover both sides of every meaningful
> branch: `if`/`else`, null checks, empty collections, boundary values. Don't chase coverage percentages for their
> own sake; generated code, trivial properties and native declarations do not need tests.

Tests use **xUnit**.

---

### Class Naming

The test class name is the name of the class under test with `Tests` appended. One test class per source class.

```
IppCapabilityMapper    →  IppCapabilityMapperTests
WindowsJobStatusMapper →  WindowsJobStatusMapperTests
```

---

### Class Location

Each source project has a matching test project, and the folder structure inside it mirrors the source project.

| Source project | Test project |
|---|---|
| `src/Latiprimere.Abstractions` | `tests/Latiprimere.Abstractions.Tests` |
| `src/Latiprimere` | `tests/Latiprimere.Tests` |
| `src/Latiprimere.Windows` | `tests/Latiprimere.Windows.Tests` |
| `src/Latiprimere.Cups` | `tests/Latiprimere.Cups.Tests` |
| `src/Latiprimere.Ipp` | `tests/Latiprimere.Ipp.Tests` |

```
src/Latiprimere.Cups/
└── Mapping/
    └── IppCapabilityMapper.cs

tests/Latiprimere.Cups.Tests/
└── Mapping/
    ├── IppCapabilityMapperTests.cs
    ├── Builders/
    │   └── PrinterAttributesBuilder.cs
    └── Data/
        └── hp-envy-5640-attributes.json
```

---

### Method Naming

Test method names use **PascalCase with no underscores**, following a **GivenWhenThen** structure.

```csharp
// ✅ Correct
[Fact]
public void GivenDuplexSupportedAttributeWhenMapThenReturnsLongAndShortEdge() { ... }

[Fact]
public void GivenNullAttributesWhenMapThenThrowsArgumentNullException() { ... }

// ❌ Incorrect — underscores
public void Given_Duplex_When_Map_Then_Returns() { ... }

// ❌ Incorrect — vague
public void MapTest() { ... }
```

---

### Arrange / Act / Assert

Test bodies have three labelled sections, `// Arrange`, `// Act` and `// Assert`, separated by blank lines. No section
may be omitted.

```csharp
[Fact]
public void GivenTwoSidedLongEdgeWhenMapThenCapabilitiesIncludeLongEdgeDuplex()
{
    // Arrange
    IppPrinterAttributes attributes = new PrinterAttributesBuilder()
        .WithSidesSupported("one-sided", "two-sided-long-edge")
        .Build();
    IppCapabilityMapper mapper = new IppCapabilityMapper();

    // Act
    PrintCapabilities capabilities = mapper.Map(attributes);

    // Assert
    Assert.Contains(DuplexMode.LongEdge, capabilities.DuplexModes);
}
```

For `[Theory]` tests, annotate the `// Arrange` comment to explain what the theory data represents:

```csharp
[Theory]
[InlineData("")]
[InlineData("   ")]
public async Task GivenBlankPrinterIdWhenGetPrinterThenThrowsArgumentException(string printerId)
{
    // Arrange — printerId is empty or whitespace (provided by InlineData above)
    FakeIppTransport transport = new FakeIppTransport();
    CupsPrinterCatalog catalog = new CupsPrinterCatalog(transport);

    // Act
    Func<Task> act = () => catalog.GetPrinterAsync(printerId, CancellationToken.None);

    // Assert
    await Assert.ThrowsAsync<ArgumentException>(act);
}
```

---

### Supporting Files

Supporting classes live in named subfolders beneath the test class, never in the project root or mixed in with test
classes.

| Subfolder | Contents |
|---|---|
| `Builders/` | Test data builders with sensible defaults and a fluent API. |
| `Fakes/` | Hand-rolled in-memory implementations of interfaces, used instead of generated mocks. |
| `Fixtures/` | Shared xUnit `IClassFixture` implementations for expensive setup. |
| `Data/` | Static test data files: captured IPP attribute dumps, Windows capability XML, sample PDFs. |

When several test classes build the same object, give it a builder rather than repeating the set-up in each
class. When several test projects need the same fakes or builders, put them in a shared test-support library named
`*.Testing` (for example `tests/Latiprimere.Ipp.Testing`), using the same subfolders. These libraries are not test
projects, are never packed, and must not contain tests. `tests/Latiprimere.Testing` holds what every test project may
need: the shared test documents and the integration-test fixtures.

**Builder example:**

```csharp
// Builders/PrintOptionsBuilder.cs
internal sealed class PrintOptionsBuilder
{
    private int        copies    = 1;                  // Number of copies to request
    private DuplexMode duplex    = DuplexMode.OneSided; // Duplex mode to request
    private MediaSize  mediaSize = MediaSize.IsoA4;     // Paper size to request

    /// <summary>Sets the number of copies on the options being built.</summary>
    public PrintOptionsBuilder WithCopies(int copies)
    {
        this.copies = copies;

        return this;
    }

    /// <summary>Builds and returns the configured <see cref="PrintOptions"/>.</summary>
    public PrintOptions Build()
    {
        return new PrintOptions
        {
            Copies    = this.copies,
            Duplex    = this.duplex,
            MediaSize = this.mediaSize
        };
    }
}
```

**Fake example:**

```csharp
// Fakes/FakeIppTransport.cs
internal sealed class FakeIppTransport : IIppTransport
{
    private readonly List<IppRequest> sentRequests = new List<IppRequest>(); // Requests received, for assertions

    /// <summary>Gets the requests sent through this transport.</summary>
    public IReadOnlyList<IppRequest> SentRequests
    {
        get
        {
            return this.sentRequests;
        }
    }

    /// <summary>Records the request and returns a canned successful response.</summary>
    public Task<IppResponse> SendAsync(IppRequest request, CancellationToken cancellationToken)
    {
        this.sentRequests.Add(request);

        return Task.FromResult(IppResponse.SuccessfulOk);
    }
}
```

---

### Integration Tests and Real Printers

- Tests that need a print system (CUPS, the Windows spooler, a virtual IPP printer) are **integration tests**. Mark
  them with `[Trait("Category", "Integration")]` and the target OS, so unit-test runs stay fast and portable.
- Integration tests target **virtual printers only**: `ippeveprinter` or CUPS-PDF on Linux and macOS, and
  "Microsoft Print to PDF" or a Generic / Text Only file queue on Windows. Setup scripts live in `tools/`.
- Tests must never send output to a physical printer. The one exception is the test harness's manual mode
  (`manual --printer <id>`, see [docs/manual-tests](docs/manual-tests/README.md)): a person runs it, names the
  printer on its command line, and confirms each job at a prompt before anything prints
  ([ADR 0029](docs/decisions/0029-end-to-end-test-harness.md)). Scenarios that need a person declare
  `TargetNeeds.Person`, and nothing but manual mode provides one.
- Capture real-printer behaviour as data files (attribute dumps) and test the mapping code against those instead.
- End-to-end tests of a print system go in an `Integration/` folder of its test project, in a class named after the
  print system with `IntegrationTests` appended (for example `Integration/CupsPrintSystemIntegrationTests.cs`). They
  build the catalog through dependency injection, as applications do, with `CatalogFixture` from
  `tests/Latiprimere.Testing`.
- Tests that need virtual printers outside the test process are **skipped unless an environment variable says where
  they are**. Once a variable is set, a test that can't reach its printer fails rather than skips, so CI can't pass by
  skipping. `IntegrationEnvironment` in `tests/Latiprimere.Testing` reads them:

  | Variable | Value | Tests it switches on |
  |---|---|---|
  | `LATIPRIMERE_TEST_CUPS_SERVER` | The CUPS test container or a Mac's own CUPS, `127.0.0.1:631` | CUPS queues |
  | `LATIPRIMERE_TEST_CUPS_OUTPUT` | The container's `/output` on the host, or the Mac's capture folder | Byte-for-byte checks of raw jobs |
  | `LATIPRIMERE_TEST_CUPS_SETUP` | `container` (the default) or `macos` | On `macos`, skips the CUPS-PDF and Secure tests |
  | `LATIPRIMERE_TEST_IPP_PRINTER` | `ippeveprinter` in the container or on the Mac, `ipp://127.0.0.1:8631/ipp/print` | Direct IPP |
  | `LATIPRIMERE_TEST_WINDOWS_OUTPUT` | The output directory given to `Install-TestPrinters.ps1` | The Windows spooler test printers |
  | `LATIPRIMERE_TEST_E2E` | `1` | The end-to-end scenarios under xUnit (`tests/Latiprimere.EndToEnd.Tests`); CI leaves it unset |
  | `LATIPRIMERE_TEST_MDNS_NAME` | The name a printer announces over mDNS, such as `Virtual IPP Printer` | The end-to-end mDNS discovery scenario |

  Use `127.0.0.1` rather than `localhost`: the container publishes on IPv4 only, so each new connection to
  `localhost` first tries IPv6 and starts IPv4 only after the 250 ms Happy Eyeballs delay.
- To run them locally against the container (see `tools/virtual-printers/README.md`):

  ```bash
  export LATIPRIMERE_TEST_CUPS_OUTPUT="$PWD/out/cups-output"
  docker compose -f tools/virtual-printers/compose.yaml up -d --build --wait
  export LATIPRIMERE_TEST_CUPS_SERVER=127.0.0.1:631 LATIPRIMERE_TEST_IPP_PRINTER=ipp://127.0.0.1:8631/ipp/print
  dotnet test
  ```

- On a Mac, which has no Docker in CI, the same queues except CUPS-PDF and Secure can be set up on the Mac's own CUPS
  instead; see `tools/macos-test-printers/README.md`.
- Checks that span several packages, through the public API only, are **end-to-end scenarios** in
  `tests/Latiprimere.EndToEnd`, not integration tests. They run from `tools/test-harness/run.ps1` or `run.sh`, which
  start the virtual printers and write a report; see `tools/test-harness/README.md`. They don't run in CI.
- `dotnet test` runs each target framework's tests in its own process at the same time, so two processes print to
  the same virtual printer at once. Tests must allow for that: give each job distinct data, and on Windows hold a
  `PrinterLock` around jobs to printers that can't share (Microsoft Print to PDF, and file ports, which overwrite one
  file).

---

## Appendix A: Naming Parts & Pairs

### A.1 Common Adjective Pairs

| Pair |
|---|
| Old… / New… |
| Source… / Destination… |
| Source… / Target… |
| First… / Next… / Current… / Previous… / Last… |
| Min… / Max… |

### A.2 Common Property Prefixes

| Prefix |
|---|
| `Allow…` / `Allows…` |
| `Can…` |
| `Contains…` |
| `Has…` |
| `Is…` |
| `Use…` / `Uses…` |
| `Supports…` |

### A.3 Common Verb Pairs

| Pair | Pair |
|---|---|
| Add… / Remove… | Open… / Close… |
| Insert… / Delete… | Create… / Destroy… |
| Increment… / Decrement… | Acquire… / Release… |
| Lock… / Unlock… | Up… / Down… |
| Begin… / End… | Show… / Hide… |
| Fetch… / Store… | Start… / Stop… |
| Submit… / Cancel… | To… / From… *(Convert implied)* |

### A.4 Common Qualifier Suffixes

| Suffix | Suffix |
|---|---|
| …Avg | …Limit |
| …Count | …Ref |
| …Entry | …Sum |
| …Index | …Total |

> **Note:** Avoid `Num`, which is ambiguous; use `Index` or `Count` instead. Avoid `Temp`; use a descriptive name
> such as `swapValue`.
