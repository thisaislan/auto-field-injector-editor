using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Thisaislan.AutoFieldInjectorEditor.Editor.Data
{
    /// <summary>
    /// Registry of the fields waiting to be injected, grouped by the EntityId of their target
    /// MonoBehaviour.
    /// </summary>
    internal static class PendingFieldStore
    {
        /// <summary>Fields waiting to be injected, grouped by the EntityId of their target MonoBehaviour.</summary>
        private static readonly Dictionary<EntityId, List<PendingField>> pendingFields = new Dictionary<EntityId, List<PendingField>>();

        /// <summary>
        /// Returns the pending fields stored for the given target, or null when there are none.
        /// </summary>
        /// <param name="targetId">The EntityId of the target.</param>
        /// <returns>The pending fields of the target, or null.</returns>
        internal static List<PendingField> GetPendingFields(EntityId targetId)
        {
            return pendingFields.TryGetValue(targetId, out List<PendingField> list) ? list : null;
        }

        /// <summary>
        /// Returns the pending fields stored for the given target, creating the entry when it does
        /// not exist yet.
        /// </summary>
        /// <param name="targetId">The EntityId of the target.</param>
        /// <returns>The pending fields of the target, never null.</returns>
        internal static List<PendingField> GetOrCreatePendingList(EntityId targetId)
        {
            if (!pendingFields.TryGetValue(targetId, out List<PendingField> list))
            {
                list = new List<PendingField>();
                pendingFields[targetId] = list;
            }

            return list;
        }

        /// <summary>
        /// Returns whether the given target currently has any pending fields.
        /// </summary>
        /// <param name="targetId">The EntityId of the target.</param>
        /// <returns>True when the target has pending fields, false otherwise.</returns>
        internal static bool HasPending(EntityId targetId)
        {
            return pendingFields.TryGetValue(targetId, out List<PendingField> list) && list.Count > 0;
        }

        /// <summary>
        /// Removes the pending fields stored for the given target.
        /// </summary>
        /// <param name="targetId">The EntityId of the target.</param>
        internal static void Remove(EntityId targetId)
        {
            pendingFields.Remove(targetId);
        }

        /// <summary>
        /// Removes pending entries whose target object no longer exists, so the registry cannot
        /// accumulate stale keys over a long editor session.
        /// </summary>
        internal static void CleanupStale()
        {
            List<EntityId> staleKeys = null;

            foreach (KeyValuePair<EntityId, List<PendingField>> kvp in pendingFields)
            {
                if (kvp.Key == EntityId.None || EditorUtility.EntityIdToObject(kvp.Key) == null)
                {
                    if (staleKeys == null)
                    {
                        staleKeys = new List<EntityId>();
                    }
                    staleKeys.Add(kvp.Key);
                }
            }

            if (staleKeys == null)
            {
                return;
            }

            for (int i = 0; i < staleKeys.Count; i++)
            {
                pendingFields.Remove(staleKeys[i]);
            }
        }
    }
}