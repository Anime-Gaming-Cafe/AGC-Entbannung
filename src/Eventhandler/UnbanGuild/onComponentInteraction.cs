#region

using AGC_Entbannungssystem.Helpers;
using DisCatSharp;
using DisCatSharp.ApplicationCommands;
using DisCatSharp.Enums;
using DisCatSharp.EventArgs;

#endregion

namespace AGC_Entbannungssystem.Eventhandler.UnbanGuild;

[EventHandler]
public class onComponentInteraction : ApplicationCommandsModule
{
    [Event]
    public async Task ComponentInteractionCreated(DiscordClient client, ComponentInteractionCreateEventArgs e)
    {
        _ = Task.Run(async () =>
        {
            string cid = e.Interaction.Data.CustomId;
            if (cid == "open_appealticketinfo")
            {
                await Methodes.PanelCheckBan(client, e);
            }

            return Task.CompletedTask;
        });
    }
}