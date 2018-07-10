using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

#if DEBUG
[assembly: AssemblyTitle("SenseNetTaskAgent (Debug)")]
#elif DIAGNOSTIC
[assembly: AssemblyTitle("SenseNetTaskAgent (Diagnostic)")]
#else
[assembly: AssemblyTitle("SenseNetTaskAgent (Release)")]
#endif
[assembly: AssemblyDescription("sensenet TaskManagement agent")]
[assembly: AssemblyConfiguration("")]
[assembly: AssemblyCompany("Sense/Net Inc.")]
[assembly: AssemblyCopyright("Copyright © Sense/Net Inc.")]
[assembly: AssemblyTrademark("Sense/Net Inc.")]
[assembly: AssemblyProduct("sensenet TaskManagement")]
[assembly: AssemblyCulture("")]

[assembly: ComVisible(false)]
[assembly: Guid("8497a5fc-d0da-457f-ae92-3a15feaee32e")]

[assembly: AssemblyVersion("1.2.0.0")]
[assembly: AssemblyFileVersion("1.2.0.0")]

[assembly: InternalsVisibleTo("SenseNet.TaskManagement.Tests")]
