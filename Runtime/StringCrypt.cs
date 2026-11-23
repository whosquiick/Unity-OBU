using System;
using System.Security.Cryptography;
using System.Text;

namespace UnityOBU
{
    // Symmetric helper for strings you don't want sitting in the binary as plain UTF-16.
    // This keeps casual string dumps from turning up URLs, keys or room names. It is not a
    // substitute for server-side auth: the process still holds the plaintext after Decrypt.
    public static class StringCrypt
    {
        private const int SaltSize = 16;
        private const int Iterations = 20000;

        // Encrypts to a base64 blob of [salt][iv][cipher]. Used by the Editor weaver menu.
        public static string Encrypt(string plaintext, string password)
        {
            if (plaintext == null)
            {
                throw new ArgumentNullException(nameof(plaintext));
            }

            byte[] salt = new byte[SaltSize];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(salt);
            }

            using (var aes = Aes.Create())
            {
                aes.KeySize = 256;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;

                using (var kdf = new Rfc2898DeriveBytes(password ?? string.Empty, salt, Iterations, HashAlgorithmName.SHA256))
                {
                    aes.Key = kdf.GetBytes(aes.KeySize / 8);
                }

                aes.GenerateIV();
                byte[] body = Encoding.UTF8.GetBytes(plaintext);
                byte[] cipher;
                using (var encryptor = aes.CreateEncryptor())
                {
                    cipher = encryptor.TransformFinalBlock(body, 0, body.Length);
                }

                byte[] output = new byte[salt.Length + aes.IV.Length + cipher.Length];
                Buffer.BlockCopy(salt, 0, output, 0, salt.Length);
                Buffer.BlockCopy(aes.IV, 0, output, salt.Length, aes.IV.Length);
                Buffer.BlockCopy(cipher, 0, output, salt.Length + aes.IV.Length, cipher.Length);
                return Convert.ToBase64String(output);
            }
        }

        // Decrypts a blob produced by Encrypt. Throws on a bad key or corrupt input.
        public static string Decrypt(string blob, string password)
        {
            if (string.IsNullOrEmpty(blob))
            {
                return string.Empty;
            }

            byte[] all = Convert.FromBase64String(blob);
            using (var aes = Aes.Create())
            {
                aes.KeySize = 256;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;

                int ivSize = aes.BlockSize / 8;
                if (all.Length < SaltSize + ivSize)
                {
                    throw new CryptographicException("Blob too short.");
                }

                byte[] salt = new byte[SaltSize];
                byte[] iv = new byte[ivSize];
                Buffer.BlockCopy(all, 0, salt, 0, SaltSize);
                Buffer.BlockCopy(all, SaltSize, iv, 0, ivSize);

                int cipherLen = all.Length - SaltSize - ivSize;
                byte[] cipher = new byte[cipherLen];
                Buffer.BlockCopy(all, SaltSize + ivSize, cipher, 0, cipherLen);

                using (var kdf = new Rfc2898DeriveBytes(password ?? string.Empty, salt, Iterations, HashAlgorithmName.SHA256))
                {
                    aes.Key = kdf.GetBytes(aes.KeySize / 8);
                }

                aes.IV = iv;
                using (var decryptor = aes.CreateDecryptor())
                {
                    byte[] plain = decryptor.TransformFinalBlock(cipher, 0, cipher.Length);
                    return Encoding.UTF8.GetString(plain);
                }
            }
        }

        // Convenience overloads that pull the key from the project settings asset.
        public static string Encrypt(string plaintext)
        {
            return Encrypt(plaintext, SecuritySettings.LoadOrDefault().stringKey);
        }

        public static string Reveal(string blob)
        {
            return Decrypt(blob, SecuritySettings.LoadOrDefault().stringKey);
        }
    }

    // Marks a const/static string that the Editor "Encrypt marked strings" menu should
    // replace with an encrypted blob plus a Reveal() call. Kept as an attribute so the
    // source reads normally and the rewrite is opt-in per field.
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class EncryptedStringAttribute : Attribute
    {
    }
}
