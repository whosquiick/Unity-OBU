using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace UnityOBU
{
    // AES-256-CBC over a whole file, with the KDF salt and IV written into the header so the
    // runtime can rebuild the key from the project key string. Streamed so a large bundle
    // never has two full copies in memory at once.
    //
    // A protected file is: magic[4] "OBU1" | salt[16] | iv[16] | aes-cbc(payload).
    public static class BundleCrypt
    {
        private static readonly byte[] Magic = Encoding.ASCII.GetBytes("OBU1");
        private const int SaltSize = 16;
        private const int Iterations = 20000;

        public static void EncryptFile(string sourcePath, string destPath, string password)
        {
            byte[] salt = new byte[SaltSize];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(salt);
            }

            using (var aes = CreateAes(password, salt, out byte[] iv))
            using (var input = File.OpenRead(sourcePath))
            using (var output = File.Create(destPath))
            {
                output.Write(Magic, 0, Magic.Length);
                output.Write(salt, 0, salt.Length);
                output.Write(iv, 0, iv.Length);

                using (var encryptor = aes.CreateEncryptor())
                using (var crypto = new CryptoStream(output, encryptor, CryptoStreamMode.Write))
                {
                    input.CopyTo(crypto);
                }
            }
        }

        // Returns the decrypted bytes. Callers hand these straight to
        // AssetBundle.LoadFromMemoryAsync so the plaintext bundle never touches disk.
        public static byte[] DecryptBytes(byte[] protectedData, string password)
        {
            if (protectedData == null || protectedData.Length < Magic.Length + SaltSize + 16)
            {
                throw new CryptographicException("Protected bundle is too small or truncated.");
            }

            for (int i = 0; i < Magic.Length; i++)
            {
                if (protectedData[i] != Magic[i])
                {
                    throw new CryptographicException("Not an OBU protected bundle (bad magic).");
                }
            }

            byte[] salt = new byte[SaltSize];
            Buffer.BlockCopy(protectedData, Magic.Length, salt, 0, SaltSize);

            using (var aes = Aes.Create())
            {
                aes.KeySize = 256;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;

                int ivSize = aes.BlockSize / 8;
                byte[] iv = new byte[ivSize];
                Buffer.BlockCopy(protectedData, Magic.Length + SaltSize, iv, 0, ivSize);

                using (var kdf = new Rfc2898DeriveBytes(password ?? string.Empty, salt, Iterations, HashAlgorithmName.SHA256))
                {
                    aes.Key = kdf.GetBytes(aes.KeySize / 8);
                }

                aes.IV = iv;

                int offset = Magic.Length + SaltSize + ivSize;
                using (var decryptor = aes.CreateDecryptor())
                using (var ms = new MemoryStream())
                {
                    using (var source = new MemoryStream(protectedData, offset, protectedData.Length - offset, false))
                    using (var crypto = new CryptoStream(source, decryptor, CryptoStreamMode.Read))
                    {
                        crypto.CopyTo(ms);
                    }

                    return ms.ToArray();
                }
            }
        }

        private static Aes CreateAes(string password, byte[] salt, out byte[] iv)
        {
            var aes = Aes.Create();
            aes.KeySize = 256;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            using (var kdf = new Rfc2898DeriveBytes(password ?? string.Empty, salt, Iterations, HashAlgorithmName.SHA256))
            {
                aes.Key = kdf.GetBytes(aes.KeySize / 8);
            }

            aes.GenerateIV();
            iv = aes.IV;
            return aes;
        }
    }
}
