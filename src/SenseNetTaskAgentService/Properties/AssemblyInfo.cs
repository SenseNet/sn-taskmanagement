using System.Reflection;
using System.Runtime.InteropServices;

#if DEBUG
[assembly: AssemblyTitle("SenseNetTaskAgentService (Debug)")]
#elif DIAGNOSTIC
[assembly: AssemblyTitle("SenseNetTaskAgentService (Diagnostic)")]
#else
[assembly: AssemblyTitle("SenseNetTaskAgentService (Release)")]
#endif
[assembly: AssemblyDescription("sensenet TaskManagement service")]
[assembly: AssemblyConfiguration("")]
[assembly: AssemblyCompany("Sense/Net Inc.")]
[assembly: AssemblyCopyright("Copyright © Sense/Net Inc.")]
[assembly: AssemblyTrademark("Sense/Net Inc.")]
[assembly: AssemblyProduct("sensenet TaskManagement")]
[assembly: AssemblyCulture("")]

[assembly: ComVisible(false)]
[assembly: Guid("422c729c-40b6-41cf-ace8-34fe1a293352")]

[assembly: AssemblyVersion("1.2.0.0")]
[assembly: AssemblyFileVersion("1.2.0.0")]
