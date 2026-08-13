using UnityEngine;
using UnityEditor;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Thisaislan.AutoFieldInjectorEditor.Editor.Data;
using Thisaislan.AutoFieldInjectorEditor.Editor.Windows;
using Thisaislan.AutoFieldInjectorEditor.Editor.Utilities;
using Thisaislan.AutoFieldInjectorEditor.Utilities;

namespace Thisaislan.AutoFieldInjectorEditor.Editor.Inspectors
{
    /// <summary>
    /// Custom inspector for MonoBehaviour scripts that lets the user drag an object onto the
    /// inspector, pick a reference, and injects a serialized reference field into the target
    /// script. The dragged object is wired up to the new field after recompilation.
    /// </summary>
    [CustomEditor(typeof(MonoBehaviour), true)]
    internal class AutoFieldInjectorInspector : UnityEditor.Editor
    {
        /// <summary>The key used to store pending assignments in EditorPrefs.</summary>
        private const string EditorPrefsKey = "AutoFieldInjector_PendingAssignments";

        /// <summary>The text shown inside the drop zone.</summary>
        private const string DropZoneMessage = "Drop here to add a serialized reference";

        /// <summary>The header of the pending additions list.</summary>
        private const string PendingListHeader = "Pending Additions";

        /// <summary>The text of the apply button.</summary>
        private const string ApplyButtonText = "Apply (Recompile)";

        /// <summary>The text of the clear all button.</summary>
        private const string ClearAllButtonText = "Clear All";

        /// <summary>The hint about automatic application when leaving the inspector.</summary>
        private const string AutoApplyHint = "If you leave this inspector, changes will be auto‑applied and recompiled.";

        /// <summary>Display name template for candidates. Format: name, type.</summary>
        private const string DisplayNameFormat = "{0} ({1})";

        /// <summary>Display name template for collection candidates. Format: type, count.</summary>
        private const string CollectionCandidateDisplayFormat = "{0} ({1})";

        /// <summary>The text of the remove button.</summary>
        private const string RemoveButtonText = "✕";

        /// <summary>Text describing a public field in the pending list.</summary>
        private const string PublicText = "public";

        /// <summary>Text describing a serialized field in the pending list.</summary>
        private const string SerializedText = "serialized";

        /// <summary>Template of a pending field row. Format: type, name, visibility.</summary>
        private const string PendingFieldDisplayFormat = "{0} {1} ({2})";

        /// <summary>The minimum number of objects required before a collection is suggested.</summary>
        private const int MinimumCollectionSize = 2;

        /// <summary>Error template when applying pending changes fails. Format: target name, exception.</summary>
        private const string ApplyFailedMessage = "Failed to apply pending changes for {0}.\n{1}";

        /// <summary>Error template when the script path cannot be resolved. Format: target name.</summary>
        private const string ScriptPathNotFoundErrorFormat = "Could not find script path for {0}.";

        /// <summary>Error template when the script file cannot be read. Format: path, exception.</summary>
        private const string ReadScriptErrorFormat = "Could not read script file '{0}'.\n{1}";

        /// <summary>Error template when the script file cannot be written. Format: path, exception.</summary>
        private const string WriteScriptErrorFormat = "Could not write script file '{0}'.\n{1}";

        /// <summary>Error template when the script file cannot be rolled back. Format: path, exception.</summary>
        private const string RollbackScriptErrorFormat = "Could not restore the original script content for '{0}'.\n{1}";

        /// <summary>Error template when no class is found in the script. Format: script path.</summary>
        private const string ClassNotFoundErrorFormat = "Could not find a class in script '{0}'.";

        /// <summary>Warning template when a field is skipped due to a member name collision. Format: field, class.</summary>
        private const string DuplicateMemberWarning = "Skipping field '{0}': a member with that name already exists in {1}.";

        /// <summary>Error shown when none of the pending fields could be applied.</summary>
        private const string NoFieldsAppliedMessage = "None of the pending fields could be applied. They were skipped.";

        /// <summary>Warning template when stored assignments cannot be parsed and are replaced. Format: exception.</summary>
        private const string ParseAssignmentsWarning = "Could not parse stored pending assignments. They will be replaced.\n{0}";

        /// <summary>Warning template when the assigned field is not found. Format: field name, target name.</summary>
        private const string FieldNotFoundWarning = "Field '{0}' not found on {1}. It may not exist yet.";

        /// <summary>Warning template when the assigned object no longer exists. Format: raw id, field name.</summary>
        private const string ObjectNotFoundWarning =
            "Could not find object with entity ID '{0}' for field {1}. The reference is no longer valid (e.g. the object was destroyed or the editor session was restarted).";

        /// <summary>The height of the drop zone in pixels.</summary>
        private const int DropZoneHeight = 50;

        /// <summary>The minimum height requested for the drop zone rect.</summary>
        private const float DropZoneMinHeight = 0f;

        /// <summary>The font size of the drop zone message.</summary>
        private const int DropZoneFontSize = 11;

        /// <summary>The padding around the drop zone message.</summary>
        private const int DropZonePadding = 8;

        /// <summary>The width of the selection window in pixels.</summary>
        private const int SelectionWindowWidth = 340;

        /// <summary>The height of the selection window in pixels.</summary>
        private const int SelectionWindowHeight = 450;

        /// <summary>The width of the remove button in the pending list.</summary>
        private const float RemoveButtonWidth = 20f;

        /// <summary>The size used for the zero-sized drop-down window anchor rect.</summary>
        private const float ZeroSize = 0f;

        /// <summary>The color of the drop zone message text.</summary>
        private static readonly Color DropZoneTextColor = new Color(0.7f, 0.7f, 0.7f);

        /// <summary>The encoding used when a script file has no detectable encoding.</summary>
        private static readonly Encoding DefaultScriptEncoding = new UTF8Encoding(false);

        /// <summary>Whether the inspected target is a user script as opposed to a built-in one.</summary>
        private bool isUserScript;

        /// <summary>The EntityId of the inspected target, used to index the pending fields dictionary.</summary>
        private EntityId targetId;

        /// <summary>Whether the inspected target currently has any pending fields.</summary>
        private bool hasPending => PendingFieldStore.HasPending(targetId);

