using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VaultCoreLite.Infrastructure.Migrations;

public partial class InitialLedgerSchema : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
CREATE TABLE products (
  id uuid PRIMARY KEY,
  name text NOT NULL UNIQUE,
  created_at timestamptz NOT NULL
);

CREATE TABLE product_versions (
  id uuid PRIMARY KEY,
  product_id uuid NOT NULL REFERENCES products(id),
  version int NOT NULL,
  tside text NOT NULL,
  denominations text[] NOT NULL,
  contract_name text NOT NULL,
  contract_version text NOT NULL,
  params_schema jsonb NOT NULL DEFAULT '{}',
  event_types jsonb NOT NULL DEFAULT '[]',
  status text NOT NULL,
  created_at timestamptz NOT NULL,
  UNIQUE(product_id, version)
);

CREATE TABLE accounts (
  id uuid PRIMARY KEY,
  is_internal boolean NOT NULL DEFAULT false,
  product_version_id uuid REFERENCES product_versions(id),
  status text NOT NULL,
  permitted_denoms text[] NOT NULL,
  tside text NOT NULL,
  opened_at timestamptz,
  closed_at timestamptz,
  created_at timestamptz NOT NULL,
  CHECK (is_internal = (product_version_id IS NULL))
);

CREATE TABLE account_parameters (
  account_id uuid NOT NULL REFERENCES accounts(id),
  name text NOT NULL,
  value text NOT NULL,
  updated_at timestamptz NOT NULL,
  PRIMARY KEY(account_id, name)
);

CREATE TABLE account_parameter_history (
  id uuid PRIMARY KEY,
  account_id uuid NOT NULL,
  name text NOT NULL,
  old_value text,
  new_value text NOT NULL,
  changed_by text NOT NULL,
  changed_at timestamptz NOT NULL
);

CREATE TABLE posting_instruction_batches (
  id uuid PRIMARY KEY,
  client_id text NOT NULL,
  client_batch_id text NOT NULL,
  status text NOT NULL,
  rejection_code text,
  rejection_reason text,
  source text NOT NULL,
  value_timestamp timestamptz NOT NULL,
  inserted_at timestamptz NOT NULL,
  UNIQUE(client_id, client_batch_id)
);

CREATE TABLE posting_instructions (
  id uuid PRIMARY KEY,
  batch_id uuid NOT NULL REFERENCES posting_instruction_batches(id),
  seq int NOT NULL,
  type text NOT NULL,
  client_transaction_id text,
  amount numeric(28,9),
  denomination text,
  final boolean NOT NULL DEFAULT false,
  UNIQUE(batch_id, seq)
);

CREATE TABLE postings (
  id uuid PRIMARY KEY,
  instruction_id uuid NOT NULL REFERENCES posting_instructions(id),
  batch_id uuid NOT NULL,
  account_id uuid NOT NULL REFERENCES accounts(id),
  account_address text NOT NULL DEFAULT 'DEFAULT',
  asset text NOT NULL DEFAULT 'COMMERCIAL_BANK_MONEY',
  denomination text NOT NULL,
  amount numeric(28,9) NOT NULL CHECK(amount > 0),
  credit boolean NOT NULL,
  phase text NOT NULL,
  value_timestamp timestamptz NOT NULL,
  inserted_at timestamptz NOT NULL
);
CREATE INDEX idx_postings_account_time ON postings(account_id, value_timestamp);
CREATE INDEX idx_postings_batch ON postings(batch_id);

CREATE TABLE balances (
  account_id uuid NOT NULL,
  account_address text NOT NULL,
  asset text NOT NULL,
  denomination text NOT NULL,
  phase text NOT NULL,
  total_credits numeric(28,9) NOT NULL DEFAULT 0,
  total_debits numeric(28,9) NOT NULL DEFAULT 0,
  last_posting_at timestamptz,
  updated_at timestamptz NOT NULL,
  PRIMARY KEY(account_id, account_address, asset, denomination, phase)
);

