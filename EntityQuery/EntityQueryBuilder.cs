using System;
using System.Collections.Concurrent;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Reflection;
using System.Text;

namespace EntityQuery;

public class EqBuilder<T>
{
    private static readonly ConcurrentDictionary<string, string> _cache = new();

    public static bool StringBuilderCacheEnabled = true;

    private readonly Type _type;

    private string? _cacheKey;

    private string? _cachedQuery;

    private readonly StringBuilder? _sb;

    public static EqBuilder<T> Create(string? cacheKey = null)
    {
        return new EqBuilder<T>();
    }

    public EqBuilder(string? cacheKey = null)
    {
        _type = typeof(T);
        _cacheKey = cacheKey;

        if (
            StringBuilderCacheEnabled
            && _cacheKey != null
            && _cache.TryGetValue(TypedCacheKey(cacheKey), out var query)
        )
        {
            _cachedQuery = query;
            _sb = null;
        }
        else
        {
            _cachedQuery = null;
            _sb = new StringBuilder(8);
        }
    }

    public string Build()
    {
        if (_sb != null)
        {
            _cachedQuery = _sb.ToString();
            if (StringBuilderCacheEnabled && string.IsNullOrWhiteSpace(_cacheKey) == false)
            {
                _cache.TryAdd(TypedCacheKey(_cacheKey), _cachedQuery);
            }
        }

        return _cachedQuery!;
    }

    private string TypedCacheKey(string? key) => $"{typeof(T).FullName}__{key}";

    public bool UseCached => _sb == null;

    private void AppendCached(string cacheKey, Action<StringBuilder> stringBuilderAction)
    {
        if (StringBuilderCacheEnabled)
        {
            var cached = _cache.GetOrAdd(
                cacheKey,
                (_) =>
                {
                    StringBuilder newSb = new StringBuilder();
                    stringBuilderAction(newSb);
                    return newSb.ToString();
                }
            );
            _sb?.Append(cached);
        }
        else
        {
            if (_sb != null)
            {
                stringBuilderAction(_sb);
            }
        }
    }

    public EqBuilder<T> Append(string value)
    {
        if (_sb != null)
        {
            _sb.Append(value);
        }
        return this;
    }

    public EqBuilder<T> AppendLine(string value)
    {
        if (_sb != null)
        {
            _sb.AppendLine(value);
        }
        return this;
    }

    public EqBuilder<T> WhereById()
    {
        if (_sb == null)
        {
            return this;
        }

        AppendCached(
            TypedCacheKey("WhereById"),
            (sb) =>
            {
                var idProps = _type.GetIdProperties();
                if (idProps.Any() == false)
                {
                    throw new ArgumentException(
                        "Only available an entity with a [Key] or Id property"
                    );
                }

                sb.Append(" WHERE ");
                for (int i = 0; i < idProps.Length; ++i)
                {
                    if (i > 0)
                    {
                        sb.Append(" AND ");
                    }
                    sb.Append(idProps[i].GetColumnName()).Append("=@").Append(idProps[i].Name);
                }
            }
        );

        return this;
    }

    public EqBuilder<T> Where(string where)
    {
        if (string.IsNullOrEmpty(where))
        {
            return this;
        }

        if (_sb == null)
        {
            return this;
        }

        if (where.ToLower().TrimStart().StartsWith("where"))
        {
            throw new ArgumentException("Where : No need to include 'WHERE'");
        }
        _sb.Append(" WHERE (").Append(where).Append(")");

        return this;
    }

    public EqBuilder<T> And(string where)
    {
        if (string.IsNullOrEmpty(where))
        {
            return this;
        }

        if (_sb == null)
        {
            return this;
        }

        if (where.ToLower().TrimStart().StartsWith("where"))
        {
            throw new ArgumentException("And : No need to include 'WHERE'");
        }
        _sb.Append(" AND (").Append(where).Append(")");

        return this;
    }

    public EqBuilder<T> Or(string where)
    {
        if (string.IsNullOrEmpty(where))
        {
            return this;
        }

        if (_sb == null)
        {
            return this;
        }

        if (where.ToLower().TrimStart().StartsWith("where"))
        {
            throw new ArgumentException("Or : No need to include 'WHERE'");
        }
        _sb.Append(" OR (").Append(where).Append(")");

        return this;
    }

    public EqBuilder<T> OrderBy(string col)
    {
        if (string.IsNullOrEmpty(col))
        {
            return this;
        }

        if (_sb == null)
        {
            return this;
        }

        _sb.Append(" ORDER BY ").Append(col.Encapsulate());
        return this;
    }

    public EqBuilder<T> OrderByDesc(string col)
    {
        if (string.IsNullOrEmpty(col))
        {
            return this;
        }

        if (_sb == null)
        {
            return this;
        }

        _sb.Append(" ORDER BY ").Append(col.Encapsulate()).Append(" DESC ");
        return this;
    }

    public EqBuilder<T> OrderByMore(string col)
    {
        if (string.IsNullOrEmpty(col))
        {
            return this;
        }

        if (_sb == null)
        {
            return this;
        }

        _sb.Append(", ").Append(col.Encapsulate());
        return this;
    }

    public EqBuilder<T> OrderByMoreDesc(string col)
    {
        if (string.IsNullOrEmpty(col))
        {
            return this;
        }

        if (_sb == null)
        {
            return this;
        }

        _sb.Append(", ").Append(col.Encapsulate()).Append(" DESC");
        return this;
    }

    public EqBuilder<T> Limit()
    {
        if (_sb == null)
        {
            return this;
        }

        _sb.Append($" LIMIT @Limit");
        return this;
    }

