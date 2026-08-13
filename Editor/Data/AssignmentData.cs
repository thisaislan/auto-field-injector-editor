using System;
using UnityEngine;

namespace Thisaislan.AutoFieldInjectorEditor.Editor.Data
{
    /// <summary>
    /// Serializable record of a generated field and the object references assigned to it,
    /// persisted in EditorPrefs until the script recompiles.
    /// </summary>
    [Serializable]
    internal struct AssignmentData
    {
        /// <summary>The EntityId of the target MonoBehaviour, stored as a string for JSON serialization.</summary>
        [SerializeField]
        internal string targetRawId;

        /// <summary>The name of the generated field.</summary>
        [SerializeField]
        internal string fieldName;

        /// <summary>The EntityIds of the assigned objects, stored as strings for JSON serialization.
        /// Contains a single entry for single references and one entry per element for collections.</summary>
        [SerializeField]
        internal string[] objectRawIds;
    }
}