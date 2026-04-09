# Stap 14: Error Handling voor Satellietapplicaties

## Doel

Satellietapplicaties (TREE, SUBST, XCOPY, DOSKEY, etc.) moeten fouten eenduidig kunnen communiceren naar Bat, zodat onbekende switches en andere fouten correct worden afgehandeld.

## Achtergrond

Momenteel gebruikt `DotNetLibraryExecutor` altijd `ArgumentSpec.Empty` bij het aanroepen van satellietapplicaties. Dit betekent:
- Alle single-character switches worden geaccepteerd zonder validatie
- Satellietapplicaties kunnen geen error geven voor onbekende switches
- Er is geen gestandaardiseerde manier voor error reporting

De echte CMD utilities (zoals `tree.com`) geven wel errors voor onbekende switches: "Invalid switch - /x"

## Scope

### 1. IArgumentSet interface uitbreiden

Voeg `ErrorMessage` property toe aan `IArgumentSet`:
```csharp
public interface IArgumentSet
{
    string? ErrorMessage { get; }
    // ... bestaande members
}
```

### 2. ArgumentSpec definiëren per satellietapplicatie

Elke satellietapplicatie definieert zijn eigen ArgumentSpec:

**Tree:**
```csharp
private static readonly ArgumentSpec TreeSpec = new(
    Flags: ["A", "D", "E", "F"],
    Options: []
);
```

**Subst:**
```csharp
private static readonly ArgumentSpec SubstSpec = new(
    Flags: ["D"],
    Options: []
);
```

### 3. DotNetLibraryExecutor aanpassen

Optie A: Conventie-gebaseerd (zoek static ArgumentSpec field)
```csharp
var specField = assembly.GetTypes()
    .SelectMany(t => t.GetFields(BindingFlags.Public | BindingFlags.Static))
    .FirstOrDefault(f => f.FieldType == typeof(ArgumentSpec) && f.Name == "Spec");
var spec = specField?.GetValue(null) as ArgumentSpec ?? ArgumentSpec.Empty;
var args = Commands.ArgumentSet.Parse(tokens, spec);
```

Optie B: Tweede Main overload
```csharp
// Zoek Main(IContext, IArgumentSet, ArgumentSpec)
// Als gevonden, gebruik die; anders fallback naar Main(IContext, IArgumentSet)
```

### 4. Error handling in satellietapplicaties

Elke satellietapplicatie checkt ErrorMessage:
```csharp
public static async Task<int> Main(IContext context, IArgumentSet args)
{
    if (args.IsHelpRequest) { /* ... */ return 0; }
    
    if (args.ErrorMessage != null)
    {
        await System.Console.Error.WriteLineAsync(args.ErrorMessage);
        return 1;
    }
    
    // ... rest van implementatie
}
```

## Test strategie

1. **Unit tests voor ArgumentSpec matching**
   - Test dat bekende flags worden geaccepteerd
   - Test dat onbekende flags ErrorMessage genereren

2. **Integration tests voor satellietapplicaties**
   - `tree /f` → succesvol (exit 0)
   - `tree /x` → "Invalid switch - /x" op stderr, exit 1
   - `subst /d Q:` → succesvol
   - `subst /invalid` → error

3. **Backwards compatibility**
   - Satellietapplicaties zonder ArgumentSpec werken nog steeds (ArgumentSpec.Empty fallback)

## Acceptance criteria

- ✅ `tree /invalidswitch` toont "Invalid switch - invalidswitch" op stderr en exit code 1
- ✅ `tree /f` werkt normaal (exit 0)
- ✅ `subst X: /wrongflag` toont error op stderr
- ✅ Alle satellietapplicaties gebruiken dezelfde error reporting
- ✅ Bestaande tests blijven slagen

## Implementatie volgorde

1. Extend IArgumentSet met ErrorMessage property
2. Update ArgumentSet implementatie
3. Update DotNetLibraryExecutor om ArgumentSpec te detecteren
4. Voeg ArgumentSpec toe aan Tree
5. Voeg ArgumentSpec toe aan Subst  
6. Test en verifieer
7. Update overige satellietapplicaties (Xcopy, Doskey) wanneer die worden geïmplementeerd

## Referenties

- CMD error messages: Test met `tree /?`, `subst /?`, etc.
- ArgumentSet implementatie: `Bat/Commands/ArgumentSet.cs`
- DotNetLibraryExecutor: `Bat/Execution/DotNetLibraryExecutor.cs`
