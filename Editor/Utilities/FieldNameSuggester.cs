using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using Thisaislan.AutoFieldInjectorEditor.Editor.Data;

namespace Thisaislan.AutoFieldInjectorEditor.Editor.Utilities
{
    /// <summary>
    /// Suggests and validates the field names used for generated references. Field names must be
    /// valid C# identifiers that do not shadow reserved words or common Unity member names.
    /// </summary>
    internal static class FieldNameSuggester
    {
        /// <summary>
        /// C# keywords, contextual keywords and common Unity member names that must not be used
        /// as generated field names.
        /// </summary>
        private static readonly HashSet<string> ReservedIdentifiers = new HashSet<string>
        {
            // C# keywords
            "abstract", "as", "base", "bool", "break", "byte", "case", "catch", "char",
            "checked", "class", "const", "continue", "decimal", "default", "delegate", "do",
            "double", "else", "enum", "event", "explicit", "extern", "false", "finally",
            "fixed", "float", "for", "foreach", "goto", "if", "implicit", "in", "int",
            "interface", "internal", "is", "lock", "long", "namespace", "new", "null",
            "object", "operator", "out", "override", "params", "private", "protected",
            "public", "readonly", "ref", "return", "sbyte", "sealed", "short", "sizeof",
            "stackalloc", "static", "string", "struct", "switch", "this", "throw", "true",
            "try", "typeof", "uint", "ulong", "unchecked", "unsafe", "ushort", "using",
            "virtual", "void", "volatile", "while",
            // C# contextual keywords
            "add", "alias", "and", "ascending", "async", "await", "by", "descending",
            "dynamic", "equals", "file", "from", "get", "global", "group", "init", "into",
            "join", "let", "managed", "nameof", "nint", "not", "notnull", "nuint", "on",
            "or", "orderby", "partial", "record", "remove", "required", "scoped", "select",
            "set", "unmanaged", "value", "var", "when", "where", "with", "yield",
            // Common Unity built-in member names that would shadow MonoBehaviour members
            "transform", "gameobject", "collider", "rigidbody", "renderer", "meshfilter",
            "meshrenderer", "skinnedmeshrenderer", "animator", "animation", "audio",
            "audiosource", "light", "camera", "material", "texture", "sprite", "script",
            "name", "tag", "enabled", "gameObject"
        };

        /// <summary>Matches a valid C# field identifier.</summary>
        private static readonly Regex FieldNameRegex = new Regex(@"^[a-zA-Z_][a-zA-Z0-9_]*$", RegexOptions.Compiled);

        /// <summary>Removes every non-alphanumeric character from an object name.</summary>
        private static readonly Regex SanitizeRegex = new Regex(@"[^a-zA-Z0-9_]", RegexOptions.Compiled);

        /// <summary>Placeholder used when an object name cannot be turned into a valid identifier.</summary>
        private const string FallbackFieldName = "ref";

        /// <summary>Suffix appended when the suggested name is a reserved identifier.</summary>
        private const string ReservedSuffix = "Ref";

        /// <summary>Suffix appended to the plural base name of a List field.</summary>
        private const string ListSuffix = "List";

        /// <summary>Suffix appended to the plural base name of an Array field.</summary>
        private const string ArraySuffix = "Array";

        /// <summary>
        /// Returns whether the given value is a valid, non-reserved C# field identifier.
        /// </summary>
        /// <param name="fieldName">The field name to validate.</param>
        /// <returns>True when the field name can be used, false otherwise.</returns>
        internal static bool IsValidIdentifier(string fieldName)
        {
            return !string.IsNullOrEmpty(fieldName)
                && FieldNameRegex.IsMatch(fieldName)
                && !IsReserved(fieldName);
        }

        /// <summary>
        /// Returns whether the given field name is a reserved identifier.
        /// </summary>
        /// <param name="fieldName">The field name to check.</param>
        /// <returns>True when the name is reserved, false otherwise.</returns>
        internal static bool IsReserved(string fieldName)
        {
            return ReservedIdentifiers.Contains(fieldName.ToLowerInvariant());
        }

