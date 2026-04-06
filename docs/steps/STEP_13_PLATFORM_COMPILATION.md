# Stap 13: Platform-specifieke compilatie

## Doel

Windows-only code mag niet aanwezig zijn in Unix-binaries en vice versa.  
Alle runtime OS-checks buiten `ContextFactory` worden vervangen door compile-time `#if` guards.  
Debug-builds (geen RID) compileren beide paden zodat ontwikkeling op elk OS mogelijk blijft.

## Achtergrond

De abstractielaag `IContext` / `IFileSystem` zorgt er al voor dat platform-specifieke code **nooit aangeroepen** wordt op het verkeerde OS. Maar twee problemen blijven:

1. De klassen worden wél meegecompileerd (`DosFileSystem` in Linux-binary, `UxFileSystemAdapter` in Windows-binary).
2. Op meerdere plekken in de code staan runtime OS-checks (`Path.DirectorySeparatorChar`, `OperatingSystem.IsWindows()`, enz.) die eigenlijk compile-time beslissingen zijn.

De copilot-instructies schrijven voor dat OS-detectie **uitsluitend in `ContextFactory`** mag staan. Na deze stap is dat ook structureel afgedwongen.

## Aanpak

### 1. MSBuild-constanten instellen

Voeg toe aan `Bat/Bat.csproj`:

```xml
<PropertyGroup Condition="$(RuntimeIdentifier.StartsWith('win'))">
    <DefineConstants>$(DefineConstants);WINDOWS</DefineConstants>
</PropertyGroup>
<PropertyGroup Condition="$(RuntimeIdentifier.StartsWith('linux')) OR $(RuntimeIdentifier.StartsWith('osx'))">
    <DefineConstants>$(DefineConstants);UNIX</DefineConstants>
</PropertyGroup>
```

### 2. Platform-specifieke bestanden uitsluiten

```xml
<ItemGroup Condition="'$(RuntimeIdentifier)' != '' AND !$(RuntimeIdentifier.StartsWith('win'))">
    <Compile Remove="Context\DosFileSystem.cs" />
    <Compile Remove="Context\DosContext.cs" />
    <Compile Remove="Context\DosPath.cs" />
</ItemGroup>

<ItemGroup Condition="'$(RuntimeIdentifier)' != '' AND $(RuntimeIdentifier.StartsWith('win'))">
    <Compile Remove="Context\UxFileSystemAdapter.cs" />
    <Compile Remove="Context\UxContextAdapter.cs" />
    <Compile Remove="Context\UnixFileOwner.cs" />
</ItemGroup>
```

De conditie `'$(RuntimeIdentifier)' != ''` zorgt dat debug-builds (geen RID) beide paden compileren.

### 3. ContextFactory opsplitsen

`ContextFactory.cs` bevat nu `OperatingSystem.IsWindows()` — dat mag alleen in platform-specifieke bestanden staan.

Splits op in drie bestanden:

**`ContextFactory.cs`** — gedeelde interface, geen OS-detectie:
```csharp
namespace Bat.Context;

internal static partial class ContextFactory
{
    public static IContext Create(BatArguments args) => CreatePlatformContext(args);
}
```

**`ContextFactory.Windows.cs`** — alleen in Windows-builds:
```csharp
#if WINDOWS || !UNIX
namespace Bat.Context;

internal static partial class ContextFactory
{
    private static IContext CreatePlatformContext(BatArguments args) => new DosContext(args);
}
#endif
```

**`ContextFactory.Unix.cs`** — alleen in Unix-builds:
```csharp
#if UNIX
namespace Bat.Context;

internal static partial class ContextFactory
{
    private static IContext CreatePlatformContext(BatArguments args) => new UxContextAdapter(args);
}
#endif
```

De `#if WINDOWS || !UNIX` guard op de Windows-partial zorgt dat debug-builds (geen RID, dus geen constante gezet) de Windows-implementatie gebruiken — consistent met huidig gedrag op Windows-ontwikkelmachines.

### 4. Runtime OS-checks vervangen door compile-time guards

Zoek alle voorkomens van de volgende patronen buiten `ContextFactory` op en vervang ze:

| Runtime check | Vervangen door |
|---|---|
| `OperatingSystem.IsWindows()` | `#if WINDOWS` |
| `OperatingSystem.IsLinux()` / `OperatingSystem.IsMacOS()` | `#if UNIX` |
| `RuntimeInformation.IsOSPlatform(OSPlatform.Windows)` | `#if WINDOWS` |
| `Path.DirectorySeparatorChar` | `'\\'` in `#if WINDOWS`, `'/'` in `#if UNIX` |
| `Path.PathSeparator` | `';'` in `#if WINDOWS`, `':'` in `#if UNIX` |

Bestanden die typisch deze patronen bevatten: `DosPath.cs`, `UxContextAdapter.cs`, `PathTranslator.cs`.  
Na stap 2 staan de platform-specifieke bestanden toch al uitsluitend in de juiste binary — gebruik de `#if` guards alleen waar code in een **gedeeld** bestand toch een platformverschil moet afhandelen.

## TDD

Er zijn geen gedragswijzigingen, dus geen nieuwe tests nodig. Verificatie:

1. `dotnet build` (geen RID) → bouwt succesvol, alle bestaande tests slagen
2. `dotnet publish -r win-x64` → compileert; controleer met `strings bat.exe | grep UxFileSystem` → geen hit
3. `dotnet publish -r linux-x64` → compileert; controleer met `strings bat | grep DosFileSystem` → geen hit
4. Grep op `OperatingSystem.Is`, `Path.DirectorySeparatorChar`, `Path.PathSeparator`, `RuntimeInformation` buiten `ContextFactory.*` → geen hits

