namespace LibMatrix.FederationTest.Utilities;

public static class UnpaddedBase64 {
    public static string Encode(byte[] data) {
        return Convert.ToBase64String(data).TrimEnd('=');
    }

    public static byte[] Decode(string base64) {
        string paddedBase64 = base64;
        switch (paddedBase64.Length % 4) {
            case 2: paddedBase64 += "=="; break;
            case 3: paddedBase64 += "="; break;
        }

        return Convert.FromBase64String(paddedBase64);
    }
}