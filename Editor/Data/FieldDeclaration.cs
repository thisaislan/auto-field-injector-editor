namespace Thisaislan.AutoFieldInjectorEditor.Editor.Data
{
    /// <summary>
    /// A ready-to-inject field declaration together with the assignments that must survive
    /// recompilation.
    /// </summary>
    internal class FieldDeclaration
    {
        /// <summary>The C# declaration text of the field.</summary>
        internal string declaration;

        /// <summary>The name of the generated field.</summary>
        internal string fieldName;

        /// <summary>The EntityId of the target MonoBehaviour, stored as a string for JSON serialization.</summary>
        internal string targetRawId;

        /// <summary>The raw EntityIds of the assigned objects, one per element.</summary>
        internal string[] objectRawIds;
    }
}