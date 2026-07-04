namespace VaultCoreLite.Domain.Ledger;

public enum TSide { Asset, Liability }
public enum Phase { Committed, PendingIn, PendingOut }
public enum InstructionType { InboundAuth, OutboundAuth, Settlement, Release, InboundHardSettlement, OutboundHardSettlement, Transfer, Custom }
public enum BatchSource { Api, Contract, Scheduler, Migration }
public enum BatchStatus { Accepted, Rejected }
public enum AccountStatus { Pending, Open, Closed }
public enum ProductVersionStatus { Draft, Active, Retired }
public enum ClientTransactionDirection { In, Out }
public enum ClientTransactionStatus { Authorised, PartiallySettled, Settled, Released }
public enum SimulationKind { Transaction, SavingsPlan, LoanPayoff, CashflowForecast, ProductLifecycle }
public enum SimulationStatus { PendingConfirmation, Confirmed, Executed, Expired, Rejected, Cancelled }
public enum ConfirmationStatus { Accepted, Failed, Expired, Replay }

public static class LedgerConstants
{
    public const string DefaultAddress = "DEFAULT";
    public const string DefaultAsset = "COMMERCIAL_BANK_MONEY";
    public const string SettlementSuspense = "SETTLEMENT_SUSPENSE";
}
