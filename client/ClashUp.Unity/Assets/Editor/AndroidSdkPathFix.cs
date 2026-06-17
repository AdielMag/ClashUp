using UnityEditor;

/// Sdkmanager.bat uses findstr.exe (in System32) in its Java version check.
/// If System32 is missing from the process PATH, findstr fails → version string is empty
/// → check fails even though Java 17 is present → SDK detection returns 0.
/// Prepend System32 here so all child processes can find it.
[InitializeOnLoad]
static class AndroidSdkPathFix
{
    static AndroidSdkPathFix()
    {
        const string system32 = @"C:\Windows\System32";
        const string powershell = @"C:\Windows\System32\WindowsPowerShell\v1.0";
        const string windows = @"C:\Windows";

        var path = System.Environment.GetEnvironmentVariable("PATH") ?? "";
        if (!path.Contains(system32, System.StringComparison.OrdinalIgnoreCase))
        {
            System.Environment.SetEnvironmentVariable(
                "PATH",
                $"{system32};{powershell};{windows};{path}");
        }
    }
}
