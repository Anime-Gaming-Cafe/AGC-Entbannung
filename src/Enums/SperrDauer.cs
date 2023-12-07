using DisCatSharp.ApplicationCommands.Attributes;

namespace AGC_Entbannungssystem.Enums;

public enum SperrDauer
{
    [ChoiceName("3 Monate")]
    DreiMonate,
    [ChoiceName("6 Monate (Nur Absprache mit dem Team)")]
    SechsMonate,
    [ChoiceName("1 Jahr (Nur Absprache mit dem Team)")]
    EinJahr,
    [ChoiceName("Permanent (Nur Absprache mit dem Team)")]
    Permanent,
}