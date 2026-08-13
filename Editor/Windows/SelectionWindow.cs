using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using Thisaislan.AutoFieldInjectorEditor.Editor.Data;
using Thisaislan.AutoFieldInjectorEditor.Editor.Utilities;
using Thisaislan.AutoFieldInjectorEditor.Editor.Inspectors;

namespace Thisaislan.AutoFieldInjectorEditor.Editor.Windows
{
    /// <summary>
    /// Drop-down window that lets the user pick which object, component or collection of objects
    /// to reference and configure the generated field (name, visibility and container) before
    /// adding it to the pending list.
    /// </summary>
    internal class SelectionWindow : EditorWindow
    {
        /// <summary>The candidates the user can pick from. For a single drop these are references;
        /// for a multi drop these are the shared types a collection can be created from.</summary>
        private List<Candidate> candidates;

        /// <summary>Whether this window collects multiple objects into a List or Array instead of
        /// picking a single reference.</summary>
        private bool isCollectionMode;

        /// <summary>The EntityId of the target MonoBehaviour the generated field belongs to.</summary>
        private EntityId targetId;

        /// <summary>The main inspector that opened this window, used to repaint it after a change.</summary>
        private AutoFieldInjectorInspector parentEditor;

        /// <summary>The candidate currently highlighted in the list.</summary>
        private Candidate selectedCandidate;

        /// <summary>The selected container kind for the collection (List or Array).</summary>
        private CollectionKind collectionKind = CollectionKind.List;

        /// <summary>The name of the field to generate.</summary>
        private string fieldName = string.Empty;

        /// <summary>Whether the generated field should be public instead of serialized private.</summary>
        private bool isPublic;

        /// <summary>The scroll position of the candidate list.</summary>
        private Vector2 scrollPos;

        /// <summary>Label of the candidate selection section.</summary>
        private const string SelectReferenceLabel = "Select the reference to add:";

        /// <summary>Label of the shared type selection section in the collection window.</summary>
        private const string SelectSharedTypeLabel = "Pick one shared type, then choose a List or Array:";

        /// <summary>Message shown when the selection window has no candidates.</summary>
        private const string NoCandidatesMessage = "No valid candidates.";

        /// <summary>Message shown when the dropped objects share no common type.</summary>
        private const string NoCommonTypeMessage =
            "These objects don't share a common type, so no collection can be created. You can cancel and drop a single object to create a single reference.";

        /// <summary>Label of the field name input.</summary>
        private const string FieldNameLabel = "Field Name:";

        /// <summary>Message shown when the entered field name is not a valid C# identifier.</summary>
        private const string InvalidIdentifierMessage =
            "Invalid identifier. Use letters, digits, underscore, start with letter or underscore.";

        /// <summary>Message shown when a pending field with the same name already exists. Format: field name.</summary>
        private const string DuplicateFieldMessage = "A field named '{0}' is already pending.";

        /// <summary>Message shown when a field name already exists as a member in the target script. Format: field name.</summary>
        private const string DuplicateMemberMessage = "A member named '{0}' already exists in the script.";

        /// <summary>Label of the visibility popup.</summary>
        private const string VisibilityLabel = "Visibility";

        /// <summary>Label of the container type popup in the selection window.</summary>
        private const string ContainerTypeLabel = "Container Type";

        /// <summary>The text of the add field button.</summary>
        private const string AddFieldButton = "Add Field";

        /// <summary>The text of the cancel button.</summary>
        private const string CancelButton = "Cancel";

        /// <summary>Preview text template of the field that will be added. Format: declaration.</summary>
        private const string WillAddPreview = "Will add:\n{0}";

        /// <summary>Preview text of a collection to create. Format: count, element type.</summary>
        private const string CollectionPreviewFormat = "Will reference {0} × {1}";

        /// <summary>The visibility options shown in the popup.</summary>
        private static readonly string[] VisibilityOptions = { "SerializeField (private)", "public" };

        /// <summary>The container options offered for collections. Index 0 is the default (List).</summary>
        private static readonly string[] ContainerOptions = { "List", "Array" };

        /// <summary>The index of the List option in <see cref="ContainerOptions"/>.</summary>
        private const int ListContainerIndex = 0;

        /// <summary>The index of the Array option in <see cref="ContainerOptions"/>.</summary>
        private const int ArrayContainerIndex = 1;

        /// <summary>The height of the candidate list inside the selection window in pixels.</summary>
        private const int CandidateListHeight = 150;

