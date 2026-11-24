using System.Runtime.InteropServices;

// PDFium comes with the application, from the bblanchon.PDFium packages: .NET finds it among the application's own
// native libraries first, then beside this assembly. Beyond those, only the folders Windows counts as safe are
// searched, never the current directory or the PATH, where another pdfium.dll could be planted (ADR 0058). Other
// platforms search the same way they did.
[assembly: DefaultDllImportSearchPaths(DllImportSearchPath.AssemblyDirectory | DllImportSearchPath.SafeDirectories)]