CREATE TABLE client_transactions (
  client_id text NOT NULL,
  client_transaction_id text NOT NULL,
  account_id uuid NOT NULL,
  settlement_account_id uuid NOT NULL,
  denomination text NOT NULL,
  direction text NOT NULL,
  authorised numeric(28,9) NOT NULL DEFAULT 0,
  settled numeric(28,9) NOT NULL DEFAULT 0,
  released numeric(28,9) NOT NULL DEFAULT 0,
  status text NOT NULL,
  updated_at timestamptz NOT NULL,
  PRIMARY KEY(client_id, client_transaction_id)
);

CREATE TABLE outbox_events (
  seq bigserial PRIMARY KEY,
  topic text NOT NULL,
  key text NOT NULL,
  payload jsonb NOT NULL,
  inserted_at timestamptz NOT NULL
);

CREATE TABLE contract_executions (
  id uuid PRIMARY KEY,
  account_id uuid,
  hook text NOT NULL,
  trigger_id uuid NOT NULL,
  contract_name text NOT NULL,
  outcome text NOT NULL,
  detail jsonb NOT NULL,
  duration_ms int NOT NULL,
  inserted_at timestamptz NOT NULL
);

CREATE TABLE schedules (
  id uuid PRIMARY KEY,
  account_id uuid NOT NULL REFERENCES accounts(id),
  event_name text NOT NULL,
  cron text NOT NULL,
  timezone text NOT NULL DEFAULT 'UTC',
  next_due_at timestamptz NOT NULL,
  status text NOT NULL,
  created_at timestamptz NOT NULL,
  updated_at timestamptz NOT NULL,
  UNIQUE(account_id, event_name)
);
CREATE INDEX idx_schedules_due ON schedules(next_due_at) WHERE status='ACTIVE';

CREATE TABLE schedule_runs (
  id uuid PRIMARY KEY,
  schedule_id uuid NOT NULL REFERENCES schedules(id),
  due_at timestamptz NOT NULL,
  runner_id text NOT NULL,
  started_at timestamptz,
  finished_at timestamptz,
  status text NOT NULL,
  attempts int NOT NULL DEFAULT 1,
  error text,
  UNIQUE(schedule_id, due_at)
);

CREATE OR REPLACE FUNCTION forbid_ledger_mutation() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
  RAISE EXCEPTION 'ledger/audit tables are append-only';
END $$;
CREATE TRIGGER posting_instruction_batches_append_only BEFORE UPDATE OR DELETE ON posting_instruction_batches FOR EACH ROW EXECUTE FUNCTION forbid_ledger_mutation();
CREATE TRIGGER posting_instructions_append_only BEFORE UPDATE OR DELETE ON posting_instructions FOR EACH ROW EXECUTE FUNCTION forbid_ledger_mutation();
CREATE TRIGGER postings_append_only BEFORE UPDATE OR DELETE ON postings FOR EACH ROW EXECUTE FUNCTION forbid_ledger_mutation();
CREATE TRIGGER outbox_events_append_only BEFORE UPDATE OR DELETE ON outbox_events FOR EACH ROW EXECUTE FUNCTION forbid_ledger_mutation();
CREATE TRIGGER contract_executions_append_only BEFORE UPDATE OR DELETE ON contract_executions FOR EACH ROW EXECUTE FUNCTION forbid_ledger_mutation();
""");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
DROP TABLE IF EXISTS schedule_runs;
DROP TABLE IF EXISTS schedules;
DROP TABLE IF EXISTS contract_executions;
DROP TABLE IF EXISTS outbox_events;
DROP TABLE IF EXISTS client_transactions;
DROP TABLE IF EXISTS balances;
DROP TABLE IF EXISTS postings;
DROP TABLE IF EXISTS posting_instructions;
DROP TABLE IF EXISTS posting_instruction_batches;
DROP TABLE IF EXISTS account_parameter_history;
DROP TABLE IF EXISTS account_parameters;
DROP TABLE IF EXISTS accounts;
DROP TABLE IF EXISTS product_versions;
DROP TABLE IF EXISTS products;
DROP FUNCTION IF EXISTS forbid_ledger_mutation();
""");
    }
}
