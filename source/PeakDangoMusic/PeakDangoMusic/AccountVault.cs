using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using BepInEx;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace PeakDangoMusic;

internal static class AccountVault
{
	private struct Blob
	{
		internal int Length;

		internal IntPtr Data;
	}

	private static string FilePath => Path.Combine(Paths.ConfigPath, "PeakDangoMusic-Accounts.dpapi");

	[DllImport("crypt32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
	private static extern bool CryptProtectData(ref Blob input, string description, IntPtr entropy, IntPtr reserved, IntPtr prompt, int flags, out Blob output);

	[DllImport("crypt32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
	private static extern bool CryptUnprotectData(ref Blob input, out IntPtr description, IntPtr entropy, IntPtr reserved, IntPtr prompt, int flags, out Blob output);

	[DllImport("kernel32.dll")]
	private static extern IntPtr LocalFree(IntPtr memory);

	private static byte[] Transform(byte[] bytes, bool protect)
	{
		Blob input = new Blob
		{
			Length = bytes.Length,
			Data = Marshal.AllocHGlobal(bytes.Length)
		};
		Blob output = default;
		IntPtr description = IntPtr.Zero;
		try
		{
			Marshal.Copy(bytes, 0, input.Data, bytes.Length);
			if (!(protect ? CryptProtectData(ref input, "PeakDangoMusic", IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 1, out output) : CryptUnprotectData(ref input, out description, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 1, out output)))
			{
				throw new Win32Exception(Marshal.GetLastWin32Error(), "Windows登录凭证保护失败");
			}
			byte[] array = new byte[output.Length];
			Marshal.Copy(output.Data, array, 0, array.Length);
			return array;
		}
		finally
		{
			Marshal.FreeHGlobal(input.Data);
			if (output.Data != IntPtr.Zero)
			{
				LocalFree(output.Data);
			}
			if (description != IntPtr.Zero)
			{
				LocalFree(description);
			}
		}
	}

	internal static JObject Load()
	{
		if (!File.Exists(FilePath))
		{
			return new JObject();
		}
		byte[] array = File.ReadAllBytes(FilePath);
		if (array.Length > 262144)
		{
			throw new InvalidDataException("登录凭证文件过大");
		}
		byte[] array2 = Transform(array, protect: false);
		try
		{
			return JObject.Parse(Encoding.UTF8.GetString(array2));
		}
		finally
		{
			Array.Clear(array2, 0, array2.Length);
		}
	}

	internal static void Save(JObject credentials)
	{
		byte[] bytes = Encoding.UTF8.GetBytes(((JToken)credentials).ToString((Formatting)0, Array.Empty<JsonConverter>()));
		try
		{
			byte[] bytes2 = Transform(bytes, protect: true);
			string text = FilePath + ".tmp";
			File.WriteAllBytes(text, bytes2);
			if (File.Exists(FilePath))
			{
				File.Replace(text, FilePath, null);
			}
			else
			{
				File.Move(text, FilePath);
			}
		}
		finally
		{
			Array.Clear(bytes, 0, bytes.Length);
		}
	}
}
