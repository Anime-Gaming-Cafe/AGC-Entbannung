using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AGC_Entbannungssystem.Entities.Database;

[Table("abstimmungen")]
public class Abstimmung
{
    [Key]
    [Column("channel_id")]
    public long ChannelId { get; set; }

    [Column("message_id")]
    public long MessageId { get; set; }

    [Column("expires_at")]
    public long ExpiresAt { get; set; }

    [Column("pvotes")]
    public int PositiveVotes { get; set; }

    [Column("nvotes")]
    public int NegativeVotes { get; set; }

    [Column("endpending")]
    public bool EndPending { get; set; }

    // Navigation property
    public virtual ICollection<AbstimmungTeamler> TeamlerVotes { get; set; } = new List<AbstimmungTeamler>();
}