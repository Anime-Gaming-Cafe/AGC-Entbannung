using AGC_Entbannungssystem.Helpers;
using Microsoft.EntityFrameworkCore;

namespace AGC_Entbannungssystem.Services;

public class AgcDbContextFactory
{
    private static readonly DbContextOptions<AgcDbContext> _options;

    static AgcDbContextFactory()
    {
        var optionsBuilder = new DbContextOptionsBuilder<AgcDbContext>();
        optionsBuilder.UseNpgsql(Helperfunctions.DbString());
        _options = optionsBuilder.Options;
    }

    public static AgcDbContext CreateDbContext()
    {
        return new AgcDbContext(_options);
    }
}