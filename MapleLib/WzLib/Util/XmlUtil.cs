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

		public static string SanitizeText(string text)
		{
			// Parameterless nodes and cleared values carry no text; the XML
			// namespaces spell that as an empty attribute value.
			text ??= string.Empty;
			int length = text.Length;
			int firstSpecialCharacter = FirstIndexNeedingEscape(text);
			if (firstSpecialCharacter < 0)
				return text;

			StringBuilder fixedText = new StringBuilder(length + 8);
			fixedText.Append(text.AsSpan(0, firstSpecialCharacter));
			for (int i = firstSpecialCharacter; i < length; i++)
			{
				char c = text[i];
				switch (c)
				{
					case '"': fixedText.Append("&quot;"); break;
					case '\'': fixedText.Append("&apos;"); break;
					case '&': fixedText.Append("&amp;"); break;
					case '<': fixedText.Append("&lt;"); break;
					case '>': fixedText.Append("&gt;"); break;
					// Attribute value normalization folds literal whitespace
					// to spaces, so these travel as character references.
					case '\t': fixedText.Append("&#x9;"); break;
					case '\n': fixedText.Append("&#xA;"); break;
					case '\r': fixedText.Append("&#xD;"); break;
					default:
						if (char.IsHighSurrogate(c) && i + 1 < length && char.IsLowSurrogate(text[i + 1]))
						{
							// A surrogate pair is valid XML content.
							fixedText.Append(c);
							fixedText.Append(text[++i]);
						}
						else if (char.IsSurrogate(c) || c < 0x20 || c == 0xFFFE || c == 0xFFFF)
						{
							// The XML reader rejects numeric references to
							// characters outside the Char production, so those
							// code points are dropped rather than encoded.
							fixedText.Append('_');
						}
						else
						{
							fixedText.Append(c);
						}
						break;
				}
			}
			return fixedText.ToString();
		}

		/// <summary>
		/// Attribute text must survive the XML reader on import. Control
		/// characters, U+FFFE/U+FFFF and unpaired surrogates are invalid XML 1.0
		/// content, and literal tab/LF/CR would be normalized to spaces, so all
		/// of them travel as numeric character references.
		/// </summary>
		private static int FirstIndexNeedingEscape(string text)
		{
			for (int i = 0; i < text.Length; i++)
			{
				char c = text[i];
				if (char.IsSurrogate(c))
				{
					bool paired = char.IsHighSurrogate(c) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]);
					if (!paired)
						return i;

					i++; // the low half of a valid pair needs no escaping
					continue;
				}

				if (c == '"' || c == '\'' || c == '&' || c == '<' || c == '>' ||
					c < 0x20 ||
					c > 0xFFFD || c == 0xFFFE || c == 0xFFFF)
				{
					return i;
				}
			}

			return -1;
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
