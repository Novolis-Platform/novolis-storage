namespace Novolis.Storage.AzureBlob;

internal static class AzureBlobNames
{
    public static bool IsValidContainerName(string value)
    {
        if (value.Length is < 3 or > 63
            || !IsLowercaseLetterOrDigit(value[0])
            || !IsLowercaseLetterOrDigit(value[^1]))
            return false;

        for (var index = 1; index < value.Length - 1; index++)
        {
            var character = value[index];
            if (!IsLowercaseLetter(character)
                && !char.IsAsciiDigit(character)
                && character != '-')
                return false;
        }

        return true;
    }

    private static bool IsLowercaseLetterOrDigit(char character) =>
        IsLowercaseLetter(character) || char.IsAsciiDigit(character);

    private static bool IsLowercaseLetter(char character) =>
        character is >= 'a' and <= 'z';
}
