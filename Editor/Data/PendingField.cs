using System;
using UnityEngine;

namespace Thisaislan.AutoFieldInjectorEditor.Editor.Data
{
    /// <summary>
    /// Describes a field that is waiting to be injected into the target script.
    /// </summary>
    internal class PendingField
    {
        /// <summary>The element (or single) type of the field that will be generated.</summary>
        internal Type fieldType;

        /// <summary>The name of the field that will be generated.</summary>
        public string fieldName;

        /// <summary>Whether the generated field should be public instead of serialized private.</summary>
        public bool isPublic;

        /// <summary>The kind of container of the generated field.</summary>
        public CollectionKind kind;

        /// <summary>The objects that will be assigned to the generated field. Contains a single
        /// entry for single references and one entry per element for List and Array containers.</summary>
        internal UnityEngine.Object[] assignedObjects;
    }
}