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

    public AgcDbContext(DbContextOptions<AgcDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure Abstimmung
        modelBuilder.Entity<Abstimmung>(entity =>
        {
            entity.HasKey(e => e.ChannelId);
            entity.Property(e => e.ChannelId).ValueGeneratedNever();
        });

        // Configure AbstimmungTeamler
        modelBuilder.Entity<AbstimmungTeamler>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedOnAdd();
        });

        // Configure Antragsverlauf
        modelBuilder.Entity<Antragsverlauf>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedOnAdd();
        });

        // Configure Antragssperre
        modelBuilder.Entity<Antragssperre>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedOnAdd();
        });

        // Configure RequirementConfirmation
        modelBuilder.Entity<RequirementConfirmation>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedOnAdd();
        });

        // Configure Flag
        modelBuilder.Entity<Flag>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedOnAdd();
        });
    }
}