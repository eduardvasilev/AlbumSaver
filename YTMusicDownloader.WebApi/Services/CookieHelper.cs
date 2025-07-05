using System;
using System.Collections.Generic;
using System.IO;
using System.Net;

public static class CookieParser
{
    public static List<Cookie> LoadCookiesFromTxt(string filePath)
    {
        var cookies = new List<Cookie>();
        foreach (var line in File.ReadLines(filePath))
        {
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
                continue;

            var parts = line.Split('\t');
            if (parts.Length != 7)
                continue;

            string domain = parts[0];
            bool secure = parts[3] == "TRUE";
            DateTime expires = DateTimeOffset.FromUnixTimeSeconds(long.Parse(parts[4])).UtcDateTime;
            string name = parts[5];
            string value = parts[6];

            var cookie = new Cookie(name, value, "/", domain)
            {
                Secure = secure,
                Expires = expires
            };

            cookies.Add(cookie);
        }

        return cookies;
    }
}
