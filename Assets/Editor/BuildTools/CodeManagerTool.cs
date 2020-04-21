using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AssetPipeline;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 用于JSB模式与Mono模式之间的切换，还有在出包前移除或转移该模式下冗余代码，减少出包大小
/// </summary>
public class CodeManagerTool
{
    public const string TempJSBCodeRoot = "Assets/TempJSBCode/Editor";
    public const string TempMonoCodeRoot = "Assets/TempMonoCode/Editor";

    public static readonly string[] JSB_FilterFileOrDirectory =
    {
        "/Scripts",
        //"/Scripts/GameProtocol/app-clientservice/AppProtobuf",
        //"/Scripts/MyTestScripts",
        "/Plugins/uLua",
        "/Plugins/ulua.bundle",
        "/Plugins/IOS/libulua.a",
        "/Plugins/Android/libs/armeabi-v7a/libulua.so",
        "/Plugins/Android/libs/x86/libulua.so",
        "/Plugins/Protobuf-net"
    };

    //jsb模式下需要保留的目录或文件
    public static readonly string[] JSB_FilterFileOrDirectory_Backup =
    {
        "/Scripts/GameProtocol/app-clientservice/javascript"
    };

    public static readonly string[] Mono_FilterFileOrDirectory =
    {
        "/Editor/JSBinding",
        "/Standard Assets/JSBinding",
        "/Scripts/JsbUnitTest",
        "/Plugins/IOS/libMozjsWrap.a",
        "/Plugins/IOS/libjs_static.a",
        "/Plugins/Android/libs/libmozjswrap.so",
        "/Plugins/mozjswrap.bundle",
        "/Plugins/x86/mozjs-31.dll",
        "/Plugins/x86/mozjswrap.dll",
        "/Plugins/x86_64/mozjs-31.dll",
        "/Plugins/x86_64/mozjswrap.dll",
        "/Plugins/x86_64/mozjs-31.lib"
    };

    [MenuItem("JSB/Fix Win Load Dll not found", false, 160)]
    public static void CopyMozjsDllToEditorFolder()
    {
        try
        {
            string dllName = "mozjs-31.dll";
            string origin = "Assets/Plugins/x86/" + dllName;
            string dest = Path.Combine(Path.GetDirectoryName(EditorApplication.applicationPath), dllName);
            File.Copy(origin, dest, true);
            EditorUtility.DisplayDialog("修复", "拷贝" + dllName + "完成", "OK");
        }
        catch (Exception e)
        {
            Debug.LogException(e);
        }
    }

    private static bool EnableJSB
    {
        get
        {
#if ENABLE_JSB
            return true;
#else
            return false;
#endif
        }
    }

    private static void DeleteFile(string filePath)
    {
        bool success = FileUtil.DeleteFileOrDirectory(filePath);
        if (!success)
        {
            Debug.LogError(string.Format("删除文件或目录失败：{0}", filePath));
            return;
        }

        string metaFile = AssetDatabase.GetTextMetaFilePathFromAssetPath(filePath);
        if (!string.IsNullOrEmpty(metaFile))
        {
            FileUtil.DeleteFileOrDirectory(metaFile);
        }
        else
        {
            Debug.LogError("Can find MetaFile: " + filePath);
        }
    }

