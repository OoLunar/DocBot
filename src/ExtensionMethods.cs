using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.CSharp;

namespace OoLunar.DocBot
{
    public static class ExtensionMethods
    {
        private static readonly CSharpCodeProvider _codeDom = new();

        public static string TrimLength(this string value, int length) => value.Length > length ? $"{value[..(length - 1)].Trim()}…" : value;

        public static string GetFullName(this MemberInfo memberInfo) => memberInfo switch
        {
            Type typeInfo => typeInfo.GetFullGenericTypeName(),
            MethodInfo methodInfo => methodInfo.GetFullName(),
            PropertyInfo propertyInfo => propertyInfo.DeclaringType is null ? $"<runtime generated>.{propertyInfo.Name}" : $"{propertyInfo.DeclaringType.GetFullGenericTypeName()}.{propertyInfo.Name}",
            FieldInfo fieldInfo => fieldInfo.DeclaringType is null ? $"<runtime generated>.{fieldInfo.Name}" : $"{fieldInfo.DeclaringType.GetFullGenericTypeName()}.{fieldInfo.Name}",
            EventInfo eventInfo => eventInfo.DeclaringType is null ? $"<runtime generated>.{eventInfo.Name}" : $"{eventInfo.DeclaringType.GetFullGenericTypeName()}.{eventInfo.Name}",
            ConstructorInfo constructorInfo => constructorInfo.DeclaringType is null ? $"<runtime generated>.{constructorInfo.Name}" : $"{constructorInfo.DeclaringType.GetFullGenericTypeName()}.{constructorInfo.Name}",
            _ => memberInfo.Name
        };

        public static string GetFullName(this MethodInfo methodInfo)
        {
            StringBuilder stringBuilder = new();
            if (methodInfo.DeclaringType is not null)
            {
                stringBuilder.Append(methodInfo.DeclaringType.GetFullGenericTypeName());
                stringBuilder.Append('.');
            }

            stringBuilder.Append(methodInfo.Name);
            stringBuilder.Append('(');
            ParameterInfo[] parameters = methodInfo.GetParameters();
            for (int i = 0; i < parameters.Length; i++)
            {
                ParameterInfo parameter = parameters[i];
                stringBuilder.Append(parameter.ParameterType.GetFullGenericTypeName());
                stringBuilder.Append(' ');
                stringBuilder.Append(parameter.Name);
                if (i != parameters.Length - 1)
                {
                    stringBuilder.Append(", ");
                }
            }
            stringBuilder.Append(')');

            return stringBuilder.ToString();
        }

        public static string GetMemberType(this MemberInfo memberInfo) => memberInfo switch
        {
            Type type => type.IsEnum ? "Enum" : type.IsInterface ? "Interface" : type.IsValueType ? "Struct" : "Class",
            MethodInfo methodInfo => methodInfo.IsSpecialName ? "Property" : "Method",
            PropertyInfo => "Property",
            FieldInfo => "Field",
            EventInfo => "Event",
            ConstructorInfo => "Constructor",
            _ => "Unknown"
        };

        public static string GetDeclarationSyntax(this MemberInfo memberInfo)
        {
            StringBuilder stringBuilder = new();
            stringBuilder.Append(memberInfo.GetAttributeSyntax());
            if (stringBuilder.Length != 0)
            {
                stringBuilder.Append('\n');
            }

            return memberInfo switch
            {
                Type type => stringBuilder.Append(type.GetTypeDeclarationSyntax()).ToString(),
                MethodBase methodBase => stringBuilder.Append(methodBase.GetMethodDeclarationSyntax()).ToString(),
                PropertyInfo propertyInfo => stringBuilder.Append(propertyInfo.GetPropertyDeclarationSyntax()).ToString(),
                FieldInfo fieldInfo => stringBuilder.Append(fieldInfo.GetFieldDeclarationSyntax()).ToString(),
                EventInfo eventInfo => stringBuilder.Append(eventInfo.GetEventDeclarationSyntax()).ToString(),
                _ => stringBuilder.ToString(),
            };
        }

        public static string GetAttributeSyntax(this MemberInfo memberInfo)
        {
            IList<CustomAttributeData> attributes = memberInfo.GetCustomAttributesData();
            return attributes.Count == 0 ? string.Empty : GetAttributeSyntax(attributes);
        }

