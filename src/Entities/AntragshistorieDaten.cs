namespace AGC_Entbannungssystem.Entities;

public class AntragshistorieDaten
{
    public long user_id { get; set; }
    public string antragsnummer { get; set; }
    public bool unbanned { get; set; }
    public string grund { get; set; }
    public long timestamp { get; set; }
    public long mod_id { get; set; }
}