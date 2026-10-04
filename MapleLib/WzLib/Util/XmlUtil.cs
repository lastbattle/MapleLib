using System;
using System.Buffers;
using System.Collections;
using System.IO;
using System.Text;
using MapleLib.MapleCryptoLib;

namespace MapleLib.WzLib.Util
{
	public class XmlUtil
	{

		private static readonly SearchValues<char> specialCharacters = SearchValues.Create("\"'&<>");

		public static string SanitizeText(string text)
		{
			int length = text.Length; // Preserve the existing NullReferenceException for null input.
			int firstSpecialCharacter = text.AsSpan().IndexOfAny(specialCharacters);
			if (firstSpecialCharacter < 0)
				return text;

			StringBuilder fixedText = new StringBuilder(length + 8);
			fixedText.Append(text.AsSpan(0, firstSpecialCharacter));
			for (int i = firstSpecialCharacter; i < length; i++)
			{
				switch (text[i])
				{
					case '"': fixedText.Append("&quot;"); break;
					case '\'': fixedText.Append("&apos;"); break;
					case '&': fixedText.Append("&amp;"); break;
					case '<': fixedText.Append("&lt;"); break;
					case '>': fixedText.Append("&gt;"); break;
					default: fixedText.Append(text[i]); break;
				}
			}
			return fixedText.ToString();
		}

		public static string OpenNamedTag(string tag, string name, bool finish)
		{
			return OpenNamedTag(tag, name, finish, false);
		}

		public static string EmptyNamedTag(string tag, string name)
		{
			return OpenNamedTag(tag, name, true, true);
		}

		public static string EmptyNamedValuePair(string tag, string name, string value)
		{
			return OpenNamedTag(tag, name, false, false) + Attrib("value", value, true, true);
		}

		public static string OpenNamedTag(string tag, string name, bool finish, bool empty)
		{
			return "<" + tag + " name=\"" + SanitizeText(name ?? string.Empty) + "\"" + (finish ? (empty ? "/>" : ">") : " ");
		}

		public static string Attrib(string name, string value)
		{
			return Attrib(name, value, false, false);
		}

		public static string Attrib(string name, string value, bool closeTag, bool empty)
		{
			return name + "=\"" + SanitizeText(value) + "\"" + (closeTag ? (empty ? "/>" : ">") : " ");
		}

		public static string CloseTag(string tag)
		{
			return "</" + tag + ">";
		}

		public static string Indentation(int level)
		{
			if (level < 0)
				throw new OverflowException();
			return new string('\t', level);
		}
	}
}
