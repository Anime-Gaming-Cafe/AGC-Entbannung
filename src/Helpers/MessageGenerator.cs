#region

using DisCatSharp.Entities;

#endregion

namespace AGC_Entbannungssystem.Helpers;

public static class MessageGenerator
{
    public static List<DiscordEmbed> UnbanNoteGenerate()
    {
        var color = new DiscordColor("2f3136");
        var embeds = new List<DiscordEmbed>();
        var embed1 = new DiscordEmbedBuilder();
        embed1.WithAuthor("Aufmerksam lesen!");
        embed1.WithTitle("Warum wurde ich gebannt?");
        embed1.WithDescription("Du wurdest gebannt, weil du auf AGC gegen die Regeln verstoßen hast. \n" +
                               "Sofern du nicht weißt, wieso du gebannt wurdest, erfrage bitte dies gleich zum Anfang deines Entbannungsantrags. Wir geben dir dann Auskunft darüber, wieso du gebannt wurdest. \n\n" +
                               "- Die Erfragung des Grundes fließt nicht in die Bewertung deines Entbannungsantrags ein. \n\n" +
                               "> ⚠️ Wenn du denkst, dass du zu unrecht gebannt wurdest, teile uns dies bitte __zu Beginn__ deines Antrags mit!");
        embed1.WithColor(color);
        embeds.Add(embed1.Build());
        var embed2 = new DiscordEmbedBuilder();
        embed2.WithTitle("Antragsstellung");
        embed2.WithDescription(
            "Es wird von dir erwartet, dass du dich umfassend zu deinem Bann äußerst. Außerdem erwarten wir, dass du folgende Fragen ausführlich beantwortest: \n" +
            "- Wieso wurdest du gebannt bzw hast gegen unsere Serverregeln verstoßen? \n" +
            "- Was hast du daraus gelernt? \n" +
            "- Was wirst du anders machen, um so eine Situation in Zukunft zu vermeiden? \n" +
            "- Wie können wir sicher sein, dass du dich in Zukunft an unsere Regeln hältst? \n");
        embed2.WithFooter(
            "Das herrunterrattern als Stichpunkte führt möglicherweise zu einer Ablehnung! Antworte so ausführlich wie möglich und in einem Fließtext!");
        embed2.WithColor(color);
        embeds.Add(embed2.Build());
        var embed3 = new DiscordEmbedBuilder();
        embed3.WithTitle("Wichtig!");
        embed3.WithDescription(
            "Unser Team achtet auf eine angemessene Ausdrucksweise. Achte auch so gut wie möglich auf Rechtschreibung und Grammatik. \n" +
            "Verzichte bitte auf mögliche Lückenfüller, indem du auf unnötige Rechtfertigungen verzichtest, wie z.B. warum du entbannt werden möchtest. \n\n" +
            "> Solltest du Schwierigkeiten mit der Rechtschreibung haben, durch z.b eine Lese- und Rechtschreibschwäche, so kannst du dies gerne in deinem Antrag erwähnen. \n\n" +
            "Nimm dir außerdem Zeit. Wir möchten sehen, dass dir etwas an der Entbannung liegt. Schau also dass du alle Kriterien erfüllst. \n\n" +
            "⚠️ Wenn der Entbannungsantrag nicht ordentlich bearbeitet wird, wird er abgelehnt! \n\n" +
            "⚠️ Das verwenden einer AI/KI kann zu einer direkten Ablehnung führen. \n\n" +
            "🛑 Sollte dein Antrag abgelehnt werden, wirst du für 3 Monate gesperrt. \n\n" +
            "🕰️ Du hast für den Entbannungsantrag 24 Stunden Zeit. Sollte in dieser Zeit kein Antrag gestellt werden, wirst du ebenfalls abgelehnt und für 3 Monate gesperrt.\n\n" +
            "⌚ **Das nichtlesen der Anforderungen kann zu einer __direkten Ablehnung__ führen. Daher bitten wir dich __in jedem fall__, die Anforderungen sorgfältig zu lesen.**");
        embed3.WithColor(color);
        embeds.Add(embed3.Build());
        return embeds;
    }

    public static DiscordEmbed getVoteEmbedInRunning(DiscordChannel votechannel, long targetTimestamp,
        int negativeVotes = 0, int positiveVotes = 0, int resultForColor = 0, int teamMemberCount = 0)
    {
        var color = resultForColor switch
        {
            0 => DiscordColor.Red, // more negative votes
            1 => DiscordColor.Green, // more positive votes
            2 => DiscordColor.Yellow, // tie
            _ => DiscordColor.Gray // default color if no votes yet
        };
        
        int totalVotes = positiveVotes + negativeVotes;
        double votePercentage = teamMemberCount > 0 ? (double)totalVotes / teamMemberCount * 100 : 0;
        
        var embed = new DiscordEmbedBuilder();
        embed.WithTitle("Abstimmung läuft!");
        embed.WithDescription(
            $"Die Abstimmung für den Antrag ``{votechannel.Name}`` | ({votechannel.Mention}) steht bereit!\n" +
            $"**Positive Stimmen:** {positiveVotes}\n" +
            $"**Negative Stimmen:** {negativeVotes}\n" +
            $"**Abstimmungsbeteiligung:** {votePercentage:F1}% ({totalVotes}/{teamMemberCount})\n" +
            $"Die Abstimmung läuft bis <t:{targetTimestamp}:f> (<t:{targetTimestamp}:R>)\n\n" +
            $"-# Die Anzahl der Stimmen wird alle 5 Minuten aktualisiert.\n");
        embed.WithColor(color);
        return embed.Build();
    }

    public static DiscordEmbed getVoteEmbedFinished(DiscordChannel votechannel, long targetTimestamp,
        int negativeVotes = 0, int positiveVotes = 0, int resultForColor = 0, int teamMemberCount = 0)
    {
        var color = resultForColor switch
        {
            0 => DiscordColor.Red, // more negative votes
            1 => DiscordColor.Green, // more positive votes
            2 => DiscordColor.Yellow, // tie
            _ => DiscordColor.Gray // default color if no votes yet
        };
        
        int totalVotes = positiveVotes + negativeVotes;
        double votePercentage = teamMemberCount > 0 ? (double)totalVotes / teamMemberCount * 100 : 0;
        
        var embed = new DiscordEmbedBuilder();
        embed.WithTitle("Abstimmung beendet!");
        embed.WithDescription(
            $"Die Abstimmung für den Antrag ``{votechannel.Name}`` | ({votechannel.Mention}) ist beendet!\n" +
            $"**Positive Stimmen:** {positiveVotes}\n" +
            $"**Negative Stimmen:** {negativeVotes}\n" +
            $"**Abstimmungsbeteiligung:** {votePercentage:F1}% ({totalVotes}/{teamMemberCount})\n" +
            $"Die Abstimmung endete am <t:{targetTimestamp}:f> (<t:{targetTimestamp}:R>)\n\n");
        embed.WithColor(color);
        return embed.Build();
    }

    public static DiscordEmbed getVoteEmbedCanceled(DiscordChannel votechannel, long targetTimestamp)
    {
        var embed = new DiscordEmbedBuilder();
        embed.WithTitle("Abstimmung abgebrochen!");
        embed.WithDescription(
            $"Die Abstimmung für den Antrag {votechannel.Name} ({votechannel.Mention}) wurde abgebrochen!\n" +
            $"Die Abstimmung wurde am <t:{targetTimestamp}:f> (<t:{targetTimestamp}:R>) abgebrochen.\n\n");
        embed.WithColor(DiscordColor.DarkRed);
        return embed.Build();
    }
}