        /// <summary>
        /// Builds the suggested camelCase field name for a single reference candidate, appending a
        /// suffix when the name collides with a reserved identifier.
        /// </summary>
        /// <param name="candidate">The candidate to build the name from.</param>
        /// <returns>The suggested field name.</returns>
        internal static string SuggestSingleFieldName(Candidate candidate)
        {
            string suggestedName = BuildSuggestedName(candidate);

            if (IsReserved(suggestedName))
            {
                suggestedName += ReservedSuffix;
            }
            return suggestedName;
        }

        /// <summary>
        /// Builds a plural camelCase field name for a collection of the given element type, e.g.
        /// "gameObjectsList" or "transformsArray".
        /// </summary>
        /// <param name="elementType">The element type of the collection.</param>
        /// <param name="kind">The container kind, which contributes the name suffix.</param>
        /// <returns>The suggested collection field name.</returns>
        internal static string SuggestCollectionFieldName(Type elementType, CollectionKind kind)
        {
            string pluralBase = PluralizeTypeName(elementType);
            string containerSuffix = (kind == CollectionKind.List) ? ListSuffix : ArraySuffix;
            return ToCamelCase(pluralBase) + containerSuffix;
        }

        /// <summary>
        /// Builds a camelCase field name from the candidate's object name and type.
        /// </summary>
        /// <param name="candidate">The candidate to build the name from.</param>
        /// <returns>The suggested field name.</returns>
        private static string BuildSuggestedName(Candidate candidate)
        {
            string objectName = SanitizeObjectName(candidate);

            if (candidate.type == typeof(GameObject))
            {
                return ToCamelCase(objectName);
            }
            return ToCamelCase(objectName + candidate.type.Name);
        }

        /// <summary>
        /// Returns the object name with all non-alphanumeric characters removed, falling back
        /// to a placeholder when nothing usable remains.
        /// </summary>
        /// <param name="candidate">The candidate whose object name should be sanitized.</param>
        /// <returns>A name-safe version of the object name.</returns>
        private static string SanitizeObjectName(Candidate candidate)
        {
            string objectName = candidate.assignedObjects[0].name;

            if (string.IsNullOrEmpty(objectName))
            {
                objectName = candidate.type.Name;
            }

            objectName = SanitizeRegex.Replace(objectName, string.Empty);

            if (string.IsNullOrEmpty(objectName))
            {
                objectName = FallbackFieldName;
            }
            return objectName;
        }

        /// <summary>
        /// Returns a pluralised version of the type name using common English pluralisation rules.
        /// </summary>
        /// <param name="type">The type whose name is pluralised.</param>
        /// <returns>The pluralised type name.</returns>
        private static string PluralizeTypeName(Type type)
        {
            string name = type.Name;

            if (string.IsNullOrEmpty(name))
            {
                return name;
            }

            char last = name[name.Length - 1];

            if (last == 'y' && name.Length > 1 && !IsVowel(name[name.Length - 2]))
            {
                return name.Substring(0, name.Length - 1) + "ies";
            }
            if (last == 's' || last == 'x' || last == 'z')
            {
                return name + "es";
            }
            if (name.EndsWith("ch", StringComparison.Ordinal) || name.EndsWith("sh", StringComparison.Ordinal))
            {
                return name + "es";
            }
            return name + "s";
        }

        /// <summary>
        /// Returns whether the character is a vowel.
        /// </summary>
        /// <param name="c">The character to check.</param>
        /// <returns>True when the character is a vowel, false otherwise.</returns>
        private static bool IsVowel(char c)
        {
            switch (char.ToLowerInvariant(c))
            {
                case 'a':
                case 'e':
                case 'i':
                case 'o':
                case 'u':
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Converts the first character of the value to lower case, producing a camelCase name.
        /// </summary>
        /// <param name="value">The value to convert.</param>
        /// <returns>The value with a lower-case first character, or the value unchanged when empty.</returns>
        private static string ToCamelCase(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return value;
            }
            return char.ToLowerInvariant(value[0]) + value.Substring(1);
        }
    }
}