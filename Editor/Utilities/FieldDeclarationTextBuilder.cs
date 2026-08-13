using System;
using Thisaislan.AutoFieldInjectorEditor.Editor.Data;

namespace Thisaislan.AutoFieldInjectorEditor.Editor.Utilities
{
    /// <summary>
    /// Builds the C# source text of serialized field declarations for single references and
    /// List/Array collections.
    /// </summary>
    internal static class FieldDeclarationTextBuilder
    {
        /// <summary>The single newline character used in generated code.</summary>
        private const string NewLine = "\n";

        /// <summary>Template of a public field declaration. Format: indent, type, name.</summary>
        private const string PublicFieldFormat = "{0}public {1} {2};";

        /// <summary>Template of a serialized private field declaration. Format: indent, type, name.</summary>
        private const string PrivateFieldFormat = "{0}[SerializeField]" + NewLine + "{0}private {1} {2};";

        /// <summary>Template of a serialized List field declaration. Format: indent, element type, name.</summary>
        private const string SerializedListFieldFormat = "{0}[SerializeField]" + NewLine + "{0}private List<{1}> {2};";

        /// <summary>Template of a public List field declaration. Format: indent, element type, name.</summary>
        private const string PublicListFieldFormat = "{0}public List<{1}> {2};";

        /// <summary>Template of a serialized array field declaration. Format: indent, element type, name.</summary>
        private const string SerializedArrayFieldFormat = "{0}[SerializeField]" + NewLine + "{0}private {1}[] {2};";

        /// <summary>Template of a public array field declaration. Format: indent, element type, name.</summary>
        private const string PublicArrayFieldFormat = "{0}public {1}[] {2};";

        /// <summary>
        /// Builds the C# source text of a field declaration for a single reference.
        /// </summary>
        /// <param name="typeName">The type name of the field.</param>
        /// <param name="fieldName">The name of the field.</param>
        /// <param name="isPublic">Whether the field should be public.</param>
        /// <returns>The C# declaration text of the field.</returns>
        internal static string BuildSingleDeclarationText(string typeName, string fieldName, bool isPublic)
        {
            return BuildSingleDeclarationText(typeName, fieldName, isPublic, string.Empty);
        }

        /// <summary>
        /// Builds the C# source text of a field declaration for a single reference with the given indent.
        /// </summary>
        /// <param name="typeName">The type name of the field.</param>
        /// <param name="fieldName">The name of the field.</param>
        /// <param name="isPublic">Whether the field should be public.</param>
        /// <param name="fieldIndent">The indentation to use for the generated field.</param>
        /// <returns>The C# declaration text of the field.</returns>
        internal static string BuildSingleDeclarationText(string typeName, string fieldName, bool isPublic, string fieldIndent)
        {
            if (isPublic)
            {
                return string.Format(PublicFieldFormat, fieldIndent, typeName, fieldName);
            }

            return string.Format(PrivateFieldFormat, fieldIndent, typeName, fieldName);
        }

        /// <summary>
        /// Builds the C# source text of a collection (List or Array) field declaration with the given indent.
        /// </summary>
        /// <param name="elementTypeName">The element type name of the collection.</param>
        /// <param name="fieldName">The name of the field.</param>
        /// <param name="isPublic">Whether the field should be public.</param>
        /// <param name="kind">The container kind (List or Array).</param>
        /// <param name="fieldIndent">The indentation to use for the generated field.</param>
        /// <returns>The C# declaration text of the collection field.</returns>
        internal static string BuildCollectionDeclarationText(string elementTypeName, string fieldName, bool isPublic, CollectionKind kind, string fieldIndent)
        {
            if (kind == CollectionKind.List)
            {
                if (isPublic)
                {
                    return string.Format(PublicListFieldFormat, fieldIndent, elementTypeName, fieldName);
                }
                return string.Format(SerializedListFieldFormat, fieldIndent, elementTypeName, fieldName);
            }

            if (isPublic)
            {
                return string.Format(PublicArrayFieldFormat, fieldIndent, elementTypeName, fieldName);
            }
            return string.Format(SerializedArrayFieldFormat, fieldIndent, elementTypeName, fieldName);
        }

        /// <summary>
        /// Builds the C# source text of a collection declaration for the selection window preview.
        /// </summary>
        /// <param name="elementType">The element type of the collection.</param>
        /// <param name="fieldName">The name of the field.</param>
        /// <param name="isPublic">Whether the field should be public.</param>
        /// <param name="kind">The container kind (List or Array).</param>
        /// <returns>The C# declaration text of the collection field.</returns>
        internal static string BuildCollectionDeclarationText(Type elementType, string fieldName, bool isPublic, CollectionKind kind)
        {
            return BuildCollectionDeclarationText(elementType.Name, fieldName, isPublic, kind, string.Empty);
        }

        /// <summary>
        /// Builds the C# source text of a field declaration for a pending field, routing to the single
        /// or collection builder according to its container kind.
        /// </summary>
        /// <param name="typeName">The element type name of the field.</param>
        /// <param name="fieldName">The name of the field.</param>
        /// <param name="isPublic">Whether the field should be public.</param>
        /// <param name="kind">The container kind (Single, List or Array).</param>
        /// <param name="fieldIndent">The indentation to use for the generated field.</param>
        /// <returns>The C# declaration text of the field.</returns>
        internal static string BuildDeclarationText(string typeName, string fieldName, bool isPublic, CollectionKind kind, string fieldIndent)
        {
            if (kind == CollectionKind.Single)
            {
                return BuildSingleDeclarationText(typeName, fieldName, isPublic, fieldIndent);
            }
            return BuildCollectionDeclarationText(typeName, fieldName, isPublic, kind, fieldIndent);
        }
    }
}