using Domain.Entities;
using Domain.Common;
using Domain.Repositories;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace Infrastructure.Repositories;

public class Repository<T> : IRepository<T> where T : class
{
    protected readonly ApplicationDbContext _context;
    protected readonly DbSet<T> _dbSet;

    public Repository(ApplicationDbContext context)
    {
        _context = context;
        _dbSet = context.Set<T>();
    }

    public async Task<T?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        try
        {
            // Use FindAsync which benefits from EF tracking when needed
            return await _dbSet.FindAsync(new object[] { id }, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // Request was cancelled - return null to allow callers to handle gracefully
            return null;
        }
    }

    public async Task<IEnumerable<T>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // Use AsNoTracking for read-only queries to reduce change tracker overhead
            return await _dbSet.AsNoTracking().ToListAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return Enumerable.Empty<T>();
        }
    }

    public async Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
    {
        try
        {
            // Use AsNoTracking and check cancellation token before starting the DB query
            if (cancellationToken.IsCancellationRequested)
                return Enumerable.Empty<T>();

            return await _dbSet.AsNoTracking().Where(predicate).ToListAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // If the request was cancelled while executing the query, return empty set
            return Enumerable.Empty<T>();
        }
    }

    public async Task<T> AddAsync(T entity, CancellationToken cancellationToken = default)
    {
        await _dbSet.AddAsync(entity, cancellationToken);
        return entity;
    }

    public Task UpdateAsync(T entity, CancellationToken cancellationToken = default)
    {
        _dbSet.Update(entity);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(T entity, CancellationToken cancellationToken = default)
    {
        _dbSet.Remove(entity);
        return Task.CompletedTask;
    }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return await _context.SaveChangesAsync(cancellationToken);
    }
}