        /// <summary>The size of the candidate icons in pixels.</summary>
        private const int IconSize = 20;

        /// <summary>The popup index of the public visibility option.</summary>
        private const int PublicVisibilityIndex = 1;

        /// <summary>The popup index of the serialized private visibility option.</summary>
        private const int PrivateVisibilityIndex = 0;

        /// <summary>The fallback color of the window background when the drop zone style has no texture.</summary>
        private static readonly Color WindowBackgroundFallbackColor = new Color(0.13f, 0.13f, 0.13f, 1f);

        /// <summary>The texture used to paint the window background, created once on first use.</summary>
        private static Texture2D windowBackgroundTexture;

        /// <summary>
        /// Initializes the window with the candidates to pick from, the collection mode flag and
        /// the target context.
        /// </summary>
        /// <param name="candidates">The references (or shared types) the user can pick from.</param>
        /// <param name="isCollectionMode">Whether the window creates a List or Array collection.</param>
        /// <param name="targetId">The EntityId of the target MonoBehaviour.</param>
        /// <param name="parent">The main inspector that opened this window.</param>
        internal void Initialize(
            List<Candidate> candidates,
            bool isCollectionMode,
            EntityId targetId,
            AutoFieldInjectorInspector parent)
        {
            this.candidates = candidates;
            this.isCollectionMode = isCollectionMode;
            this.targetId = targetId;
            parentEditor = parent;
            selectedCandidate = (candidates != null && candidates.Count > 0) ? candidates[0] : null;
            UpdateNameSuggestion();
        }

        /// <summary>
        /// Recomputes the suggested field name from the current selection (single candidate or
        /// collection of the selected candidate's type and container kind).
        /// </summary>
        private void UpdateNameSuggestion()
        {
            if (selectedCandidate == null)
            {
                fieldName = string.Empty;
                return;
            }

            if (isCollectionMode)
            {
                fieldName = FieldNameSuggester.SuggestCollectionFieldName(selectedCandidate.type, collectionKind);
                return;
            }

            fieldName = FieldNameSuggester.SuggestSingleFieldName(selectedCandidate);
        }

        /// <summary>
        /// Draws the whole window every editor frame.
        /// </summary>
        private void OnGUI()
        {
            DrawWindowBackground();

            if (candidates == null || candidates.Count == 0)
            {
                DrawNoCandidates();
                return;
            }

            DrawSelection();
            DrawCollectionOptions();
            DrawFieldNameInput();
            DrawVisibilitySelector();
            DrawActionButtons();
            DrawPreview();
            HandleEscapeKey();
        }

        /// <summary>
        /// Draws the window background using the drop zone's box style, so the window matches the rest
        /// of the inspector, before any content is laid out.
        /// </summary>
        private void DrawWindowBackground()
        {
            Rect backgroundRect = new Rect(0f, 0f, position.width, position.height);
            GUI.DrawTexture(backgroundRect, GetWindowBackgroundTexture(), ScaleMode.StretchToFill);
        }

        /// <summary>
        /// Returns the texture used to paint the window background, reusing the drop zone box texture
        /// so the window matches it, and falling back to a solid dark color when the style has no
        /// texture.
        /// </summary>
        /// <returns>The cached window background texture.</returns>
        private static Texture2D GetWindowBackgroundTexture()
        {
            if (windowBackgroundTexture == null)
            {
                windowBackgroundTexture = BuildWindowBackgroundTexture();
            }
            return windowBackgroundTexture;
        }

        /// <summary>
        /// Builds the window background texture from the drop zone box style, falling back to a solid
        /// dark color.
        /// </summary>
        /// <returns>The window background texture.</returns>
        private static Texture2D BuildWindowBackgroundTexture()
        {
            Texture2D dropZoneTexture = GUI.skin.box.normal.background;

            if (dropZoneTexture != null)
            {
                return dropZoneTexture;
            }

            Texture2D texture = new Texture2D(1, 1);
            texture.SetPixel(0, 0, WindowBackgroundFallbackColor);
            texture.Apply();
            return texture;
        }

        /// <summary>
        /// Draws a message explaining that there is nothing to pick from, and a cancel button.
        /// </summary>
        private void DrawNoCandidates()
        {
            string message = isCollectionMode ? NoCommonTypeMessage : NoCandidatesMessage;
            EditorGUILayout.HelpBox(message, MessageType.Info);

            if (GUILayout.Button(CancelButton))
            {
                Close();
            }
        }

