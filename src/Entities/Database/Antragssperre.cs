using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AGC_Entbannungssystem.Entities.Database;

[Table("antragssperre")]
public class Antragssperre
{
    [Key]
    [Column("user_id")]
    public long UserId { get; set; }

    [Column("reason")]
    public string Reason { get; set; } = string.Empty;

    [Column("expires_at")]
    public long ExpiresAt { get; set; }
}