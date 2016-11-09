using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

// General Information about an assembly is controlled through the following 
// set of attributes. Change these attribute values to modify the information
// associated with an assembly.
#if DEBUG
[assembly: AssemblyTitle("SenseNetTaskAgent (Debug)")]
#elif DIAGNOSTIC
[assembly: AssemblyTitle("SenseNetTaskAgent (Diagnostic)")]
#else
[assembly: AssemblyTitle("SenseNetTaskAgent (Release)")]
#endif
[assembly: AssemblyDescription("Sense/Net TaskManagement agent")]
[assembly: AssemblyConfiguration("")]
[assembly: AssemblyCompany("Sense/Net Inc.")]
[assembly: AssemblyCopyright("Copyright © Sense/Net Inc.")]
[assembly: AssemblyTrademark("Sense/Net Inc.")]
[assembly: AssemblyProduct("Sense/Net TaskManagement")]
[assembly: AssemblyCulture("")]

// Setting ComVisible to false makes the types in this assembly not visible 
// to COM components.  If you need to access a type in this assembly from 
// COM, set the ComVisible attribute to true on that type.
[assembly: ComVisible(false)]

// The following GUID is for the ID of the typelib if this project is exposed to COM
[assembly: Guid("8497a5fc-d0da-457f-ae92-3a15feaee32e")]

// Version information for an assembly consists of the following four values:
//
//      Major Version
//      Minor Version 
//      Build Number
//      Revision
//
// You can specify all the values or you can default the Build and Revision Numbers 
// by using the '*' as shown below:
[assembly: AssemblyVersion("1.1.0.0")]
[assembly: AssemblyFileVersion("1.1.0.0")]

[assembly: InternalsVisibleTo("SenseNet.TaskManagement.Tests")]
