using System;
using System.Text;

namespace WAPPushSMPP
{
	/// <summary>
	/// Methods for decoding a byte array to it's Hex representation
	/// </summary>
	public class HexDecoder : System.Text.Decoder
	{
		internal HexDecoder()
		{
		}

		/// <summary>
		/// Returns the length of the Hex represenation of a byte array
		/// </summary>
		/// <param name="bytes">array of bytes to represent</param>
		/// <param name="index">start index in buffer</param>
		/// <param name="maxBytes">number of bytes from start to represent</param>
		/// <returns></returns>
		public override int GetCharCount(byte[] bytes, int index, int maxBytes)
		{
			return Math.Min(bytes.Length-index, maxBytes)*2;
		}

		/// <summary>
		/// Returns the char for a given byte value
		/// </summary>
		/// <param name="b"></param>
		/// <returns></returns>
		private static char GetChar(byte b)
		{
			if (b<=9)
				return (char)('0'+b);
			else
				return (char)('A'+(b-10));
		}

		/// <summary>
		/// Write the Hex representation of a byte array to the char array passed in
		/// </summary>
		/// <param name="bytes">array to represent</param>
		/// <param name="byteIndex">start index</param>
		/// <param name="maxBytes">number of bytes to represent</param>
		/// <param name="chars">target array</param>
		/// <param name="charIndex">start index in target array</param>
		/// <returns>number of characters written</returns>
		public override int GetChars(byte[] bytes, int byteIndex, int maxBytes, char[] chars, int charIndex)
		{
			//Work out how many chars to return.
			int charCount = GetCharCount(bytes, byteIndex, maxBytes);

			//Check the buffer size.
			if (chars.Length-charIndex<charCount)
				throw new ArgumentException("The character array is not large enough to contain the characters that will be generated from the byte buffer.", "chars");

			for (int i=byteIndex; i<maxBytes; i++, charIndex+=2)
			{
				byte upperValue = (byte)Math.Floor(bytes[i]/16.0);
				byte lowerValue = (byte)(bytes[i]%16);
				chars[charIndex] = GetChar(upperValue);
				chars[charIndex+1] = GetChar(lowerValue);
			}

			return charCount;
		}
		/// <summary>
		/// Returns the Hex representation of a byte array
		/// </summary>
		/// <param name="bytes">The byte array to represent</param>
		/// <returns>char array representing the byte array</returns>
		public char[] GetChars(byte[] bytes)
		{
			int charCount = GetCharCount(bytes, 0, bytes.Length);
			char[] chars = new char[charCount];
			GetChars(bytes, 0, bytes.Length, chars, 0);
			return chars;
		}

	}
}
