namespace VaultCoreLite.Domain.Common;

public static class EntityId
{
    public static Guid New() => Guid.CreateVersion7();
}