        /// <summary>The member names declared by the inspected target's script, cached at enable time.</summary>
        private HashSet<string> targetMemberNames;

        /// <summary>The reusable drop zone style, created lazily because GUIStyle needs an active skin.</summary>
        private GUIStyle dropZoneStyle;

        /// <summary>
        /// Called when the inspector is enabled. Detects whether the target is a user script and
        /// restores any assignments that were stored before the last recompilation.
        /// </summary>
        private void OnEnable()
        {
            MonoScript monoScript = MonoScript.FromMonoBehaviour((MonoBehaviour)target);
            isUserScript = (monoScript != null) && !string.IsNullOrEmpty(AssetDatabase.GetAssetPath(monoScript));
            targetId = target.GetEntityId();

            PendingFieldStore.CleanupStale();

            if (isUserScript)
            {
                targetMemberNames = BuildTargetMemberNames();
                ApplyPendingAssignmentsFromPrefs();
            }
        }

        /// <summary>
        /// Returns whether the inspected target's script already declares a member with the given
        /// name, using the member names cached when the inspector was enabled.
        /// </summary>
        /// <param name="memberName">The member name to look for.</param>
        /// <returns>True when the script already declares the member, false otherwise.</returns>
        internal bool TargetHasMemberName(string memberName)
        {
            return targetMemberNames != null && targetMemberNames.Contains(memberName);
        }

        /// <summary>
        /// Reads the inspected target's script once and returns the set of member names it declares,
        /// or null when the script cannot be resolved.
        /// </summary>
        /// <returns>The cached member names, or null on any resolution failure.</returns>
        private HashSet<string> BuildTargetMemberNames()
        {
            MonoScript monoScript = MonoScript.FromMonoBehaviour((MonoBehaviour)target);

            if (monoScript == null)
            {
                return null;
            }

            string scriptPath = AssetDatabase.GetAssetPath(monoScript);

            if (string.IsNullOrEmpty(scriptPath))
            {
                return null;
            }

            ScriptFile scriptFile = ReadScriptFile(GetFullScriptPath(scriptPath));
            
            if (scriptFile == null)
            {
                return null;
            }

            Type classType = monoScript.GetClass();

            if (classType == null)
            {
                return null;
            }

            return ScriptGenerator.GetMemberNames(scriptFile.content, classType.Name);
        }

        /// <summary>
        /// Called when the inspector is disabled. Applies any pending changes so they are not lost,
        /// logging any unexpected failure.
        /// </summary>
        private void OnDisable()
        {
            if (!hasPending)
            {
                return;
            }

            try
            {
                ApplyPendingChanges((MonoBehaviour)target, targetId);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        /// <summary>
        /// Draws the custom inspector GUI: the drop zone, the pending additions and the default
        /// inspector for non-user scripts.
        /// </summary>
        public override void OnInspectorGUI()
        {
            if (!isUserScript)
            {
                DrawDefaultInspector();
                return;
            }

            DrawDropZone();
            DrawPendingSection();
            DrawDefaultInspector();
        }

        /// <summary>
        /// Draws the pending additions list or an informative message when there is nothing pending.
        /// </summary>
        private void DrawPendingSection()
        {
            List<PendingField> list = PendingFieldStore.GetPendingFields(targetId);

            if (list != null && list.Count > 0)
            {
                DrawPendingList(list);
            }

            EditorGUILayout.Space();
        }

        /// <summary>
        /// Draws the drop zone that accepts dragged objects.
        /// </summary>
        private void DrawDropZone()
        {
            GUIStyle style = GetDropZoneStyle();
            Rect dropArea = GUILayoutUtility.GetRect(DropZoneMinHeight, (float)DropZoneHeight, GUILayout.ExpandWidth(true));
            GUI.Box(dropArea, DropZoneMessage, style);

            Event evt = Event.current;

            if (evt.type != EventType.DragUpdated && evt.type != EventType.DragPerform)
            {
                return;
            }
            if (!dropArea.Contains(evt.mousePosition))
            {
                return;
            }

            DragAndDrop.visualMode = DragAndDropVisualMode.Copy;

            if (evt.type == EventType.DragPerform)
            {
                HandleDrop();
            }
        }

        /// <summary>
        /// Returns the drop zone style, creating it once on first use.
        /// </summary>
        /// <returns>The cached drop zone style.</returns>
        private GUIStyle GetDropZoneStyle()
        {
            if (dropZoneStyle == null)
            {
                dropZoneStyle = CreateDropZoneStyle();
            }
            return dropZoneStyle;
        }

        /// <summary>
        /// Creates the style used to render the drop zone box.
        /// </summary>
        /// <returns>The new drop zone style.</returns>
        private static GUIStyle CreateDropZoneStyle()
        {
            return new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = DropZoneFontSize,
                normal = { textColor = DropZoneTextColor },
                padding = new RectOffset(DropZonePadding, DropZonePadding, DropZonePadding, DropZonePadding)
            };
        }

        /// <summary>
        /// Accepts the drop and opens the selection window for the dropped objects, detecting whether
        /// they share a common element type so a list or array can be suggested.
        /// </summary>
        private void HandleDrop()
        {
            DragAndDrop.AcceptDrag();
            UnityEngine.Object[] droppedObjects = GetDroppedObjects();

            if (droppedObjects == null || droppedObjects.Length == 0)
            {
                return;
            }

            OpenSelectionWindow(droppedObjects);
        }

        /// <summary>
        /// Returns all objects currently being dragged, or an empty array when nothing is being dragged.
        /// </summary>
        /// <returns>The dragged objects, or an empty array.</returns>
        private static UnityEngine.Object[] GetDroppedObjects()
        {
            UnityEngine.Object[] references = DragAndDrop.objectReferences;
            if (references != null && references.Length > 0)
            {
                return references;
            }

            return new UnityEngine.Object[0];
        }

        /// <summary>
        /// Draws the list of pending fields with a remove button for each row.
        /// </summary>
        /// <param name="list">The pending fields of the inspected target.</param>
        private void DrawPendingList(List<PendingField> list)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField(PendingListHeader, EditorStyles.boldLabel);

            for (int i = 0; i < list.Count; i++)
            {
                if (DrawPendingFieldRow(list, i))
                {
                    break;
                }
            }

            DrawPendingListActions();
        }

