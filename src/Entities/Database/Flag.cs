using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AGC_Entbannungssystem.Entities.Database;

[Table("flags")]
public class Flag
{
    [Key]
    [Column("userid")]
    public long UserId { get; set; }

    [Key]
    [Column("caseid")]
    public string CaseId { get; set; } = string.Empty;

    [Column("punisherid")]
    public long PunisherId { get; set; }

    [Column("datum")]
    public long Datum { get; set; }

    [Column("description")]
    public string Description { get; set; } = string.Empty;
}