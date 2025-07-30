using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AGC_Entbannungssystem.Entities.Database;

[Table("autocompletions")]
public class Autocompletion
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("type")]
    public string Type { get; set; } = string.Empty;

    [Column("data")]
    public string Data { get; set; } = string.Empty;
}