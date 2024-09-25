/******************************************************************************
 * Spine Runtimes License Agreement
 * Last updated July 28, 2023. Replaces all prior versions.
 *
 * Copyright (c) 2013-2023, Esoteric Software LLC
 *
 * Integration of the Spine Runtimes into software or otherwise creating
 * derivative works of the Spine Runtimes is permitted under the terms and
 * conditions of Section 2 of the Spine Editor License Agreement:
 * http://esotericsoftware.com/spine-editor-license
 *
 * Otherwise, it is permitted to integrate the Spine Runtimes into software or
 * otherwise create derivative works of the Spine Runtimes (collectively,
 * "Products"), provided that each user of the Products must obtain their own
 * Spine Editor license and redistribution of the Products in any form must
 * include this license and copyright notice.
 *
 * THE SPINE RUNTIMES ARE PROVIDED BY ESOTERIC SOFTWARE LLC "AS IS" AND ANY
 * EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
 * WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
 * DISCLAIMED. IN NO EVENT SHALL ESOTERIC SOFTWARE LLC BE LIABLE FOR ANY
 * DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
 * (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES,
 * BUSINESS INTERRUPTION, OR LOSS OF USE, DATA, OR PROFITS) HOWEVER CAUSED AND
 * ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
 * (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THE
 * SPINE RUNTIMES, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 *****************************************************************************/

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace Spine {
	public static class Json {
		public static object Deserialize (TextReader text) {
			SharpJson.JsonDecoder parser = new SharpJson.JsonDecoder(true);
			return parser.Decode(text.ReadToEnd());
		}
	}
}

/**
 * Copyright (c) 2016 Adriano Tinoco d'Oliveira Rezende
 *
 * Based on the JSON parser by Patrick van Bergen
 * http://techblog.procurios.nl/k/news/view/14605/14863/how-do-i-write-my-own-parser-(for-json).html
 *
 * Changes made:
 *
 * - Optimized parser speed (deserialize roughly near 3x faster than original)
 * - Added support to handle lexer/parser error messages with line numbers
 * - Added more fine grained control over type conversions during the parsing
 * - Refactory API (Separate Lexer code from Parser code and the Encoder from Decoder)
 *
 * Permission is hereby granted, free of charge, to any person obtaining a copy of this software
 * and associated documentation files (the "Software"), to deal in the Software without restriction,
 * including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense,
 * and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so,
 * subject to the following conditions:
 * The above copyright notice and this permission notice shall be included in all copies or substantial
 * portions of the Software.
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT
 * LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT.
 * IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY,
 * WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE
 * OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
 */
namespace SharpJson {
	internal sealed class Lexer {
		public enum Token {
			None,
			Null,
			True,
			False,
			Colon,
			Comma,
			String,
			Number,
			CurlyOpen,
			CurlyClose,
			SquaredOpen,
			SquaredClose,
		};

		public bool HasError => !_success;

		public int LineNumber { get; private set; }

		private bool _parseNumbersAsFloat;

		private readonly char[] _json;
		private int _index = 0;
		private bool _success = true;
		private readonly char[] _stringBuffer = new char[4096];

		public Lexer (string text, bool parseNumbersAsFloat = false) {
			Reset();

			_json = text.ToCharArray();
			_parseNumbersAsFloat = parseNumbersAsFloat;
		}

		private void Reset () {
			_index = 0;
			LineNumber = 1;
			_success = true;
		}

		public string ParseString () {
			int idx = 0;
			StringBuilder builder = null;

			SkipWhiteSpaces();

			// "
			char c = _json[_index++];

			bool failed = false;
			bool complete = false;

			while (!complete && !failed) {
				if (_index == _json.Length)
					break;

				c = _json[_index++];
				if (c == '"') {
					complete = true;
					break;
				} else if (c == '\\') {
					if (_index == _json.Length)
						break;

					c = _json[_index++];

					switch (c) {
					case '"':
						_stringBuffer[idx++] = '"';
						break;
					case '\\':
						_stringBuffer[idx++] = '\\';
						break;
					case '/':
						_stringBuffer[idx++] = '/';
						break;
					case 'b':
						_stringBuffer[idx++] = '\b';
						break;
					case 'f':
						_stringBuffer[idx++] = '\f';
						break;
					case 'n':
						_stringBuffer[idx++] = '\n';
						break;
					case 'r':
						_stringBuffer[idx++] = '\r';
						break;
					case 't':
						_stringBuffer[idx++] = '\t';
						break;
					case 'u':
						int remainingLength = _json.Length - _index;
						if (remainingLength >= 4) {
							string hex = new string(_json, _index, 4);

							// XXX: handle UTF
							_stringBuffer[idx++] = (char)Convert.ToInt32(hex, 16);

							// skip 4 chars
							_index += 4;
						} else {
							failed = true;
						}
						break;
					}
				} else {
					_stringBuffer[idx++] = c;
				}

				if (idx >= _stringBuffer.Length) {
					builder ??= new StringBuilder();

					builder.Append(_stringBuffer, 0, idx);
					idx = 0;
				}
			}

			if (!complete) {
				_success = false;
				return null;
			}

			if (builder != null)
				return builder.ToString();
			else
				return new string(_stringBuffer, 0, idx);
		}

		private string GetNumberString () {
			SkipWhiteSpaces();

			int lastIndex = GetLastIndexOfNumber(_index);
			int charLength = (lastIndex - _index) + 1;

			string result = new string(_json, _index, charLength);

			_index = lastIndex + 1;

			return result;
		}

