using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AGC_Entbannungssystem.Entities.Database;

[Table("antragsverlauf")]
public class Antragsverlauf
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("user_id")]
    public long UserId { get; set; }

    [Column("antragsnummer")]
    public string Antragsnummer { get; set; } = string.Empty;

    [Column("unbanned")]
    public bool Unbanned { get; set; }

    [Column("grund")]
    public string Grund { get; set; } = string.Empty;

    [Column("mod_id")]
    public long ModId { get; set; }

    [Column("timestamp")]
    public long Timestamp { get; set; }
}