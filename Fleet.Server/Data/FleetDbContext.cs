using Fleet.Server.Data.Entities;
using Fleet.Server.Auth;
using Microsoft.EntityFrameworkCore;

namespace Fleet.Server.Data;

public class FleetDbContext(DbContextOptions<FleetDbContext> options) : DbContext(options)
{
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<WorkItem> WorkItems => Set<WorkItem>();
    public DbSet<WorkItemLevel> WorkItemLevels => Set<WorkItemLevel>();
    public DbSet<AgentExecution> AgentExecutions => Set<AgentExecution>();
    public DbSet<LogEntry> LogEntries => Set<LogEntry>();
    public DbSet<DashboardAgent> DashboardAgents => Set<DashboardAgent>();
    public DbSet<ChatSession> ChatSessions => Set<ChatSession>();
    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();
    public DbSet<ChatAttachment> ChatAttachments => Set<ChatAttachment>();
    public DbSet<UserProfile> UserProfiles => Set<UserProfile>();
    public DbSet<LinkedAccount> LinkedAccounts => Set<LinkedAccount>();
    public DbSet<Subscription> Subscriptions => Set<Subscription>();
    public DbSet<AgentPhaseResult> AgentPhaseResults => Set<AgentPhaseResult>();
    public DbSet<NotificationEvent> NotificationEvents => Set<NotificationEvent>();
    public DbSet<MonthlyUsageLedger> MonthlyUsageLedgers => Set<MonthlyUsageLedger>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ── Project ────────────────────────────────────────────────
        modelBuilder.Entity<Project>(builder =>
        {
            builder.HasKey(p => p.Id);

            // Slugs are unique per owner, not globally.
            builder.HasIndex(p => new { p.OwnerId, p.Slug }).IsUnique();

            // JSON columns for summary objects (PostgreSQL jsonb)
            builder.OwnsOne(p => p.WorkItemSummary, b => b.ToJson());
            builder.OwnsOne(p => p.AgentSummary, b => b.ToJson());
        });

        // ── WorkItem ───────────────────────────────────────────────
        modelBuilder.Entity<WorkItem>(builder =>
        {
            builder.HasKey(w => w.Id);
            builder.Property(w => w.Id).ValueGeneratedOnAdd();

            builder.HasIndex(w => new { w.ProjectId, w.WorkItemNumber }).IsUnique();

            builder.HasOne(w => w.Project)
                .WithMany(p => p.WorkItems)
                .HasForeignKey(w => w.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);

            // Self-referencing parent/child hierarchy
            builder.HasOne(w => w.Parent)
                .WithMany(w => w.Children)
                .HasForeignKey(w => w.ParentId)
                .OnDelete(DeleteBehavior.Restrict);

            // Work item level (type/tier)
            builder.HasOne(w => w.Level)
                .WithMany(l => l.WorkItems)
                .HasForeignKey(w => w.LevelId)
                .OnDelete(DeleteBehavior.SetNull);

            // Tags stored as PostgreSQL text[] (native array support via Npgsql)
        });

        // ── WorkItemLevel ──────────────────────────────────────────
        modelBuilder.Entity<WorkItemLevel>(builder =>
        {
            builder.HasKey(l => l.Id);
            builder.Property(l => l.Id).ValueGeneratedOnAdd();

            builder.HasOne(l => l.Project)
                .WithMany(p => p.WorkItemLevels)
                .HasForeignKey(l => l.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);

            // Unique level name per project
            builder.HasIndex(l => new { l.ProjectId, l.Name }).IsUnique();
        });

        // ── AgentExecution ─────────────────────────────────────────
        modelBuilder.Entity<AgentExecution>(builder =>
        {
            builder.HasKey(e => e.Id);

            builder.HasOne(e => e.Project)
                .WithMany(p => p.AgentExecutions)
                .HasForeignKey(e => e.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);

            // JSON column for nested agent collection (PostgreSQL jsonb)
            builder.OwnsMany(e => e.Agents, b => b.ToJson());
        });