        public static string GetAttributeSyntax(this ParameterInfo parameterInfo)
        {
            IList<CustomAttributeData> attributes = parameterInfo.GetCustomAttributesData();
            return attributes.Count == 0 ? string.Empty : GetAttributeSyntax(attributes);
        }

        public static string GetAttributeSyntax(IList<CustomAttributeData> attributes)
        {
            StringBuilder stringBuilder = new();
            for (int i = 0; i < attributes.Count; i++)
            {
                if (i == 0)
                {
                    stringBuilder.Append('[');
                }

                CustomAttributeData attribute = attributes[i];
                stringBuilder.Append(attribute.AttributeType.GetFullGenericTypeName());
                Type[] genericArguments = attribute.AttributeType.GetGenericArguments();
                if (genericArguments.Length != 0)
                {
                    stringBuilder.Append('<');
                    for (int j = 0; j < genericArguments.Length; j++)
                    {
                        Type genericArgument = genericArguments[j];
                        stringBuilder.Append(genericArgument.GetFullGenericTypeName());
                        if (j != genericArguments.Length - 1)
                        {
                            stringBuilder.Append(", ");
                        }
                    }
                    stringBuilder.Append('>');
                }

                if (attribute.ConstructorArguments.Count != 0 || attribute.NamedArguments.Count != 0)
                {
                    stringBuilder.Append('(');
                    for (int j = 0; j < attribute.ConstructorArguments.Count; j++)
                    {
                        CustomAttributeTypedArgument argument = attribute.ConstructorArguments[j];
                        stringBuilder.Append(argument.Value);
                        if (j != attribute.ConstructorArguments.Count - 1)
                        {
                            stringBuilder.Append(", ");
                        }
                    }

                    for (int j = 0; j < attribute.NamedArguments.Count; j++)
                    {
                        CustomAttributeNamedArgument argument = attribute.NamedArguments[j];
                        stringBuilder.Append(argument.MemberName);
                        stringBuilder.Append(" = ");
                        stringBuilder.Append(argument.TypedValue.Value);
                        if (j != attribute.NamedArguments.Count - 1)
                        {
                            stringBuilder.Append(", ");
                        }
                    }
                    stringBuilder.Append(')');
                }

                if (i == attributes.Count - 1)
                {
                    stringBuilder.Append(']');
                }
            }

            return stringBuilder.ToString();
        }

