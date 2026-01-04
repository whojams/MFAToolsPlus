using System;

namespace MFAToolsPlus.Utilities.Attributes;

[AttributeUsage(AttributeTargets.Field)]
public class MaaJsonPropertyAttribute(string name) : Attribute
{
    public string Name => name;

}