        /// <summary>
        /// Draws a single pending field row and removes it when its remove button is pressed.
        /// </summary>
        /// <param name="list">The pending fields of the inspected target.</param>
        /// <param name="index">The index of the row to draw.</param>
        /// <returns>True when the row was removed, false otherwise.</returns>
        private bool DrawPendingFieldRow(List<PendingField> list, int index)
        {
            PendingField pendingField = list[index];
            string displayText = BuildFieldDisplay(pendingField);
            bool removeRequested;

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(displayText);
            removeRequested = GUILayout.Button(RemoveButtonText, GUILayout.Width(RemoveButtonWidth));
            EditorGUILayout.EndHorizontal();

            if (!removeRequested)
            {
                return false;
            }

            RemovePendingField(list, index);
            Repaint();
            return true;
        }

        /// <summary>
        /// Removes a pending field at the given index, dropping the target entry when the list becomes empty.
        /// </summary>
        /// <param name="list">The pending fields of the inspected target.</param>
        /// <param name="index">The index of the field to remove.</param>
        private void RemovePendingField(List<PendingField> list, int index)
        {
            list.RemoveAt(index);

            if (list.Count == 0)
            {
                PendingFieldStore.Remove(targetId);
            }
        }

        /// <summary>
        /// Builds the display text of a pending field row.
        /// </summary>
        /// <param name="pendingField">The pending field to describe.</param>
        /// <returns>The display text including type, name and visibility.</returns>
        private static string BuildFieldDisplay(PendingField pendingField)
        {
            string visibilityText = pendingField.isPublic ? PublicText : SerializedText;
            string typeText = BuildFieldTypeDisplay(pendingField);

            return string.Format(PendingFieldDisplayFormat, typeText, pendingField.fieldName, visibilityText);
        }

        /// <summary>
        /// Builds the type text of a pending field, reflecting its container kind.
        /// </summary>
        /// <param name="pendingField">The pending field to describe.</param>
        /// <returns>The type text of the pending field.</returns>
        private static string BuildFieldTypeDisplay(PendingField pendingField)
        {
            switch (pendingField.kind)
            {
                case CollectionKind.List:
                    return "List<" + pendingField.fieldType.Name + ">";
                case CollectionKind.Array:
                    return pendingField.fieldType.Name + "[]";
                default:
                    return pendingField.fieldType.Name;
            }
        }

        /// <summary>
        /// Draws the apply and clear all buttons together with the auto-apply hint.
        /// </summary>
        private void DrawPendingListActions()
        {
            EditorGUILayout.Space();
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button(ApplyButtonText))
            {
                ApplyAndRefresh();
            }

            if (GUILayout.Button(ClearAllButtonText))
            {
                PendingFieldStore.Remove(targetId);
                Repaint();
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.HelpBox(AutoApplyHint, MessageType.None);
        }

        /// <summary>
        /// Applies the pending changes and refreshes the asset database so Unity picks up the new script.
        /// </summary>
        private void ApplyAndRefresh()
        {
            ApplyPendingChanges((MonoBehaviour)target, targetId);
            AssetDatabase.Refresh();
        }

        /// <summary>
        /// Applies all pending fields for the target when any exist, logging any unexpected failure.
        /// </summary>
        /// <param name="target">The target MonoBehaviour whose script will be modified.</param>
        /// <param name="targetId">The EntityId of the target.</param>
        private static void ApplyPendingChanges(MonoBehaviour target, EntityId targetId)
        {
            List<PendingField> list = PendingFieldStore.GetPendingFields(targetId);
            if (list == null || list.Count == 0)
            {
                return;
            }

            try
            {
                ApplyPendingChangesInternal(target, targetId, list);
            }
            catch (Exception e)
            {
                Debug.LogError(string.Format(ApplyFailedMessage, target.name, e));
            }
        }

        /// <summary>
        /// Core logic that injects the pending fields into the target script, writes the file and
        /// stores the assignments that must survive recompilation.
        /// </summary>
        /// <param name="target">The target MonoBehaviour whose script will be modified.</param>
        /// <param name="targetId">The EntityId of the target.</param>
        /// <param name="list">The pending fields to inject.</param>
        private static void ApplyPendingChangesInternal(MonoBehaviour target, EntityId targetId, List<PendingField> list)
        {
            MonoScript monoScript = MonoScript.FromMonoBehaviour(target);
            string scriptPath = GetScriptPath(monoScript, target);

            if (string.IsNullOrEmpty(scriptPath))
            {
                return;
            }

            string fullPath = GetFullScriptPath(scriptPath);

            ScriptFile scriptFile = ReadScriptFile(fullPath);

            if (scriptFile == null)
            {
                return;
            }

            Type classType = GetScriptClass(monoScript, scriptPath);

            if (classType == null)
            {
                return;
            }

            List<FieldDeclaration> declarations = BuildFieldDeclarations(targetId, list, scriptFile.content, classType);

            if (declarations.Count == 0)
            {
                PendingFieldStore.Remove(targetId);
                Debug.LogError(NoFieldsAppliedMessage);
                return;
            }

            List<string> missingNamespaces = ScriptGenerator.GetMissingNamespaces(
                scriptFile.content, GetFieldTypes(list), RequiresGenericCollectionsNamespace(list));

            if (missingNamespaces.Count > 0)
            {
                scriptFile.content = ScriptGenerator.InsertUsingDirectives(scriptFile.content, missingNamespaces);
            }

            string newContent = ScriptGenerator.InsertFields(scriptFile.content, classType.Name, ToDeclarationTexts(declarations));

            if (!WriteScriptFile(fullPath, scriptFile, newContent))
            {
                return;
            }

            StoreAssignmentsInPrefs(ToAssignmentDataList(declarations));

            PendingFieldStore.Remove(targetId);

            if (!EditorApplication.isCompiling)
            {
                AssetDatabase.Refresh();
            }
        }

