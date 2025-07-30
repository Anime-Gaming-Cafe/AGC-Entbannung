#region

using AGC_Entbannungssystem.Helpers;
using AGC_Entbannungssystem.Services;
using DisCatSharp;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

#endregion

namespace AGC_Entbannungssystem.Tasks;

public static class FillAutocompletions
{
    public static List<string> SperreCompletions = new();
    public static List<string> EntbannungsCompletions = new();

    public static async Task Run(DiscordClient client)
    {
        while (true)
        {
            try
            {
                await using var context = AgcDbContextFactory.CreateDbContext();

                // Load Sperre completions
                var sperreData = await context.Autocompletions
                    .Where(a => a.Type == "sperre")
                    .Select(a => a.Data)
                    .ToListAsync();
                
                SperreCompletions.Clear();
                SperreCompletions.AddRange(sperreData);

                // Load Entbannung completions
                var entbannungData = await context.Autocompletions
                    .Where(a => a.Type == "entbannung")
                    .Select(a => a.Data)
                    .ToListAsync();
                
                EntbannungsCompletions.Clear();
                EntbannungsCompletions.AddRange(entbannungData);
            }
            catch (Exception err)
            {
                client.Logger.LogError(err, "Error in FillAutocompletions");
                await ErrorReporting.SendErrorToDev(client, client.CurrentUser, err);
            }

            await Task.Delay(TimeSpan.FromMinutes(10));
        }
    }
}