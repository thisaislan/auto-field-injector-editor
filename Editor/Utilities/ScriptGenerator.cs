using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace Thisaislan.AutoFieldInjectorEditor.Utilities
{
    /// <summary>
    /// Pure string and code-generation helpers that build field declarations and edit the text of
    /// a script file. Kept free of Editor and UnityEngine state so it can be unit-tested in isolation.
    /// </summary>
    internal static class ScriptGenerator
    {
        /// <summary>The single newline character used in generated code.</summary>
        private const string NewLine = "\n";

        /// <summary>A double newline, the maximum allowed gap between members.</summary>
        private const string DoubleNewLine = "\n\n";

        /// <summary>The tab character.</summary>
        private const string Tab = "\t";

        /// <summary>The prefix of a using directive.</summary>
        private const string UsingPrefix = "using ";

        /// <summary>The trailing semicolon of a declaration.</summary>
        private const string Semicolon = ";";

        /// <summary>The default indentation used when no existing indentation can be detected.</summary>
        internal const string DefaultIndent = "    ";

        /// <summary>The namespace required by generic collection types such as List.</summary>
        internal const string GenericCollectionsNamespace = "System.Collections.Generic";

        /// <summary>The pattern used to find existing using directives.</summary>
        internal const string UsingPattern = @"using\s+([\w\.]+)\s*;";

        /// <summary>The template of a regex matching a class declaration. Format: escaped class name.</summary>
        private const string ClassRegexFormat = @"class\s+{0}\s*:?\s*\w*\s*{{";

        /// <summary>The pattern used to detect the indentation of an existing field.</summary>
        private const string FieldIndentRegexPattern = @"\n(\s+)(public|private|protected|internal)?\s*(static\s+)?\s*\w+\s+\w+\s*;";

        /// <summary>The pattern used to find the last serialized field.</summary>
        private const string SerializeFieldRegexPattern = @"\[SerializeField\]\s*\n\s*(private|public)?\s*\w+\s+\w+\s*;";

        /// <summary>The pattern used to find the last public field.</summary>
        private const string PublicFieldRegexPattern = @"\n\s*public\s+\w+\s+\w+\s*;";

        /// <summary>The pattern used to find the last static, const or enum declaration.</summary>
        private const string StaticDeclarationRegexPattern = @"\n\s*(public|private|protected)?\s*(static|const|enum)\s+";

        /// <summary>The pattern used to collapse runs of newlines.</summary>
        private const string MultipleNewlinesPattern = @"\n{3,}";

        /// <summary>The pattern used to detect the indentation of a line.</summary>
        private const string IndentationRegexPattern = @"^\s+";

        /// <summary>The pattern used to extract every identifier-like word from the class body.</summary>
        private const string WordPattern = @"\w+";

        /// <summary>Regex that finds all using directives in a script.</summary>
        private static readonly Regex UsingRegex = new Regex(UsingPattern, RegexOptions.Compiled);

        /// <summary>Regex that finds the last using directive in a script.</summary>
        private static readonly Regex UsingRightToLeftRegex = new Regex(UsingPattern, RegexOptions.Compiled | RegexOptions.RightToLeft);

        /// <summary>Regex that detects the indentation of an existing field declaration.</summary>
        private static readonly Regex FieldIndentRegex = new Regex(FieldIndentRegexPattern, RegexOptions.Compiled);

        /// <summary>Regex that finds the last serialized field in a class body.</summary>
        private static readonly Regex SerializeFieldRegex = new Regex(SerializeFieldRegexPattern, RegexOptions.Compiled | RegexOptions.RightToLeft);

        /// <summary>Regex that finds the last public field in a class body.</summary>
        private static readonly Regex PublicFieldRegex = new Regex(PublicFieldRegexPattern, RegexOptions.Compiled | RegexOptions.RightToLeft);

        /// <summary>Regex that finds the last static, const or enum declaration in a class body.</summary>
        private static readonly Regex StaticDeclarationRegex = new Regex(StaticDeclarationRegexPattern, RegexOptions.Compiled | RegexOptions.RightToLeft);

        /// <summary>Regex that collapses three or more newlines into two.</summary>
        private static readonly Regex MultipleNewlinesRegex = new Regex(MultipleNewlinesPattern, RegexOptions.Compiled);

        /// <summary>Regex that captures the leading whitespace of a line.</summary>
        private static readonly Regex IndentationRegex = new Regex(IndentationRegexPattern, RegexOptions.Compiled);

        /// <summary>Regex that extracts every identifier-like word from the class body.</summary>
        private static readonly Regex WordRegex = new Regex(WordPattern, RegexOptions.Compiled);

        /// <summary>The namespaces assumed to already be imported by every Unity script.</summary>
        private static readonly HashSet<string> CommonNamespaceSet = new HashSet<string>
        {
            "System",
            "UnityEngine",
            "System.Collections",
            "System.Collections.Generic"
        };

        /// <summary>
        /// Returns the namespaces of the given types that are not already imported by the script and
        /// are not among the most common Unity namespaces, optionally forcing the generic collection
        /// namespace so generated List fields compile.
        /// </summary>
        /// <param name="scriptContent">The current content of the script.</param>
        /// <param name="fieldTypes">The types the script will reference.</param>
        /// <param name="requireGenericCollections">Whether the generic collection namespace is mandatory.</param>
        /// <returns>The namespaces that still need a using directive.</returns>
        internal static List<string> GetMissingNamespaces(string scriptContent, List<Type> fieldTypes, bool requireGenericCollections)
        {
            HashSet<string> existingUsings = GetExistingUsings(scriptContent);
            HashSet<string> neededNamespaces = GetNeededNamespaces(fieldTypes);

            if (requireGenericCollections)
            {
                neededNamespaces.Add(GenericCollectionsNamespace);
            }

            List<string> missing = new List<string>();
            foreach (string ns in neededNamespaces)
            {
                if (!existingUsings.Contains(ns))
                {
                    missing.Add(ns);
                }
            }

            return missing;
        }

        /// <summary>
        /// Collects the namespaces already imported by the script.
        /// </summary>
        /// <param name="scriptContent">The current content of the script.</param>
        /// <returns>The set of namespaces that already have a using directive.</returns>
        private static HashSet<string> GetExistingUsings(string scriptContent)
        {
            HashSet<string> existingUsings = new HashSet<string>();
            MatchCollection usingMatches = UsingRegex.Matches(scriptContent);

            for (int i = 0; i < usingMatches.Count; i++)
            {
                Match match = usingMatches[i];
                if (match.Groups.Count > 1)
                {
                    existingUsings.Add(match.Groups[1].Value);
                }
            }

            return existingUsings;
        }

        /// <summary>
        /// Collects the namespaces of the given types, ignoring the most common Unity namespaces.
        /// </summary>
        /// <param name="fieldTypes">The types the script will reference.</param>
        /// <returns>The set of namespaces referenced by the types.</returns>
        private static HashSet<string> GetNeededNamespaces(List<Type> fieldTypes)
        {
            HashSet<string> neededNamespaces = new HashSet<string>();

            for (int i = 0; i < fieldTypes.Count; i++)
            {
                Type type = fieldTypes[i];
                string ns = type.Namespace;

                if (string.IsNullOrEmpty(ns) || CommonNamespaceSet.Contains(ns))
                {
                    continue;
                }
                neededNamespaces.Add(ns);
            }
            return neededNamespaces;
        }

        /// <summary>
        /// Inserts using directives for the given namespaces right after the last existing using
        /// directive, or at the start of the file when there is none.
        /// </summary>
        /// <param name="scriptContent">The current content of the script.</param>
        /// <param name="namespaces">The namespaces to import.</param>
        /// <returns>The script content with the new using directives inserted.</returns>
        internal static string InsertUsingDirectives(string scriptContent, List<string> namespaces)
        {
            int insertIndex = FindUsingInsertIndex(scriptContent);
            string usingLines = BuildUsingLines(namespaces);
            return scriptContent.Insert(insertIndex, usingLines + NewLine);
        }

        /// <summary>
        /// Finds the index right after the last using directive, skipping any trailing whitespace.
        /// </summary>
        /// <param name="scriptContent">The current content of the script.</param>
        /// <returns>The index where new using directives should be inserted.</returns>
        private static int FindUsingInsertIndex(string scriptContent)
        {
            Match lastUsing = UsingRightToLeftRegex.Match(scriptContent);

            if (!lastUsing.Success)
            {
                return 0;
            }

            int insertIndex = lastUsing.Index + lastUsing.Length;
            
            while (insertIndex < scriptContent.Length && char.IsWhiteSpace(scriptContent[insertIndex]))
            {
                insertIndex++;
            }
            return insertIndex;
        }

        /// <summary>
        /// Builds the text of the using directives for the given namespaces.
        /// </summary>
        /// <param name="namespaces">The namespaces to import.</param>
        /// <returns>The concatenated using directive lines.</returns>
        private static string BuildUsingLines(List<string> namespaces)
        {
            StringBuilder builder = new StringBuilder();

            for (int i = 0; i < namespaces.Count; i++)
            {
                builder.Append(UsingPrefix).Append(namespaces[i]).Append(Semicolon).Append(NewLine);
            }

            return builder.ToString();
        }

        /// <summary>
        /// Determines the indentation to use for new fields, reusing the indentation of an existing
        /// field or deriving it from the class declaration.
        /// </summary>
        /// <param name="script">The current content of the script.</param>
        /// <param name="className">The name of the class to insert the fields into.</param>
        /// <returns>The indentation string for new fields.</returns>
        internal static string GetFieldIndent(string script, string className)
        {
            Match classMatch = FindClass(script, className);

            if (!classMatch.Success)
            {
                return DefaultIndent;
            }

            string existingFieldIndent = FindExistingFieldIndent(script, classMatch);

            if (existingFieldIndent != null)
            {
                return existingFieldIndent;
            }

            string classIndent = DetectIndentation(script, className);

            return BuildIndentFromClassIndent(classIndent);
        }

        /// <summary>
        /// Looks for an existing field in the class and returns its indentation.
        /// </summary>
        /// <param name="script">The current content of the script.</param>
        /// <param name="classMatch">The match of the class declaration.</param>
        /// <returns>The indentation of an existing field, or null when none is found.</returns>
        private static string FindExistingFieldIndent(string script, Match classMatch)
        {
            int braceIndex = classMatch.Index + classMatch.Length;
            string afterBrace = script.Substring(braceIndex);

            Match fieldMatch = FieldIndentRegex.Match(afterBrace);

            if (fieldMatch.Success)
            {
                return fieldMatch.Groups[1].Value;
            }

            return null;
        }

        /// <summary>
        /// Builds the field indentation from the indentation of the class declaration, matching its
        /// use of tabs or spaces.
        /// </summary>
        /// <param name="classIndent">The indentation of the class declaration, or null.</param>
        /// <returns>The indentation for new fields.</returns>
        private static string BuildIndentFromClassIndent(string classIndent)
        {
            if (string.IsNullOrEmpty(classIndent))
            {
                classIndent = string.Empty;
            }

            if (classIndent.Contains(Tab))
            {
                return classIndent + Tab;
            }

            return classIndent + DefaultIndent;
        }

        /// <summary>
        /// Inserts the given field declarations into the class body at the best position found.
        /// </summary>
        /// <param name="script">The current content of the script.</param>
        /// <param name="className">The name of the class to insert the fields into.</param>
        /// <param name="fieldDeclarations">The field declaration texts to insert.</param>
        /// <returns>The script content with the fields inserted.</returns>
        internal static string InsertFields(string script, string className, List<string> fieldDeclarations)
        {
            Match classMatch = FindClass(script, className);

            if (!classMatch.Success)
            {
                return script;
            }

            int insertIndex = FindInsertionIndex(script, classMatch);
            string insertText = BuildInsertText(fieldDeclarations);
            string newScript = script.Insert(insertIndex, insertText);

            return MultipleNewlinesRegex.Replace(newScript, DoubleNewLine);
        }

        /// <summary>
        /// Finds the best position to insert new fields: after the last serialized field, the last
        /// public field, the last static/const/enum declaration, or right after the opening brace.
        /// </summary>
        /// <param name="script">The current content of the script.</param>
        /// <param name="classMatch">The match of the class declaration.</param>
        /// <returns>The character index where the new fields should be inserted.</returns>
        private static int FindInsertionIndex(string script, Match classMatch)
        {
            string afterBrace = script.Substring(classMatch.Index);

            Match attrMatch = SerializeFieldRegex.Match(afterBrace);

            if (attrMatch.Success)
            {
                return classMatch.Index + attrMatch.Index + attrMatch.Length;
            }

            Match publicMatch = PublicFieldRegex.Match(afterBrace);

            if (publicMatch.Success)
            {
                return classMatch.Index + publicMatch.Index + publicMatch.Length;
            }

            Match staticMatch = StaticDeclarationRegex.Match(afterBrace);

            if (staticMatch.Success)
            {
                return FindEndOfDeclaration(afterBrace, staticMatch, classMatch);
            }

            return classMatch.Index + classMatch.Length;
        }

        /// <summary>
        /// Finds the index after the end of a static, const or enum declaration.
        /// </summary>
        /// <param name="afterBrace">The script content starting at the class declaration.</param>
        /// <param name="staticMatch">The match of the static, const or enum declaration.</param>
        /// <param name="classMatch">The match of the class declaration.</param>
        /// <returns>The index after the declaration, or right after the opening brace when it cannot be found.</returns>
        private static int FindEndOfDeclaration(string afterBrace, Match staticMatch, Match classMatch)
        {
            int declarationStart = staticMatch.Index + staticMatch.Length;
            int endOfLine = afterBrace.IndexOf(Semicolon, declarationStart);

            if (endOfLine == -1)
            {
                endOfLine = afterBrace.IndexOf('{', declarationStart);
            }

            if (endOfLine != -1)
            {
                return classMatch.Index + endOfLine + 1;
            }

            return classMatch.Index + classMatch.Length;
        }

        /// <summary>
        /// Builds the text inserted into the script, opening with a blank line so the first declaration
        /// is separated from the member it follows, and separating every declaration by a blank line.
        /// </summary>
        /// <param name="fieldDeclarations">The field declaration texts to insert.</param>
        /// <returns>The text to insert.</returns>
        private static string BuildInsertText(List<string> fieldDeclarations)
        {
            StringBuilder builder = new StringBuilder(DoubleNewLine);

            for (int i = 0; i < fieldDeclarations.Count; i++)
            {
                if (i > 0)
                {
                    builder.Append(DoubleNewLine);
                }

                builder.Append(fieldDeclarations[i]).Append(NewLine);
            }

            return builder.ToString();
        }

        /// <summary>
        /// Returns every identifier mentioned in the class body together with the class name itself.
        /// Members are matched by word boundary so this also covers fields, properties, methods and
        /// local variables.
        /// </summary>
        /// <param name="script">The current content of the script.</param>
        /// <param name="className">The name of the class to inspect.</param>
        /// <returns>The set of member names declared in the class body.</returns>
        internal static HashSet<string> GetMemberNames(string script, string className)
        {
            HashSet<string> memberNames = new HashSet<string>
            {
                className
            };

            Match classMatch = FindClass(script, className);

            if (!classMatch.Success)
            {
                return memberNames;
            }

            string classBody = ExtractClassBody(script, classMatch);

            if (classBody == null)
            {
                return memberNames;
            }

            MatchCollection wordMatches = WordRegex.Matches(classBody);

            for (int i = 0; i < wordMatches.Count; i++)
            {
                memberNames.Add(wordMatches[i].Value);
            }
            return memberNames;
        }

        /// <summary>
        /// Extracts the text between the class braces by brace-matching.
        /// </summary>
        /// <param name="script">The current content of the script.</param>
        /// <param name="classMatch">The match of the class declaration.</param>
        /// <returns>The class body text, or null when the braces are unbalanced.</returns>
        private static string ExtractClassBody(string script, Match classMatch)
        {
            int openBrace = classMatch.Index + classMatch.Length - 1;
            int depth = 0;

            for (int i = openBrace; i < script.Length; i++)
            {
                char c = script[i];
                if (c == '{')
                {
                    depth++;
                }
                else if (c == '}')
                {
                    depth--;
                    if (depth == 0)
                    {
                        return script.Substring(openBrace + 1, i - openBrace - 1);
                    }
                }
            }
            return null;
        }

        /// <summary>
        /// Finds the class declaration with the given name in the script.
        /// </summary>
        /// <param name="script">The current content of the script.</param>
        /// <param name="className">The name of the class to find.</param>
        /// <returns>The match of the class declaration, or an unsuccessful match when none is found.</returns>
        private static Match FindClass(string script, string className)
        {
            string pattern = string.Format(ClassRegexFormat, Regex.Escape(className));

            return Regex.Match(script, pattern);
        }

        /// <summary>
        /// Detects the indentation of the line that declares the class.
        /// </summary>
        /// <param name="script">The current content of the script.</param>
        /// <param name="className">The name of the class to find.</param>
        /// <returns>The leading whitespace of the class declaration line, or null.</returns>
        private static string DetectIndentation(string script, string className)
        {
            Match match = FindClass(script, className);

            if (!match.Success)
            {
                return null;
            }

            int lineStart = script.LastIndexOf(NewLine, match.Index) + 1;
            string line = script.Substring(lineStart, match.Index - lineStart);
            Match whitespaceMatch = IndentationRegex.Match(line);

            if (whitespaceMatch.Success)
            {
                return whitespaceMatch.Value;
            }
            
            return null;
        }
    }
}
