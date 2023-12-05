namespace AGC_Entbannungssystem.Helpers;

public static class Helperfunctions
{
    public static string GenerateCaseId()
    {
        var guid = Guid.NewGuid().ToString("N");
        return guid.Substring(0, 10);
    }
}