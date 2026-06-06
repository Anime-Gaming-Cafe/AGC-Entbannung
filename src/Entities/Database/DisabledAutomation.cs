using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AGC_Entbannungssystem.Entities.Database;

[Table("disabled_automations")]
public class DisabledAutomation
{
    [Key]
    [Column("channel_id")]
    public long ChannelId { get; set; }

    [Column("disabled_by")]
    public long DisabledBy { get; set; }

    [Column("timestamp")]
    public long Timestamp { get; set; }
}
