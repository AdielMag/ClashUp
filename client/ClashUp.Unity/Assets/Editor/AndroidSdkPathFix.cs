using UnityEditor;
using System.Runtime.InteropServices;
using System.Text;

/// Sdkmanager.bat uses findstr.exe (in System32) inside a FOR /F pipe.
/// System.Environment.SetEnvironmentVariable only updates .NET's cached view —
/// the Win32 process environment block (inherited by child processes) requires
/// a direct kernel32 call. Also: the Contains() guard must do exact segment
/// matching, not substring — System32\OpenSSH contains "System32" as a prefix
/// and would falsely suppress the fix.
[InitializeOnLoad]
static class AndroidSdkPathFix
{
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern bool SetEnvironmentVariable(string name, string value);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern uint GetEnvironmentVariable(string name, StringBuilder buffer, uint size);

    const string System32   = @"C:\Windows\System32";
    const string PowerShell = @"C:\Windows\System32\WindowsPowerShell\v1.0";
    const string Windows    = @"C:\Windows";

    static AndroidSdkPathFix()
    {
        var buf = new StringBuilder(32767);
        GetEnvironmentVariable("PATH", buf, (uint)buf.Capacity);
        var realPath = buf.ToString();

        bool hasSystem32 = System.Array.Exists(
            realPath.Split(';'),
            p => p.TrimEnd('\\').Equals(System32.TrimEnd('\\'), System.StringComparison.OrdinalIgnoreCase));

        if (!hasSystem32)
        {
            var newPath = $"{System32};{PowerShell};{Windows};{realPath}";
            SetEnvironmentVariable("PATH", newPath);
            System.Environment.SetEnvironmentVariable("PATH", newPath);
            UnityEngine.Debug.Log("[AndroidSdkPathFix] Prepended System32 to Win32 PATH");
        }
        else
        {
            UnityEngine.Debug.Log("[AndroidSdkPathFix] System32 already present, no change");
        }
    }
}
