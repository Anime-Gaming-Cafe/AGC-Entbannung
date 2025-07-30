using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AGC_Entbannungssystem.Entities.Database;

[Table("requirementconfirmation")]
public class RequirementConfirmation
{
    [Key]
    [Column("user_id")]
    public long UserId { get; set; }

    [Column("time")]
    public long Time { get; set; }
}