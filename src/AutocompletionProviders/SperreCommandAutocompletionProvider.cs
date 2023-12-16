using AGC_Entbannungssystem.Tasks;
using DisCatSharp.ApplicationCommands.Attributes;
using DisCatSharp.ApplicationCommands.Context;
using DisCatSharp.Entities;

namespace AGC_Entbannungssystem.AutocompletionProviders;

public class SperreCommandAutocompletionProvider : IAutocompleteProvider
{
    public async Task<IEnumerable<DiscordApplicationCommandAutocompleteChoice>> Provider(AutocompleteContext ctx)
    {
        var options = new List<DiscordApplicationCommandAutocompleteChoice>();
        
        List<string> completions = FillAutocompletions.SperreCompletions;
        foreach (var completion in completions)
        {
            options.Add(new DiscordApplicationCommandAutocompleteChoice(completion, completion));
        }

        return await Task.FromResult(options.AsEnumerable());
    }
}