        public static string GetTypeDeclarationSyntax(this Type type)
        {
            if (type.IsEnum)
            {
                return type.GetEnumDeclarationSyntax();
            }

            StringBuilder stringBuilder = new();

            // Access modifiers, ignore nested
            if (type.IsPublic)
            {
                stringBuilder.Append("public ");
            }
            else if (type.IsNotPublic)
            {
                stringBuilder.Append("private ");
            }
            else if (type.IsNestedAssembly)
            {
                stringBuilder.Append("internal ");
            }

            if (type.IsNestedFamily)
            {
                stringBuilder.Append("protected ");
            }

            // Modifiers
            if (type.IsAbstract && type.IsSealed)
            {
                stringBuilder.Append("static ");
            }
            else if (type.IsAbstract)
            {
                stringBuilder.Append("abstract ");
            }
            else if (type.IsSealed)
            {
                stringBuilder.Append("sealed ");
            }

            if (type.GetCustomAttribute<IsReadOnlyAttribute>() is not null)
            {
                stringBuilder.Append("readonly ");
            }

            if (type.IsValueType)
            {
                stringBuilder.Append("struct ");
            }
            else if (type.IsClass)
            {
                stringBuilder.Append("class ");
            }
            else if (type.IsInterface)
            {
                stringBuilder.Append("interface ");
            }

            // Type name
            stringBuilder.Append(type.Name);

            // Generic parameters
            if (type.IsGenericType)
            {
                Type[] genericArguments = type.GetGenericArguments();
                stringBuilder.Append('<');
                for (int i = 0; i < genericArguments.Length; i++)
                {
                    Type genericArgument = genericArguments[i];
                    if (genericArgument.GenericParameterAttributes == GenericParameterAttributes.Covariant)
                    {
                        stringBuilder.Append("in ");
                    }
                    else if (genericArgument.GenericParameterAttributes == GenericParameterAttributes.Contravariant)
                    {
                        stringBuilder.Append("out ");
                    }

                    stringBuilder.Append(genericArgument.GetFullGenericTypeName());
                    if (i != genericArguments.Length - 1)
                    {
                        stringBuilder.Append(", ");
                    }
                }
                stringBuilder.Append('>');
            }

            // Base type or interfaces
            Type[] interfaces = type.GetInterfaces();
            if (type.BaseType is not null || interfaces.Length != 0)
            {
                stringBuilder.Append(" : ");
                if (type.BaseType is not null)
                {
                    stringBuilder.Append(type.BaseType.GetFullGenericTypeName());
                    if (interfaces.Length != 0)
                    {
                        stringBuilder.Append(", ");
                    }
                }

                for (int i = 0; i < interfaces.Length; i++)
                {
                    Type @interface = interfaces[i];
                    stringBuilder.Append(@interface.GetFullGenericTypeName());
                    if (i != interfaces.Length - 1)
                    {
                        stringBuilder.Append(", ");
                    }
                }
            }

            // Type constraints
            if (type.IsGenericType)
            {
                Type[] genericArguments = type.GetGenericArguments();
                if (!genericArguments.Any(genericArgument => genericArgument.GenericParameterAttributes.HasFlag(GenericParameterAttributes.SpecialConstraintMask)))
                {
                    return stringBuilder.ToString();
                }

                stringBuilder.Append(" where ");
                for (int i = 0; i < genericArguments.Length; i++)
                {
                    Type genericArgument = genericArguments[i];
                    if (genericArgument.GenericParameterAttributes == GenericParameterAttributes.None)
                    {
                        continue;
                    }

                    stringBuilder.Append(genericArgument.Name);
                    stringBuilder.Append(" : ");

                    if (genericArgument.GenericParameterAttributes.HasFlag(GenericParameterAttributes.ReferenceTypeConstraint))
                    {
                        stringBuilder.Append("class, ");
                    }
                    else if (genericArgument.GenericParameterAttributes.HasFlag(GenericParameterAttributes.NotNullableValueTypeConstraint))
                    {
                        stringBuilder.Append("struct, ");
                    }

                    if (genericArgument.GenericParameterAttributes.HasFlag(GenericParameterAttributes.DefaultConstructorConstraint))
                    {
                        stringBuilder.Append("new()");
                    }

                    Type[] constraints = genericArgument.GetGenericParameterConstraints();
                    for (int j = 0; j < constraints.Length; j++)
                    {
                        Type constraint = constraints[j];
                        stringBuilder.Append(constraint.GetFullGenericTypeName());
                        if (j != constraints.Length - 1)
                        {
                            stringBuilder.Append(", ");
                        }
                    }

                    if (stringBuilder[^1] != ' ')
                    {
                        stringBuilder.Append(", ");
                    }
                }

                // Remove trailing ", "
                stringBuilder.Remove(stringBuilder.Length - 2, 2);
            }

            stringBuilder.Append(" { }");
            return stringBuilder.ToString();
        }

        public static string GetEnumDeclarationSyntax(this Type enumType)
        {
            StringBuilder stringBuilder = new();

            // Access modifiers, ignore nested
            if (enumType.IsPublic)
            {
                stringBuilder.Append("public ");
            }
            else if (enumType.IsNotPublic)
            {
                stringBuilder.Append("private ");
            }
            else if (enumType.IsNestedAssembly)
            {
                stringBuilder.Append("internal ");
            }

            // Enum name
            stringBuilder.Append("enum ");
            stringBuilder.Append(enumType.GetFullGenericTypeName());

            // Base type
            if (enumType.GetEnumUnderlyingType() is Type underlyingType)
            {
                stringBuilder.Append(" : ");
                stringBuilder.Append(underlyingType.GetFullGenericTypeName());
            }

            // Enum values
            string[] names = enumType.GetEnumNames();
            if (names.Length != 0)
            {
                stringBuilder.Append('\n');
                stringBuilder.Append("{\n");
                stringBuilder.Append("  ");

                Array values = enumType.GetEnumValues();
                for (int i = 0; i < names.Length; i++)
                {
                    string name = names[i];
                    object value = values.GetValue(i)!;
                    stringBuilder.Append(name);
                    stringBuilder.Append(" = ");
                    stringBuilder.Append(value);
                    if (i != names.Length - 1)
                    {
                        stringBuilder.Append(",\n  ");
                    }
                }

                stringBuilder.Append("\n}");
            }
            else
            {
                stringBuilder.Append(" { }");
            }

            return stringBuilder.ToString();
        }

