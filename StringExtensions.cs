using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace RAT
{
    static class StringExtensions
    {
        public static string AlignCenter(this string source, int finalLength, char padChar = ' ')
        {
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < finalLength / 2 - source.Length / 2; i++)
            {
                sb.Append(padChar);
            }
            sb.Append(source);
            return sb.ToString().PadRight(finalLength, padChar);
        }
    }
}
