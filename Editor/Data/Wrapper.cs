using System;
using System.Collections.Generic;
using UnityEngine;

namespace Thisaislan.AutoFieldInjectorEditor.Editor.Data
{
    /// <summary>
    /// Generic JSON wrapper required by JsonUtility to serialize and deserialize lists.
    /// </summary>
    [Serializable]
    internal class Wrapper<T>
    {
        /// <summary>The wrapped list of items.</summary>
        [SerializeField]
        internal List<T> list;
    }
}