        /// <summary>
        /// Draws the pickable list: shared types when creating a collection, or references for a
        /// single drop.
        /// </summary>
        private void DrawSelection()
        {
            if (isCollectionMode)
            {
                DrawSharedTypeSelection();
            }
            else
            {
                DrawReferenceSelection();
            }
        }

        /// <summary>
        /// Draws the scrollable list of shared types the user can pick as the collection element
        /// type, each showing how many of the dropped objects it covers.
        /// </summary>
        private void DrawSharedTypeSelection()
        {
            EditorGUILayout.LabelField(SelectSharedTypeLabel, EditorStyles.boldLabel);
            scrollPos = EditorGUILayout.BeginScrollView(scrollPos, GUILayout.Height((float)CandidateListHeight));

            for (int i = 0; i < candidates.Count; i++)
            {
                DrawCandidateRow(candidates[i]);
            }

            EditorGUILayout.EndScrollView();
        }

        /// <summary>
        /// Draws the container kind dropdown (List or Array) when creating a collection, together
        /// with a preview of how many objects will be referenced.
        /// </summary>
        private void DrawCollectionOptions()
        {
            if (!isCollectionMode || selectedCandidate == null)
            {
                return;
            }

            EditorGUILayout.Space();

            int containerIndex = (collectionKind == CollectionKind.Array) ? ArrayContainerIndex : ListContainerIndex;
            EditorGUILayout.LabelField(ContainerTypeLabel, EditorStyles.boldLabel);

            int newIndex = EditorGUILayout.Popup(containerIndex, ContainerOptions);
            CollectionKind newKind = (newIndex == ArrayContainerIndex) ? CollectionKind.Array : CollectionKind.List;

            if (newKind != collectionKind)
            {
                collectionKind = newKind;
                UpdateNameSuggestion();
            }

            EditorGUILayout.HelpBox(
                string.Format(CollectionPreviewFormat, selectedCandidate.assignedObjects.Length, selectedCandidate.type.Name),
                MessageType.Info);
        }

        /// <summary>
        /// Draws the scrollable list of candidates the user can select for a single reference.
        /// </summary>
        private void DrawReferenceSelection()
        {
            EditorGUILayout.LabelField(SelectReferenceLabel, EditorStyles.boldLabel);
            scrollPos = EditorGUILayout.BeginScrollView(scrollPos, GUILayout.Height((float)CandidateListHeight));

            for (int i = 0; i < candidates.Count; i++)
            {
                DrawCandidateRow(candidates[i]);
            }

            EditorGUILayout.EndScrollView();
        }

        /// <summary>
        /// Draws a single candidate row with its icon and selection button.
        /// </summary>
        /// <param name="candidate">The candidate to draw.</param>
        private void DrawCandidateRow(Candidate candidate)
        {
            bool isSelected = (candidate == selectedCandidate);

            EditorGUILayout.BeginHorizontal();
            DrawCandidateIcon(candidate);

            if (GUILayout.Button(candidate.displayName, isSelected ? EditorStyles.boldLabel : EditorStyles.label))
            {
                selectedCandidate = candidate;
                UpdateNameSuggestion();
            }

            EditorGUILayout.EndHorizontal();
        }

        /// <summary>
        /// Draws the icon of a candidate inside the candidate row.
        /// </summary>
        /// <param name="candidate">The candidate whose icon should be drawn.</param>
        private void DrawCandidateIcon(Candidate candidate)
        {
            GUILayout.Label(candidate.iconContent ?? GUIContent.none, GUILayout.Width((float)IconSize), GUILayout.Height((float)IconSize));
        }

        /// <summary>
        /// Draws the field name text field and validates the entered identifier.
        /// </summary>
        private void DrawFieldNameInput()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField(FieldNameLabel, EditorStyles.boldLabel);
            fieldName = EditorGUILayout.TextField(fieldName);

            string fieldNameError = GetFieldNameError();

            if (fieldNameError != null)
            {
                EditorGUILayout.HelpBox(fieldNameError, MessageType.Error);
            }
        }

        /// <summary>
        /// Returns the reason the current field name cannot be used, or null when it is valid.
        /// </summary>
        /// <returns>The field name error, or null.</returns>
        private string GetFieldNameError()
        {
            if (string.IsNullOrEmpty(fieldName))
            {
                return null;
            }

            if (!FieldNameSuggester.IsValidIdentifier(fieldName))
            {
                return InvalidIdentifierMessage;
            }

            if (IsDuplicateFieldName())
            {
                return string.Format(DuplicateFieldMessage, fieldName);
            }

            if (parentEditor != null && parentEditor.TargetHasMemberName(fieldName))
            {
                return string.Format(DuplicateMemberMessage, fieldName);
            }

            return null;
        }

