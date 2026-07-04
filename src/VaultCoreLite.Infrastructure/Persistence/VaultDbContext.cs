using Microsoft.EntityFrameworkCore;
using VaultCoreLite.Domain.Ledger;

namespace VaultCoreLite.Infrastructure.Persistence;

public sealed class VaultDbContext : DbContext
{
    public VaultDbContext(DbContextOptions<VaultDbContext> options) : base(options) { }

    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductVersion> ProductVersions => Set<ProductVersion>();
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<AccountParameter> AccountParameters => Set<AccountParameter>();
    public DbSet<AccountParameterHistory> AccountParameterHistory => Set<AccountParameterHistory>();
    public DbSet<PostingInstructionBatch> PostingInstructionBatches => Set<PostingInstructionBatch>();
    public DbSet<PostingInstruction> PostingInstructions => Set<PostingInstruction>();
    public DbSet<Posting> Postings => Set<Posting>();
    public DbSet<Balance> Balances => Set<Balance>();
    public DbSet<ClientTransaction> ClientTransactions => Set<ClientTransaction>();
    public DbSet<OutboxEvent> OutboxEvents => Set<OutboxEvent>();
    public DbSet<ContractExecution> ContractExecutions => Set<ContractExecution>();
    public DbSet<Schedule> Schedules => Set<Schedule>();
    public DbSet<ScheduleRun> ScheduleRuns => Set<ScheduleRun>();
    public DbSet<MoneySimulation> MoneySimulations => Set<MoneySimulation>();
    public DbSet<SimulationConfirmationAudit> SimulationConfirmationAudits => Set<SimulationConfirmationAudit>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresExtension("uuid-ossp");

        modelBuilder.Entity<Product>(e =>
        {
            e.ToTable("products");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
            e.HasIndex(x => x.Name).IsUnique();
            e.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
        });

