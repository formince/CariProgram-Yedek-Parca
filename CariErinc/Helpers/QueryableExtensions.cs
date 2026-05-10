using Microsoft.EntityFrameworkCore;

namespace CariErinc.Helpers;

public static class QueryableExtensions
{
    public static async Task<PagedResult<T>> ToPagedListAsync<T>(
        this IQueryable<T> query, 
        int page, 
        int pageSize = 30)
    {
        var count = await query.CountAsync();
        var items = await query.Skip((page - 1) * pageSize)
                               .Take(pageSize)
                               .ToListAsync();
        
        return new PagedResult<T>(items, count, page, pageSize);
    }
}