        /// <summary>
        /// Resolves the project-relative script path of the target, logging an error when it fails.
        /// </summary>
        /// <param name="monoScript">The MonoScript of the target.</param>
        /// <param name="target">The target MonoBehaviour used for error reporting.</param>
        /// <returns>The project-relative script path, or null when it could not be resolved.</returns>
        private static string GetScriptPath(MonoScript monoScript, MonoBehaviour target)
        {
            string scriptPath = AssetDatabase.GetAssetPath(monoScript);

            if (string.IsNullOrEmpty(scriptPath))
            {
                Debug.LogError(string.Format(ScriptPathNotFoundErrorFormat, target.name));
                return null;
            }

            return scriptPath;
        }

        /// <summary>
        /// Converts a project-relative script path into an absolute file system path.
        /// </summary>
        /// <param name="scriptPath">The project-relative script path.</param>
        /// <returns>The absolute path of the script file.</returns>
        private static string GetFullScriptPath(string scriptPath)
        {
            return Path.Combine(Path.GetDirectoryName(Application.dataPath), scriptPath);
        }

        /// <summary>
        /// Reads the script file, preserving its detected encoding, or logs an error on failure.
        /// </summary>
        /// <param name="fullPath">The absolute path of the script file.</param>
        /// <returns>The script content and encoding, or null when the file could not be read.</returns>
        private static ScriptFile ReadScriptFile(string fullPath)
        {
            try
            {
                using (StreamReader reader = new StreamReader(fullPath, DefaultScriptEncoding, true))
                {
                    return new ScriptFile
                    {
                        content = reader.ReadToEnd(),
                        encoding = reader.CurrentEncoding
                    };
                }
            }
            catch (Exception e)
            {
                Debug.LogError(string.Format(ReadScriptErrorFormat, fullPath, e));
                return null;
            }
        }

        /// <summary>
        /// Returns the compiled class declared by the script, logging an error when none is found.
        /// </summary>
        /// <param name="monoScript">The MonoScript of the target.</param>
        /// <param name="scriptPath">The project-relative script path used for error reporting.</param>
        /// <returns>The class type declared by the script, or null when none could be found.</returns>
        private static Type GetScriptClass(MonoScript monoScript, string scriptPath)
        {
            Type classType = monoScript.GetClass();

            if (classType == null)
            {
                Debug.LogError(string.Format(ClassNotFoundErrorFormat, scriptPath));
            }
            return classType;
        }

        /// <summary>
        /// Builds the field declarations for all pending fields, skipping those that collide with an
        /// existing member in the class.
        /// </summary>
        /// <param name="targetId">The EntityId of the target.</param>
        /// <param name="list">The pending fields to build declarations for.</param>
        /// <param name="scriptContent">The current content of the script.</param>
        /// <param name="classType">The compiled class declared by the script.</param>
        /// <returns>The field declarations that can be injected.</returns>
        private static List<FieldDeclaration> BuildFieldDeclarations(EntityId targetId, List<PendingField> list, string scriptContent, Type classType)
        {
            string className = classType.Name;
            string fieldIndent = ScriptGenerator.GetFieldIndent(scriptContent, className);
            HashSet<string> existingMembers = ScriptGenerator.GetMemberNames(scriptContent, className);

            List<FieldDeclaration> declarations = new List<FieldDeclaration>(list.Count);

            for (int i = 0; i < list.Count; i++)
            {
                PendingField pendingField = list[i];
                if (existingMembers.Contains(pendingField.fieldName))
                {
                    Debug.LogWarning(string.Format(DuplicateMemberWarning, pendingField.fieldName, className));
                    continue;
                }
                declarations.Add(CreateFieldDeclaration(pendingField, fieldIndent, targetId));
            }
            return declarations;
        }

        /// <summary>
        /// Creates a field declaration for a pending field, including the object assignments that must
        /// survive recompilation.
        /// </summary>
        /// <param name="pendingField">The pending field to build a declaration for.</param>
        /// <param name="fieldIndent">The indentation to use for the generated field.</param>
        /// <param name="targetId">The EntityId of the target.</param>
        /// <returns>The created field declaration.</returns>
        private static FieldDeclaration CreateFieldDeclaration(PendingField pendingField, string fieldIndent, EntityId targetId)
        {
            return new FieldDeclaration
            {
                declaration = BuildDeclarationText(pendingField, fieldIndent),
                fieldName = pendingField.fieldName,
                targetRawId = EntityId.ToULong(targetId).ToString(),
                objectRawIds = BuildObjectRawIds(pendingField.assignedObjects)
            };
        }

        /// <summary>
        /// Returns the raw EntityId strings of the assigned objects, one per element.
        /// </summary>
        /// <param name="assignedObjects">The objects assigned to the field.</param>
        /// <returns>The raw EntityId strings of the objects.</returns>
        private static string[] BuildObjectRawIds(UnityEngine.Object[] assignedObjects)
        {
            if (assignedObjects == null || assignedObjects.Length == 0)
            {
                return new string[0];
            }

            string[] rawIds = new string[assignedObjects.Length];

            for (int i = 0; i < assignedObjects.Length; i++)
            {
                rawIds[i] = EntityId.ToULong(assignedObjects[i].GetEntityId()).ToString();
            }

            return rawIds;
        }

        /// <summary>
        /// Builds the C# source text of a field declaration respecting its visibility and container kind.
        /// </summary>
        /// <param name="pendingField">The pending field to build text for.</param>
        /// <param name="fieldIndent">The indentation to use for the generated field.</param>
        /// <returns>The C# declaration text of the field.</returns>
        private static string BuildDeclarationText(PendingField pendingField, string fieldIndent)
        {
            return FieldDeclarationTextBuilder.BuildDeclarationText(pendingField.fieldType.Name, pendingField.fieldName, pendingField.isPublic, pendingField.kind, fieldIndent);
        }

        /// <summary>
        /// Collects the field types of all pending fields.
        /// </summary>
        /// <param name="list">The pending fields to collect types from.</param>
        /// <returns>The list of field types.</returns>
        private static List<Type> GetFieldTypes(List<PendingField> list)
        {
            List<Type> fieldTypes = new List<Type>(list.Count);

            for (int i = 0; i < list.Count; i++)
            {
                fieldTypes.Add(list[i].fieldType);
            }

            return fieldTypes;
        }