        /// <summary>
        /// Returns whether the current field name can be used to create a new field.
        /// </summary>
        /// <returns>True when the field name is valid, false otherwise.</returns>
        private bool IsFieldNameValid()
        {
            return !string.IsNullOrEmpty(fieldName) && GetFieldNameError() == null;
        }

        /// <summary>
        /// Draws the visibility popup (serialized private or public).
        /// </summary>
        private void DrawVisibilitySelector()
        {
            EditorGUILayout.Space();
            int visibilityIndex = isPublic ? PublicVisibilityIndex : PrivateVisibilityIndex;
            visibilityIndex = EditorGUILayout.Popup(VisibilityLabel, visibilityIndex, VisibilityOptions);
            isPublic = (visibilityIndex == PublicVisibilityIndex);
        }

        /// <summary>
        /// Draws the Add Field and Cancel buttons.
        /// </summary>
        private void DrawActionButtons()
        {
            EditorGUILayout.Space();
            EditorGUILayout.BeginHorizontal();
            GUI.enabled = IsFieldNameValid() && HasSelection();

            if (GUILayout.Button(AddFieldButton))
            {
                AddPendingField();
                Close();
            }

            GUI.enabled = true;

            if (GUILayout.Button(CancelButton))
            {
                Close();
            }

            EditorGUILayout.EndHorizontal();
        }

        /// <summary>
        /// Returns whether the user has made a valid selection (single reference or shared type).
        /// </summary>
        /// <returns>True when a selection exists, false otherwise.</returns>
        private bool HasSelection()
        {
            return selectedCandidate != null;
        }

        /// <summary>
        /// Draws a preview of the field that will be generated.
        /// </summary>
        private void DrawPreview()
        {
            if (!IsFieldNameValid() || !HasSelection())
            {
                return;
            }

            string preview = BuildPreviewText();
            EditorGUILayout.HelpBox(string.Format(WillAddPreview, preview), MessageType.None);
        }

        /// <summary>
        /// Builds the text of the field declaration shown in the preview.
        /// </summary>
        /// <returns>The preview field declaration.</returns>
        private string BuildPreviewText()
        {
            if (isCollectionMode)
            {
                return FieldDeclarationTextBuilder.BuildCollectionDeclarationText(selectedCandidate.type, fieldName, isPublic, collectionKind);
            }

            return FieldDeclarationTextBuilder.BuildSingleDeclarationText(selectedCandidate.type.Name, fieldName, isPublic);
        }

        /// <summary>
        /// Closes the window when the Escape key is pressed.
        /// </summary>
        private void HandleEscapeKey()
        {
            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape)
            {
                Close();
            }
        }

        /// <summary>
        /// Closes the window when it loses focus.
        /// </summary>
        private void OnLostFocus()
        {
            Close();
        }

        /// <summary>
        /// Adds the current selection as a pending field.
        /// </summary>
        private void AddPendingField()
        {
            if (!HasSelection() || !IsFieldNameValid())
            {
                return;
            }

            PendingField pendingField = CreatePendingField();
            PendingFieldStore.GetOrCreatePendingList(targetId).Add(pendingField);

            if (parentEditor != null)
            {
                parentEditor.Repaint();
            }
        }

        /// <summary>
        /// Creates a pending field from the current window selection (single reference or
        /// collection of the selected shared type).
        /// </summary>
        /// <returns>The created pending field.</returns>
        private PendingField CreatePendingField()
        {
            if (isCollectionMode)
            {
                return new PendingField
                {
                    fieldType = selectedCandidate.type,
                    fieldName = fieldName,
                    isPublic = isPublic,
                    kind = collectionKind,
                    assignedObjects = selectedCandidate.assignedObjects
                };
            }

            return new PendingField
            {
                fieldType = selectedCandidate.type,
                fieldName = fieldName,
                isPublic = isPublic,
                kind = CollectionKind.Single,
                assignedObjects = selectedCandidate.assignedObjects
            };
        }

        /// <summary>
        /// Returns whether a pending field with the current field name already exists.
        /// </summary>
        /// <returns>True when the name is already pending, false otherwise.</returns>
        private bool IsDuplicateFieldName()
        {
            List<PendingField> list = PendingFieldStore.GetOrCreatePendingList(targetId);

            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].fieldName == fieldName)
                {
                    return true;
                }
            }
            return false;
        }
    }
}