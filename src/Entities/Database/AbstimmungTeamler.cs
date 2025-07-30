using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AGC_Entbannungssystem.Entities.Database;

[Table("abstimmungen_teamler")]
public class AbstimmungTeamler
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("vote_id")]
    public string VoteId { get; set; } = string.Empty;

    [Column("user_id")]
    public long UserId { get; set; }

    [Column("votevalue")]
    public int VoteValue { get; set; }
}