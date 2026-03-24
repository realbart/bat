# STEP 14 - Console Resize Events

**Doel:** `IConsole` exposures een `Resized`-event zodat commands als DIR en het REPL-prompt
woordbreedte-gevoelig kunnen reageren op terminal resize zonder polling.

## Context

`IConsole` heeft al `WindowWidth` en `WindowHeight` (toegevoegd in stap 4/5). De waarden worden
nu echter eenmalig gelezen per gebruik. Wanneer de gebruiker het venster vergroot of verkleint
terwijl een commando loopt (bijv. DIR /P, of de promptregel) moet dat detecteerbaar zijn.

Platforms verschillen fundamenteel:

| Platform | Mechanisme |
|----------|-----------|
| Windows  | `ReadConsoleInput` met `WINDOW_BUFFER_SIZE_EVENT` (Win32 API) |
| Linux/macOS | `SIGWINCH`-signaal, afgevangen via `PosixSignalRegistration.Create(PosixSignal.SIGWINCH, ...)` |

## Gewenste interface

```csharp
internal interface IConsole
{
    TextWriter Out { get; }
    TextWriter Error { get; }
    TextReader In { get; }
    int WindowWidth { get; }
    int WindowHeight { get; }

    /// Fired when the terminal window is resized.
    event EventHandler<ConsoleSizeChangedEventArgs>? Resized;
}

internal sealed class ConsoleSizeChangedEventArgs(int width, int height) : EventArgs
{
    public int Width { get; } = width;
    public int Height { get; } = height;
}
```

## Implementaties

### WindowsConsole

- Spawn een `Thread` (of `Task`) die `ReadConsoleInput` aanroept (P/Invoke: `kernel32.dll`).
- Filter op `WINDOW_BUFFER_SIZE_EVENT`, lees `dwSize.X`/`dwSize.Y`.
- Raise `Resized` op de thread die subscribed (of via `SynchronizationContext`).

```csharp
[StructLayout(LayoutKind.Sequential)]
private struct COORD { public short X, Y; }

[StructLayout(LayoutKind.Sequential)]
private struct WINDOW_BUFFER_SIZE_RECORD { public COORD dwSize; }

// ... ReadConsoleInput loop
```

### UnixConsole

```csharp
PosixSignalRegistration.Create(PosixSignal.SIGWINCH, _ =>
{
    Resized?.Invoke(this, new ConsoleSizeChangedEventArgs(
        System.Console.WindowWidth, System.Console.WindowHeight));
});
```

### TestConsole

```csharp
public int WindowWidth { get; set; } = 80;
public int WindowHeight { get; set; } = 24;
public event EventHandler<ConsoleSizeChangedEventArgs>? Resized;

// Helper voor tests:
public void SimulateResize(int width, int height)
{
    WindowWidth = width;
    WindowHeight = height;
    Resized?.Invoke(this, new ConsoleSizeChangedEventArgs(width, height));
}
```

## Gebruikers

- **DIR /P**: pagineert elke `WindowHeight - 2` regels.
- **DIR /W**: kolombreedte aanpassen aan `WindowWidth`.
- **REPL-prompt**: na resize kan de promptregel opnieuw getekend worden
  (relevant zodanig character-for-character input geïmplementeerd is in stap X).

## Volgorde

Implementeer pas als één van de bovenstaande gebruikers er reëel baat bij heeft.
`WindowWidth`/`WindowHeight` zijn al leesbaar; dit voegt alleen het event toe.