        public static string GetMethodDeclarationSyntax(this MethodBase methodBase)
        {
            StringBuilder stringBuilder = new();

            // Access modifiers
            if (methodBase.IsPublic)
            {
                stringBuilder.Append("public ");
            }
            else if (methodBase.IsPrivate)
            {
                stringBuilder.Append("private ");
            }
            else if (methodBase.IsAssembly)
            {
                stringBuilder.Append("internal ");
            }

            if (methodBase.IsFamily)
            {
                stringBuilder.Append("protected ");
            }

            // Modifiers
            if (methodBase.IsStatic)
            {
                stringBuilder.Append("static ");
            }

            if (methodBase.IsAbstract)
            {
                stringBuilder.Append("abstract ");
            }
            else if (methodBase.IsVirtual)
            {
                stringBuilder.Append("virtual ");
            }
            else if (methodBase.IsFinal)
            {
                stringBuilder.Append("sealed ");
            }

            // Method name
            if (methodBase is ConstructorInfo)
            {
                // <class name>
                // Program
                stringBuilder.Append(methodBase.DeclaringType!.GetFullGenericTypeName());
            }
            else if (methodBase is MethodInfo methodInfo)
            {
                MethodInfo baseImplementation = methodInfo.GetBaseDefinition();
                if (baseImplementation != methodInfo)
                {
                    // override <return type> <base type>.<base method>
                    // override void object.ToString
                    stringBuilder.Append("override ");
                    stringBuilder.Append(methodInfo.ReturnType.GetFullGenericTypeName());
                    stringBuilder.Append(' ');
                    stringBuilder.Append(baseImplementation.DeclaringType!.GetFullGenericTypeName());
                    stringBuilder.Append('.');
                    stringBuilder.Append(baseImplementation.Name);
                }
                else
                {
                    // <return type> <method name>
                    // void ToString
                    stringBuilder.Append(methodInfo.ReturnType.GetFullGenericTypeName());
                    stringBuilder.Append(' ');
                    stringBuilder.Append(methodInfo.Name);
                }
            }

            // Parameters
            ParameterInfo[] parameters = methodBase.GetParameters();
            if (parameters.Length != 0)
            {
                stringBuilder.Append("\n(");
                for (int i = 0; i < parameters.Length; i++)
                {
                    stringBuilder.Append("\n\t");

                    ParameterInfo parameter = parameters[i];
                    string attributeSyntax = parameter.GetAttributeSyntax();
                    if (!string.IsNullOrWhiteSpace(attributeSyntax))
                    {
                        stringBuilder.Append(attributeSyntax);
                        stringBuilder.Append(' ');
                    }
                    stringBuilder.Append(parameter.ParameterType.GetFullGenericTypeName());
                    stringBuilder.Append(' ');
                    stringBuilder.Append(parameter.Name);

                    if (parameter.HasDefaultValue)
                    {
                        stringBuilder.Append(" = ");
                        stringBuilder.Append(parameter.DefaultValue switch
                        {
                            Enum @enum => $"{@enum.GetType().Name}.{@enum}",
                            string @string => $"\"{@string}\"",
                            char @char => $"'{@char}'",
                            bool @bool => @bool ? "true" : "false",
                            null => "null",
                            _ => parameter.DefaultValue
                        });
                    }

                    if (i != parameters.Length - 1)
                    {
                        stringBuilder.Append(',');
                    }
                }
                stringBuilder.Append("\n);");
            }
            else
            {
                stringBuilder.Append("();");
            }

            return stringBuilder.ToString();
        }

