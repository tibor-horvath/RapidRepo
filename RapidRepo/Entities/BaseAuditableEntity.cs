using RapidRepo.Entities;
using RapidRepo.Entities.Interfaces;

namespace Repository.Entities;

/// <summary>
/// Represents a base auditable entity with common audit fields.
/// </summary>
/// <typeparam name="TId">The type of the entity's identifier.</typeparam>
/// <typeparam name="TKey">The type of the user identifier.</typeparam>
public class BaseAuditableEntity<TId, TKey> : BaseEntity<TId>, IAuditableEntity<TKey>
    where TId : struct
    where TKey : struct
{
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public TKey CreatedBy { get; set; }
    public DateTime? ModifiedAt { get; set; }
    public TKey? ModifiedBy { get; set; }
}
