using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security;
using FUREQ.Workspace.Application;

namespace FUREQ.Workspace.Infrastructure;

[SupportedOSPlatform("windows")]
public sealed class WindowsCredentialVault : ICredentialVault
{
    public string? Read(string target)
    {
        if (!CredRead(target, 1, 0, out var credentialPointer))
        {
            var error = Marshal.GetLastWin32Error();
            if (error == 1168) return null;
            throw new Win32Exception(error, "Could not read the GitHub credential from Windows Credential Manager.");
        }

        try
        {
            var credential = Marshal.PtrToStructure<NativeCredential>(credentialPointer);
            return Marshal.PtrToStringUni(credential.CredentialBlob, checked((int)credential.CredentialBlobSize / 2));
        }
        finally
        {
            CredFree(credentialPointer);
        }
    }

    public void Write(string target, string value)
    {
        var blob = Marshal.StringToCoTaskMemUni(value);
        try
        {
            var credential = new NativeCredential
            {
                Type = 1,
                TargetName = target,
                CredentialBlob = blob,
                CredentialBlobSize = checked((uint)(value.Length * sizeof(char))),
                Persist = 2,
                UserName = Environment.UserName
            };
            if (!CredWrite(ref credential, 0))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not save the GitHub credential to Windows Credential Manager.");
            }
        }
        finally
        {
            Marshal.ZeroFreeCoTaskMemUnicode(blob);
        }
    }

    public void Delete(string target)
    {
        if (!CredDelete(target, 1, 0) && Marshal.GetLastWin32Error() != 1168)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not remove the GitHub credential from Windows Credential Manager.");
        }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NativeCredential
    {
        public uint Flags;
        public uint Type;
        [MarshalAs(UnmanagedType.LPWStr)] public string? TargetName;
        [MarshalAs(UnmanagedType.LPWStr)] public string? Comment;
        public System.Runtime.InteropServices.ComTypes.FILETIME LastWritten;
        public uint CredentialBlobSize;
        public IntPtr CredentialBlob;
        public uint Persist;
        public uint AttributeCount;
        public IntPtr Attributes;
        [MarshalAs(UnmanagedType.LPWStr)] public string? TargetAlias;
        [MarshalAs(UnmanagedType.LPWStr)] public string? UserName;
    }

    [DllImport("advapi32.dll", EntryPoint = "CredReadW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CredRead(string target, uint type, uint flags, out IntPtr credential);

    [DllImport("advapi32.dll", EntryPoint = "CredWriteW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CredWrite(ref NativeCredential credential, uint flags);

    [DllImport("advapi32.dll", EntryPoint = "CredDeleteW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CredDelete(string target, uint type, uint flags);

    [DllImport("advapi32.dll", EntryPoint = "CredFree", SetLastError = false)]
    private static extern void CredFree(IntPtr buffer);
}
