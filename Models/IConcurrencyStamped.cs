namespace GharAagan.Models;

/// <summary>
/// Marks an entity that carries an optimistic-concurrency token. SQLite has no native
/// rowversion type, so the token is a Guid re-stamped on every insert/update by the
/// DbContext. A stale token causes SaveChanges to throw DbUpdateConcurrencyException.
/// </summary>
public interface IConcurrencyStamped
{
    Guid RowVersion { get; set; }
}
