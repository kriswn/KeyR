using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace SupTask;

public static class TT2FileManager
{
	private static readonly byte[] Key = Encoding.UTF8.GetBytes("T1nyT@skPlu$K3y1");

	private static readonly byte[] IV = Encoding.UTF8.GetBytes("T1nyT@skPlu$1V00");

	public static void Save(string path, string jsonContent)
	{
		using Aes aes = Aes.Create();
		aes.Key = Key;
		aes.IV = IV;
		ICryptoTransform transform = aes.CreateEncryptor(aes.Key, aes.IV);
		using FileStream stream = new FileStream(path, FileMode.Create);
		using CryptoStream stream2 = new CryptoStream(stream, transform, CryptoStreamMode.Write);
		using StreamWriter streamWriter = new StreamWriter(stream2);
		streamWriter.Write(jsonContent);
	}

	public static string Load(string path)
	{
		using Aes aes = Aes.Create();
		aes.Key = Key;
		aes.IV = IV;
		ICryptoTransform transform = aes.CreateDecryptor(aes.Key, aes.IV);
		using FileStream stream = new FileStream(path, FileMode.Open);
		using CryptoStream stream2 = new CryptoStream(stream, transform, CryptoStreamMode.Read);
		using StreamReader streamReader = new StreamReader(stream2);
		return streamReader.ReadToEnd();
	}
}