        public static string GetPropertyDeclarationSyntax(this PropertyInfo propertyInfo)
        {
            StringBuilder stringBuilder = new();
            stringBuilder.Append(propertyInfo.GetAttributeSyntax());
            if (stringBuilder.Length != 0)
            {
                stringBuilder.Append('\n');
            }

            if (propertyInfo.GetMethod is null && propertyInfo.SetMethod is null)
            {
                stringBuilder.Append(propertyInfo.Name);
                stringBuilder.Append(';');
                return stringBuilder.ToString();
            }

            // Access modifiers
            if (propertyInfo.GetMethod is not null || propertyInfo.SetMethod is not null)
            {
                if ((propertyInfo.GetMethod?.IsPublic ?? false) || (propertyInfo.SetMethod?.IsPublic ?? false))
                {
                    stringBuilder.Append("public ");
                }

                if ((propertyInfo.GetMethod?.IsFamily ?? false) || (propertyInfo.SetMethod?.IsFamily ?? false))
                {
                    stringBuilder.Append("protected ");
                }

                if ((propertyInfo.GetMethod?.IsAssembly ?? false) || (propertyInfo.SetMethod?.IsAssembly ?? false))
                {
                    stringBuilder.Append("internal ");
                }
                else if ((propertyInfo.GetMethod?.IsPrivate ?? false) || (propertyInfo.SetMethod?.IsPrivate ?? false))
                {
                    stringBuilder.Append("private ");
                }

                // static, abstract, override
                if ((propertyInfo.GetMethod?.IsStatic ?? false) || (propertyInfo.SetMethod?.IsStatic ?? false))
                {
                    stringBuilder.Append("static ");
                }

                if ((propertyInfo.GetMethod?.IsAbstract ?? false) || (propertyInfo.SetMethod?.IsAbstract ?? false))
                {
                    stringBuilder.Append("abstract ");
                }

                if ((propertyInfo.GetMethod?.IsVirtual ?? false) || (propertyInfo.SetMethod?.IsVirtual ?? false))
                {
                    stringBuilder.Append("virtual ");
                }

                if ((propertyInfo.GetMethod?.IsFinal ?? false) || (propertyInfo.SetMethod?.IsFinal ?? false))
                {
                    stringBuilder.Append("sealed ");
                }
            }

            // Property type
            stringBuilder.Append(propertyInfo.PropertyType.GetFullGenericTypeName());
            stringBuilder.Append(' ');

            // Property name
            stringBuilder.Append(propertyInfo.Name);
            stringBuilder.Append(" { ");

            // Accessors
            if (propertyInfo.GetMethod is not null)
            {
                if (propertyInfo.GetMethod.IsPublic)
                {
                    stringBuilder.Append("get; ");
                }
                else if (propertyInfo.GetMethod.IsAssembly)
                {
                    stringBuilder.Append("internal get; ");
                }
                else if (propertyInfo.GetMethod.IsPrivate)
                {
                    stringBuilder.Append("private get; ");
                }

                // Auto-implemented properties must have a get accessor
                if (propertyInfo.SetMethod is not null)
                {
                    if (propertyInfo.SetMethod.IsAssembly)
                    {
                        stringBuilder.Append("internal ");
                    }
                    else if (propertyInfo.SetMethod.IsPrivate)
                    {
                        stringBuilder.Append("private ");
                    }

                    stringBuilder.Append(propertyInfo.SetMethod.ReturnParameter.GetRequiredCustomModifiers().Contains(typeof(IsExternalInit)) ? "init; " : "set; ");
                }

                stringBuilder.Append('}');
            }

            if (propertyInfo.GetCustomAttribute<DefaultValueAttribute>() is DefaultValueAttribute defaultValueAttribute)
            {
                stringBuilder.Append(" = ");
                stringBuilder.Append(defaultValueAttribute.Value);
                stringBuilder.Append(';');
            }

            return stringBuilder.ToString();
        }

        public static string GetFieldDeclarationSyntax(this FieldInfo fieldInfo)
        {
            StringBuilder stringBuilder = new();

            // Access modifiers
            if (fieldInfo.IsPublic)
            {
                stringBuilder.Append("public ");
            }

            if (fieldInfo.IsFamily)
            {
                stringBuilder.Append("protected ");
            }

            if (fieldInfo.IsAssembly)
            {
                stringBuilder.Append("internal ");
            }
            else if (fieldInfo.IsPrivate)
            {
                stringBuilder.Append("private ");
            }

            // Modifiers
            if (fieldInfo.IsStatic)
            {
                stringBuilder.Append("static ");
            }

            if (fieldInfo.IsInitOnly)
            {
                stringBuilder.Append("readonly ");
            }

            // Field type
            stringBuilder.Append(fieldInfo.FieldType.GetFullGenericTypeName());
            stringBuilder.Append(' ');

            // Field name
            stringBuilder.Append(fieldInfo.Name);

            if (fieldInfo.GetCustomAttribute<DefaultValueAttribute>() is DefaultValueAttribute defaultValueAttribute)
            {
                stringBuilder.Append(" = ");
                stringBuilder.Append(defaultValueAttribute.Value);
            }

            stringBuilder.Append(';');
            return stringBuilder.ToString();
        }

