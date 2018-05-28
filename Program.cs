using System;
using Microsoft.Win32;
using System.Runtime.InteropServices;
using System.Security;
using System.IO;
using System.Security.Principal;

class Program
{
	[DllImport("advapi32.dll", CharSet = CharSet.Auto, SetLastError = true)]
	static extern IntPtr OpenSCManager(string machineName, string databaseName, uint dwAccess);

	[DllImport("advapi32.dll", CharSet = CharSet.Auto, SetLastError = true)]
	static extern IntPtr OpenService(IntPtr hSCManager, string lpServiceName, uint dwDesiredAccess);

	[DllImport("advapi32.dll", CharSet = CharSet.Auto, SetLastError = true)]
	static extern Boolean ChangeServiceConfig(
		IntPtr hService,
		UInt32 nServiceType,
		UInt32 nStartType,
		UInt32 nErrorControl,
		IntPtr lpBinaryPathName,
		IntPtr lpLoadOrderGroup,
		IntPtr lpdwTagId,
		IntPtr lpDependencies,
		IntPtr lpServiceStartName,
		IntPtr lpPassword,
		IntPtr lpDisplayName);

	[DllImport("advapi32.dll", CharSet=CharSet.Unicode, SetLastError=true)] 
	static extern bool QueryServiceConfig(
		IntPtr hService,
		IntPtr lpQueryConfig,
		UInt32 cbBufSize,
		out UInt32 pcbBytesNeeded); 

	[DllImport("advapi32.dll", EntryPoint = "CloseServiceHandle")]
	static extern int CloseServiceHandle(IntPtr hSCObject);

	[StructLayout(LayoutKind.Sequential)] 
	public struct QUERY_SERVICE_CONFIG 
	{ 
		[MarshalAs(System.Runtime.InteropServices.UnmanagedType.U4)] 
		public UInt32 dwServiceType;

		[MarshalAs(System.Runtime.InteropServices.UnmanagedType.U4)] 
		public UInt32 dwStartType;

		[MarshalAs(System.Runtime.InteropServices.UnmanagedType.U4)] 
		public UInt32 dwErrorControl;

		[MarshalAs(System.Runtime.InteropServices.UnmanagedType.LPWStr)] 
		public String lpBinaryPathName;

		[MarshalAs(System.Runtime.InteropServices.UnmanagedType.LPWStr)] 
		public String lpLoadOrderGroup;

		[MarshalAs(System.Runtime.InteropServices.UnmanagedType.U4)] 
		public UInt32 dwTagID;

		[MarshalAs(System.Runtime.InteropServices.UnmanagedType.LPWStr)] 
		public String lpDependencies;

		[MarshalAs(System.Runtime.InteropServices.UnmanagedType.LPWStr)] 
		public String lpServiceStartName;

		[MarshalAs(System.Runtime.InteropServices.UnmanagedType.LPWStr)] 
		public String lpDisplayName; 
	}

	[StructLayout(LayoutKind.Sequential, Pack = 0)]
	public struct SERVICE_STATUS 
	{
		public int dwServiceType;
		public int dwCurrentState;  
		public uint dwControlsAccepted;  
		public uint dwWin32ExitCode;  
		public uint dwServiceSpecificExitCode;  
		public uint dwCheckPoint;  
		public uint dwWaitHint;
	}

	[DllImport("advapi32.dll", SetLastError=true)]
	[return: MarshalAs(UnmanagedType.Bool)]
	static extern bool ControlService(IntPtr hService, int dwControl, ref SERVICE_STATUS lpServiceStatus);

	static void AnyKey()
	{
		Console.WriteLine(@"Press any key to continue...");
		Console.ReadKey();
		Environment.Exit(0);
	}