    private static void MoveFile(string oldPath, string newPath, string tempRoot)
    {
        bool existsFile = File.Exists(oldPath);
        bool existsDir = Directory.Exists(oldPath);
        if (!existsFile && !existsDir)
        {
            Debug.LogError(string.Format("不存在文件或目录：{0}", oldPath));
            return;
        }

        FileHelper.CreateDirectory(Path.GetDirectoryName(newPath));

        string metaFile = AssetDatabase.GetTextMetaFilePathFromAssetPath(oldPath);
        if (!string.IsNullOrEmpty(metaFile))
        {
            string newMetaFile;
            if (metaFile.Contains(tempRoot))
            {
                newMetaFile = Application.dataPath + metaFile.Replace(tempRoot, "");
            }
            else
            {
                newMetaFile = tempRoot + metaFile.Remove(0, "Assets".Length);
            }

            try
            {
                FileUtil.MoveFileOrDirectory(oldPath, newPath);
                FileUtil.MoveFileOrDirectory(metaFile, newMetaFile);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }
        else
        {
            Debug.LogError("Can find MetaFile: " + oldPath);
        }
    }

    private static void CopyFile(string oldPath, string newPath, string tempRoot)
    {
        bool existsFile = File.Exists(oldPath);
        bool existsDir = Directory.Exists(oldPath);
        if (!existsFile && !existsDir)
        {
            Debug.LogError(string.Format("不存在文件或目录：{0}", oldPath));
            return;
        }

        FileHelper.CreateDirectory(Path.GetDirectoryName(newPath));

        string metaFile = AssetDatabase.GetTextMetaFilePathFromAssetPath(oldPath);
        if (!string.IsNullOrEmpty(metaFile))
        {
            string newMetaFile;
            if (metaFile.Contains(tempRoot))
            {
                newMetaFile = Application.dataPath + metaFile.Replace(tempRoot, "");
            }
            else
            {
                newMetaFile = tempRoot + metaFile.Remove(0, "Assets".Length);
            }

            try
            {
                FileUtil.CopyFileOrDirectory(oldPath, newPath);
                FileUtil.CopyFileOrDirectory(metaFile, newMetaFile);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }
        else
        {
            Debug.LogError("Can find MetaFile: " + oldPath);
        }
    }

    #region JSB框架相关

    [MenuItem("JSB/CodeManager/ChangeToMono", true, 1)]
    [MenuItem("JSB/CodeManager/Move UnUsed MonoCode", true, 100)]
    [MenuItem("JSB/CodeManager/Revert UnUsed MonoCode", true, 100)]
    [MenuItem("JSB/CodeManager/Remove UnUsed MonoCode", true, 100)]
    public static bool ValidateJSBOption()
    {
        return EnableJSB;
    }

    [MenuItem("JSB/CodeManager/ChangeToMono", false, 1)]
    public static void ChangeToMono()
    {
        var definesList = GetDefineSymbols();
        definesList.Remove("ENABLE_JSB");
        definesList.Remove("USE_JSZ");

        string defineSymbols = string.Join(";", definesList.ToArray());
#if UNITY_IPHONE
        PlayerSettings.SetScriptingDefineSymbolsForGroup(BuildTargetGroup.iOS, defineSymbols);
#elif UNITY_ANDROID
        PlayerSettings.SetScriptingDefineSymbolsForGroup(BuildTargetGroup.Android, defineSymbols);
#else
        PlayerSettings.SetScriptingDefineSymbolsForGroup(BuildTargetGroup.Standalone, defineSymbols);
#endif
    }

    private static List<string> GetJSBNeedMoveFileList()
    {
        var result = new HashSet<string>();
        //FilesToAddJsType.txt记录的文件路径
        //string addTypeInfoFile = JSAnalyzer.GetTempFileNameFullPath(JsExternalTools.FilesToAddJsType);
        //if (File.Exists(addTypeInfoFile))
        //{
        //    var jsFiles = File.ReadAllLines(addTypeInfoFile);
        //    for (int i = 0; i < jsFiles.Length; i++)
        //    {
        //        string file = jsFiles[i].Trim();
        //        result.Add(file);
        //    }
        //}
        //else
        //{
        //    Debug.LogError(string.Format("不存在文件：{0},中断移动操作", addTypeInfoFile));
        //}

        //指定文件或目录
        for (int i = 0; i < JSB_FilterFileOrDirectory.Length; i++)
        {
            result.Add(JSB_FilterFileOrDirectory[i]);
        }

        return result.ToList();
    }

    private static List<string> GetJSBNeedMoveFileList_Backup()
    {
        var result = new HashSet<string>();

        //指定文件或目录
        for (int i = 0; i < JSB_FilterFileOrDirectory_Backup.Length; i++)
        {
            result.Add(JSB_FilterFileOrDirectory_Backup[i]);
        }

        return result.ToList();
    }

    public static List<string> GetDefineSymbols()
    {
#if UNITY_IPHONE
        string symbolsDefines = PlayerSettings.GetScriptingDefineSymbolsForGroup(BuildTargetGroup.iOS);
#elif UNITY_ANDROID
        string symbolsDefines = PlayerSettings.GetScriptingDefineSymbolsForGroup(BuildTargetGroup.Android);
#else
        string symbolsDefines = PlayerSettings.GetScriptingDefineSymbolsForGroup(BuildTargetGroup.Standalone);
#endif
        return symbolsDefines.Split(';').ToList();
    }

    [MenuItem("JSB/CodeManager/Move UnUsed MonoCode", false, 100)]
    public static void MoveUnUsedMonoCode()
    {
        moveUnUsedMonoCode();
    }

    public static void moveUnUsedMonoCode(bool cmd = false)
    {
        return;

        if (!cmd && !EditorUtility.DisplayDialog("移动代码", "确认是否移动JSB框架不需要的代码", "确认", "取消"))
        {
            return;
        }

        var needMoveFiles = GetJSBNeedMoveFileList();
        for (int i = 0; i < needMoveFiles.Count; i++)
        {
            var file = needMoveFiles[i];
            string oldPath = "Assets" + file;
            string newPath = TempJSBCodeRoot + file;
            MoveFile(oldPath, newPath, TempJSBCodeRoot);
            if (i == 0) //优化mac下显示进度条耗时
            {
                EditorUtility.DisplayProgressBar("移动文件到临时目录中", string.Format(" {0} / {1} ", i, needMoveFiles.Count),
                    i / (float)needMoveFiles.Count);
            }
        }

        var needBackupFiles = GetJSBNeedMoveFileList_Backup();
        for (int i = 0; i < needBackupFiles.Count; i++)
        {
            var file = needBackupFiles[i];
            string oldPath = TempJSBCodeRoot + file;
            string newPath = "Assets" + file;
            CopyFile(oldPath, newPath, TempJSBCodeRoot);
        }


        if(!cmd)
            AssetDatabase.Refresh();
        EditorUtility.ClearProgressBar();
    }

    [MenuItem("JSB/CodeManager/Revert UnUsed MonoCode", false, 100)]
    public static void RevertUnUsedMonoCode()
    {
        if (!EditorUtility.DisplayDialog("恢复代码", "是否还原JSB框架移动过的代码", "确认", "取消"))
        {
            return;
        }

        revertUnUsedMonoCode();
    }

    public static void revertUnUsedMonoCode(bool cmd = false)
    {
        DeleteFile("Assets/Scripts");
        var needBackupFiles = GetJSBNeedMoveFileList_Backup();
        for (int i = 0; i < needBackupFiles.Count; i++)
        {
            var file = needBackupFiles[i];
            string deletePath = "Assets" + file;
            FileUtil.DeleteFileOrDirectory(deletePath);
        }

        var needMoveFiles = GetJSBNeedMoveFileList();
        for (int i = 0; i < needMoveFiles.Count; i++)
        {
            var file = needMoveFiles[i];
            string oldPath = TempJSBCodeRoot + file;
            string newPath = "Assets" + file;
            MoveFile(oldPath, newPath, TempJSBCodeRoot);
            if (i == 0) //优化mac下显示进度条耗时
            {
                EditorUtility.DisplayProgressBar("移回所有文件到原本目录中", string.Format(" {0} / {1} ", i, needMoveFiles.Count),
                    i / (float)needMoveFiles.Count);
            }
        }
        AssetDatabase.Refresh();
        DeleteFile(TempJSBCodeRoot.Replace("/Editor", ""));
        EditorUtility.ClearProgressBar();
    }

    [MenuItem("JSB/CodeManager/Remove UnUsed MonoCode", false, 100)]
    public static void RemoveUnUsedMonoCode()
    {
        if (!EditorUtility.DisplayDialog("移除代码", "确认是否移除JSB框架不需要的代码", "确认", "取消"))
        {
            return;
        }

        var needRemoveFiles = GetJSBNeedMoveFileList();
        for (int i = 0; i < needRemoveFiles.Count; i++)
        {
            var file = needRemoveFiles[i];
            string oldPath = "Assets" + file;
            DeleteFile(oldPath);
            if (i == 0) //优化mac下显示进度条耗时
            {
                EditorUtility.DisplayProgressBar("删除文件中", string.Format(" {0} / {1} ", i, needRemoveFiles.Count),
                i / (float)needRemoveFiles.Count);
            }
        }

        AssetDatabase.Refresh();
        EditorUtility.ClearProgressBar();
    }

    #endregion

    #region Mono原生框架相关

    [MenuItem("JSB/CodeManager/ChangeToJSB", true, 1)]
//    [MenuItem("JSB/CodeManager/Move JSBFramework", true, 200)]
//    [MenuItem("JSB/CodeManager/Revert JSBFramework", true, 200)]
//    [MenuItem("JSB/CodeManager/Remove JSBFramework", true, 200)]
    public static bool ValidateMonoOption()
    {
        return !EnableJSB;
    }

    [MenuItem("JSB/CodeManager/ChangeToJSB", false, 1)]
    public static void ChangeToJSB()
    {
        var definesList = GetDefineSymbols();
        if (!definesList.Contains("ENABLE_JSB"))
        {
            definesList.Add("ENABLE_JSB");
        }

        if (!definesList.Contains("USE_JSZ"))
        {
            definesList.Add("USE_JSZ");
        }

        string defineSymbols = string.Join(";", definesList.ToArray());
#if UNITY_IPHONE
        PlayerSettings.SetScriptingDefineSymbolsForGroup(BuildTargetGroup.iOS, defineSymbols);
#elif UNITY_ANDROID
        PlayerSettings.SetScriptingDefineSymbolsForGroup(BuildTargetGroup.Android, defineSymbols);
#else
        PlayerSettings.SetScriptingDefineSymbolsForGroup(BuildTargetGroup.Standalone, defineSymbols);
#endif
    }

    private static List<string> GetMonoNeedMoveFileList()
    {
        var result = new HashSet<string>();
        //指定文件或目录
        for (int i = 0; i < Mono_FilterFileOrDirectory.Length; i++)
        {
            result.Add(Mono_FilterFileOrDirectory[i]);
        }

        return result.ToList();
    }

//    [MenuItem("JSB/CodeManager/Move JSBFramework", false, 200)]
    public static void MoveJSBFramework()
    {
        moveJSBFramework();
    }

    public static void moveJSBFramework(bool cmd = false)
    {
        // 多余，不需要，Android没必要移动，文件并不多
        return;

        if (!cmd && !EditorUtility.DisplayDialog("移动代码", "确认是否移动Mono框架不需要的代码", "确认", "取消"))
        {
            return;
        }

        var needMoveFiles = GetMonoNeedMoveFileList();
        for (int i = 0; i < needMoveFiles.Count; i++)
        {
            var file = needMoveFiles[i];
            string oldPath = "Assets" + file;
            string newPath = TempMonoCodeRoot + file;
            MoveFile(oldPath, newPath, TempMonoCodeRoot);
            EditorUtility.DisplayProgressBar("移动文件到临时目录中", string.Format(" {0} / {1} ", i, needMoveFiles.Count),
                i / (float)needMoveFiles.Count);
        }
        if(!cmd)
            AssetDatabase.Refresh();
        EditorUtility.ClearProgressBar();
    }

//    [MenuItem("JSB/CodeManager/Revert JSBFramework", false, 200)]
    public static void RevertJSBFramework()
    {
        if (!EditorUtility.DisplayDialog("恢复代码", "是否还原Mono框架移动过的代码", "确认", "取消"))
        {
            return;
        }
        var needMoveFiles = GetMonoNeedMoveFileList();
        for (int i = 0; i < needMoveFiles.Count; i++)
        {
            var file = needMoveFiles[i];
            string oldPath = TempMonoCodeRoot + file;
            string newPath = "Assets" + file;
            MoveFile(oldPath, newPath, TempMonoCodeRoot);
            EditorUtility.DisplayProgressBar("移回所有文件到原本目录中", string.Format(" {0} / {1} ", i, needMoveFiles.Count),
                i / (float)needMoveFiles.Count);
        }
        AssetDatabase.Refresh();
        DeleteFile(TempMonoCodeRoot.Replace("/Editor", ""));
        EditorUtility.ClearProgressBar();
    }

//    [MenuItem("JSB/CodeManager/Remove JSBFramework", false, 200)]
    public static void RemoveJSBFramework()
    {
        if (!EditorUtility.DisplayDialog("移除代码", "确认是否移除Mono框架不需要的代码", "确认", "取消"))
        {
            return;
        }

        var needRemoveFiles = GetMonoNeedMoveFileList();
        for (int i = 0; i < needRemoveFiles.Count; i++)
        {
            var file = needRemoveFiles[i];
            string oldPath = "Assets" + file;
            DeleteFile(oldPath);
            EditorUtility.DisplayProgressBar("删除文件中", string.Format(" {0} / {1} ", i, needRemoveFiles.Count),
                i / (float)needRemoveFiles.Count);
        }

        AssetDatabase.Refresh();
        EditorUtility.ClearProgressBar();
    }

    #endregion
}