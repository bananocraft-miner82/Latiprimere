using System.Runtime.InteropServices;

// Every library this assembly calls is part of Windows: gdi32.dll, winspool.drv, prntvpt.dll, ole32.dll and
// oleaut32.dll. Only System32 is searched for them, so a DLL of the same name in the application's folder, the
// current directory or on the PATH is never loaded in their place (ADR 0058). winspool.drv and prntvpt.dll aren't
// among the KnownDLLs Windows always takes from System32.
[assembly: DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