## Acceptance criteria

- [ ] `dotnet build` zonder RID slaagt; alle bestaande tests slagen
- [ ] `dotnet publish -r win-x64` compileert zonder `UxFileSystemAdapter`, `UxContextAdapter`, `UnixFileOwner`
- [ ] `dotnet publish -r linux-x64` compileert zonder `DosFileSystem`, `DosContext`, `DosPath`
- [ ] `dotnet publish -r osx-arm64` compileert zonder `DosFileSystem`, `DosContext`, `DosPath`
- [ ] `ContextFactory.cs` (gedeeld bestand) bevat geen OS-detectie
- [ ] Geen `OperatingSystem.*`, `Path.DirectorySeparatorChar`, `Path.PathSeparator` of `RuntimeInformation` buiten `ContextFactory.Windows.cs` / `ContextFactory.Unix.cs`

## Referenties

- Copilot-instructies: OS-detectie uitsluitend toegestaan in `ContextFactory`
- MSBuild conditional compilation: https://learn.microsoft.com/visualstudio/msbuild/msbuild-conditions
- .NET RID catalog: https://learn.microsoft.com/dotnet/core/rid-catalog


## Aanpak

### 1. MSBuild-constanten instellen

Voeg toe aan `Bat/Bat.csproj`:

```xml
<PropertyGroup Condition="$(RuntimeIdentifier.StartsWith('win'))">
    <DefineConstants>$(DefineConstants);WINDOWS</DefineConstants>
</PropertyGroup>
<PropertyGroup Condition="$(RuntimeIdentifier.StartsWith('linux')) OR $(RuntimeIdentifier.StartsWith('osx'))">
    <DefineConstants>$(DefineConstants);UNIX</DefineConstants>
</PropertyGroup>
```

### 2. Platform-specifieke bestanden uitsluiten

```xml
<ItemGroup Condition="'$(RuntimeIdentifier)' != '' AND !$(RuntimeIdentifier.StartsWith('win'))">
    <Compile Remove="Context\DosFileSystem.cs" />
    <Compile Remove="Context\DosContext.cs" />
    <Compile Remove="Context\DosPath.cs" />
</ItemGroup>

<ItemGroup Condition="'$(RuntimeIdentifier)' != '' AND $(RuntimeIdentifier.StartsWith('win'))">
    <Compile Remove="Context\UxFileSystemAdapter.cs" />
    <Compile Remove="Context\UxContextAdapter.cs" />
    <Compile Remove="Context\UnixFileOwner.cs" />
</ItemGroup>
```

De conditie `'$(RuntimeIdentifier)' != ''` zorgt dat debug-builds (geen RID) beide paden compileren.

### 3. ContextFactory opsplitsen

`ContextFactory.cs` bevat nu `OperatingSystem.IsWindows()` — dat mag alleen in platform-specifieke bestanden staan.

Splits op in drie bestanden:

**`ContextFactory.cs`** — gedeelde interface, geen OS-detectie:
```csharp
namespace Bat.Context;

internal static partial class ContextFactory
{
    public static IContext Create(BatArguments args) => CreatePlatformContext(args);
}
```

**`ContextFactory.Windows.cs`** — alleen in Windows-builds:
```csharp
#if WINDOWS || !UNIX
namespace Bat.Context;

internal static partial class ContextFactory
{
    private static IContext CreatePlatformContext(BatArguments args) => new DosContext(args);
}
#endif
```

**`ContextFactory.Unix.cs`** — alleen in Unix-builds:
```csharp
#if UNIX
namespace Bat.Context;

internal static partial class ContextFactory
{
    private static IContext CreatePlatformContext(BatArguments args) => new UxContextAdapter(args);
}
#endif
```

De `#if WINDOWS || !UNIX` guard op de Windows-partial zorgt dat debug-builds (geen RID, dus geen constante gezet) de Windows-implementatie gebruiken — consistent met huidig gedrag op Windows-ontwikkelmachines.

## TDD

Er zijn geen gedragswijzigingen, dus geen nieuwe tests nodig. Verificatie:

1. `dotnet build` (geen RID) → bouwt succesvol, alle bestaande tests slagen
2. `dotnet publish -r win-x64` → compileert; controleer met `strings bat.exe | grep UxFileSystem` → geen hit
3. `dotnet publish -r linux-x64` → compileert; controleer met `strings bat | grep DosFileSystem` → geen hit

## Acceptance criteria

- [ ] `dotnet build` zonder RID slaagt; alle tests slagen
- [ ] `dotnet publish -r win-x64` compileert zonder `UxFileSystemAdapter`, `UxContextAdapter`, `UnixFileOwner`
- [ ] `dotnet publish -r linux-x64` compileert zonder `DosFileSystem`, `DosContext`, `DosPath`
- [ ] `dotnet publish -r osx-arm64` compileert zonder `DosFileSystem`, `DosContext`, `DosPath`
- [ ] `ContextFactory` bevat geen `OperatingSystem.IsWindows()` meer in het gedeelde bestand

## Referenties

- Copilot-instructies: OS-detectie uitsluitend toegestaan in `ContextFactory`
- MSBuild conditional compilation: https://learn.microsoft.com/visualstudio/msbuild/msbuild-conditions
- .NET RID catalog: https://learn.microsoft.com/dotnet/core/rid-catalog
