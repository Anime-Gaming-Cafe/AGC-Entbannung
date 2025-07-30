using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AGC_Entbannungssystem.Entities.Database;

[Table("permas")]
public class PermaBlock
{
    [Key]
    [Column("userid")]
    public long UserId { get; set; }
}