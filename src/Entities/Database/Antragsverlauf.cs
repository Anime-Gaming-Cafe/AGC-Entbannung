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

    [Column("antrags_id")]
    public string AntragsId { get; set; } = string.Empty;

    [Column("entbannt")]
    public bool Entbannt { get; set; }

    [Column("reason")]
    public string Reason { get; set; } = string.Empty;

    [Column("mod_id")]
    public long ModId { get; set; }

    [Column("timestamp")]
    public long Timestamp { get; set; }
}