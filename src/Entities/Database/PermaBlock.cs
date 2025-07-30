using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AGC_Entbannungssystem.Entities.Database;

[Table("permas")]
public class PermaBlock
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("userid")]
    public long UserId { get; set; }
}