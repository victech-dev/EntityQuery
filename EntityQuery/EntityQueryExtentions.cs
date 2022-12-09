using System.Collections.Concurrent;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Reflection;

namespace EntityQuery;

public static partial class EntityQueryExtensions
{
    private static readonly ConcurrentDictionary<Type, string> _TableNames = new();

    private static readonly ConcurrentDictionary<string, string> _columnNames = new();

    public static ITableNameResolver _tableNameResolver = new TableNameResolver();

    public static IColumnNameResolver _columnNameResolver = new ColumnNameResolver();

    private static readonly ConcurrentDictionary<Type, PropertyInfo[]> _idPropertyInfoDict = new();

    private static readonly ConcurrentDictionary<
        Type,
        PropertyInfo[]
    > _scaffoldablePropertyInfoDict = new();

    // Global TableNameResolver 변경
    public static void SetTableNameResolver(ITableNameResolver resolver)
    {
        _tableNameResolver = resolver;
    }

    // Global ColumnNameResolver 변경
    public static void SetColumnNameResolver(IColumnNameResolver resolver)
    {
        _columnNameResolver = resolver;
    }

    public static string GetTableName(this Type type)
    {
        if (_TableNames.TryGetValue(type, out var tableName))
        {
            return tableName;
        }
        else
        {
            tableName = _tableNameResolver.ResolveTableName(type);
            _TableNames.AddOrUpdate(type, tableName, (t, v) => tableName);
            return tableName;
        }
    }

    public static string GetColumnName(this PropertyInfo propertyInfo)
    {
        string key = string.Format("{0}.{1}", propertyInfo.DeclaringType, propertyInfo.Name);

        if (_columnNames.TryGetValue(key, out var columnName))
        {
            return columnName;
        }
        else
        {
            columnName = _columnNameResolver.ResolveColumnName(propertyInfo);
            _columnNames.AddOrUpdate(key, columnName, (t, v) => columnName);
            return columnName;
        }
    }

    // KeyAttribute 가 있거나 이름이 "Id"인 pi를 반환
    public static PropertyInfo[] GetIdProperties(this Type type)
    {
        return _idPropertyInfoDict.GetOrAdd(
            type,
            (type) =>
            {
                var keyProperties = type.GetProperties()
                    .Where(p => p.GetCustomAttribute<KeyAttribute>(true) != null);
                return keyProperties.Any()
                    ? keyProperties.ToArray()
                    : type.GetProperties()
                        .Where(p => p.Name.Equals("Id", StringComparison.OrdinalIgnoreCase))
                        .ToArray();
            }
        );
    }

    // Editable(false) attribute가 아닌 모든 properties
    public static PropertyInfo[] GetScaffoldableProperties<TEntity>()
    {
        return typeof(TEntity).GetScaffoldableProperties();
    }

    public static PropertyInfo[] GetScaffoldableProperties(this Type type)
    {
        PropertyInfo[] props = _scaffoldablePropertyInfoDict.GetOrAdd(
            type,
            (type) =>
            {
                return type.GetProperties()
                    .Where(p =>
                    {
                        var editableAttribute = p.GetCustomAttribute<EditableAttribute>(false);
                        if (editableAttribute != null)
                        {
                            // 명시적으로 Editable() 이 명시되어 있다면 그 값으로 판단
                            return editableAttribute.AllowEdit;
                        }
                        else
                        {
                            // Editable이 없다면 SimpleType인지 여부로 판단
                            return p.PropertyType.IsSimpleType();
                        }
                    })
                    .ToArray();
            }
        );
        return props;
    }

    // System.ComponentModel.DataAnnotations.EditableAttribute의 AllowEdit 여부를 반환
    // 없으면 기본 false (= editable)
    public static bool IsEditable(this PropertyInfo pi)
    {
        var editableAttribute = pi.GetCustomAttribute<EditableAttribute>(false);
        return editableAttribute?.AllowEdit ?? false;
    }

