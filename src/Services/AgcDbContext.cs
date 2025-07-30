using AGC_Entbannungssystem.Entities.Database;
using AGC_Entbannungssystem.Helpers;
using Microsoft.EntityFrameworkCore;

namespace AGC_Entbannungssystem.Services;

public class AgcDbContext : DbContext
{
    public DbSet<Abstimmung> Abstimmungen { get; set; }
    public DbSet<AbstimmungTeamler> AbstimmungenTeamler { get; set; }
    public DbSet<Antragsverlauf> Antragsverlauf { get; set; }
    public DbSet<Antragssperre> Antragssperren { get; set; }
    public DbSet<RequirementConfirmation> RequirementConfirmations { get; set; }
    public DbSet<Flag> Flags { get; set; }
    public DbSet<PermaBlock> PermaBlocks { get; set; }
    public DbSet<Autocompletion> Autocompletions { get; set; }

    public AgcDbContext(DbContextOptions<AgcDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure Abstimmung - channel_id is the primary key
        modelBuilder.Entity<Abstimmung>(entity =>
        {
            entity.HasKey(e => e.ChannelId);
            entity.Property(e => e.ChannelId).ValueGeneratedNever();
        });

        // Configure AbstimmungTeamler - composite key of user_id and vote_id
        modelBuilder.Entity<AbstimmungTeamler>(entity =>
        {
            entity.HasKey(e => new { e.UserId, e.VoteId });
        });

        // Configure Antragsverlauf - composite key of user_id and antrags_id
        modelBuilder.Entity<Antragsverlauf>(entity =>
        {
            entity.HasKey(e => new { e.UserId, e.AntragsId });
        });

        // Configure Antragssperre - user_id is the primary key
        modelBuilder.Entity<Antragssperre>(entity =>
        {
            entity.HasKey(e => e.UserId);
            entity.Property(e => e.UserId).ValueGeneratedNever();
        });

        // Configure RequirementConfirmation - user_id is the primary key
        modelBuilder.Entity<RequirementConfirmation>(entity =>
        {
            entity.HasKey(e => e.UserId);
            entity.Property(e => e.UserId).ValueGeneratedNever();
        });

        // Configure Flag - composite key of userid and caseid
        modelBuilder.Entity<Flag>(entity =>
        {
            entity.HasKey(e => new { e.UserId, e.CaseId });
        });

        // Configure PermaBlock - userid is the primary key
        modelBuilder.Entity<PermaBlock>(entity =>
        {
            entity.HasKey(e => e.UserId);
            entity.Property(e => e.UserId).ValueGeneratedNever();
        });

        // Configure Autocompletion - composite key of type and data
        modelBuilder.Entity<Autocompletion>(entity =>
        {
            entity.HasKey(e => new { e.Type, e.Data });
        });
    }
}