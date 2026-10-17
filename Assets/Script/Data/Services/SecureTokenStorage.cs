using Cysharp.Threading.Tasks;
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace Assets.Script.Data.Services {
    /// <summary>
    /// Production release storage for refresh tokens using AES encryption
    /// with device-specific salt/key derivation.
    /// </summary>
    public class SecureTokenStorage : ITokenStorage {
        private const string PrefKey = "TIENLEN_SECURE_AUTH_REFRESH_TOKEN";
        private static readonly byte[] Salt = Encoding.UTF8.GetBytes("TienLenCasinoSalt_2026_SecureStorage");

        public UniTask SaveRefreshTokenAsync( string token ) {
            if ( string.IsNullOrEmpty(token) ) {
                PlayerPrefs.DeleteKey(PrefKey);
            }
            else {
                try {
                    string encrypted = Encrypt(token);
                    PlayerPrefs.SetString(PrefKey, encrypted);
                }
                catch ( Exception ex ) {
                    Debug.LogError($"[SecureTokenStorage] Encryption error: {ex.Message}");
                }
            }
            PlayerPrefs.Save();
            return UniTask.CompletedTask;
        }

        public UniTask<string> LoadRefreshTokenAsync() {
            string encrypted = PlayerPrefs.GetString(PrefKey, string.Empty);
            if ( string.IsNullOrEmpty(encrypted) ) {
                return UniTask.FromResult(string.Empty);
            }

            try {
                string decrypted = Decrypt(encrypted);
                return UniTask.FromResult(decrypted);
            }
            catch ( Exception ex ) {
                Debug.LogWarning($"[SecureTokenStorage] Decryption error (invalidating corrupt token): {ex.Message}");
                ClearAsync().Forget();
                return UniTask.FromResult(string.Empty);
            }
        }

        public UniTask ClearAsync() {
            PlayerPrefs.DeleteKey(PrefKey);
            PlayerPrefs.Save();
            return UniTask.CompletedTask;
        }

        private static string Encrypt( string plainText ) {
            byte[] key = DeriveKey();
            using var aes = Aes.Create();
            aes.Key = key;
            aes.GenerateIV();

            using var encryptor = aes.CreateEncryptor(aes.Key, aes.IV);
            using var ms = new MemoryStream();
            ms.Write(aes.IV, 0, aes.IV.Length); // prepend IV

            using ( var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write) )
            using ( var sw = new StreamWriter(cs, Encoding.UTF8) ) {
                sw.Write(plainText);
            }

            return Convert.ToBase64String(ms.ToArray());
        }

        private static string Decrypt( string cipherText ) {
            byte[] allBytes = Convert.FromBase64String(cipherText);
            byte[] key = DeriveKey();

            using var aes = Aes.Create();
            aes.Key = key;

            byte[] iv = new byte[aes.BlockSize / 8];
            Array.Copy(allBytes, 0, iv, 0, iv.Length);
            aes.IV = iv;

            using var decryptor = aes.CreateDecryptor(aes.Key, aes.IV);
            using var ms = new MemoryStream(allBytes, iv.Length, allBytes.Length - iv.Length);
            using var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read);
            using var sr = new StreamReader(cs, Encoding.UTF8);

            return sr.ReadToEnd();
        }

        private static byte[] DeriveKey() {
            string seed = SystemInfo.deviceUniqueIdentifier;
            if ( string.IsNullOrEmpty(seed) || seed == SystemInfo.unsupportedIdentifier ) {
                seed = "TienLen_Fallback_Device_Seed_Key";
            }

            using var derive = new Rfc2898DeriveBytes(seed, Salt, 10000, HashAlgorithmName.SHA256);
            return derive.GetBytes(32); // AES-256
        }
    }
}
