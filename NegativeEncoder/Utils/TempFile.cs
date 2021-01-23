﻿using System;
using System.Collections.Generic;
using System.Text;

namespace NegativeEncoder.Utils
{
    public static class TempFile
    {
        public static void SaveTempFile(string fullFilename, string content, bool convertToANSI = true)
        {
            content = content.Replace("\n", "\r\n");

            var tempFs = System.IO.File.Create(fullFilename);
            if (convertToANSI)
            {
                var codePage = Console.OutputEncoding.CodePage;
                var ansi = Encoding.GetEncoding(codePage);

                var unicodeByte = Encoding.Unicode.GetBytes(content);
                byte[] tempContent = Encoding.Convert(Encoding.Unicode, ansi, unicodeByte);
                tempFs.Write(tempContent, 0, tempContent.Length);
            }
            else
            {
                var unicodeByte = Encoding.Unicode.GetBytes(content);
                byte[] tempContent = Encoding.Convert(Encoding.Unicode, Encoding.UTF8, unicodeByte);
                tempFs.Write(tempContent, 0, tempContent.Length);
            }
            tempFs.Close();
        }
    }
}