	static void Main()
	{
		if (!(new WindowsPrincipal(WindowsIdentity.GetCurrent())).IsInRole(WindowsBuiltInRole.Administrator)) {
			Console.WriteLine(@"Please run as admin");
			AnyKey();
		}

		Console.WriteLine("Welcome, getting rid of terrible sound...");

		Console.WriteLine(@"HKLM\SOFTWARE\Dolby\DAX\DolbyEnable -> 0");

		try {
			Registry.SetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Dolby\DAX", "DolbyEnable", 0, RegistryValueKind.DWord);
		} catch (ArgumentNullException ex) {
			Console.WriteLine(@"No Dolby DAX found! Hooray (1)");
			AnyKey();
		} catch (ArgumentException ex) {
			Console.WriteLine(@"No Dolby DAX found! Hooray (2)");
			AnyKey();
		} catch (UnauthorizedAccessException ex) {
			Console.WriteLine(@"Please run as admin (1)");
			AnyKey();
		} catch (SecurityException ex) {
			Console.WriteLine(@"Please run as admin (2)");
			AnyKey();
		}

		var hSCM = OpenSCManager(null, null, 0x1 | 0x2 | 0x4 | 0x20);
        var hSVC = OpenService(hSCM, "DAX2API", 0x1 | 0x2 | 0x4 | 0x20);

		if (hSVC == IntPtr.Zero) {
			Console.WriteLine(@"No Dolby DAX service found! Hooray (3)");
			AnyKey();
		}

		var lpSvcConfig = Marshal.AllocHGlobal(8 * 1024);
		uint trash;

		if (!QueryServiceConfig(hSVC, lpSvcConfig, 8 * 1024, out trash)) {
			Marshal.FreeHGlobal(lpSvcConfig);
			Console.WriteLine(@"QuerySericeConfig failed");
			AnyKey();
		}

		var svcConfig = (QUERY_SERVICE_CONFIG)Marshal.PtrToStructure(lpSvcConfig, typeof(QUERY_SERVICE_CONFIG));

		Marshal.FreeHGlobal(lpSvcConfig);

		var res = ChangeServiceConfig(
			hSVC,
			svcConfig.dwServiceType,
			0x4,
			svcConfig.dwErrorControl,
			IntPtr.Zero,
			IntPtr.Zero,
			IntPtr.Zero,
			IntPtr.Zero,
			IntPtr.Zero,
			IntPtr.Zero,
			IntPtr.Zero);

		if (!res) {
			Console.WriteLine(@"ChangeServiceConfig failed");
			AnyKey();
		}

		Console.WriteLine(@"DAX2API -> Disabled");

		var status = new SERVICE_STATUS();
		if (!ControlService(hSVC, 0x1, ref status)) {
			Console.WriteLine(@"ControlService failed");
			AnyKey();
		}

		Console.WriteLine(@"DAX2API -> Stopped");

		CloseServiceHandle(hSVC);
		CloseServiceHandle(hSCM);

		if (!File.Exists(@"C:\Program Files\Dolby\Dolby DAX2\DAX2_API\DolbyDAX2API.exe")) {
			Console.WriteLine(@"No Dolby DAX API file found! Hooray (4)");
			AnyKey();
		}

		if (File.Exists(@"C:\Program Files\Dolby\Dolby DAX2\DAX2_API\DolbyDAX2API.exe_")) {
			Console.WriteLine(@"DolbyDAX2API.exe already NULed...");
		} else {
			File.Move(@"C:\Program Files\Dolby\Dolby DAX2\DAX2_API\DolbyDAX2API.exe", @"C:\Program Files\Dolby\Dolby DAX2\DAX2_API\DolbyDAX2API.exe_");
			File.WriteAllText(@"C:\Program Files\Dolby\Dolby DAX2\DAX2_API\DolbyDAX2API.exe", "NUL");

			Console.WriteLine(@"C:\Program Files\Dolby\Dolby DAX2\DAX2_API\DolbyDAX2API.exe -> NUL");
		}

		Console.WriteLine(@"Enjoy normal sound!");
		AnyKey();
	}
}