using System;
using UnityEngine;

namespace Thisaislan.AutoFieldInjectorEditor.Editor.Data
{
    /// <summary>
    /// Describes one possible choice the user can pick for dropped objects. For a single drop the
    /// candidate is a single reference (the object or one of its components); for a multi drop each
    /// candidate is one detected shared type whose objects will fill a List or Array field.
    /// </summary>
    internal class Candidate
    {
        /// <summary>The objects that will be assigned to the generated field. Contains one entry
        /// for single references and one per element for collections.</summary>
        internal UnityEngine.Object[] assignedObjects;

        /// <summary>The label shown in the selection window.</summary>
        internal string displayName;

        /// <summary>The element (or single) type of the generated field.</summary>
        internal Type type;

        /// <summary>The cached icon of the object, so it is not recreated every frame.</summary>
        internal GUIContent iconContent;
    }
}