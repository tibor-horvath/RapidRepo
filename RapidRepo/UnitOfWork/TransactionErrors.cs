namespace RapidRepo.UnitOfWork;

/// <summary>
/// The errors shared by the transaction members of <see cref="IUnitOfWork{TUserKey}"/> and
/// <see cref="UnitOfWorkBase{TUserKey}"/>, so that every unit of work without transaction support reports it the same way.
/// </summary>
internal static class TransactionErrors
{
    public static NotSupportedException NotSupported(Type unitOfWorkType) => new(
        $"'{unitOfWorkType.Name}' does not support transactions: its provider does not implement them. " +
        "Derive from the unit of work of a provider that supports transactions.");
}
