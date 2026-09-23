using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;

namespace OrchestratorConnector.Services;

/// <summary>
/// Stores the Client Secret for each preset in Windows Credential Manager (target name
/// "OrchestratorConnector:{presetId}") instead of in presets.json. Hand-rolled P/Invoke
/// over Advapi32 rather than a third-party NuGet package, since the surface needed is
/// exactly three operations on a single generic credential.
/// </summary>
public static class CredentialService
{
    private const int CredTypeGeneric = 1;
    private const int CredPersistLocalMachine = 2;
    private const int ErrorNotFound = 1168;

    public static void SaveSecret(Guid presetId, string secret)
    {
        var targetName = BuildTargetName(presetId);
        var secretBytes = Encoding.Unicode.GetBytes(secret);
        var blobPtr = Marshal.AllocHGlobal(Math.Max(secretBytes.Length, 1));

        try
        {
            if (secretBytes.Length > 0)
            {
                Marshal.Copy(secretBytes, 0, blobPtr, secretBytes.Length);
            }

            var credential = new NativeMethods.CREDENTIAL
            {
                Type = CredTypeGeneric,
                TargetName = targetName,
                CredentialBlobSize = secretBytes.Length,
                CredentialBlob = blobPtr,
                Persist = CredPersistLocalMachine,
                UserName = "OrchestratorConnector",
            };

            if (!NativeMethods.CredWrite(ref credential, 0))
            {
                throw new InvalidOperationException(
                    $"Failed to save the credential for this preset (Win32 error {Marshal.GetLastWin32Error()}).");
            }
        }
        finally
        {
            Marshal.FreeHGlobal(blobPtr);
        }
    }

    public static string? TryReadSecret(Guid presetId)
    {
        var targetName = BuildTargetName(presetId);

        if (!NativeMethods.CredRead(targetName, CredTypeGeneric, 0, out var credentialPtr))
        {
            var error = Marshal.GetLastWin32Error();
            if (error == ErrorNotFound)
            {
                return null;
            }

            throw new InvalidOperationException($"Failed to read the stored credential (Win32 error {error}).");
        }

        try
        {
            var credential = Marshal.PtrToStructure<NativeMethods.CREDENTIAL>(credentialPtr);
            if (credential.CredentialBlob == IntPtr.Zero || credential.CredentialBlobSize == 0)
            {
                return string.Empty;
            }

            var bytes = new byte[credential.CredentialBlobSize];
            Marshal.Copy(credential.CredentialBlob, bytes, 0, credential.CredentialBlobSize);
            return Encoding.Unicode.GetString(bytes);
        }
        finally
        {
            NativeMethods.CredFree(credentialPtr);
        }
    }

    public static void DeleteSecret(Guid presetId)
    {
        var targetName = BuildTargetName(presetId);

        if (!NativeMethods.CredDelete(targetName, CredTypeGeneric, 0))
        {
            var error = Marshal.GetLastWin32Error();
            if (error != ErrorNotFound)
            {
                throw new InvalidOperationException($"Failed to delete the stored credential (Win32 error {error}).");
            }
        }
    }

    private static string BuildTargetName(Guid presetId) => $"OrchestratorConnector:{presetId:D}";

    private static class NativeMethods
    {
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct CREDENTIAL
        {
            public int Flags;
            public int Type;
            public string TargetName;
            public string? Comment;
            public System.Runtime.InteropServices.ComTypes.FILETIME LastWritten;
            public int CredentialBlobSize;
            public IntPtr CredentialBlob;
            public int Persist;
            public int AttributeCount;
            public IntPtr Attributes;
            public string? TargetAlias;
            public string? UserName;
        }

        [DllImport("advapi32.dll", EntryPoint = "CredWriteW", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern bool CredWrite(ref CREDENTIAL credential, int flags);

        [DllImport("advapi32.dll", EntryPoint = "CredReadW", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern bool CredRead(string target, int type, int reservedFlag, out IntPtr credentialPtr);

        [DllImport("advapi32.dll", EntryPoint = "CredDeleteW", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern bool CredDelete(string target, int type, int flags);

        [DllImport("advapi32.dll", EntryPoint = "CredFree", SetLastError = true)]
        public static extern void CredFree(IntPtr buffer);
    }
}
