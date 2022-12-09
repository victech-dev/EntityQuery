using System;
using System.ComponentModel.DataAnnotations.Schema;
using System.Globalization;
using System.Reflection;
using System.Text;

namespace EntityQuery;

public interface ITableNameResolver
{
    string ResolveTableName(Type type);
}

public interface IColumnNameResolver
{
    string ResolveColumnName(PropertyInfo propertyInfo);
}

internal static class NameResolverExtensions
{
    public static string Encapsulate(this string databaseword)
    {
        var _encapsulation = "`{0}`";
        return string.Format(_encapsulation, databaseword);
    }

    // From EF Core codebase
    public static string ToSnakeCase(this string name)
    {
        if (string.IsNullOrEmpty(name))
            return name;

        var builder = new StringBuilder(name.Length + Math.Min(2, name.Length / 5));
        var previousCategory = default(UnicodeCategory?);

        for (var currentIndex = 0; currentIndex < name.Length; currentIndex++)
        {
            var currentChar = name[currentIndex];
            if (currentChar == '_')
            {
                builder.Append('_');
                previousCategory = null;
                continue;
            }

            var currentCategory = char.GetUnicodeCategory(currentChar);
            switch (currentCategory)
            {
                case UnicodeCategory.UppercaseLetter:
                case UnicodeCategory.TitlecaseLetter:
                    if (
                        previousCategory == UnicodeCategory.SpaceSeparator
                        || previousCategory == UnicodeCategory.LowercaseLetter
                        || previousCategory != UnicodeCategory.DecimalDigitNumber
                            && previousCategory != null
                            && currentIndex > 0
                            && currentIndex + 1 < name.Length
                            && char.IsLower(name[currentIndex + 1])
                    )
                    {
                        builder.Append('_');
                    }

                    currentChar = char.ToLower(currentChar);
                    break;

                case UnicodeCategory.LowercaseLetter:
                case UnicodeCategory.DecimalDigitNumber:
                    if (previousCategory == UnicodeCategory.SpaceSeparator)
                        builder.Append('_');
                    break;

                default:
                    if (previousCategory != null)
                        previousCategory = UnicodeCategory.SpaceSeparator;
                    continue;
            }

            builder.Append(currentChar);
            previousCategory = currentCategory;
        }

        return builder.ToString();
    }
}

public class TableNameResolver : ITableNameResolver
{
    public virtual string ResolveTableName(Type type)
    {
        var tableAttribute = type.GetCustomAttribute<TableAttribute>(true);
        if (tableAttribute != null)
        {
            return tableAttribute!.Name.Encapsulate();
        }
        else
        {
            var tableName = Dapper.DefaultTypeMap.MatchNamesWithUnderscores
                ? type.Name.ToSnakeCase()
                : type.Name;
            return tableName.Encapsulate();
        }
    }
}

public class ColumnNameResolver : IColumnNameResolver
{
    public virtual string ResolveColumnName(PropertyInfo propertyInfo)
    {
        var columnAttribute = propertyInfo.GetCustomAttribute<ColumnAttribute>(true);
        if (columnAttribute != null && columnAttribute!.Name != null)
        {
            return columnAttribute.Name.Encapsulate();
        }
        else
        {
            var columnName = Dapper.DefaultTypeMap.MatchNamesWithUnderscores
                ? propertyInfo.Name.ToSnakeCase()
                : propertyInfo.Name;
            return columnName.Encapsulate();
        }
    }
}
