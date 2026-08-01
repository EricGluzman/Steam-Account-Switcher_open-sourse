using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;

namespace SteamAccountSwitcher.Services;

public static class DpapiService
{
    private const int CryptprotectUiForbidden = 0x1;
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("SteamAccountSwitcher:v1");

    public static string Protect(string value)
    {
        ArgumentException.ThrowIfNullOrEmpty(value);
        var clear = Encoding.UTF8.GetBytes(value);
        try
        {
            return Convert.ToBase64String(ProtectBytes(clear));
        }
        finally
        {
            Array.Clear(clear);
        }
    }

    public static string Unprotect(string protectedValue)
    {
        var encrypted = Convert.FromBase64String(protectedValue);
        var clear = UnprotectBytes(encrypted);
        try
        {
            return Encoding.UTF8.GetString(clear);
        }
        finally
        {
            Array.Clear(clear);
            Array.Clear(encrypted);
        }
    }

    private static byte[] ProtectBytes(byte[] value)
    {
        using var input = DataBlob.FromBytes(value);
        using var entropy = DataBlob.FromBytes(Entropy);
        if (!CryptProtectData(ref input.Value, null, ref entropy.Value, IntPtr.Zero, IntPtr.Zero,
                CryptprotectUiForbidden, out var output))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Windows could not encrypt the password.");
        }

        return CopyAndFree(output);
    }

    private static byte[] UnprotectBytes(byte[] value)
    {
        using var input = DataBlob.FromBytes(value);
        using var entropy = DataBlob.FromBytes(Entropy);
        if (!CryptUnprotectData(ref input.Value, IntPtr.Zero, ref entropy.Value, IntPtr.Zero, IntPtr.Zero,
                CryptprotectUiForbidden, out var output))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Windows could not decrypt the password.");
        }

        return CopyAndFree(output);
    }

    private static byte[] CopyAndFree(NativeBlob blob)
    {
        try
        {
            var bytes = new byte[blob.Size];
            if (blob.Size > 0)
            {
                Marshal.Copy(blob.Data, bytes, 0, blob.Size);
            }
            return bytes;
        }
        finally
        {
            if (blob.Data != IntPtr.Zero)
            {
                LocalFree(blob.Data);
            }
        }
    }

    private sealed class DataBlob : IDisposable
    {
        public NativeBlob Value;

        private DataBlob(byte[] bytes)
        {
            Value.Size = bytes.Length;
            Value.Data = Marshal.AllocHGlobal(bytes.Length);
            Marshal.Copy(bytes, 0, Value.Data, bytes.Length);
        }

        public static DataBlob FromBytes(byte[] bytes) => new(bytes);

        public void Dispose()
        {
            if (Value.Data == IntPtr.Zero)
            {
                return;
            }

            for (var i = 0; i < Value.Size; i++)
            {
                Marshal.WriteByte(Value.Data, i, 0);
            }
            Marshal.FreeHGlobal(Value.Data);
            Value.Data = IntPtr.Zero;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeBlob
    {
        public int Size;
        public IntPtr Data;
    }

    [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CryptProtectData(
        ref NativeBlob dataIn,
        string? description,
        ref NativeBlob optionalEntropy,
        IntPtr reserved,
        IntPtr prompt,
        int flags,
        out NativeBlob dataOut);

    [DllImport("crypt32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CryptUnprotectData(
        ref NativeBlob dataIn,
        IntPtr description,
        ref NativeBlob optionalEntropy,
        IntPtr reserved,
        IntPtr prompt,
        int flags,
        out NativeBlob dataOut);

    [DllImport("kernel32.dll")]
    private static extern IntPtr LocalFree(IntPtr memory);
}
