namespace Thisaislan.AutoFieldInjectorEditor.Editor.Data
{
    /// <summary>
    /// Describes the kind of reference container that a pending field represents.
    /// </summary>
    internal enum CollectionKind
    {
        /// <summary>A single object reference.</summary>
        Single,

        /// <summary>A <see cref="System.Collections.Generic.List{T}"/> of object references.</summary>
        List,

        /// <summary>An array of object references.</summary>
        Array
    }
}