        // GetEventDeclarationSyntax
        public static string GetEventDeclarationSyntax(this EventInfo eventInfo)
        {
            StringBuilder stringBuilder = new();

            // Access modifiers
            if (eventInfo.AddMethod is not null || eventInfo.RemoveMethod is not null)
            {
                if ((eventInfo.AddMethod?.IsPublic ?? false) || (eventInfo.RemoveMethod?.IsPublic ?? false))
                {
                    stringBuilder.Append("public ");
                }

                if ((eventInfo.AddMethod?.IsFamily ?? false) || (eventInfo.RemoveMethod?.IsFamily ?? false))
                {
                    stringBuilder.Append("protected ");
                }

                if ((eventInfo.AddMethod?.IsAssembly ?? false) || (eventInfo.RemoveMethod?.IsAssembly ?? false))
                {
                    stringBuilder.Append("internal ");
                }
                else if ((eventInfo.AddMethod?.IsPrivate ?? false) || (eventInfo.RemoveMethod?.IsPrivate ?? false))
                {
                    stringBuilder.Append("private ");
                }
            }

            // Event type
            stringBuilder.Append("event ");
            if (eventInfo.EventHandlerType is not null)
            {
                stringBuilder.Append(eventInfo.EventHandlerType.GetFullGenericTypeName());
                stringBuilder.Append(' ');
            }

            // Event name
            stringBuilder.Append(eventInfo.Name);
            stringBuilder.Append(" { ");

            if (eventInfo.AddMethod is not null)
            {
                if (eventInfo.AddMethod.IsPublic)
                {
                    stringBuilder.Append("add; ");
                }
                else if (eventInfo.AddMethod.IsAssembly)
                {
                    stringBuilder.Append("internal add; ");
                }
                else if (eventInfo.AddMethod.IsPrivate)
                {
                    stringBuilder.Append("private add; ");
                }
            }

            if (eventInfo.RemoveMethod is not null)
            {
                if (eventInfo.RemoveMethod.IsPublic)
                {
                    stringBuilder.Append("remove; ");
                }
                else if (eventInfo.RemoveMethod.IsAssembly)
                {
                    stringBuilder.Append("internal remove; ");
                }
                else if (eventInfo.RemoveMethod.IsPrivate)
                {
                    stringBuilder.Append("private remove; ");
                }
            }

            stringBuilder.Append('}');
            return stringBuilder.ToString();
        }

        public static string GetFullGenericTypeName(this Type type)
        {
            StringBuilder stringBuilder = new();

            // Test if the type is nullable.
            Type? underlyingNullableType = Nullable.GetUnderlyingType(type);
            if (underlyingNullableType != null)
            {
                // GetTypeOutput returns the full namespace for the type, which is why we split by `.` and take the last element (which should be the type name)
                // We also append a `?` to the end of the type name to represent the nullable type.
                stringBuilder.Append(GetFullGenericTypeName(underlyingNullableType) + "?");
            }
            // Test if the type is a generic type.
            else if (type.IsGenericType)
            {
                // type.Name contains `1 (Action`1) instead of brackets. We chop off the backticks and append the `<` and `>` to the front and back, with the type arguments in between.
                stringBuilder.Append(type.Name.Contains('`') ? type.Name.AsSpan(0, type.Name.IndexOf('`')) : type.Name);
                stringBuilder.Append('<');

                // This is a closed generic type (e.g., List<int>)
                foreach (Type genericArgument in type.GetGenericArguments())
                {
                    if (genericArgument.IsGenericParameter)
                    {
                        if (genericArgument.GenericParameterAttributes == GenericParameterAttributes.Covariant)
                        {
                            stringBuilder.Append("in ");
                        }
                        else if (genericArgument.GenericParameterAttributes == GenericParameterAttributes.Contravariant)
                        {
                            stringBuilder.Append("out ");
                        }
                    }

                    // Surprise! It's a recursive method.
                    stringBuilder.Append(GetFullGenericTypeName(genericArgument));
                    stringBuilder.Append(", ");
                }

                // EndsWith(", ")
                if (stringBuilder[^1] == ' ' && stringBuilder[^2] == ',')
                {
                    stringBuilder.Remove(stringBuilder.Length - 2, 2);
                }
                stringBuilder.Append('>');
            }
            else
            {
                // As mentioned earlier, we use GetTypeOutput to get the full namespace for the type. We only want the type name, not the BCL name.
                stringBuilder.Append(GetFriendlyTypeName(type));
            }

            return stringBuilder.ToString();
        }

        private static string GetFriendlyTypeName(Type type) => _codeDom.GetTypeOutput(new(type)).Split('.').Last();
    }
}
