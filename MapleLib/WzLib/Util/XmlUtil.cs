using System;
using System.Collections;
using System.IO;
using System.Text;
using MapleLib.MapleCryptoLib;

namespace MapleLib.WzLib.Util
{
	public class XmlUtil
	{

		private static readonly char[] specialCharacters = {'"', '\'', '&', '<', '>'};
		private static readonly string[] replacementStrings = {"&quot;", "&apos;", "&amp;", "&lt;", "&gt;"};

		public static string SanitizeText(string text)
		{
			StringBuilder fixedText = new StringBuilder("");
			bool charFixed;
			for (int i = 0; i < text.Length; i++)
			{
				charFixed = false;
				for (int k = 0; k < specialCharacters.Length; k++)
				{

					if (text[i] == specialCharacters[k])
					{
						fixedText.Append(replacementStrings[k]);
						charFixed = true;
						break;
					}
				}
				if (!charFixed)
				{
					fixedText.Append(text[i]);
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
			return "<" + tag + " name=\"" + name + "\"" + (finish ? (empty ? "/>" : ">") : " ");
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
			char[] indent = new char[level];
			for (int i = 0; i < indent.Length; i++)
			{
				indent[i] = '\t';
			}
			return new String(indent);
		}
	}
}
