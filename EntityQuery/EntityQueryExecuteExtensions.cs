using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Dapper;

namespace EntityQuery;

public static class EntityQueryExecuteExtensions
{
    public static void TryOpen(this IDbConnection con)
    {
        if (con.State == ConnectionState.Closed)
        {
            con.Open();
        }
    }

    public static async Task<int> InsertAsync<TEntity>(
        this IDbConnection con,
        TEntity entity,
        IDbTransaction? tx = null,
        string? cacheKey = null
    )
    {
        var query = EqBuilder<TEntity>.Create(cacheKey).Insert().Build();

        con.TryOpen();
        return await con.ExecuteAsync(query, entity, tx);
    }

    public static Task<int> InsertAndGetIntIdAsync<TEntity>(
        this IDbConnection con,
        TEntity entity,
        IDbTransaction? tx = null,
        string? cacheKey = null
    )
    {
        return InsertAndGetIdAsync<int, TEntity>(con, entity, tx, cacheKey);
    }

    public static Task<long> InsertAndGetLongIdAsync<TEntity>(
        this IDbConnection con,
        TEntity entity,
        IDbTransaction? tx = null,
        string? cacheKey = null
    )
    {
        return InsertAndGetIdAsync<long, TEntity>(con, entity, tx, cacheKey);
    }

    public static async Task<TKey> InsertAndGetIdAsync<TKey, TEntity>(
        this IDbConnection con,
        TEntity entity,
        IDbTransaction? tx = null,
        string? cacheKey = null
    )
    {
        var query = EqBuilder<TEntity>.Create(cacheKey).InsertWithIdentity().Build();

        con.TryOpen();
        var ret = await con.QuerySingleAsync(query, entity, tx);
        return (TKey)ret._id;
    }

    public static async Task<int> InsertListAsync<TEntity>(
        this IDbConnection con,
        IEnumerable<TEntity> entities,
        IDbTransaction? tx = null,
        string? cacheKey = null
    )
    {
        var query = EqBuilder<TEntity>.Create(cacheKey).Insert().Build();

        con.TryOpen();
        return await con.ExecuteAsync(query, entities, tx);
    }

    public static async Task<TEntity?> SelectByIdAsync<TEntity>(
        this IDbConnection con,
        object id,
        IDbTransaction? tx = null,
        string? cacheKey = null
    )
    {
        var query = EqBuilder<TEntity>.Create(cacheKey).Select().Build();

        var idProps = typeof(TEntity).GetIdProperties();
        var param = new DynamicParameters();
        if (idProps.Length == 1)
        {
            param.Add("@" + idProps.First().Name, id);
        }
        else
        {
            foreach (var prop in idProps)
            {
                param.Add("@" + prop.Name, id.GetType().GetProperty(prop.Name)!.GetValue(id, null));
            }
        }

        con.TryOpen();
        return await con.QuerySingleOrDefaultAsync<TEntity>(query, param, tx);
    }

    public static async Task<TEntity?> SelectByEntityAsync<TEntity>(
        this IDbConnection con,
        TEntity entity,
        IDbTransaction? tx = null,
        string? cacheKey = null
    )
    {
        var query = EqBuilder<TEntity>.Create(cacheKey).Select().Build();

        var idProps = typeof(TEntity).GetIdProperties();
        var param = new DynamicParameters();
        foreach (var prop in idProps)
        {
            param.Add("@" + prop.Name, prop.GetValue(entity, null));
        }

        con.TryOpen();
        return await con.QuerySingleOrDefaultAsync<TEntity>(query, param, tx);
    }

    public static async Task<IEnumerable<TEntity>> SelectAllAsync<TEntity>(
        this IDbConnection con,
        IDbTransaction? tx = null,
        string? cacheKey = null
    )
    {
        var query = EqBuilder<TEntity>.Create(cacheKey).Select("").Build();

        con.TryOpen();
        return await con.QueryAsync<TEntity>(query, null, tx);
    }

    public static async Task<IEnumerable<TEntity>> SelectAsync<TEntity>(
        this IDbConnection con,
        string where,
        object param,
        IDbTransaction? tx = null,
        string? cacheKey = null
    )
    {
        var query = EqBuilder<TEntity>.Create(cacheKey).Select(where).Build();

        con.TryOpen();
        return await con.QueryAsync<TEntity>(query, param, tx);
    }

