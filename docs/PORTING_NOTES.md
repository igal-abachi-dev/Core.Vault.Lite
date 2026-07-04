# Porting notes from Go to C#

The Go core used Starlark for product logic. The C# port deliberately does not embed Python, JavaScript, TypeScript, or Roslyn. Product behavior is exposed as compiled trusted .NET plugins implementing `VaultCoreLite.Contracts.IProductContract`.

The architectural seam is `IProductRuntime` in the Application project. Current implementations:

- `StubProductRuntime` for local development.
- `PluginProductRuntime` using `AssemblyLoadContext` + `AssemblyDependencyResolver`.

If product logic becomes untrusted or resource-heavy, add `GrpcProductRuntime` behind the same interface.

The EF Core transaction path intentionally mirrors the Go pipeline:

1. Fill default settlement account.
2. Check idempotency.
3. Load client transactions.
4. Expand typed instructions into postings.
5. Lock accounts in deterministic order.
6. Run `pre_posting` hook.
7. Insert batch/instructions/postings.
8. Update balances.
9. Update client transaction projection.
10. Append outbox.
11. Commit.
12. Run post-commit product directives as new contract batches.
