#region

using AGC_Entbannungssystem.Services;
using DisCatSharp;
using DisCatSharp.ApplicationCommands;
using DisCatSharp.Entities;
using DisCatSharp.Enums;
using DisCatSharp.EventArgs;

#endregion

namespace AGC_Entbannungssystem.Eventhandler.UnbanGuild;

[EventHandler]
public class MoveChannelCategory : ApplicationCommandsModule
{
    [Event]
    public async Task MessageCreated(DiscordClient client, MessageCreateEventArgs e)
    {
        _ = Task.Run(async () =>
        {
            List<string> keywords = new()
            {
                "accept",
                "deny",
                "deny2",
                "deny13",
                "deny24",
                "bannsystem"
            };
            // check if guild is unban guild
            if (e.Guild?.Id != GlobalProperties.UnbanServerId) return;
            if (e.Channel.Name.StartsWith("antrag-") && !e.Channel.Name.EndsWith("-geschlossen"))
            {
                if (e.Message.Author.IsBot) return;
                ulong moderatorRoleId = ulong.Parse(BotConfigurator.GetConfig("MainConfig", "UnbanGuildTeamRoleId"));
                DiscordRole moderatorRole = e.Guild.GetRole(moderatorRoleId);
                var member = await e.Guild.GetMemberAsync(e.Message.Author.Id);
                if (!member.Roles.Contains(moderatorRole)) return;
                var cat = e.Channel.ParentId;
                var ucat = ulong.Parse(BotConfigurator.GetConfig("MainConfig", "BearbeitetCategoryId"));
                if (cat == ucat)
                {
                    return;
                }

                if (keywords.Contains(e.Message.Content.ToLower()))
                {
                    DiscordChannel newcat = e.Guild.GetChannel(ucat);
                    await e.Channel.ModifyAsync(x => x.Parent = newcat);
                }
            }
        });


        await Task.CompletedTask;
    }
}