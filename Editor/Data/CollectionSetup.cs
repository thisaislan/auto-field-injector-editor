using System;
using UnityEngine;

namespace Thisaislan.AutoFieldInjectorEditor.Editor.Data
{
    /// <summary>
    /// Describes a detected common element type across a set of dropped objects, together with the
    /// objects that can be assigned to a collection of that type.
    /// </summary>
    internal class CollectionSetup
    {
        /// <summary>The detected common element type.</summary>
        internal Type elementType;

        /// <summary>The objects assignable to the element type.</summary>
        internal UnityEngine.Object[] assignedObjects;
    }
}