        /// <summary>
        /// Returns whether any pending field is a List, which requires the generic collection namespace.
        /// </summary>
        /// <param name="list">The pending fields to inspect.</param>
        /// <returns>True when at least one pending field is a List, false otherwise.</returns>
        private static bool RequiresGenericCollectionsNamespace(List<PendingField> list)
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].kind == CollectionKind.List)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Converts field declarations into their C# source texts.
        /// </summary>
        /// <param name="declarations">The field declarations to convert.</param>
        /// <returns>The list of declaration texts.</returns>
        private static List<string> ToDeclarationTexts(List<FieldDeclaration> declarations)
        {
            List<string> texts = new List<string>(declarations.Count);

            for (int i = 0; i < declarations.Count; i++)
            {
                texts.Add(declarations[i].declaration);
            }
            return texts;
        }

        /// <summary>
        /// Converts field declarations into the assignment records that must be persisted.
        /// </summary>
        /// <param name="declarations">The field declarations to convert.</param>
        /// <returns>The list of assignment records.</returns>
        private static List<AssignmentData> ToAssignmentDataList(List<FieldDeclaration> declarations)
        {
            List<AssignmentData> assignments = new List<AssignmentData>(declarations.Count);

            for (int i = 0; i < declarations.Count; i++)
            {
                FieldDeclaration declaration = declarations[i];
                assignments.Add(new AssignmentData
                {
                    targetRawId = declaration.targetRawId,
                    fieldName = declaration.fieldName,
                    objectRawIds = declaration.objectRawIds
                });
            }

            return assignments;
        }

        /// <summary>
        /// Writes the new script content, rolling the file back to its original content when the
        /// write fails.
        /// </summary>
        /// <param name="fullPath">The absolute path of the script file.</param>
        /// <param name="scriptFile">The original script content and encoding.</param>
        /// <param name="newContent">The new script content to write.</param>
        /// <returns>True when the file was written successfully, false otherwise.</returns>
        private static bool WriteScriptFile(string fullPath, ScriptFile scriptFile, string newContent)
        {
            try
            {
                File.WriteAllText(fullPath, newContent, scriptFile.encoding ?? DefaultScriptEncoding);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError(string.Format(WriteScriptErrorFormat, fullPath, e));
                return RollbackScriptFile(fullPath, scriptFile);
            }
        }

        /// <summary>
        /// Restores the original content of a script file after a failed write.
        /// </summary>
        /// <param name="fullPath">The absolute path of the script file.</param>
        /// <param name="scriptFile">The original script content and encoding.</param>
        /// <returns>True when the file was restored successfully, false otherwise.</returns>
        private static bool RollbackScriptFile(string fullPath, ScriptFile scriptFile)
        {
            try
            {
                File.WriteAllText(fullPath, scriptFile.content, scriptFile.encoding ?? DefaultScriptEncoding);
                return false;
            }
            catch (Exception rollbackException)
            {
                Debug.LogError(string.Format(RollbackScriptErrorFormat, fullPath, rollbackException));
                return false;
            }
        }

        /// <summary>
        /// Merges the newly created assignments into the assignments already stored in EditorPrefs.
        /// </summary>
        /// <param name="newAssignments">The assignments created by the last injection.</param>
        private static void StoreAssignmentsInPrefs(List<AssignmentData> newAssignments)
        {
            List<AssignmentData> allAssignments = ReadStoredAssignments() ?? new List<AssignmentData>();

            for (int i = 0; i < newAssignments.Count; i++)
            {
                AssignmentData data = newAssignments[i];
                allAssignments.RemoveAll(a => a.targetRawId == data.targetRawId && a.fieldName == data.fieldName);
                allAssignments.Add(data);
            }

            StoreAssignments(allAssignments);
        }

        /// <summary>
        /// Reads the stored assignments from EditorPrefs, discarding them when they cannot be parsed.
        /// </summary>
        /// <returns>The stored assignments, or null when there are none or they are invalid.</returns>
        private static List<AssignmentData> ReadStoredAssignments()
        {
            string json = EditorPrefs.GetString(EditorPrefsKey, string.Empty);

            if (string.IsNullOrEmpty(json))
            {
                return null;
            }

            try
            {
                return JsonUtility.FromJson<Wrapper<AssignmentData>>(json)?.list;
            }
            catch (Exception e)
            {
                Debug.LogWarning(string.Format(ParseAssignmentsWarning, e));
                return null;
            }
        }

        /// <summary>
        /// Persists the assignments to EditorPrefs, or deletes the key when the list is empty.
        /// </summary>
        /// <param name="allAssignments">The assignments to persist.</param>
        private static void StoreAssignments(List<AssignmentData> allAssignments)
        {
            if (allAssignments == null || allAssignments.Count == 0)
            {
                EditorPrefs.DeleteKey(EditorPrefsKey);
                return;
            }

            Wrapper<AssignmentData> wrapper = new Wrapper<AssignmentData>
            {
                list = allAssignments
            };

            EditorPrefs.SetString(EditorPrefsKey, JsonUtility.ToJson(wrapper));
        }

        /// <summary>
        /// Restores the assignments that belong to the inspected target, wiring up the generated fields
        /// to the objects they point at, and consumes those assignments afterwards.
        /// </summary>
        private void ApplyPendingAssignmentsFromPrefs()
        {
            List<AssignmentData> allAssignments = ReadStoredAssignments();

            if (allAssignments == null || allAssignments.Count == 0)
            {
                return;
            }

            List<AssignmentData> myAssignments = GetAssignmentsForTarget(allAssignments, targetId);

            if (myAssignments.Count == 0)
            {
                return;
            }

            ApplyAssignmentsToSerializedObject(myAssignments);

            allAssignments.RemoveAll(a => ParseEntityId(a.targetRawId) == targetId);
            StoreAssignments(allAssignments);
        }

        /// <summary>
        /// Filters the stored assignments down to those that belong to the given target.
        /// </summary>
        /// <param name="allAssignments">All stored assignments.</param>
        /// <param name="targetId">The EntityId of the target to filter for.</param>
        /// <returns>The assignments belonging to the target.</returns>
        private static List<AssignmentData> GetAssignmentsForTarget(List<AssignmentData> allAssignments, EntityId targetId)
        {
            return allAssignments.FindAll(a => ParseEntityId(a.targetRawId) == targetId);
        }

        /// <summary>
        /// Wires up the generated fields of the target to their assigned objects, marking the target
        /// dirty when anything changed.
        /// </summary>
        /// <param name="assignments">The assignments to apply to the target.</param>
        private void ApplyAssignmentsToSerializedObject(List<AssignmentData> assignments)
        {
            SerializedObject serializedObject = new SerializedObject(target);
            bool changed = false;

            for (int i = 0; i < assignments.Count; i++)
            {
                changed |= ApplyAssignment(serializedObject, assignments[i]);
            }

            if (!changed)
            {
                return;
            }

            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(target);
        }

        /// <summary>
        /// Applies a single assignment to the target's serialized object, wiring up either a single
        /// object reference or a collection (List or Array) of object references.
        /// </summary>
        /// <param name="serializedObject">The serialized object of the target.</param>
        /// <param name="data">The assignment to apply.</param>
        /// <returns>True when the assignment was applied, false otherwise.</returns>
        private bool ApplyAssignment(SerializedObject serializedObject, AssignmentData data)
        {
            bool isCollection = data.objectRawIds != null && data.objectRawIds.Length > 1;

            List<UnityEngine.Object> resolvedObjects = ResolveObjects(data, isCollection);

            if (resolvedObjects == null)
            {
                return false;
            }

            SerializedProperty property = serializedObject.FindProperty(data.fieldName);

            if (property == null)
            {
                Debug.LogWarning(string.Format(FieldNotFoundWarning, data.fieldName, target.name));
                return false;
            }

            if (isCollection)
            {
                return ApplyCollectionAssignment(property, resolvedObjects);
            }
            return ApplySingleAssignment(property, resolvedObjects[0], data);
        }

        /// <summary>
        /// Resolves the raw object IDs of an assignment into live Unity objects, returning null when
        /// any required object is missing.
        /// </summary>
        /// <param name="data">The assignment containing the raw object IDs.</param>
        /// <param name="isCollection">Whether multiple objects are expected.</param>
        /// <returns>The resolved objects, or null when one or more are missing.</returns>
        private List<UnityEngine.Object> ResolveObjects(AssignmentData data, bool isCollection)
        {
            if (data.objectRawIds == null || data.objectRawIds.Length == 0)
            {
                return null;
            }

            List<UnityEngine.Object> objects = new List<UnityEngine.Object>(data.objectRawIds.Length);

            for (int i = 0; i < data.objectRawIds.Length; i++)
            {
                string rawId = data.objectRawIds[i];
                UnityEngine.Object assignedObject = EditorUtility.EntityIdToObject(ParseEntityId(rawId));

                if (assignedObject == null)
                {
                    Debug.LogWarning(string.Format(ObjectNotFoundWarning, rawId, data.fieldName));

                    if (!isCollection)
                    {
                        return null;
                    }
                    continue;
                }

                objects.Add(assignedObject);
            }

            return objects;
        }

        /// <summary>
        /// Applies a single object reference to a serialized property.
        /// </summary>
        /// <param name="property">The serialized property to assign.</param>
        /// <param name="assignedObject">The object to assign.</param>
        /// <param name="data">The assignment record, used for error reporting.</param>
        /// <returns>True when the assignment was applied, false otherwise.</returns>
        private bool ApplySingleAssignment(SerializedProperty property, UnityEngine.Object assignedObject, AssignmentData data)
        {
            if (property.propertyType != SerializedPropertyType.ObjectReference)
            {
                Debug.LogWarning(string.Format(FieldNotFoundWarning, data.fieldName, target.name));
                return false;
            }

            property.objectReferenceValue = assignedObject;
            return true;
        }

        /// <summary>
        /// Applies a collection of object references to a serialized property, resizing an array or
        /// List to match the resolved objects and assigning each element.
        /// </summary>
        /// <param name="property">The serialized property of the collection.</param>
        /// <param name="objects">The objects to assign.</param>
        /// <returns>True when the collection was applied, false otherwise.</returns>
        private bool ApplyCollectionAssignment(SerializedProperty property, List<UnityEngine.Object> objects)
        {
            if (!property.isArray)
            {
                Debug.LogWarning(string.Format(FieldNotFoundWarning, property.name, target.name));
                return false;
            }

            property.arraySize = objects.Count;

            for (int i = 0; i < objects.Count; i++)
            {
                SerializedProperty element = property.GetArrayElementAtIndex(i);
                if (element.propertyType == SerializedPropertyType.ObjectReference)
                {
                    element.objectReferenceValue = objects[i];
                }
            }
            return true;
        }

        /// <summary>
        /// Parses a raw EntityId string back into an EntityId.
        /// </summary>
        /// <param name="rawId">The raw EntityId string.</param>
        /// <returns>The parsed EntityId, or EntityId.None when the string is invalid.</returns>
        private static EntityId ParseEntityId(string rawId)
        {
            if (ulong.TryParse(rawId, out ulong value))
            {
                return EntityId.FromULong(value);
            }

            return EntityId.None;
        }

        /// <summary>
        /// Opens the selection window for the dropped objects. A single drop builds the usual reference
        /// candidates; a multi drop builds one candidate per detected shared type so the user can pick
        /// the element type of a List or Array collection.
        /// </summary>
        /// <param name="droppedObjects">The dropped objects to build candidates from.</param>
        private void OpenSelectionWindow(UnityEngine.Object[] droppedObjects)
        {
            if (droppedObjects.Length > 1)
            {
                OpenCollectionWindow(droppedObjects);
                return;
            }

            OpenSingleWindow(droppedObjects[0]);
        }

        /// <summary>
        /// Opens the selection window for a single dropped object, offering the object itself and its
        /// components as single references.
        /// </summary>
        /// <param name="droppedObject">The dropped object to build candidates from.</param>
        private void OpenSingleWindow(UnityEngine.Object droppedObject)
        {
            List<Candidate> candidates = BuildCandidates(droppedObject);

            SelectionWindow window = ScriptableObject.CreateInstance<SelectionWindow>();
            window.Initialize(candidates, false, targetId, this);
            ShowWindowAtMouse(window);
        }

        /// <summary>
        /// Opens the selection window for multiple dropped objects in collection mode, offering one
        /// candidate per detected shared type so the user can pick the element type of a List or Array.
        /// </summary>
        /// <param name="droppedObjects">The dropped objects to detect shared types from.</param>
        private void OpenCollectionWindow(UnityEngine.Object[] droppedObjects)
        {
            List<CollectionSetup> sharedTypes = TryDetectSharedTypes(droppedObjects);
            List<Candidate> candidates = BuildCollectionCandidates(sharedTypes);

            SelectionWindow window = ScriptableObject.CreateInstance<SelectionWindow>();
            window.Initialize(candidates, true, targetId, this);
            ShowWindowAtMouse(window);
        }

        /// <summary>
        /// Builds the candidate list for a collection window, one candidate per detected shared type.
        /// </summary>
        /// <param name="sharedTypes">The detected shared types.</param>
        /// <returns>The list of pickable collection candidates.</returns>
        private static List<Candidate> BuildCollectionCandidates(List<CollectionSetup> sharedTypes)
        {
            List<Candidate> candidates = new List<Candidate>(sharedTypes.Count);

            for (int i = 0; i < sharedTypes.Count; i++)
            {
                candidates.Add(CreateCollectionCandidate(sharedTypes[i]));
            }
            return candidates;
        }

        /// <summary>
        /// Creates a candidate describing a collection of the given shared type, showing the type name
        /// and how many objects it covers.
        /// </summary>
        /// <param name="setup">The shared type to build a candidate for.</param>
        /// <returns>The created collection candidate.</returns>
        private static Candidate CreateCollectionCandidate(CollectionSetup setup)
        {
            return new Candidate
            {
                assignedObjects = setup.assignedObjects,
                displayName = string.Format(CollectionCandidateDisplayFormat, setup.elementType.Name, setup.assignedObjects.Length),
                type = setup.elementType,
                iconContent = new GUIContent(EditorGUIUtility.ObjectContent(setup.assignedObjects[0], setup.elementType).image)
            };
        }

        /// <summary>
        /// Detects every type shared by at least the minimum number of dropped objects so that one of
        /// them can be picked as the element type of a List or Array. Considers the dropped objects'
        /// own types, the component types of dropped GameObjects and, as a fallback, the closest
        /// common base type of all dropped objects. The results are ordered by how many objects each
        /// type covers.
        /// </summary>
        /// <param name="droppedObjects">The dropped objects to analyse.</param>
        /// <returns>The detected shared types, or an empty list when none satisfy the threshold.</returns>
        private static List<CollectionSetup> TryDetectSharedTypes(UnityEngine.Object[] droppedObjects)
        {
            if (droppedObjects == null || droppedObjects.Length < MinimumCollectionSize)
            {
                return new List<CollectionSetup>();
            }

            Dictionary<Type, int> typeFrequency = new Dictionary<Type, int>();

            for (int i = 0; i < droppedObjects.Length; i++)
            {
                CountType(typeFrequency, droppedObjects[i].GetType());
                CountComponentTypes(typeFrequency, droppedObjects[i]);
            }

            List<CollectionSetup> sharedTypes = new List<CollectionSetup>();

            foreach (KeyValuePair<Type, int> kvp in typeFrequency)
            {
                if (kvp.Value < MinimumCollectionSize || kvp.Key == typeof(UnityEngine.Object))
                {
                    continue;
                }
                sharedTypes.Add(CreateCollectionSetup(kvp.Key, CollectAssignableObjects(droppedObjects, kvp.Key)));
            }

            Type commonBaseType = FindClosestCommonBaseType(droppedObjects);

            if (commonBaseType != null
                && commonBaseType != typeof(UnityEngine.Object)
                && !sharedTypes.Exists(setup => setup.elementType == commonBaseType))
            {
                sharedTypes.Add(CreateCollectionSetup(commonBaseType, droppedObjects));
            }

            SortCollectionSetupsByCoverage(sharedTypes);
            return sharedTypes;
        }

        /// <summary>
        /// Counts the given type in the frequency map.
        /// </summary>
        /// <param name="typeFrequency">The frequency map to update.</param>
        /// <param name="type">The type whose count is incremented.</param>
        private static void CountType(Dictionary<Type, int> typeFrequency, Type type)
        {
            typeFrequency.TryGetValue(type, out int count);
            typeFrequency[type] = count + 1;
        }

        /// <summary>
        /// Counts the component types of the given object when it is a GameObject, so components that
        /// are shared across all dropped objects (such as Transform) can be offered as element types.
        /// </summary>
        /// <param name="typeFrequency">The frequency map to update.</param>
        /// <param name="obj">The dropped object whose components are counted.</param>
        private static void CountComponentTypes(Dictionary<Type, int> typeFrequency, UnityEngine.Object obj)
        {
            GameObject gameObject = obj as GameObject;

            if (gameObject == null)
            {
                return;
            }

            Component[] components = gameObject.GetComponents<Component>();

            for (int i = 0; i < components.Length; i++)
            {
                Component component = components[i];
                if (component != null)
                {
                    CountType(typeFrequency, component.GetType());
                }
            }
        }

        /// <summary>
        /// Creates a collection setup for the given element type and assigned objects.
        /// </summary>
        /// <param name="elementType">The element type of the collection.</param>
        /// <param name="assignedObjects">The objects assignable to the element type.</param>
        /// <returns>The created collection setup.</returns>
        private static CollectionSetup CreateCollectionSetup(Type elementType, UnityEngine.Object[] assignedObjects)
        {
            return new CollectionSetup
            {
                elementType = elementType,
                assignedObjects = assignedObjects
            };
        }

        /// <summary>
        /// Sorts the shared types so the most representative one is shown first: types covering the
        /// most objects first, then the objects' own types before their component types (e.g. GameObject
        /// before Transform), then by type name for a stable order.
        /// </summary>
        /// <param name="sharedTypes">The shared types to sort.</param>
        private static void SortCollectionSetupsByCoverage(List<CollectionSetup> sharedTypes)
        {
            sharedTypes.Sort((a, b) =>
            {
                int coverageComparison = b.assignedObjects.Length.CompareTo(a.assignedObjects.Length);

                if (coverageComparison != 0)
                {
                    return coverageComparison;
                }

                int originComparison = IsComponentType(a.elementType).CompareTo(IsComponentType(b.elementType));

                if (originComparison != 0)
                {
                    return originComparison;
                }

                return string.Compare(a.elementType.Name, b.elementType.Name, StringComparison.Ordinal);
            });
        }

        /// <summary>
        /// Returns whether the given type derives from Component, distinguishing the dropped objects'
        /// own types from the component types discovered on GameObjects.
        /// </summary>
        /// <param name="type">The type to classify.</param>
        /// <returns>True when the type derives from Component, false otherwise.</returns>
        private static bool IsComponentType(Type type)
        {
            return typeof(Component).IsAssignableFrom(type);
        }

        /// <summary>
        /// Finds the closest type that all dropped objects share, walking up from the first object's
        /// type. Returns UnityEngine.Object as the ultimate fallback.
        /// </summary>
        /// <param name="droppedObjects">The dropped objects to analyse.</param>
        /// <returns>The closest common base type of all dropped objects.</returns>
        private static Type FindClosestCommonBaseType(UnityEngine.Object[] droppedObjects)
        {
            Type candidate = droppedObjects[0].GetType();

            while (candidate != null)
            {
                if (IsAssignableToAll(candidate, droppedObjects))
                {
                    return candidate;
                }

                candidate = candidate.BaseType;
            }

            return typeof(UnityEngine.Object);
        }

        /// <summary>
        /// Returns whether the candidate type is assignable from every dropped object's type.
        /// </summary>
        /// <param name="candidate">The candidate base type.</param>
        /// <param name="droppedObjects">The dropped objects to test against.</param>
        /// <returns>True when every dropped object is assignable to the candidate type, false otherwise.</returns>
        private static bool IsAssignableToAll(Type candidate, UnityEngine.Object[] droppedObjects)
        {
            for (int i = 0; i < droppedObjects.Length; i++)
            {
                if (!candidate.IsAssignableFrom(droppedObjects[i].GetType()))
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Collects the dropped objects that are assignable to the given element type. GameObjects
        /// contribute their matching component when the element type is a Component type, so a shared
        /// component such as Transform can collect the matching component of every GameObject.
        /// </summary>
        /// <param name="droppedObjects">The dropped objects to filter.</param>
        /// <param name="elementType">The element type to filter by.</param>
        /// <returns>The objects assignable to the element type.</returns>
        private static UnityEngine.Object[] CollectAssignableObjects(UnityEngine.Object[] droppedObjects, Type elementType)
        {
            List<UnityEngine.Object> objects = new List<UnityEngine.Object>();

            for (int i = 0; i < droppedObjects.Length; i++)
            {
                UnityEngine.Object obj = droppedObjects[i];
                
                if (elementType.IsAssignableFrom(obj.GetType()))
                {
                    objects.Add(obj);
                    continue;
                }

                GameObject gameObject = obj as GameObject;

                if (gameObject != null)
                {
                    Component component = gameObject.GetComponent(elementType);
                    if (component != null)
                    {
                        objects.Add(component);
                    }
                }
            }
            return objects.ToArray();
        }

        /// <summary>
        /// Builds the candidate list for the first dropped object, including the object itself and,
        /// for GameObjects, all of their components.
        /// </summary>
        /// <param name="dropped">The dropped object to build candidates from.</param>
        /// <returns>The list of pickable candidates.</returns>
        private static List<Candidate> BuildCandidates(UnityEngine.Object dropped)
        {
            List<Candidate> candidates = new List<Candidate>();
            candidates.Add(CreateCandidate(dropped, dropped.name, dropped.GetType(), dropped.GetType().Name));

            GameObject gameObject = dropped as GameObject;

            if (gameObject != null)
            {
                AddComponentCandidates(candidates, gameObject);
            }

            return candidates;
        }

        /// <summary>
        /// Adds a candidate for every component of the given GameObject.
        /// </summary>
        /// <param name="candidates">The list to add the component candidates to.</param>
        /// <param name="gameObject">The GameObject whose components are added.</param>
        private static void AddComponentCandidates(List<Candidate> candidates, GameObject gameObject)
        {
            Component[] components = gameObject.GetComponents<Component>();

            for (int i = 0; i < components.Length; i++)
            {
                Component component = components[i];

                if (component == null)
                {
                    continue;
                }

                candidates.Add(CreateComponentCandidate(component, gameObject.name));
            }
        }

        /// <summary>
        /// Creates a candidate for the dropped object itself.
        /// </summary>
        /// <param name="obj">The dropped object.</param>
        /// <param name="name">The object name used in the display name.</param>
        /// <param name="type">The type of the object.</param>
        /// <param name="typeName">The type name used in the display name.</param>
        /// <returns>The created candidate.</returns>
        private static Candidate CreateCandidate(UnityEngine.Object obj, string name, Type type, string typeName)
        {
            return new Candidate
            {
                assignedObjects = new[] { obj },
                displayName = string.Format(DisplayNameFormat, name, typeName),
                type = type,
                iconContent = new GUIContent(EditorGUIUtility.ObjectContent(obj, type).image)
            };
        }

        /// <summary>
        /// Creates a candidate for a component of a dropped GameObject.
        /// </summary>
        /// <param name="component">The component to create the candidate for.</param>
        /// <param name="ownerName">The name of the GameObject owning the component.</param>
        /// <returns>The created candidate.</returns>
        private static Candidate CreateComponentCandidate(Component component, string ownerName)
        {
            return new Candidate
            {
                assignedObjects = new[] { component },
                displayName = string.Format(DisplayNameFormat, component.GetType().Name, ownerName),
                type = component.GetType(),
                iconContent = new GUIContent(EditorGUIUtility.ObjectContent(component, component.GetType()).image)
            };
        }

        /// <summary>
        /// Shows the selection window as a drop-down anchored at the current mouse position.
        /// </summary>
        /// <param name="window">The selection window to show.</param>
        private void ShowWindowAtMouse(SelectionWindow window)
        {
            Vector2 mousePos = GUIUtility.GUIToScreenPoint(Event.current.mousePosition);
            
            window.ShowAsDropDown(
                new Rect(mousePos.x, mousePos.y, ZeroSize, ZeroSize),
                new Vector2((float)SelectionWindowWidth, (float)SelectionWindowHeight));
        }
    }
}