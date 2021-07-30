﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace INZFS.Theme.Services
{

    public interface IUrlEncodingService
    {
        string GetHexFromString(string value);
        string GetStringFromHex(string value);
    }


    public class UrlEncodingService : IUrlEncodingService
    {
        public string GetHexFromString(string value)
        {
            var bytes = Encoding.Unicode.GetBytes(value);

            var stringBuilder = new StringBuilder();
            foreach (var byteOfChar in bytes)
            {
                stringBuilder.Append(byteOfChar.ToString("X2"));
            }
            return stringBuilder.ToString(); 
        }

        public string GetStringFromHex(string value)
        {
            var bytes = new byte[value.Length / 2];
            for (var index = 0; index < bytes.Length; index++)
            {
                bytes[index] = Convert.ToByte(value.Substring(index * 2, 2), 16);
            }
            return Encoding.Unicode.GetString(bytes); 
        }
    }
}
