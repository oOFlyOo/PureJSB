using System.Collections.Generic;
using System.IO;
using System.Linq;
using CustomUnityCompiler;
using UnityEditor;
using UnityEngine;


public static class CustomScriptCompiler
{
    /// <summary>
    /// 参考 CodeManagerTool.JSB_FilterFileOrDirectory
    /// </summary>
    private static HashSet<string> FilterFileOrDirectory = new HashSet<string>()
    {
        "Assets/Plugins/uLua/",
        "Assets/Scripts/",
    };

    [InitializeOnLoadMethod]
    private static void InitializeOnLoadMethod()
    {
#if ENABLE_JSB
        CustomCompilerHacker.UpdateMonoIsland = Hack;
#else
        CustomCompilerHacker.UpdateMonoIsland = null;
#endif
    }

    private static void Hack(CustomCompilerHacker.CustomMonoIsland island)
    {
        if (island._editor)
        {
            return;
        }

        var outputName = Path.GetFileNameWithoutExtension(island._output);
        if (outputName != "Assembly-CSharp" && outputName != "Assembly-CSharp-firstpass")
        {
            return;
        }

        var files = island._files.ToList();
        files.RemoveAll(path => FilterFileOrDirectory.Any(path.StartsWith));
        if (files.Count == 0)
        {
            Debug.LogError(outputName + " 不支持编译文件数量为空！");
        }
        island._files = files.ToArray();
    }
}
