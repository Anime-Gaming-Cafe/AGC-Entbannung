using AGC_Entbannungssystem.Services;
using DisCatSharp;
using Npgsql;

namespace AGC_Entbannungssystem.Helpers;

public static class Helperfunctions
{
    public static string GenerateCaseId()
    {
        var guid = Guid.NewGuid().ToString("N");
        return guid.Substring(0, 10);
    }

    public static string DbString()
    {
        string databasename = BotConfigurator.GetConfig("Database", "DatabaseName");
        string dbuser = BotConfigurator.GetConfig("Database", "DatabaseUser");
        string dbpassword = BotConfigurator.GetConfig("Database", "DatabasePassword");
        string dbhost = BotConfigurator.GetConfig("Database", "DatabaseHost");
        return $"Host={dbhost};Username={dbuser};Password={dbpassword};Database={databasename}";

    }
}