    public static bool IsReadOnly(this PropertyInfo pi)
    {
        var readOnlyAttribute = pi.GetCustomAttribute<ReadOnlyAttribute>(false);
        return readOnlyAttribute?.IsReadOnly ?? false;
    }

    public static IEnumerable<PropertyInfo> GetUpdateableProperties(this Type type)
    {
        return type.GetScaffoldableProperties()
            .Where(
                p =>
                    p.Name.Equals("Id", StringComparison.OrdinalIgnoreCase) == false
                    && p.GetCustomAttribute<KeyAttribute>(true) == null
                    && p.GetCustomAttribute<IgnoreUpdateAttribute>(true) == null
                    && p.GetCustomAttribute<NotMappedAttribute>(true) == null
                    && p.IsReadOnly() == false
            );
    }

    public static IEnumerable<PropertyInfo> GetSelectProperties(this Type type)
    {
        return type.GetScaffoldableProperties()
            .Where(
                p =>
                    (
                        p.GetCustomAttribute<IgnoreSelectAttribute>(true) != null
                        || p.GetCustomAttribute<NotMappedAttribute>(true) != null
                    ) == false
            );
    }

    public static IEnumerable<PropertyInfo> GetInsertProperties(this Type type)
    {
        return type.GetScaffoldableProperties()
            .Where(
                p =>
                    (
                        p.PropertyType != typeof(string)
                        && p.GetCustomAttribute<KeyAttribute>(true) != null
                        && p.GetCustomAttribute<RequiredAttribute>(true) == null
                    ) == false
            )
            .Where(
                p =>
                    (
                        p.Name.Equals("Id", StringComparison.OrdinalIgnoreCase)
                        && p.GetCustomAttribute<RequiredAttribute>(true) == null
                    ) == false
            )
            .Where(
                p =>
                    (
                        p.GetCustomAttribute<IgnoreInsertAttribute>(true) != null
                        || p.GetCustomAttribute<NotMappedAttribute>(true) != null
                        || p.IsReadOnly()
                    ) == false
            );
    }

    public static IEnumerable<PropertyInfo> GetUpsertProperties(this Type type)
    {
        return type.GetScaffoldableProperties()
            .Where(
                p =>
                    (
                        p.GetCustomAttribute<IgnoreInsertAttribute>(true) != null
                        || p.GetCustomAttribute<NotMappedAttribute>(true) != null
                        || p.IsReadOnly()
                    ) == false
            );
    }

    public static IEnumerable<PropertyInfo> GetUpdateProperties(this Type type)
    {
        return type.GetScaffoldableProperties()
            .Where(
                p =>
                    (
                        p.Name.Equals("Id", StringComparison.OrdinalIgnoreCase)
                        || p.GetCustomAttribute<KeyAttribute>() != null
                        || p.IsReadOnly()
                        || p.GetCustomAttribute<IgnoreUpdateAttribute>() != null
                        || p.GetCustomAttribute<NotMappedAttribute>() != null
                    ) == false
            );
    }

    // insert 혹은 update에 사용 가능한 기본 타입
    static private HashSet<Type> simpleTypes = new HashSet<Type>
    {
        typeof(byte),
        typeof(sbyte),
        typeof(short),
        typeof(ushort),
        typeof(int),
        typeof(uint),
        typeof(long),
        typeof(ulong),
        typeof(float),
        typeof(double),
        typeof(decimal),
        typeof(bool),
        typeof(string),
        typeof(char),
        // typeof(Guid),
        typeof(DateTime),
        typeof(DateTimeOffset),
        typeof(TimeSpan),
        typeof(byte[])
    };

    public static bool IsSimpleType(this Type type)
    {
        var flattedType = Nullable.GetUnderlyingType(type) ?? type;
        return flattedType.IsEnum || simpleTypes.Contains(flattedType);
    }
}