    public EqBuilder<T> SelectWithoutWhere()
    {
        if (_sb == null)
        {
            return this;
        }

        AppendCached(
            TypedCacheKey("SelectWithoutWhere"),
            (sb) =>
            {
                sb.Append("SELECT ");

                var props = _type.GetSelectProperties().ToArray();
                for (int i = 0; i < props.Length; ++i)
                {
                    var p = props[i];
                    if (i != 0)
                    {
                        sb.Append(", ");
                    }
                    sb.Append(p.GetColumnName());

                    // Column명이 지정될 경우 user_id_custom AS UserId 와 같은 꼴이 되도록 추가
                    var columnAttribute = p.GetCustomAttribute<ColumnAttribute>();
                    if (columnAttribute != null)
                    {
                        sb.Append(" AS ").Append(p.Name.Encapsulate());
                    }
                }

                sb.Append(" FROM ");
                sb.Append(_type.GetTableName());
            }
        );

        return this;
    }

    public EqBuilder<T> Select()
    {
        return SelectWithoutWhere().WhereById();
    }

    public EqBuilder<T> Select(string where)
    {
        return SelectWithoutWhere().Where(where);
    }

    public EqBuilder<T> Insert()
    {
        if (_sb == null)
        {
            return this;
        }

        AppendCached(
            TypedCacheKey("Insert"),
            (sb) =>
            {
                var props = _type.GetInsertProperties().ToArray();

                sb.Append("INSERT INTO ").Append(_type.GetTableName()).Append(" (");
                sb.Append(string.Join(",", props.Select(p => p.GetColumnName())));
                sb.Append(") VALUES (");
                sb.Append(string.Join(",", props.Select(p => $"@{p.Name}")));
                sb.Append(")");
            }
        );

        return this;
    }

    private static Type[] _singleIntegerIdType = new Type[]
    {
        typeof(int),
        typeof(uint),
        typeof(long),
        typeof(ulong),
    };

    // _id 필드에 값이 담겨 온다.
    public EqBuilder<T> InsertWithIdentity()
    {
        var idProps = _type.GetIdProperties();
        if (
            idProps.Count() != 1
            || _singleIntegerIdType.Contains(idProps.First().PropertyType) == false
        )
        {
            throw new InvalidOperationException("Id column should single integer column");
        }

        return Insert().Append(";SELECT LAST_INSERT_ID() AS _id");
    }

    public EqBuilder<T> Upsert()
    {
        if (_sb == null)
        {
            return this;
        }

        AppendCached(
            TypedCacheKey("Upsert"),
            (sb) =>
            {
                var props = _type.GetUpsertProperties().ToArray();

                sb.Append("INSERT INTO ").Append(_type.GetTableName()).Append(" (");
                sb.Append(string.Join(",", props.Select(p => p.GetColumnName())));
                sb.Append(") VALUES (");
                sb.Append(string.Join(",", props.Select(p => $"@{p.Name}")));
                sb.Append(") ON DUPLICATE KEY UPDATE ");

                var updateProps = _type.GetUpdateProperties().ToArray();

                sb.Append(
                    string.Join(",", updateProps.Select(p => $"{p.GetColumnName()}=@{p.Name}"))
                );
            }
        );

        return this;
    }

    public EqBuilder<T> UpsertWithoutSet(string setClause)
    {
        if (_sb == null)
        {
            return this;
        }

        AppendCached(
            TypedCacheKey("UpsertWithoutSet"),
            (sb) =>
            {
                var props = _type.GetUpsertProperties().ToArray();

                sb.Append("INSERT INTO ").Append(_type.GetTableName()).Append(" (");
                sb.Append(string.Join(",", props.Select(p => p.GetColumnName())));
                sb.Append(") VALUES (");
                sb.Append(string.Join(",", props.Select(p => $"@{p.Name}")));
                sb.Append(") ON DUPLICATE KEY UPDATE ");

                sb.Append(setClause);
            }
        );

        return this;
    }

    public EqBuilder<T> UpdateSet()
    {
        if (_sb == null)
        {
            return this;
        }

        _sb.Append("UPDATE ").Append(_type.GetTableName()).Append(" SET ");
        return this;
    }

    public EqBuilder<T> UpdateWithoutWhere()
    {
        if (_sb == null)
        {
            return this;
        }

        AppendCached(
            TypedCacheKey("UpdateWithoutWhere"),
            (sb) =>
            {
                var props = _type.GetUpdateProperties().ToArray();

                sb.Append("UPDATE ").Append(_type.GetTableName()).Append(" SET ");
                sb.Append(string.Join(",", props.Select(p => $"{p.GetColumnName()}=@{p.Name}")));
            }
        );

        return this;
    }

    public EqBuilder<T> Update()
    {
        return UpdateWithoutWhere().WhereById();
    }

    public EqBuilder<T> Update(string where)
    {
        return UpdateWithoutWhere().Where(where);
    }

    // NOTE : 실수를 방지하기 위해 Where 없는 Delete는 public으로 노출하지 않음
    private EqBuilder<T> DeleteWithoutWhere()
    {
        if (_sb == null)
        {
            return this;
        }

        AppendCached(
            TypedCacheKey("DeleteWithoutWhere"),
            (sb) =>
            {
                var props = _type.GetUpdateProperties().ToArray();

                sb.Append("DELETE FROM ").Append(_type.GetTableName());
            }
        );

        return this;
    }

    public EqBuilder<T> Delete()
    {
        return DeleteWithoutWhere().WhereById();
    }

    public EqBuilder<T> Delete(string where)
    {
        return DeleteWithoutWhere().Where(where);
    }
}
