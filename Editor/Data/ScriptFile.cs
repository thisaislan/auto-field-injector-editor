using System.Text;

namespace Thisaislan.AutoFieldInjectorEditor.Editor.Data
{
    /// <summary>
    /// Holds the content of a script file together with its detected encoding.
    /// </summary>
    internal class ScriptFile
    {
        /// <summary>The full text content of the script file.</summary>
        internal string content;

        /// <summary>The encoding detected when the file was read.</summary>
        internal Encoding encoding;
    }
}