    public static async Task<int> DeleteByIdAsync<TEntity>(
        this IDbConnection con,
        object id,
        IDbTransaction? tx = null,
        string? cacheKey = null
    )
    {
        var query = EqBuilder<TEntity>.Create(cacheKey).Delete().Build();

        var idProps = typeof(TEntity).GetIdProperties();
        var param = new DynamicParameters();
        if (idProps.Length == 1)
        {
            param.Add("@" + idProps.First().Name, id);
        }
        else
        {
            foreach (var prop in idProps)
            {
                var propInIdObject = id.GetType().GetProperty(prop.Name);
                if (propInIdObject != null)
                {
                    param.Add("@" + prop.Name, propInIdObject!.GetValue(id, null));
                }
                else
                {
                    throw new InvalidOperationException(
                        $"Cannot find id value from argument. entity={typeof(TEntity).Name}, propertyName={prop.Name}"
                    );
                }
            }
        }

        con.TryOpen();
        return await con.ExecuteAsync(query, param, tx);
    }

    public static async Task<int> DeleteAsync<TEntity>(
        this IDbConnection con,
        TEntity entity,
        IDbTransaction? tx = null,
        string? cacheKey = null
    )
    {
        var query = EqBuilder<TEntity>.Create(cacheKey).Delete().Build();

        var idProps = typeof(TEntity).GetIdProperties();
        var param = new DynamicParameters();
        foreach (var prop in idProps)
        {
            param.Add("@" + prop.Name, prop.GetValue(entity, null));
        }

        con.TryOpen();
        return await con.ExecuteAsync(query, param, tx);
    }

    public static async Task<int> DeleteAsync<TEntity>(
        this IDbConnection con,
        string where,
        object param,
        IDbTransaction? tx = null,
        string? cacheKey = null
    )
    {
        var query = EqBuilder<TEntity>.Create(cacheKey).Delete(where).Build();

        con.TryOpen();
        return await con.ExecuteAsync(query, param, tx);
    }

    public static async Task<int> UpdateAsync<TEntity>(
        this IDbConnection con,
        TEntity entity,
        IDbTransaction? tx = null,
        string? cacheKey = null
    )
    {
        var query = EqBuilder<TEntity>.Create(cacheKey).Update().Build();

        con.TryOpen();
        return await con.ExecuteAsync(query, entity, tx);
    }

    public static async Task<int> UpdateAsync<TEntity>(
        this IDbConnection con,
        string setClause,
        string whereClause,
        object param,
        IDbTransaction? tx = null,
        string? cacheKey = null
    )
    {
        var query = EqBuilder<TEntity>
            .Create(cacheKey)
            .UpdateSet()
            .Append(" ")
            .Append(setClause)
            .Where(whereClause)
            .Build();

        con.TryOpen();
        return await con.ExecuteAsync(query, param, tx);
    }

    public static async Task<int> UpsertAsync<TEntity>(
        this IDbConnection con,
        TEntity entity,
        IDbTransaction? tx = null,
        string? cacheKey = null
    )
    {
        var query = EqBuilder<TEntity>.Create(cacheKey).Upsert().Build();

        con.TryOpen();
        return await con.ExecuteAsync(query, entity, tx);
    }

    public static async Task<int> UpsertAsync<TEntity>(
        this IDbConnection con,
        string whereClause,
        object param,
        IDbTransaction? tx = null,
        string? cacheKey = null
    )
    {
        var query = EqBuilder<TEntity>.Create(cacheKey).Upsert().Where(whereClause).Build();

        con.TryOpen();
        return await con.ExecuteAsync(query, param, tx);
    }

    public static async Task<int> UpsertAsync<TEntity>(
        this IDbConnection con,
        string setClause,
        string whereClause,
        object param,
        IDbTransaction? tx = null,
        string? cacheKey = null
    )
    {
        var query = EqBuilder<TEntity>
            .Create(cacheKey)
            .UpsertWithoutSet(setClause)
            .Where(whereClause)
            .Build();

        con.TryOpen();
        return await con.ExecuteAsync(query, param, tx);
    }

    public static async Task<int> UpsertListAsync<TEntity>(
        this IDbConnection con,
        IEnumerable<TEntity> entity,
        IDbTransaction? tx = null,
        string? cacheKey = null
    )
    {
        var query = EqBuilder<TEntity>.Create(cacheKey).Upsert().Build();

        con.TryOpen();
        return await con.ExecuteAsync(query, entity, tx);
    }
}
