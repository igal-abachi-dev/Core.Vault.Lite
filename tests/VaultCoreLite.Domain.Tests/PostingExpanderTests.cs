using VaultCoreLite.Domain.Ledger;
using Xunit;

namespace VaultCoreLite.Domain.Tests;

public sealed class PostingExpanderTests
{
    [Fact]
    public void Transfer_expands_to_zero_sum_postings()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        var req = new BatchRequest("client", "batch-1", BatchSource.Api, DateTimeOffset.UtcNow,
            new[] { new InstructionRequest(InstructionType.Transfer, a, b, null, null, 100m, "ILS", false, null) });
        var expanded = PostingExpander.Expand(Guid.NewGuid(), req, new Dictionary<string, ClientTransaction>());
        Assert.Equal(2, expanded.Postings.Count);
        PostingExpander.ValidateZeroSum(expanded.Postings);
    }

    [Fact]
    public void Settlement_cannot_exceed_authorised_amount()
    {
        var account = Guid.NewGuid();
        var suspense = Guid.NewGuid();
        var tx = new ClientTransaction("client", "ctx-1", account, suspense, "ILS", ClientTransactionDirection.Out, 100m);
        var req = new BatchRequest("client", "batch-2", BatchSource.Api, DateTimeOffset.UtcNow,
            new[] { new InstructionRequest(InstructionType.Settlement, account, null, null, "ctx-1", 150m, "ILS", false, null) });
        var ex = Assert.Throws<BusinessRejectionException>(() => PostingExpander.Expand(Guid.NewGuid(), req, new Dictionary<string, ClientTransaction> { ["ctx-1"] = tx }));
        Assert.Equal("SETTLEMENT_EXCEEDS_AUTH", ex.Code);
    }
}
