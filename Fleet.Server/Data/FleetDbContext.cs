using Fleet.Server.Data.Entities;
using Fleet.Server.Auth;
using Fleet.Server.Agents;
using Fleet.Server.Memories;
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
    public DbSet<WorkItemAttachment> WorkItemAttachments => Set<WorkItemAttachment>();
    public DbSet<UserProfile> UserProfiles => Set<UserProfile>();
    public DbSet<LinkedAccount> LinkedAccounts => Set<LinkedAccount>();
    public DbSet<McpServerConnection> McpServerConnections => Set<McpServerConnection>();
    public DbSet<MemoryEntry> MemoryEntries => Set<MemoryEntry>();
    public DbSet<PromptSkill> PromptSkills => Set<PromptSkill>();
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

            builder.HasMany(w => w.Attachments)
                .WithOne(a => a.WorkItem)
                .HasForeignKey(a => a.WorkItemId)
                .OnDelete(DeleteBehavior.Cascade);

            // Performance indexes for common query patterns
            builder.HasIndex(w => new { w.ProjectId, w.State });
            builder.HasIndex(w => w.ProjectId); // fast project-scoped listing

            // Tags stored as PostgreSQL text[] (native array support via Npgsql)
        });

        modelBuilder.Entity<WorkItemAttachment>(builder =>
        {
            builder.HasKey(a => a.Id);
            builder.HasIndex(a => new { a.WorkItemId, a.UploadedAt });
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

            builder.Property(e => e.ExecutionMode)
                .HasDefaultValue(AgentExecutionModes.Standard);

            builder.HasOne(e => e.ParentExecution)
                .WithMany(e => e.ChildExecutions)
                .HasForeignKey(e => e.ParentExecutionId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasIndex(e => new { e.ProjectId, e.ParentExecutionId });

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
            builder.HasIndex(l => new { l.ProjectId, l.ExecutionId });

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
            builder.Property(s => s.OwnerId).IsRequired();
            builder.Property(s => s.GenerationState)
                .HasDefaultValue(Models.ChatGenerationStates.Idle);
            builder.Property(s => s.RecentActivityJson)
                .HasDefaultValue("[]");
            builder.Property(s => s.IsDynamicIterationEnabled)
                .HasDefaultValue(false);
            builder.HasIndex(s => new { s.OwnerId, s.ProjectId });

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

            // Fast ordering by timestamp within a session
            builder.HasIndex(m => new { m.ChatSessionId, m.Timestamp });
        });

        modelBuilder.Entity<ChatAttachment>(builder =>
        {
            builder.HasKey(a => a.Id);

            builder.HasOne(a => a.ChatSession)
                .WithMany(s => s.Attachments)
                .HasForeignKey(a => a.ChatSessionId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(a => a.ChatMessage)
                .WithMany(m => m.Attachments)
                .HasForeignKey(a => a.ChatMessageId)
                .OnDelete(DeleteBehavior.SetNull);

            builder.HasIndex(a => new { a.ChatSessionId, a.ChatMessageId });
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
            builder.Property(a => a.IsPrimary).HasDefaultValue(false);

            builder.HasOne(a => a.UserProfile)
                .WithMany(u => u.LinkedAccounts)
                .HasForeignKey(a => a.UserProfileId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasIndex(a => new { a.UserProfileId, a.Provider });

            builder.HasIndex(a => new { a.UserProfileId, a.Provider, a.IsPrimary })
                .HasFilter("\"IsPrimary\" = true AND \"Provider\" = 'GitHub'")
                .IsUnique();
        });

        modelBuilder.Entity<McpServerConnection>(builder =>
        {
            builder.HasKey(server => server.Id);
            builder.Property(server => server.Id).ValueGeneratedOnAdd();
            builder.Property(server => server.Enabled).HasDefaultValue(true);
            builder.Property(server => server.TransportType).HasDefaultValue("stdio");
            builder.Property(server => server.ArgumentsJson).HasDefaultValue("[]");
            builder.Property(server => server.DiscoveredToolsJson).HasDefaultValue("[]");

            builder.HasOne(server => server.UserProfile)
                .WithMany(user => user.McpServerConnections)
                .HasForeignKey(server => server.UserProfileId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasIndex(server => new { server.UserProfileId, server.Name }).IsUnique();
            builder.HasIndex(server => new { server.UserProfileId, server.Enabled });
        });

        modelBuilder.Entity<MemoryEntry>(builder =>
        {
            builder.HasKey(memory => memory.Id);
            builder.Property(memory => memory.Id).ValueGeneratedOnAdd();
            builder.Property(memory => memory.Type).HasDefaultValue(Memories.MemoryEntryTypes.Project);
            builder.Property(memory => memory.AlwaysInclude).HasDefaultValue(false);

            builder.HasOne(memory => memory.UserProfile)
                .WithMany(user => user.MemoryEntries)
                .HasForeignKey(memory => memory.UserProfileId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(memory => memory.Project)
                .WithMany(project => project.MemoryEntries)
                .HasForeignKey(memory => memory.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasIndex(memory => new { memory.UserProfileId, memory.ProjectId, memory.UpdatedAtUtc });
        });

        modelBuilder.Entity<PromptSkill>(builder =>
        {
            builder.HasKey(skill => skill.Id);
            builder.Property(skill => skill.Id).ValueGeneratedOnAdd();
            builder.Property(skill => skill.Enabled).HasDefaultValue(true);

            builder.HasOne(skill => skill.UserProfile)
                .WithMany(user => user.PromptSkills)
                .HasForeignKey(skill => skill.UserProfileId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(skill => skill.Project)
                .WithMany(project => project.PromptSkills)
                .HasForeignKey(skill => skill.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasIndex(skill => new { skill.UserProfileId, skill.ProjectId, skill.Enabled });
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