		public float ParseFloatNumber () {
			string str = GetNumberString();

			if (!float.TryParse(str, NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
				return 0;

			return number;
		}

		public double ParseDoubleNumber () {
			string str = GetNumberString();

			if (!double.TryParse(str, NumberStyles.Any, CultureInfo.InvariantCulture, out var number))
				return 0;

			return number;
		}

		private int GetLastIndexOfNumber (int index) {
			int lastIndex;

			for (lastIndex = index; lastIndex < _json.Length; lastIndex++) {
				char ch = _json[lastIndex];

				if ((ch < '0' || ch > '9') && ch != '+' && ch != '-'
					&& ch != '.' && ch != 'e' && ch != 'E')
					break;
			}

			return lastIndex - 1;
		}

		private void SkipWhiteSpaces () {
			for (; _index < _json.Length; _index++) {
				char ch = _json[_index];

				if (ch == '\n')
					LineNumber++;

				if (!char.IsWhiteSpace(_json[_index]))
					break;
			}
		}

		public Token LookAhead () {
			SkipWhiteSpaces();

			int savedIndex = _index;
			return NextToken(_json, ref savedIndex);
		}

		public Token NextToken () {
			SkipWhiteSpaces();
			return NextToken(_json, ref _index);
		}

		private static Token NextToken (char[] json, ref int index) {
			if (index == json.Length)
				return Token.None;

			char c = json[index++];

			switch (c) {
			case '{':
				return Token.CurlyOpen;
			case '}':
				return Token.CurlyClose;
			case '[':
				return Token.SquaredOpen;
			case ']':
				return Token.SquaredClose;
			case ',':
				return Token.Comma;
			case '"':
				return Token.String;
			case '0':
			case '1':
			case '2':
			case '3':
			case '4':
			case '5':
			case '6':
			case '7':
			case '8':
			case '9':
			case '-':
				return Token.Number;
			case ':':
				return Token.Colon;
			}

			index--;

			int remainingLength = json.Length - index;

			// false
			if (remainingLength >= 5) {
				if (json[index] == 'f' &&
					json[index + 1] == 'a' &&
					json[index + 2] == 'l' &&
					json[index + 3] == 's' &&
					json[index + 4] == 'e') {
					index += 5;
					return Token.False;
				}
			}

			// true
			if (remainingLength >= 4) {
				if (json[index] == 't' &&
					json[index + 1] == 'r' &&
					json[index + 2] == 'u' &&
					json[index + 3] == 'e') {
					index += 4;
					return Token.True;
				}
			}

			// null
			if (remainingLength >= 4) {
				if (json[index] == 'n' &&
					json[index + 1] == 'u' &&
					json[index + 2] == 'l' &&
					json[index + 3] == 'l') {
					index += 4;
					return Token.Null;
				}
			}

			return Token.None;
		}
	}

	public sealed class JsonDecoder(bool parseNumbersAsFloat = false)
	{
		private string _errorMessage;

		private Lexer _lexer;

		public object Decode (string text) {
			_errorMessage = null;

			_lexer = new Lexer(text, parseNumbersAsFloat);

			return ParseValue();
		}

		public static object DecodeText (string text) {
			JsonDecoder builder = new JsonDecoder();
			return builder.Decode(text);
		}

		IDictionary<string, object> ParseObject () {
			Dictionary<string, object> table = new Dictionary<string, object>();

			// {
			_lexer.NextToken();

			while (true) {
				Lexer.Token token = _lexer.LookAhead();

				switch (token) {
				case Lexer.Token.None:
					TriggerError("Invalid token");
					return null;
				case Lexer.Token.Comma:
					_lexer.NextToken();
					break;
				case Lexer.Token.CurlyClose:
					_lexer.NextToken();
					return table;
				default:
					// name
					string name = EvalLexer(_lexer.ParseString());

					if (_errorMessage != null)
						return null;

					// :
					token = _lexer.NextToken();

					if (token != Lexer.Token.Colon) {
						TriggerError("Invalid token; expected ':'");
						return null;
					}

					// value
					object value = ParseValue();

					if (_errorMessage != null)
						return null;

					table[name] = value;
					break;
				}
			}
		}

		IList<object> ParseArray () {
			List<object> array = new List<object>();

			// [
			_lexer.NextToken();

			while (true) {
				Lexer.Token token = _lexer.LookAhead();

				switch (token) {
				case Lexer.Token.None:
					TriggerError("Invalid token");
					return null;
				case Lexer.Token.Comma:
					_lexer.NextToken();
					break;
				case Lexer.Token.SquaredClose:
					_lexer.NextToken();
					return array;
				default:
					object value = ParseValue();

					if (_errorMessage != null)
						return null;

					array.Add(value);
					break;
				}
			}

			//return null; // Unreachable code
		}

		object ParseValue () {
			switch (_lexer.LookAhead()) {
			case Lexer.Token.String:
				return EvalLexer(_lexer.ParseString());
			case Lexer.Token.Number:
				if (parseNumbersAsFloat)
					return EvalLexer(_lexer.ParseFloatNumber());
				else
					return EvalLexer(_lexer.ParseDoubleNumber());
			case Lexer.Token.CurlyOpen:
				return ParseObject();
			case Lexer.Token.SquaredOpen:
				return ParseArray();
			case Lexer.Token.True:
				_lexer.NextToken();
				return true;
			case Lexer.Token.False:
				_lexer.NextToken();
				return false;
			case Lexer.Token.Null:
				_lexer.NextToken();
				return null;
			case Lexer.Token.None:
				break;
			}

			TriggerError("Unable to parse value");
			return null;
		}

		private void TriggerError (string message) {
			_errorMessage = string.Format("Error: '{0}' at line {1}",
										 message, _lexer.LineNumber);
		}

		private T EvalLexer<T> (T value) {
			if (_lexer.HasError)
				TriggerError("Lexical error occurred");

			return value;
		}
	}
}