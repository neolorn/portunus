using System.Buffers.Text;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Portunus.Tests;

/// <summary>
/// The agent's answers in the forms of <c>07</c> section 6, for the fake agent to answer; a
/// secret's values are made by the test at run time, and none of them is a secret.
/// </summary>
internal static class Recorded
{
    internal static string Waiting(string reason)
    {
        return $$"""{"version":1,"state":"waiting","reason":"{{reason}}","since":"2026-11-05T09:00:00Z"}""";
    }

    internal static string Ready(int items)
    {
        return $$"""{"version":1,"state":"ready","since":"2026-11-05T09:00:00Z","source":"vault","loadedAt":"2026-11-05T09:00:12Z","items":{{items}}}""";
    }

    internal static string Secret(int current, params byte[][] versions)
    {
        string written = string.Join(",", versions.Select((value, index) => $"\"{index + 1}\":\"{Base64Url.EncodeToString(value)}\""));
        return """{"version":1,"state":"ready","secret":{"current":""" + current.ToString(CultureInfo.InvariantCulture) + ""","versions":{""" + written + "}}}";
    }

    internal static string Failure(string state, string code)
    {
        return "{\"version\":1,\"state\":\"" + state + "\",\"failure\":{\"code\":\"" + code + "\",\"details\":{}}}";
    }

    internal static byte[] Value(string text)
    {
        return Encoding.UTF8.GetBytes(text);
    }
}