namespace Bat.Context;

#if WINDOWS && UNIX
#error Build configuration invalid: both WINDOWS and UNIX are defined.
#endif

#if !WINDOWS && !UNIX
#error Build configuration invalid: define either WINDOWS or UNIX.
#endif

internal static partial class ContextFactory;
