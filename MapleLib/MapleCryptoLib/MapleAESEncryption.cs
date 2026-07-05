using System;
using System.Security.Cryptography;

namespace MapleLib.MapleCryptoLib
{

    /// <summary>
    /// Class to handle the AES Encryption routines
    /// </summary>
    public class MapleAESEncryption
    {

        /// <summary>
        /// Encrypt data using MapleStory's AES algorithm
        /// </summary>
        /// <param name="IV">IV to use for encryption</param>
        /// <param name="data">Data to encrypt</param>
        /// <param name="length">Length of data</param>
        /// <returns>Crypted data</returns>
        public static byte[] AesCrypt(byte[] IV, byte[] data, int length)
        {
            return AesCrypt(IV, data, length, MapleCryptoConstants.GetTrimmedWzUserKey());
        }

        /// <summary>
        /// Encrypt data using MapleStory's AES method
        /// </summary>
        /// <param name="IV">IV to use for encryption</param>
        /// <param name="data">data to encrypt</param>
        /// <param name="length">length of data</param>
        /// <param name="key">the AES key to use</param>
        /// <returns>Crypted data</returns>
        public static byte[] AesCrypt(byte[] IV, byte[] data, int length, byte[] key)
        {
            using Aes aes = Aes.Create();
            aes.KeySize = 256; // in bits
            aes.Key = key;
            aes.Mode = CipherMode.ECB; // Should be OFB, but this works too
            aes.Padding = PaddingMode.None;

            using ICryptoTransform encryptor = aes.CreateEncryptor();
            byte[] feedback = new byte[16];

            int remaining = length;
            int chunkLength = 0x5B0;
            int start = 0;
            while (remaining > 0)
            {
                int currentLength = Math.Min(remaining, chunkLength);
                feedback[0] = feedback[4] = feedback[8] = feedback[12] = IV[0];
                feedback[1] = feedback[5] = feedback[9] = feedback[13] = IV[1];
                feedback[2] = feedback[6] = feedback[10] = feedback[14] = IV[2];
                feedback[3] = feedback[7] = feedback[11] = feedback[15] = IV[3];

                int end = start + currentLength;
                for (int offset = start; offset < end; offset += 16)
                {
                    encryptor.TransformBlock(feedback, 0, 16, feedback, 0);
                    int blockLength = Math.Min(16, end - offset);
                    if (blockLength == 16)
                    {
                        data[offset] ^= feedback[0];
                        data[offset + 1] ^= feedback[1];
                        data[offset + 2] ^= feedback[2];
                        data[offset + 3] ^= feedback[3];
                        data[offset + 4] ^= feedback[4];
                        data[offset + 5] ^= feedback[5];
                        data[offset + 6] ^= feedback[6];
                        data[offset + 7] ^= feedback[7];
                        data[offset + 8] ^= feedback[8];
                        data[offset + 9] ^= feedback[9];
                        data[offset + 10] ^= feedback[10];
                        data[offset + 11] ^= feedback[11];
                        data[offset + 12] ^= feedback[12];
                        data[offset + 13] ^= feedback[13];
                        data[offset + 14] ^= feedback[14];
                        data[offset + 15] ^= feedback[15];
                    }
                    else
                    {
                        for (int i = 0; i < blockLength; i++)
                        {
                            data[offset + i] ^= feedback[i];
                        }
                    }
                }

                start = end;
                remaining -= currentLength;
                chunkLength = 0x5B4;
            }

            return data;
        }
    }
}
