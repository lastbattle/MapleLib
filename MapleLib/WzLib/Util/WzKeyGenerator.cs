using System.IO;
using MapleLib.MapleCryptoLib;
using System;

namespace MapleLib.WzLib.Util
{
	public class WzKeyGenerator
	{
		#region Methods

		public static byte[] GetIvFromZlz(FileStream zlzStream)
		{
			byte[] iv = new byte[4];

			zlzStream.Seek(0x10040, SeekOrigin.Begin);
			zlzStream.Read(iv, 0, 4);
			return iv;
		}

		private static byte[] GetAesKeyFromZlz(FileStream zlzStream)
		{
			byte[] aes = new byte[32];

			zlzStream.Seek(0x10060, SeekOrigin.Begin);
			for (int i = 0; i < 8; i++)
			{
				zlzStream.Read(aes, i * 4, 4);
				zlzStream.Seek(12, SeekOrigin.Current);
			}
			return aes;
		}

		/// <summary>
		/// Generates the WZ Key for .Lua property
		/// </summary>
		/// <returns></returns>
		public static WzMutableKey GenerateLuaWzKey()
		{
			return new WzMutableKey(
				WzAESConstant.WZ_MSEAIV, 
				MapleCryptoConstants.GetTrimmedUserKey(ref MapleCryptoConstants.MAPLESTORY_USERKEY_DEFAULT));
		}

		public static WzMutableKey GenerateWzKey(byte[] WzIv)
        {
            return new WzMutableKey(WzIv, MapleCryptoConstants.GetTrimmedUserKey(ref MapleCryptoConstants.UserKey_WzLib));
        }

		public static WzMutableKey GenerateWzKey(byte[] WzIv, byte[] AesUserKey)
		{
			if (AesUserKey.Length != 128)
				throw new Exception("AesUserkey expects 128 bytes, not " + AesUserKey.Length);
			return new WzMutableKey(WzIv, MapleCryptoConstants.GetTrimmedUserKey(ref AesUserKey));
		}
        #endregion
    }
}