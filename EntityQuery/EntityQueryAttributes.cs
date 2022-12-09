using System;

namespace EntityQuery;

/// <summary>
/// Optional IgnoreSelect attribute.
/// Custom for EntityQuery to exclude a property from Select methods
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public class IgnoreSelectAttribute : Attribute { }

/// <summary>
/// Optional IgnoreInsert attribute.
/// Custom for EntityQuery to exclude a property from Insert methods
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public class IgnoreInsertAttribute : Attribute { }

/// <summary>
/// Optional IgnoreUpdate attribute.
/// Custom for EntityQuery to exclude a property from Update methods
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public class IgnoreUpdateAttribute : Attribute { }
