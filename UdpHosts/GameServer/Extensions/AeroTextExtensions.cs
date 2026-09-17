namespace GameServer.Extensions;

internal static class AeroTextExtensions
{
    /// <summary>
    /// Returns the text with every non-ASCII character replaced by a '?'.
    ///
    /// Required before sending server-generated text in an [AeroString] field that is null-terminated
    /// (the default string mode): the source generator sizes the pack buffer from the *character* count
    /// (1 + text.Length) but writes the *UTF-8 byte* count (Utf8Bytes.Length + 1). The two only agree for
    /// ASCII. A single multi-byte character (e.g. an em dash in a command description) makes Pack write past
    /// the end of the buffer and throw ArgumentOutOfRangeException out of the client's network tick, logged as
    /// "HandlePacket Caught Specified argument was out of the range of valid values".
    ///
    /// No allocation is made when the text is already pure ASCII, which is the common case.
    /// </summary>
    public static string AsAeroSafeText(this string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text;
        }

        var chars = text.ToCharArray();
        var changed = false;

        for (var i = 0; i < chars.Length; i++)
        {
            if (chars[i] > 0x7F)
            {
                chars[i] = '?';
                changed = true;
            }
        }

        return changed ? new string(chars) : text;
    }
}
