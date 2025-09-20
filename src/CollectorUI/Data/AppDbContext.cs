using Microsoft.EntityFrameworkCore;

namespace CollectorUI.Data;

public class AppDbContext : DbContext
{
    public DbSet<NamespaceSelection> NamespaceSelections => Set<NamespaceSelection>();
    public DbSet<AppSetting> AppSettings => Set<AppSetting>();

    private static string GetDatabasePath()
    {
        var baseDir = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var appDir = Path.Combine(baseDir, "CollectorUI");
        Directory.CreateDirectory(appDir);
        var dbPath = Path.Combine(appDir, "collectorui.sqlite");
        return dbPath;
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
        {
            var dbPath = GetDatabasePath();
            optionsBuilder.UseSqlite($"Data Source={dbPath}");
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<NamespaceSelection>()
            .HasIndex(x => new { x.SolutionPath, x.ProjectPath, x.Namespace })
            .IsUnique(false);

        modelBuilder.Entity<AppSetting>()
            .HasIndex(x => x.Key)
            .IsUnique(true);
    }

    public static void EnsureCreated()
    {
        using var ctx = new AppDbContext();
        ctx.Database.EnsureCreated();
    }
}