        modelBuilder.Entity<ProductVersion>(e =>
        {
            e.ToTable("product_versions");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.ProductId).HasColumnName("product_id");
            e.Property(x => x.Version).HasColumnName("version");
            e.Property(x => x.TSide).HasColumnName("tside").HasConversion<string>().IsRequired();
            e.Property(x => x.Denominations).HasColumnName("denominations").HasColumnType("text[]").IsRequired();
            e.Property(x => x.ContractName).HasColumnName("contract_name").HasMaxLength(200).IsRequired();
            e.Property(x => x.ContractVersion).HasColumnName("contract_version").HasMaxLength(100).IsRequired();
            e.Property(x => x.ParamsSchemaJson).HasColumnName("params_schema").HasColumnType("jsonb").IsRequired();
            e.Property(x => x.EventTypesJson).HasColumnName("event_types").HasColumnType("jsonb").IsRequired();
            e.Property(x => x.Status).HasColumnName("status").HasConversion<string>().IsRequired();
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.HasIndex(x => new { x.ProductId, x.Version }).IsUnique();
            e.HasOne(x => x.Product).WithMany(x => x.Versions).HasForeignKey(x => x.ProductId);
        });

        modelBuilder.Entity<Account>(e =>
        {
            e.ToTable("accounts");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.IsInternal).HasColumnName("is_internal");
            e.Property(x => x.ProductVersionId).HasColumnName("product_version_id");
            e.Property(x => x.Status).HasColumnName("status").HasConversion<string>().IsRequired();
            e.Property(x => x.PermittedDenominations).HasColumnName("permitted_denoms").HasColumnType("text[]").IsRequired();
            e.Property(x => x.TSide).HasColumnName("tside").HasConversion<string>().IsRequired();
            e.Property(x => x.OpenedAt).HasColumnName("opened_at");
            e.Property(x => x.ClosedAt).HasColumnName("closed_at");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.HasOne(x => x.ProductVersion).WithMany().HasForeignKey(x => x.ProductVersionId);
        });

        modelBuilder.Entity<AccountParameter>(e =>
        {
            e.ToTable("account_parameters");
            e.HasKey(x => new { x.AccountId, x.Name });
            e.Property(x => x.AccountId).HasColumnName("account_id");
            e.Property(x => x.Name).HasColumnName("name");
            e.Property(x => x.Value).HasColumnName("value").IsRequired();
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at");
            e.HasOne(x => x.Account).WithMany(x => x.Parameters).HasForeignKey(x => x.AccountId);
        });

        modelBuilder.Entity<AccountParameterHistory>(e =>
        {
            e.ToTable("account_parameter_history");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.AccountId).HasColumnName("account_id");
            e.Property(x => x.Name).HasColumnName("name");
            e.Property(x => x.OldValue).HasColumnName("old_value");
            e.Property(x => x.NewValue).HasColumnName("new_value");
            e.Property(x => x.ChangedBy).HasColumnName("changed_by");
            e.Property(x => x.ChangedAt).HasColumnName("changed_at");
        });

        modelBuilder.Entity<PostingInstructionBatch>(e =>
        {
            e.ToTable("posting_instruction_batches");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.ClientId).HasColumnName("client_id").IsRequired();
            e.Property(x => x.ClientBatchId).HasColumnName("client_batch_id").IsRequired();
            e.Property(x => x.Status).HasColumnName("status").HasConversion<string>().IsRequired();
            e.Property(x => x.RejectionCode).HasColumnName("rejection_code");
            e.Property(x => x.RejectionReason).HasColumnName("rejection_reason");
            e.Property(x => x.Source).HasColumnName("source").HasConversion<string>().IsRequired();
            e.Property(x => x.ValueTimestamp).HasColumnName("value_timestamp").IsRequired();
            e.Property(x => x.InsertedAt).HasColumnName("inserted_at").IsRequired();
            e.HasIndex(x => new { x.ClientId, x.ClientBatchId }).IsUnique();
        });

        modelBuilder.Entity<PostingInstruction>(e =>
        {
            e.ToTable("posting_instructions");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.BatchId).HasColumnName("batch_id");
            e.Property(x => x.Seq).HasColumnName("seq");
            e.Property(x => x.Type).HasColumnName("type").HasConversion<string>().IsRequired();
            e.Property(x => x.ClientTransactionId).HasColumnName("client_transaction_id");
            e.Property(x => x.Amount).HasColumnName("amount").HasPrecision(28, 9);
            e.Property(x => x.Denomination).HasColumnName("denomination");
            e.Property(x => x.Final).HasColumnName("final");
            e.HasIndex(x => new { x.BatchId, x.Seq }).IsUnique();
            e.HasOne(x => x.Batch).WithMany(x => x.Instructions).HasForeignKey(x => x.BatchId);
        });

        modelBuilder.Entity<Posting>(e =>
        {
            e.ToTable("postings");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.InstructionId).HasColumnName("instruction_id");
            e.Property(x => x.BatchId).HasColumnName("batch_id");
            e.Property(x => x.AccountId).HasColumnName("account_id");
            e.Property(x => x.AccountAddress).HasColumnName("account_address").IsRequired();
            e.Property(x => x.Asset).HasColumnName("asset").IsRequired();
            e.Property(x => x.Denomination).HasColumnName("denomination").IsRequired();
            e.Property(x => x.Amount).HasColumnName("amount").HasPrecision(28, 9).IsRequired();
            e.Property(x => x.Credit).HasColumnName("credit").IsRequired();
            e.Property(x => x.Phase).HasColumnName("phase").HasConversion<string>().IsRequired();
            e.Property(x => x.ValueTimestamp).HasColumnName("value_timestamp");
            e.Property(x => x.InsertedAt).HasColumnName("inserted_at");
            e.HasIndex(x => new { x.AccountId, x.ValueTimestamp });
            e.HasIndex(x => x.BatchId);
            e.HasOne(x => x.Instruction).WithMany(x => x.Postings).HasForeignKey(x => x.InstructionId);
        });

        modelBuilder.Entity<Balance>(e =>
        {
            e.ToTable("balances");
            e.HasKey(x => new { x.AccountId, x.AccountAddress, x.Asset, x.Denomination, x.Phase });
            e.Property(x => x.AccountId).HasColumnName("account_id");
            e.Property(x => x.AccountAddress).HasColumnName("account_address");
            e.Property(x => x.Asset).HasColumnName("asset");
            e.Property(x => x.Denomination).HasColumnName("denomination");
            e.Property(x => x.Phase).HasColumnName("phase").HasConversion<string>();
            e.Property(x => x.TotalCredits).HasColumnName("total_credits").HasPrecision(28, 9);
            e.Property(x => x.TotalDebits).HasColumnName("total_debits").HasPrecision(28, 9);
            e.Property(x => x.LastPostingAt).HasColumnName("last_posting_at");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        });

        modelBuilder.Entity<ClientTransaction>(e =>
        {
            e.ToTable("client_transactions");
            e.HasKey(x => new { x.ClientId, x.ClientTransactionId });
            e.Property(x => x.ClientId).HasColumnName("client_id");
            e.Property(x => x.ClientTransactionId).HasColumnName("client_transaction_id");
            e.Property(x => x.AccountId).HasColumnName("account_id");
            e.Property(x => x.SettlementAccountId).HasColumnName("settlement_account_id");
            e.Property(x => x.Denomination).HasColumnName("denomination");
            e.Property(x => x.Direction).HasColumnName("direction").HasConversion<string>();
            e.Property(x => x.Authorised).HasColumnName("authorised").HasPrecision(28, 9);
            e.Property(x => x.Settled).HasColumnName("settled").HasPrecision(28, 9);
            e.Property(x => x.Released).HasColumnName("released").HasPrecision(28, 9);
            e.Property(x => x.Status).HasColumnName("status").HasConversion<string>();
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        });

        modelBuilder.Entity<OutboxEvent>(e =>
        {
            e.ToTable("outbox_events");
            e.HasKey(x => x.Seq);
            e.Property(x => x.Seq).HasColumnName("seq").ValueGeneratedOnAdd();
            e.Property(x => x.Topic).HasColumnName("topic");
            e.Property(x => x.Key).HasColumnName("key");
            e.Property(x => x.PayloadJson).HasColumnName("payload").HasColumnType("jsonb");
            e.Property(x => x.InsertedAt).HasColumnName("inserted_at");
        });

        modelBuilder.Entity<ContractExecution>(e =>
        {
            e.ToTable("contract_executions");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.AccountId).HasColumnName("account_id");
            e.Property(x => x.Hook).HasColumnName("hook");
            e.Property(x => x.TriggerId).HasColumnName("trigger_id");
            e.Property(x => x.ContractName).HasColumnName("contract_name");
            e.Property(x => x.Outcome).HasColumnName("outcome");
            e.Property(x => x.DetailJson).HasColumnName("detail").HasColumnType("jsonb");
            e.Property(x => x.DurationMs).HasColumnName("duration_ms");
            e.Property(x => x.InsertedAt).HasColumnName("inserted_at");
        });

        modelBuilder.Entity<Schedule>(e =>
        {
            e.ToTable("schedules");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.AccountId).HasColumnName("account_id");
            e.Property(x => x.EventName).HasColumnName("event_name");
            e.Property(x => x.Cron).HasColumnName("cron");
            e.Property(x => x.Timezone).HasColumnName("timezone");
            e.Property(x => x.NextDueAt).HasColumnName("next_due_at");
            e.Property(x => x.Status).HasColumnName("status");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at");
            e.HasIndex(x => new { x.AccountId, x.EventName }).IsUnique();
            e.HasIndex(x => x.NextDueAt).HasDatabaseName("idx_schedules_due");
        });



        modelBuilder.Entity<MoneySimulation>(e =>
        {
            e.ToTable("money_simulations");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.Kind).HasColumnName("kind").HasConversion<string>().IsRequired();
            e.Property(x => x.ClientId).HasColumnName("client_id").IsRequired();
            e.Property(x => x.ClientBatchId).HasColumnName("client_batch_id").IsRequired();
            e.Property(x => x.RequestedBy).HasColumnName("requested_by").IsRequired();
            e.Property(x => x.RequestHash).HasColumnName("request_hash").IsRequired();
            e.Property(x => x.RequestJson).HasColumnName("request_json").HasColumnType("jsonb").IsRequired();
            e.Property(x => x.PreviewJson).HasColumnName("preview_json").HasColumnType("jsonb").IsRequired();
            e.Property(x => x.ConfirmationTokenHash).HasColumnName("confirmation_token_hash").IsRequired();
            e.Property(x => x.Status).HasColumnName("status").HasConversion<string>().IsRequired();
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.ExpiresAt).HasColumnName("expires_at");
            e.Property(x => x.ConfirmedAt).HasColumnName("confirmed_at");
            e.Property(x => x.ExecutedAt).HasColumnName("executed_at");
            e.Property(x => x.ExecutedBatchId).HasColumnName("executed_batch_id");
            e.Property(x => x.RejectionCode).HasColumnName("rejection_code");
            e.Property(x => x.RejectionReason).HasColumnName("rejection_reason");
            e.HasIndex(x => new { x.ClientId, x.ClientBatchId }).IsUnique();
            e.HasIndex(x => x.Status);
            e.HasIndex(x => x.ExpiresAt);
        });

        modelBuilder.Entity<SimulationConfirmationAudit>(e =>
        {
            e.ToTable("simulation_confirmation_audits");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.SimulationId).HasColumnName("simulation_id");
            e.Property(x => x.Status).HasColumnName("status").HasConversion<string>().IsRequired();
            e.Property(x => x.Actor).HasColumnName("actor").IsRequired();
            e.Property(x => x.Reason).HasColumnName("reason").IsRequired();
            e.Property(x => x.OccurredAt).HasColumnName("occurred_at");
            e.HasOne(x => x.Simulation).WithMany().HasForeignKey(x => x.SimulationId);
        });

        modelBuilder.Entity<ScheduleRun>(e =>
        {
            e.ToTable("schedule_runs");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.ScheduleId).HasColumnName("schedule_id");
            e.Property(x => x.DueAt).HasColumnName("due_at");
            e.Property(x => x.RunnerId).HasColumnName("runner_id");
            e.Property(x => x.StartedAt).HasColumnName("started_at");
            e.Property(x => x.FinishedAt).HasColumnName("finished_at");
            e.Property(x => x.Status).HasColumnName("status");
            e.Property(x => x.Attempts).HasColumnName("attempts");
            e.Property(x => x.Error).HasColumnName("error");
            e.HasIndex(x => new { x.ScheduleId, x.DueAt }).IsUnique();
            e.HasOne(x => x.Schedule).WithMany().HasForeignKey(x => x.ScheduleId);
        });
    }
}
