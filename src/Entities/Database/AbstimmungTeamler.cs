using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AGC_Entbannungssystem.Entities.Database;

[Table("abstimmungen_teamler")]
public class AbstimmungTeamler
{
    [Key]
    [Column("user_id")]
    public long UserId { get; set; }

    [Key]
    [Column("vote_id")]
    public string VoteId { get; set; } = string.Empty;

    [Column("votevalue")]
    public int VoteValue { get; set; }
}