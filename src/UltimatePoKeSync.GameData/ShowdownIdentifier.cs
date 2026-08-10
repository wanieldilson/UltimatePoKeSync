namespace UltimatePoKeSync.GameData;

internal static class ShowdownIdentifier
{
    public static string Normalize(string value)
    {
        var characters = new List<char>(value.Length);
        foreach (char character in value.Trim().ToLowerInvariant())
        {
            if (char.IsAsciiLetterOrDigit(character))
            {
                characters.Add(character);
            }
            else if (character == '♀')
            {
                characters.Add('f');
            }
            else if (character == '♂')
            {
                characters.Add('m');
            }
        }

        return new string([.. characters]);
    }
}
