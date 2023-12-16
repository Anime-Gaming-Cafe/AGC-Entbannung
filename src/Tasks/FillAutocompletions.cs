#region

using System.Data.Common;
using AGC_Entbannungssystem.Helpers;
using AGC_Entbannungssystem.Services;
using DisCatSharp;
using DisCatSharp.Entities;
using Microsoft.Extensions.Logging;
using Npgsql;

#endregion

namespace AGC_Entbannungssystem.Tasks;

public static class FillAutocompletions
{
    public static List<string> SperreCompletions = new List<string>();
    public static List<string> EntbannungsCompletions = new List<string>();
    public static async Task Run(DiscordClient client)
    {
        while (true)
        {
            try
            {
                var consting = Helperfunctions.DbString(); // psql
                await using var con = new NpgsqlConnection(consting);
                await con.OpenAsync();
                await using var cmd = new NpgsqlCommand("SELECT data FROM autocompletions WHERE type = 'sperre'", con);
                await using var reader = await cmd.ExecuteReaderAsync();
                SperreCompletions.Clear();
                while (await reader.ReadAsync())
                {
                    SperreCompletions.Add(reader.GetString(0));
                }

                await reader.CloseAsync();
                await con.CloseAsync();
                
                await using var con2 = new NpgsqlConnection(consting);
                await con2.OpenAsync();

                await using var cmd2 = new NpgsqlCommand("SELECT data FROM autocompletions WHERE type = 'entbannung'", con2);
                await using var reader2 = await cmd2.ExecuteReaderAsync();
                EntbannungsCompletions.Clear();
                while (await reader2.ReadAsync())
                {
                    EntbannungsCompletions.Add(reader2.GetString(0));
                }

                

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