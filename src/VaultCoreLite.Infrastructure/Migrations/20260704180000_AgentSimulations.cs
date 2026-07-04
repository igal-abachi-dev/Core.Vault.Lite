using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VaultCoreLite.Infrastructure.Migrations;

public partial class AgentSimulations : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
CREATE TABLE IF NOT EXISTS money_simulations (
  id uuid PRIMARY KEY,
  kind text NOT NULL,
  client_id text NOT NULL,
  client_batch_id text NOT NULL,
  requested_by text NOT NULL,
  request_hash text NOT NULL,
  request_json jsonb NOT NULL,
  preview_json jsonb NOT NULL,
  confirmation_token_hash text NOT NULL,
  status text NOT NULL,
  created_at timestamptz NOT NULL,
  expires_at timestamptz NOT NULL,
  confirmed_at timestamptz,
  executed_at timestamptz,
  executed_batch_id uuid,
  rejection_code text,
  rejection_reason text,
  UNIQUE(client_id, client_batch_id)
);
CREATE INDEX IF NOT EXISTS idx_money_simulations_status ON money_simulations(status);
CREATE INDEX IF NOT EXISTS idx_money_simulations_expires ON money_simulations(expires_at);

CREATE TABLE IF NOT EXISTS simulation_confirmation_audits (
  id uuid PRIMARY KEY,
  simulation_id uuid NOT NULL REFERENCES money_simulations(id),
  status text NOT NULL,
  actor text NOT NULL,
  reason text NOT NULL,
  occurred_at timestamptz NOT NULL
);
CREATE INDEX IF NOT EXISTS idx_simulation_confirmation_audits_simulation ON simulation_confirmation_audits(simulation_id, occurred_at);
""");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
DROP TABLE IF EXISTS simulation_confirmation_audits;
DROP TABLE IF EXISTS money_simulations;
""");
    }
}
