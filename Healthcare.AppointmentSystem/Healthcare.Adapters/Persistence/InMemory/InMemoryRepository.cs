using Healthcare.Domain.Common;

namespace Healthcare.Adapters.Persistence.InMemory;

/// <summary>
/// Base class for in-memory repositories providing common CRUD operations.
/// </summary>
/// <typeparam name="TEntity">The entity type (must inherit from Entity).</typeparam>
/// <remarks>
/// Design Pattern: Repository Pattern + Template Method Pattern
/// 
/// This base class provides:
/// - Thread-safe in-memory storage
/// - Auto-incrementing IDs
/// - Common operations (Add, Update, Delete, GetById)
/// 
/// Thread Safety:
/// Uses lock statements to ensure data integrity in concurrent scenarios.
/// In production, you'd use a real database with transaction isolation.
/// </remarks>
public abstract class InMemoryRepository<TEntity> where TEntity : Entity
{
    private readonly List<TEntity> _entities = new();
    private readonly object _lock = new();
    private int _nextId = 1;

    /// <summary>
    /// Gets all entities from the repository.
    /// </summary>
    protected Task<IEnumerable<TEntity>> GetAllAsync()
    {
        lock (_lock)
        {
            // Return a copy to prevent external modifications
            return Task.FromResult(_entities.ToList().AsEnumerable());
        }
    }

    /// <summary>
    /// Gets an entity by its ID.
    /// </summary>
    protected Task<TEntity?> GetByIdAsync(int id)
    {
        lock (_lock)
        {
            var entity = _entities.FirstOrDefault(e => e.Id == id);
            return Task.FromResult(entity);
        }
    }

    /// <summary>
    /// Adds a new entity to the repository.
    /// </summary>
    protected Task AddAsync(TEntity entity)
    {
        lock (_lock)
        {
            // Assign auto-incrementing ID using reflection
            var idProperty = typeof(TEntity).GetProperty("Id");
            if (idProperty != null && entity.Id == 0)
            {
                idProperty.SetValue(entity, _nextId++);
            }

            _entities.Add(entity);
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// Updates an existing entity.
    /// </summary>
    protected Task UpdateAsync(TEntity entity)
    {
        lock (_lock)
        {
            var existing = _entities.FirstOrDefault(e => e.Id == entity.Id);
            if (existing != null)
            {
                var index = _entities.IndexOf(existing);
                _entities[index] = entity;
            }
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// Deletes an entity by its ID.
    /// </summary>
    protected Task DeleteAsync(int id)
    {
        lock (_lock)
        {
            var entity = _entities.FirstOrDefault(e => e.Id == id);
            if (entity != null)
            {
                _entities.Remove(entity);
            }
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// Finds entities matching a predicate.
    /// </summary>
    protected Task<IEnumerable<TEntity>> FindAsync(Func<TEntity, bool> predicate)
    {
        lock (_lock)
        {
            var results = _entities.Where(predicate).ToList();
            return Task.FromResult(results.AsEnumerable());
        }
    }

    /// <summary>
    /// Checks if any entity matches the predicate.
    /// </summary>
    protected Task<bool> AnyAsync(Func<TEntity, bool> predicate)
    {
        lock (_lock)
        {
            return Task.FromResult(_entities.Any(predicate));
        }
    }

    /// <summary>
    /// Gets the count of entities.
    /// </summary>
    protected Task<int> CountAsync()
    {
        lock (_lock)
        {
            return Task.FromResult(_entities.Count);
        }
    }

    /// <summary>
    /// Clears all entities (useful for testing).
    /// </summary>
    protected void Clear()
    {
        lock (_lock)
        {
            _entities.Clear();
            _nextId = 1;
        }
    }
}