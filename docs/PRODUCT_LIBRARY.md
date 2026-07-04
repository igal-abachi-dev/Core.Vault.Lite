# Product library

`plugins/BankProducts.Plugin` contains the default v1.1 product pack.

## Contract names

Use these names in `POST /v1/product-versions`:

| ContractName | TSide | Product type | Important parameters | Common schedules |
|---|---|---|---|---|
| `CurrentAccount` | `Liability` | Current/checking account | `overdraft_limit`, `transaction_fee`, `monthly_fee` | `MONTHLY_ACCOUNT_FEE` |
| `SavingsAccount` | `Liability` | Savings account | `min_balance`, `annual_rate` | `ACCRUE_INTEREST`, `APPLY_INTEREST` |
| `TermDeposit` | `Liability` | Fixed/term deposit | `maturity_date`, `allow_early_withdrawal`, `term_rate` | `MATURITY_INTEREST` |
| `Wallet` | `Liability` | Prepaid wallet | none required | none |
| `PersonalLoan` | `Asset` | Personal loan | `principal_limit`, `annual_rate` | `ACCRUE_INTEREST` |
| `MortgageLoan` | `Asset` | Mortgage/home loan | `principal_limit`, `annual_rate` | `ACCRUE_INTEREST` |
| `CreditCard` | `Asset` | Credit card/revolving credit | `credit_limit`, `apr`, `annual_fee` | `ACCRUE_INTEREST`, `ANNUAL_FEE` |

## Notes

- Liability products are credit-positive from the customer-facing perspective.
- Asset products are debit-positive from the customer-facing perspective.
- Product directives always go back through the same posting engine as normal API batches.
- Contract plugins do not receive `DbContext`, repositories, connection strings, service providers, or file/network abstractions.
