#region

using AGC_Entbannungssystem.Entities;
using AGC_Entbannungssystem.Services;
using DisCatSharp.Entities;
using Newtonsoft.Json;
using Npgsql;

#endregion

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


    public static async Task<List<BannSystemReport?>?> GetBannsystemReports(DiscordUser user)
    {
        using HttpClient client = new();
        string apiKey = BotConfigurator.GetConfig("ModHQConfig", "API_Key");
        string apiUrl = BotConfigurator.GetConfig("ModHQConfig", "API_URL");

        client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", apiKey);
        HttpResponseMessage response = await client.GetAsync($"{apiUrl}{user.Id}");
        if (response.IsSuccessStatusCode)
        {
            string json = await response.Content.ReadAsStringAsync();
            UserInfoApiResponse apiResponse = JsonConvert.DeserializeObject<UserInfoApiResponse>(json);
            List<BannSystemReport> data = apiResponse.reports;
            return data;
        }

        return null;
    }

    public static async Task<List<BannSystemReport?>?> BSReportToWarn(DiscordUser user)
    {
        try
        {
            var data = await GetBannsystemReports(user);

            return data.Select(warn => new BannSystemReport
            {
                reportId = warn.reportId,
                authorId = warn.authorId,
                reason = warn.reason,
                timestamp = warn.timestamp,
                active = warn.active
            }).ToList();
        }
        catch (Exception e)
        {
            // ignored
        }

        return new List<BannSystemReport>();
    }

    public static async Task<List<AntragshistorieDaten>> GetAntragshistorie(DiscordUser user)
    {
        var constring = DbString();
        await using var con = new NpgsqlConnection(constring);
        await con.OpenAsync();
        await using var cmd = new NpgsqlCommand("SELECT * FROM antragsverlauf WHERE user_id = @userid", con);
        cmd.Parameters.AddWithValue("userid", (long)user.Id);
        await using var reader = await cmd.ExecuteReaderAsync();
        var data = new List<AntragshistorieDaten>();
        while (await reader.ReadAsync())
        {
            data.Add(new AntragshistorieDaten
            {
                user_id = reader.GetInt64(0),
                antragsnummer = reader.GetString(1),
                unbanned = reader.GetBoolean(2),
                grund = reader.GetString(3),
                mod_id = reader.GetInt64(4),
                timestamp = reader.GetInt64(5)
            });
        }

        return data;
    }


    public static string BoolToEmoji(bool value)
    {
        return value ? "<:angenommen:1190335045341282314>" : "<:abgelehnt:1190335046591205426>";
    }

    public static bool HasActiveBannSystemReport(List<BannSystemReport> reports)
    {
        return reports.Any(report => report.active);
    }
}