        // ── AgentPhaseResult ───────────────────────────────────────
        modelBuilder.Entity<AgentPhaseResult>(builder =>
        {
            builder.HasKey(r => r.Id);
            builder.Property(r => r.Id).ValueGeneratedOnAdd();

            builder.HasOne(r => r.Execution)
                .WithMany(e => e.PhaseResults)
                .HasForeignKey(r => r.ExecutionId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasIndex(r => new { r.ExecutionId, r.PhaseOrder });
        });

        // ── LogEntry ───────────────────────────────────────────────
        modelBuilder.Entity<LogEntry>(builder =>
        {
            builder.HasKey(l => l.Id);
            builder.Property(l => l.Id).ValueGeneratedOnAdd();

            builder.HasOne(l => l.Project)
                .WithMany(p => p.LogEntries)
                .HasForeignKey(l => l.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ── DashboardAgent ─────────────────────────────────────────
        modelBuilder.Entity<DashboardAgent>(builder =>
        {
            builder.HasKey(d => d.Id);
            builder.Property(d => d.Id).ValueGeneratedOnAdd();

            builder.HasOne(d => d.Project)
                .WithMany(p => p.DashboardAgents)
                .HasForeignKey(d => d.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ── ChatSession ────────────────────────────────────────────
        modelBuilder.Entity<ChatSession>(builder =>
        {
            builder.HasKey(s => s.Id);

            builder.HasOne(s => s.Project)
                .WithMany(p => p.ChatSessions)
                .HasForeignKey(s => s.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ── ChatMessage ────────────────────────────────────────────
        modelBuilder.Entity<ChatMessage>(builder =>
        {
            builder.HasKey(m => m.Id);

            builder.HasOne(m => m.ChatSession)
                .WithMany(s => s.Messages)
                .HasForeignKey(m => m.ChatSessionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ── UserProfile ────────────────────────────────────────────
        modelBuilder.Entity<UserProfile>(builder =>
        {
            builder.HasKey(u => u.Id);
            builder.Property(u => u.Id).ValueGeneratedOnAdd();
            builder.Property(u => u.Role).HasDefaultValue(UserRoles.Free);

            builder.HasIndex(u => u.EntraObjectId).IsUnique();
            builder.HasIndex(u => u.Email).IsUnique();

            // JSON column for preferences (PostgreSQL jsonb)
            builder.OwnsOne(u => u.Preferences, b => b.ToJson());
        });

        // ── LinkedAccount ──────────────────────────────────────────
        modelBuilder.Entity<LinkedAccount>(builder =>
        {
            builder.HasKey(a => a.Id);
            builder.Property(a => a.Id).ValueGeneratedOnAdd();

            builder.HasOne(a => a.UserProfile)
                .WithMany(u => u.LinkedAccounts)
                .HasForeignKey(a => a.UserProfileId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ── Subscription ───────────────────────────────────────────
        modelBuilder.Entity<Subscription>(builder =>
        {
            builder.HasKey(s => s.Id);
            builder.Property(s => s.Id).ValueGeneratedOnAdd();

            // JSON columns for complex nested collections (PostgreSQL jsonb)
            builder.OwnsMany(s => s.UsageMeters, b => b.ToJson());
            builder.OwnsMany(s => s.Plans, b =>
            {
                b.ToJson();
                // Features is a primitive collection within JSON — supported in EF Core 8+
            });
        });

        modelBuilder.Entity<NotificationEvent>(builder =>
        {
            builder.HasKey(n => n.Id);
            builder.Property(n => n.Id).ValueGeneratedOnAdd();

            builder.HasOne(n => n.Project)
                .WithMany()
                .HasForeignKey(n => n.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasIndex(n => new { n.UserProfileId, n.IsRead, n.CreatedAtUtc });
        });

        modelBuilder.Entity<MonthlyUsageLedger>(builder =>
        {
            builder.HasKey(l => l.Id);
            builder.Property(l => l.Id).ValueGeneratedOnAdd();
            builder.HasIndex(l => new { l.UserProfileId, l.UtcMonth }).IsUnique();
        });
    }
}
