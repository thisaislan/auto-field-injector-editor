# Changelog

#### v0.1.0:
 - Drag-and-drop injection : Drag any element or listo of elements onto a custom script's Inspector header to create fields
 - Smart component picker : When dropping a GameObject, a window shows the GameObject and all its components with icons for easy selection
 - Auto-generated field names : Intelligent naming based on object name and type
 - Visibility options : Choose between [SerializeField] private (default) or public fields
 - Automatic using directives : Detects and adds missing namespaces for dropped types
 - Code formatting : Proper indentation, placement after existing serialized fields, and normalised newlines
 - Pending changes list : Add multiple fields at once with a clear UI showing pending additions
 - Survives recompilation : Assignments persist through Unity's domain reload using EditorPrefs
 - Clean UI : Compact drop zone, clear pending list, and windows that close on ESC or click-outside
