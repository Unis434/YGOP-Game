using System;
using System.Collections.Generic;

using System.Text;


public static class AzureHTML
{
    public static string Encode(string value)
    {
        value = value.Replace("\r", "&#13;");
        value = value.Replace("\n", "&#10;");

        return value;
    }

    public static string Decode(string value)
    {
        value = value.Replace("&#13;", "\r");
        value = value.Replace("&#10;", "\n");

        return value;
    }
}
