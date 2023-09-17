using System;
using System.Linq;
using System.Reflection;
using UnityEngine;

public static class EnumExtensions
{
    public static string[] GetInspectorNames(this Enum e)
    {
        Type enumType = e.GetType();
        string[] enumNames = Enum.GetNames(enumType);
        for (int i = 0; i < enumNames.Length; i++)
        {
            var attribute = enumType.GetMember(enumNames[i]).First().GetCustomAttribute<InspectorNameAttribute>();
            if (attribute != null)
                enumNames[i] = attribute.displayName;
        }
        return enumNames;
    }

    //string[] GetEnumInspectorNames(Type enumType)
    //{
    //    if (enumType != typeof(Enum))
    //        throw new InvalidCastException();

    //    string[] enumNames = Enum.GetNames(enumType);
    //    for (int i = 0; i < enumNames.Length; i++)
    //    {
    //        var attribute = enumType.GetMember(enumNames[i]).First().GetCustomAttribute<InspectorNameAttribute>();
    //        if (attribute != null)
    //            enumNames[i] = attribute.displayName;
    //    }

    //    return enumNames;
    //}
}
