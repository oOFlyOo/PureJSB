#if UNITY_EDITOR_WIN
#define USE_SHELL
#endif
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using cg;
using AssetPipeline;
using LITJson;
using SharpKit.JavaScript;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

public class JsExternalTools
{
    #region JsMinify

    private static readonly HashSet<string> ignoreMinifySet = new HashSet<string>
    {
        //"UnityEngine_MonoBehaviour",
    };

    private static List<string> GetNeedMinifyJsFiles()
    {
        var jsCodeFiles = Directory.GetFiles(JSPathSettings.jsDir, "*" + JSPathSettings.jsExtension,
            SearchOption.AllDirectories);
        var protoJsFiles =
            Directory.GetFiles(Path.Combine(Application.dataPath, "Scripts/GameProtocol/app-clientservice/javascript"),
                "*" + JSPathSettings.jsExtension, SearchOption.AllDirectories);
        var jsFiles = new List<string>();
        for (int i = 0; i < jsCodeFiles.Length; i++)
        {
            string file = jsCodeFiles[i];
            if (FilterJsFile(file))
                continue;
            jsFiles.Add(file);
        }

        for (int i = 0; i < protoJsFiles.Length; i++)
        {
            string file = protoJsFiles[i];
            if (FilterJsFile(file))
                continue;
            jsFiles.Add(file);
        }

        return jsFiles;
    }

    private static bool FilterJsFile(string jsFile)
    {
        string jsFileName = Path.GetFileNameWithoutExtension(jsFile);
        if (jsFileName.Contains(".min"))
            return true;
        return ignoreMinifySet.Contains(jsFileName);
    }

    [MenuItem("JSB/Minify All JsCode", false, 132)]
    public static void MinifyJsCode()
    {
        MinifyJsCode(true);
    }

    public static void MinifyJsCode(bool displayDialog)
    {
        var jsFiles = GetNeedMinifyJsFiles();

        if (displayDialog)
        {
            if (!EditorUtility.DisplayDialog("TIP",
                    "Total: " + jsFiles.Count + "个Js文件进行Minify",
                    "OK",
                    "Cancel"))
            {
                Debug.Log("Operation canceled.");
                return;
            }
        }


        string workingDir = Application.dataPath.Replace("/Assets", "");
#if UNITY_EDITOR_WIN
        string exePath = Path.Combine(workingDir, "JSBExternalTools/closure-compiler/minifyJs.bat");
        string arguments = String.Join(" ", jsFiles.ToArray());
#else
        string exePath = "/bin/bash";
        string arguments = Path.Combine(workingDir, "JSBExternalTools/closure-compiler/minifyJs.sh") + " " + string.Join(" ", jsFiles.ToArray());
#endif
        //这里要使用UseShellExecute的方式执行批处理脚本,重定向输出信息的话,会导致Unity卡死
        //暂不知道原因可能是Java程序没执行完毕
        var processInfo = new ProcessStartInfo
        {
#if USE_SHELL
            CreateNoWindow = true,
            UseShellExecute = true,
            RedirectStandardOutput = false,
            RedirectStandardError = false,
            FileName = exePath,
            Arguments = arguments
#else
            CreateNoWindow = true,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            FileName = exePath,
            Arguments = arguments
#endif
        };

        var process = Process.Start(processInfo);
#if !USE_SHELL
        string outputLog = process.StandardOutput.ReadToEnd();
        string errorLog = process.StandardError.ReadToEnd();
#endif
        // 等待结束
        process.WaitForExit();

        int exitCode = process.ExitCode;
        process.Close();
        if (exitCode != 0)
        {
            if (displayDialog)
                EditorUtility.DisplayDialog("JsMinify", "Minify failed. exit code = " + exitCode, "OK");
#if !USE_SHELL
            if (!string.IsNullOrEmpty(errorLog))
                Debug.LogError(errorLog);
#endif
        }
        else
        {
            if (displayDialog)
                EditorUtility.DisplayDialog("JsMinify", "JsCode Minify success.", "OK");
        }

        //输出Minify日志
#if !USE_SHELL
        string logFile = GetTempFileNameFullPath("minify_log.txt");
        File.WriteAllText(logFile, outputLog);
        Debug.LogError(outputLog);

        string relPath = logFile.Replace("\\", "/").Substring(logFile.IndexOf("Assets/"));
        var context = AssetDatabase.LoadAssetAtPath<Object>(relPath);
        Debug.Log("生成了文件 " + relPath + "，请检查（点击此条可定位文件）", context);
#endif
        AssetDatabase.Refresh();
    }

    [MenuItem("JSB/Delete All Minify JsCode", false, 133)]
    public static void DeleteAllMinifyJsCode()
    {
        var jsFiles = GetNeedMinifyJsFiles();

        if (!EditorUtility.DisplayDialog("TIP",
                "Total: " + jsFiles.Count + "个Js文件Minify文件删除",
                "OK",
                "Cancel"))
        {
            Debug.Log("Operation canceled.");
            return;
        }

        for (int i = 0; i < jsFiles.Count; i++)
        {
            string minifyFile = Path.ChangeExtension(jsFiles[i], ".min" + JSPathSettings.jsExtension);
            //            FileHelper.DeleteFile(minifyFile);
            FileUtil.DeleteFileOrDirectory(minifyFile);
        }

        AssetDatabase.Refresh();
    }

    #endregion

    #region JsCompiler

    public const string JsCompilerPath = "JSBExternalTools/JsCompiler/skc5.exe";
    public const string ouputDllPath = "Temp/obj/Debug/SharpKitProj.dll";
    /// <summary>
    /// 不明作用，暂时保留
    /// </summary>
    private static bool _rebuild;
    private static bool _quickRebuild;
    private static bool _editorDefine;
    private static bool cmd = false;

    private static Dictionary<string, List<string>> typesImpByJs;

    private static bool CompileJsCode(string allInvokeOutputPath, string allInvokeWithLocationOutputPath,
                                      string YieldReturnTypeOutputPath, bool displayDialog)
    {
        string workingDir = Application.dataPath.Replace("/Assets", "");

        var args = new args();

        // working dir
        args.AddFormat("/dir:\"{0}\"", workingDir);

        var define = GetDefines();
        args.AddFormat("/define:{0}", define);

        if (_rebuild)
            args.Add("/rebuild");

        // references
        foreach (var reference in GetReferences())
        {
            args.AddFormat("/reference:\"{0}\"", reference);
        }

        // out, target, target framework version
        args.Add("/out:" + ouputDllPath);
        args.Add("/target:library");
        args.Add("/TargetFrameworkVersion:v3.5");
        args.AddFormat("/AllInvocationsOutput:\"{0}\"", allInvokeOutputPath);
        args.AddFormat("/AllInvocationsWithLocationOutput:\"{0}\"", allInvokeWithLocationOutputPath);
        args.AddFormat("/YieldReturnTypeOutput:\"{0}\"", YieldReturnTypeOutputPath);
#if USE_SHELL
        args.Add("/exitReadKey");
#endif

        // source files
        foreach (var csFile in GetCsFiles())
        {
            args.Add("\"" + csFile.Replace(workingDir, ".") + "\"");
        }

        // 把参数写到文件中，然后把这个文件路径做为参数传递给 skc5.exe
        string argFile = GetTempFileNameFullPath("skc_args.txt");
        string strArgs = args.Format(args.ArgsFormat.Space);
        File.WriteAllText(argFile, strArgs);

        //windows下直接调用skc5编译，mac下需要通过mono调用skc5
#if UNITY_EDITOR_WIN
        string exePath = Path.Combine(workingDir, JsCompilerPath);
        string arguments = String.Format("/paramFile:\"{0}\"", argFile);
#else
		string exePath = "/usr/local/bin/mono";
		string arguments = Path.Combine(workingDir,JsCompilerPath) + string.Format(" /paramFile:\"{0}\"",argFile);
#endif
        var processInfo = new ProcessStartInfo
        {
#if USE_SHELL
            CreateNoWindow = true,
            UseShellExecute = true,
            RedirectStandardOutput = false,
            RedirectStandardError = false,
            FileName = exePath,
            Arguments = arguments
#else
            CreateNoWindow = true,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            FileName = exePath,
            Arguments = arguments
#endif
        };

        var process = Process.Start(processInfo);
#if !USE_SHELL
        string outputLog = process.StandardOutput.ReadToEnd();
        string errorLog = process.StandardError.ReadToEnd();
#endif
        // 等待结束
        process.WaitForExit();

        int exitCode = process.ExitCode;
        process.Close();
        if (exitCode != 0)
        {
            if (displayDialog)
            {
                EditorUtility.DisplayDialog("SharpKitCompiler", "Compile failed. exit code = " + exitCode, "OK");
            }
#if !USE_SHELL
            Debug.LogError(outputLog + "just Log, 非Error (可以忽略)");
            if (!string.IsNullOrEmpty(errorLog))
                Debug.LogError(errorLog);

            File.WriteAllText("skcErr.txt",outputLog+"\n"+errorLog);
#endif

            return false;
        }

#if !USE_SHELL
		Debug.LogError(outputLog);
#endif
        if (displayDialog)
        {
            EditorUtility.DisplayDialog("SharpKitCompiler", "Compile success.", "OK");
        }
        return true;
    }

    [MenuItem("JSB/Check Compile", false, 142)]
    private static void CheckCompile()
    {
        _editorDefine = false;
        var dllPath = Path.GetDirectoryName(Application.dataPath) + "/Temp/CheckCompile.dll";
        var msg = EditorUtility.CompileCSharp(GetCsFiles(true).ToArray(), GetReferences(false).ToArray(), GetDefines().Split(';'),
            dllPath);
        if (msg != null)
        {
            var sb = new StringBuilder();
            foreach (var s in msg)
            {
                if (s.Contains(" error ") || s.Contains("Exception"))
                {
                    sb.AppendLine(s);
                }
            }
            if (sb.Length > 1)
            {
                Debug.LogError(sb);
            }
            else
            {
                Debug.LogError("Check Success!");
            }
        }
    }

    public static List<string> GetReferences(bool sharpKitMode = true)
    {
        var references = new HashSet<string>();
        if (sharpKitMode)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var path = assembly.Location;
                if (!path.Contains("-Editor"))
                {
                    references.Add(path);
                }
            }
        }
        else
        {
            foreach (var assemblyName in AppDomain.CurrentDomain.Load("Assembly-CSharp").GetReferencedAssemblies())
            {
                var path = AppDomain.CurrentDomain.Load(assemblyName).Location;
                references.Add(path);
            }
            foreach (var assemblyName in AppDomain.CurrentDomain.Load("Assembly-CSharp-firstpass").GetReferencedAssemblies())
            {
                var path = AppDomain.CurrentDomain.Load(assemblyName).Location;
                references.Add(path);
            }
            references.RemoveWhere(s => s.Contains("mono"));
        }

        return references.ToList();
    }

    public static List<string> GetCsFiles(bool checkCompile = false)
    {
        var files = new List<string>();
        var sources = Directory.GetFiles(Application.dataPath, "*.cs", SearchOption.AllDirectories);
        for (int i = 0; i < sources.Length; i++)
        {
            //Ignore Editor Folder *.cs files
            string filePath = sources[i].Replace('\\', '/');
            if (filePath.Contains("/WebPlayerTemplates") || filePath.Contains("/Plugins/") || filePath.Contains("/Standard Assets/"))
                continue;
            if (filePath.Contains("/Editor/") && (!filePath.EndsWith("JsTypeInfo.cs") || checkCompile))
                continue;

            files.Add(filePath);
        }

        return files;
    }

    public static string GetDefines()
    {
        // define		
        var define = "TRACE";

#if UNITY_ANDROID
        define += ";UNITY_ANDROID";
#endif

        // Deprecated
#if UNITY_IPHONE
        define += ";UNITY_IPHONE";
#endif

#if UNITY_IOS
        define += ";UNITY_IOS";
#endif

#if UNITY_STANDALONE
        define += ";UNITY_STANDALONE";
#endif

#if UNITY_4_6
        define += ";UNITY_4_6";
#endif

#if UNITY_4_7
        define += ";UNITY_4_7";
#endif

#if UNITY_4_8
        define += ";UNITY_4_8";
#endif

#if UNITY_5
        define += ";UNITY_5";
#endif

#if UNITY_5_0
        define += ";UNITY_5_0";
#endif

#if UNITY_5_1
        define += ";UNITY_5_1";
#endif

#if UNITY_5_2
        define += ";UNITY_5_2";
#endif

#if UNITY_5_3
        define += ";UNITY_5_3";
#endif

#if UNITY_5_4
        define += ";UNITY_5_4";
#endif

        //在这里可以加入自定义宏
#if UNITY_ANDROID
        string scriptDefines = PlayerSettings.GetScriptingDefineSymbolsForGroup(BuildTargetGroup.Android);
#elif UNITY_IPHONE
        string scriptDefines = PlayerSettings.GetScriptingDefineSymbolsForGroup(BuildTargetGroup.iOS);
#else
        string scriptDefines = PlayerSettings.GetScriptingDefineSymbolsForGroup(BuildTargetGroup.Standalone);
#endif
        define += ";" + scriptDefines;
        if (!scriptDefines.Contains("ENABLE_JSB"))
            define += ";ENABLE_JSB";

        if (_editorDefine)
            define += ";UNITY_EDITOR";

        return define;
    }


    // 加载文本文件
    // 内容是所有 Logic 调用 Framework 的代码信息
    // Dict: className -> (memberName -> locations)
    private static Dictionary<string, Dictionary<string, List<Location>>> LoadAllInvoked(string path)
    {
        var D = new Dictionary<string, Dictionary<string, List<Location>>>();

        string content = File.ReadAllText(path);
        var lines = content.Split("\r\n".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);

        Dictionary<string, List<Location>> E = null;
        List<Location> L = null;
        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i];
            if (String.IsNullOrEmpty(line))
                continue;

            if (line[0] == '[')
            {
                string typeName = line.Substring(1, line.Length - 2);
                E = new Dictionary<string, List<Location>>();
                D.Add(typeName, E);
                continue;
            }

            bool b4 = line.Length >= 4 && line.Substring(0, 4) == "    ";
            bool b8 = line.Length >= 8 && line.Substring(0, 8) == "        ";

            if (b4 && !b8)
            {
                L = new List<Location>();
                E.Add(line.Substring(4), L);
            }
            else if (b8)
            {
                var loc = line.Substring(8).Split(',');
                //int index = int.Parse(loc[0]);
                L.Add(new Location { FileName = loc[1], Line = Int32.Parse(loc[2]) });
            }
            else
                throw new Exception("Line is invalid: '" + line + "'");
        }

        return D;
    }

    // 加载文本文件
    // 内容是所有 导出到JS的
    // Dict: className -> member names
    private static Dictionary<string, HashSet<string>> LoadAllExported(string path)
    {
        var D = new Dictionary<string, HashSet<string>>();

        string content = File.ReadAllText(path);
        var lines = content.Split("\r\n".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);

        HashSet<string> L = null;
        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i];
            if (String.IsNullOrEmpty(line))
                continue;

            if (line[0] == '[')
            {
                string typeName = line.Substring(1, line.Length - 2);
                L = new HashSet<string>();
                D.Add(typeName, L);
            }
            else if (line.Substring(0, 4) == "    ")
            {
                L.Add(line.Substring(4));
            }
            else
                throw new Exception("Line is invalid: " + line);
        }

        return D;
    }

    private static void CheckError_Invocation(string allInvokeWithLocationOutputPath)
    {
        if (typesImpByJs == null)
        {
            typesImpByJs = new Dictionary<string, List<string>>();
            typesImpByJs["T"] = new List<string> { "T" };
            typesImpByJs["System.Action"] = new List<string> { "" /* 调用 action */};
            typesImpByJs["System.Action$1"] = new List<string> { "" /* 调用 action */};
            typesImpByJs["System.Action$2"] = new List<string> { "" /* 调用 action */};
            typesImpByJs["System.Action$3"] = new List<string> { "" /* 调用 action */};
            typesImpByJs["System.Action$4"] = new List<string> { "" /* 调用 action */};
            typesImpByJs["System.Func$1"] = new List<string> { "" /* 调用 action */};
            typesImpByJs["System.Func$2"] = new List<string> { "" /* 调用 action */};
            typesImpByJs["System.Func$3"] = new List<string> { "" /* 调用 action */};
            typesImpByJs["System.Func$4"] = new List<string> { "" /* 调用 action */};
            typesImpByJs["System.Exception"] = new List<string> { "ctor$$String" };
            typesImpByJs["System.NotImplementedException"] = new List<string> { "ctor" };
            typesImpByJs["System.Array"] = new List<string>
            {
                "length",
                "CopyTo",
                "Static_Convertall",
                "Static_Sort$1$$T$Array"
            };
            typesImpByJs["System.Collections.Generic.List$1"] = new List<string>
            {
                "ctor",
                "ctor$$IEnumerable$1",
                "ctor$$Int32",
                "RemoveRange",
                "Clear",
                "get_Item$$Int32",
                "set_Item$$Int32",
                "get_Count",
                "GetEnumerator",
                "ToArray",
                "AddRange",
                "Add",
                "Remove",
                "Contains",
                "SetItems",
                "IndexOf",
                "Exists",
                "IndexOf$$T",
                "Insert",
                "RemoveAt",
                "RemoveAll",
                //"TryRemove",
                "CopyTo",
                //"get_IsReadOnly",
                "Reverse",
                "Sort",
                "Sort$$Comparison$1",
                "ForEach",
                "Find",
                "FindIndex$$Predicate$1",
                "FindIndex$$Int32$$Predicate$1",
                "FindIndex$$Int32$$Int32$$Predicate$1",
                "FindLastIndex$$Predicate$1",
                "FindLastIndex$$Int32$$Predicate$1",
                "FindLastIndex$$Int32$$Int32$$Predicate$1",
                "FindAll",
                "GetRange",
                "InsertRange"
            };
            typesImpByJs["System.Collections.Generic.Dictionary$2"] = new List<string>
            {
                "ctor",
                "ctor$$Int32",
                "Add",
                "Remove",
                "get_Item$$TKey",
                "set_Item$$TKey",
                "ContainsKey",
                "GetEnumerator",
                "Clear",
                "TryGetValue",
                "get_Count",
                "get_Keys",
                "get_Values"
            };
            typesImpByJs["System.Collections.Generic.KeyValuePair$2"] = new List<string>
            {
                "get_Key",
                "get_Value",
                "ctor$$TKey$$TValue"
            };
            typesImpByJs["System.Collections.Generic.Dictionary.ValueCollection$2"] = new List<string> { "CopyTo" };
            // 特殊！
            typesImpByJs["System.Collections.Generic.Dictionary.KeyCollection$2"] = new List<string> { "CopyTo" }; // 特殊！
            typesImpByJs["System.Linq.Enumerable"] = new List<string> { "Static_ToArray$1" };
            typesImpByJs["System.Collections.Generic.HashSet$1"] = new List<string>
            {
                "ctor",
                "Add",
                "get_Count",
                "Clear",
                "Contains",
                "Remove"
            };
            typesImpByJs["System.Collections.Generic.Queue$1"] = new List<string>
            {
                "ctor",
                "ctor$$Int32",
                "Clear",
                "get_Count",
                "Enqueue",
                "Dequeue",
                "Peek",
                "Contains",
                "ToArray"
            };
            typesImpByJs["System.String"] = new List<string>
            {
                // native
                "toString",
                "length",
                "replace",
                "split",
                "indexOf",
                "substr",
                "charAt",
                /// static
                "Static_Empty",
                "Static_Format$$String$$Object",
                "Static_Format$$String$$Object$$Object",
                "Static_Format$$String$$Object$$Object$$Object",
                "Static_IsNullOrEmpty",
                "Static_Equals$$String$$String$$StringComparison",
                "Static_Join$$String$$String$Array",
                // instance
                "ctor$$Char$Array",
                "ctor$$Char$Array$$Int32$$Int32",
                "Insert",
                "Substring$$Int32",
                "Substring$$Int32$$Int32",
                "Substring",
                "ToLower",
                "toLowerCase",
                "ToUpper",
                "toUpperCase",
                "getItem",
                "IndexOf$$String",
                "IndexOf$$Char",
                "LastIndexOf",
                "LastIndexOf$$Char",
                "LastIndexOf$$String",
                "Remove$$Int32",
                "Remove$$Int32$$Int32",
                "StartsWith$$String",
                "EndsWith$$String",
                "Contains",
                "get_Length",
                "Split$$Char$Array",
                "trim",
                "Trim",
                "ltrim",
                "rtrim",
                "Static_Format$$String$$Object$Array",
                "Replace$$String$$String",
                "Replace$$Char$$Char",
                "PadLeft$$Int32$$Char",
                "PadRight$$Int32$$Char",
                "ToCharArray"
            };
            typesImpByJs["System.Char"] = new List<string> { "toString", "Static_IsNumber$$Char" };
            typesImpByJs["System.Int32"] = new List<string>
            {
                "toString",
                "Static_Parse$$String",
                "Static_TryParse$$String$$Int32",
                "ToString$$String",
                "CompareTo$$Int32"
            };
            typesImpByJs["System.UInt64"] = new List<string>
            {
                "toString",
                "Static_Parse$$String",
                "Static_TryParse$$String$$UInt64"
            };
            typesImpByJs["System.Int64"] = new List<string>
            {
                "toString",
                "Static_Parse$$String",
                "Static_TryParse$$String$$Int64",
                "ToString$$String",
                "CompareTo$$Int64"
            };
            typesImpByJs["System.Boolean"] = new List<string>
            {
                "toString",
                "CompareTo$$Boolean"
            };
            typesImpByJs["System.Double"] = new List<string>
            {
                "toString",
                "ToString$$String",
                "CompareTo$$Double",
                "Static_tryParse",
                "Static_Parse$$String",
                "Static_TryParse$$String$$Double"
            };
            typesImpByJs["System.Single"] = new List<string>
            {
                "toString",
                "ToString$$String",
                "CompareTo$$Single",
                "Static_tryParse",
                "Static_Parse$$String",
                "Static_TryParse$$String$$Single"
            };
            //			typesImpByJs["System.Int32"] = new List<string> { "toString", "Static_Parse$$String",  };
            typesImpByJs["System.Enum"] = new List<string> { "toString" };
            typesImpByJs["System.MulticastDelegate"] = new List<string>();
        }


        string allExportedMembersFile = GetAllExportedMembersFile();

        var allInvoked = LoadAllInvoked(allInvokeWithLocationOutputPath);
        var allExported = LoadAllExported(allExportedMembersFile);
        foreach (var KV in typesImpByJs)
        {
            HashSet<string> HS = null;
            if (!allExported.TryGetValue(KV.Key, out HS))
            {
                HS = new HashSet<string>();
                allExported.Add(KV.Key, HS);
            }
            if (KV.Value == null)
                continue;

            foreach (string m in KV.Value)
            {
                if (!HS.Contains(m))
                    HS.Add(m);
            }
        }

        var sbError = new StringBuilder();

        int errCount = 0;
        foreach (var KV in allInvoked)
        {
            string typeName = KV.Key;
            HashSet<string> hsExported;
            var DInvoked = KV.Value;

            // 类有导出吗？
            if (!allExported.TryGetValue(typeName, out hsExported))
            {
                errCount++;
                sbError.AppendFormat("[{0}] not exported.", typeName);
                sbError.AppendLine();
                foreach (var KV2 in DInvoked)
                {
                    string methodName = KV2.Key;
                    sbError.AppendFormat("      {0}", methodName);
                    sbError.AppendLine();

                    foreach (var loc in KV2.Value)
                    {
                        sbError.AppendFormat("        {0} {1}", loc.FileName, loc.Line);
                        sbError.AppendLine();
                    }
                }
            }
            else
            {
                foreach (var KV2 in DInvoked)
                {
                    string methodName = KV2.Key;
                    // 函数可用/有导出吗
                    if (hsExported == null || !hsExported.Contains(methodName))
                    {
                        errCount++;
                        sbError.AppendFormat("[{0}].{1} not valid.", typeName, methodName);
                        sbError.AppendLine();

                        foreach (var loc in KV2.Value)
                        {
                            sbError.AppendFormat("        {0} {1}", loc.FileName, loc.Line);
                            sbError.AppendLine();
                        }
                    }
                }
            }
        }

        string fullpath = GetTempFileNameFullPath("CompilerCheckErrorResult.txt");
        File.Delete(fullpath);

        if (errCount > 0)
        {
            File.WriteAllText(fullpath, sbError.ToString());

            string relPath = fullpath.Replace("\\", "/").Substring(fullpath.IndexOf("Assets/"));
            var context = AssetDatabase.LoadAssetAtPath<Object>(relPath);
            Debug.LogError("Check invocation error result: (" + errCount + " errors) （点击此条可定位文件）", context);
            Debug.LogError(sbError);
        }
        else
        {
            Debug.Log("Check invocation error result: 0 error");
        }
    }

    private static void CheckError_Inheritance()
    {
        var sb = new StringBuilder();
        int errCount = 0;
        foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (a.FullName.Contains("Assembly-CSharp")) // 可能是 Assembly-CSharp-Editor
            {
                var types = a.GetTypes();
                foreach (var type in types)
                {
                    bool toJS = WillTypeBeTranslatedToJavaScript(type);
                    if (!toJS)
                        continue;

                    var baseType = type.BaseType;
                    if (baseType == null ||
                        baseType == typeof(object) ||
                        baseType == typeof(Enum) ||
                        baseType == typeof(ValueType) ||
                        baseType == typeof(MonoBehaviour))
                    {
                        continue;
                    }

                    // 特赦
                    //					if (baseType == typeof(Swift.Component) ||
                    //					    baseType == typeof(Swift.Port) ||
                    //					    baseType == typeof(Swift.PortAgent))
                    //					{
                    //						continue;
                    //					}


                    bool baseToJS = WillTypeBeTranslatedToJavaScript(baseType);
                    if (!baseToJS)
                    {
                        errCount++;
                        sb.AppendFormat("[{0}] 继承自 [{1}]，是否应该放 Framework？", type.FullName, baseType.FullName);
                        sb.AppendLine();
                    }
                }
            }
        }
        if (errCount > 0)
            Debug.LogError(sb.ToString());
    }

    /// <summary>
    /// 强制正式环境
    /// </summary>
    /// <returns></returns>
    private static bool IsRealEnvironment()
    {
        return EditorUserBuildSettings.activeBuildTarget == BuildTarget.iOS;
    }

    private static bool IsRebuild()
    {
        // 防止iOS遗漏，反正也挺快的
        if (IsRealEnvironment())
        {
            return true;
        }

        return EditorUtility.DisplayDialog("编译JS", "是否完整编译？增量编译更快，但可能会出问题！", "完整",
            " 增量");
    }

    [MenuItem("JSB/Build Mobile JsCode", false, 141)]
    public static void BuildJsCode()
    {
        var rebuild = IsRebuild();
        BuildJsCode(true, rebuild);
    }

    public static void BuildJsCode(bool displayDialog, bool rebuild)
    {
        _rebuild = true;
        _editorDefine = false;
        CompileCsToJs(displayDialog, rebuild);
    }


    [MenuItem("JSB/Build Editor JsCode", false, 131)]
    public static void BuildEditorJsCode()
    {
        var rebuild = IsRebuild();
        BuildEditorJsCode(true, rebuild);
    }

    public static void BuildEditorJsCode(bool displayDialog, bool rebuild)
    {
        _rebuild = true;
        _editorDefine = true;
        CompileCsToJs(displayDialog, rebuild);
    }

    //[MenuItem("JSB/Rebuild Editor JsCode", false, 131)]
    //public static void RebuildEditorJsCode()
    //{
    //    _rebuild = true;
    //    _editorDefine = true;
    //    CompileCsToJs();
    //}

    [MenuItem("JSB/One Key Build All/One Key Build for Editor", false, 151)]
    public static void OneKeyBuildAllEditor()
    {
        var rebuild = IsRebuild();

        OneKeyBuildAll(false, false, rebuild);
    }

    [MenuItem("JSB/One Key Build All/One Key Build for Mobile", false, 152)]
    public static void OneKeyBuildAllMobile()
    {
        var rebuild = IsRebuild();
        OneKeyBuildAll(true, false, rebuild);
    }

    [MenuItem("JSB/One Key Build All/One Key Build for Mobile Except Framework", false, 153)]
    public static void OneKeyBuildMobileAndMinify()
    {
        var rebuild = IsRebuild();
        var displayDialog = false;

        ScriptRecompileHelper.WaitIfCompiling<bool>(GenerateJsTypeInfo, displayDialog);
        ScriptRecompileHelper.WaitIfCompiling(GenerateJsInfoConfig);
        ScriptRecompileHelper.WaitIfCompiling<bool, bool>(BuildJsCode, displayDialog, rebuild);
        if (IsRealEnvironment())
        {
            ScriptRecompileHelper.WaitIfCompiling<bool>(MinifyJsCode, displayDialog);
        }
    }

    public static void OneKeyBuildAll(bool pForMobile, bool displayDialog, bool rebuild)
    {
        ScriptRecompileHelper.CheckBeforeUsing();
        ScriptRecompileHelper.WaitIfCompiling(CodeManagerTool.ChangeToJSB);
        ScriptRecompileHelper.WaitIfCompiling<bool>(CSGenerator.GenerateJSCSBindings, displayDialog);
        ScriptRecompileHelper.WaitIfCompiling<bool>(GenerateJsTypeInfo, displayDialog);
        ScriptRecompileHelper.WaitIfCompiling(GenerateJsInfoConfig);
        if (pForMobile || IsRealEnvironment())
        {
            ScriptRecompileHelper.WaitIfCompiling<bool, bool>(BuildJsCode, displayDialog, rebuild);
        }
        else
        {
            ScriptRecompileHelper.WaitIfCompiling<bool, bool>(BuildEditorJsCode, displayDialog, rebuild);
        }

        if (IsRealEnvironment())
        {
            ScriptRecompileHelper.WaitIfCompiling<bool>(MinifyJsCode, displayDialog);
        }

        if (!displayDialog)
        {
            ScriptRecompileHelper.WaitIfCompiling(() => { EditorHelper.DisplayResultDialog(); });
        }
    }

    public static bool CheckTempJSBCodeRoot()
    {
        if (Directory.Exists(CodeManagerTool.TempJSBCodeRoot))
        {
            return EditorUtility.DisplayDialog("TIP", "检测到TempJSBCodeRoot目录存在，是否还原业务代码？", "OK", "CANCEL");
        }
        return false;
    }

    public static void CompileCsToJs(bool displayDialog, bool rebuild)
    {
        // 这个用于查看
        string allInvokeOutputPath = GetTempFileNameFullPath("AllInvocations.txt");
        // 这个用于分析
        string allInvokeWithLocationOutputPath = GetTempFileNameFullPath("AllInvocationsWithLocation.txt");
        // 
        string YieldReturnTypeOutputPath = GetTempFileNameFullPath("YieldReturnTypes.txt");

        // 编译
        //        if (!CompileJsCode(allInvokeOutputPath, allInvokeWithLocationOutputPath, YieldReturnTypeOutputPath, displayDialog))
        //        {
        //            return;
        //        }
        if (!JsQuikBuild.QuickCompileJsCode(rebuild))
        {
            return;
        }


        // 查错
        //CheckError_Invocation(allInvokeWithLocationOutputPath);
        //CheckError_Inheritance();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        // 提示生成 yield 结果
        string relPath =
            YieldReturnTypeOutputPath.Replace("\\", "/").Substring(YieldReturnTypeOutputPath.IndexOf("Assets/"));
        var context = AssetDatabase.LoadAssetAtPath<Object>(relPath);
        Debug.Log("生成了文件 " + relPath + "，请检查（点击此条可定位文件）", context);
    }

    public class Location
    {
        public string FileName;
        public int Line;
    }

    #endregion

    #region JsTypeGenerator

    public const string FilesToAddJsType = "FilesToAddJsType.txt";
    private static HashSet<string> _jsTypeNameSet;

    /// <summary>
    ///     缓存记录JsTypeInfo生成的类型名集合
    /// </summary>
    /// <value>The js type name set.</value>
    public static HashSet<string> JsTypeNameSet
    {
        get
        {
            if (_jsTypeNameSet == null)
            {
                _jsTypeNameSet = new HashSet<string>();
                var assemblyAttrs = typeof(JsExternalTools).Assembly.GetCustomAttributes(typeof(JsTypeAttribute), false);
                foreach (var attr in assemblyAttrs)
                {
                    JsTypeAttribute jsAttr = attr as JsTypeAttribute;
                    _jsTypeNameSet.Add(jsAttr.TargetTypeName);
                }
            }
            return _jsTypeNameSet;
        }
    }

    public const string JsTypeInfoFileTemplate = @"
//------------------------------------------------------------------------------
// <auto-generated>
// This code was generated by CSGenerator.
// </auto-generated>
//------------------------------------------------------------------------------
using SharpKit.JavaScript;

[assembly: JsExport(Minify = false, DefaultFilename = ""Assets/JavaScript/GameLogicCode.bytes"")]

#region JsType
{0}
#endregion";

    public const string JsTypeFormat = @"[assembly: JsType(TargetTypeName = ""{0}"", Mode = JsMode.Clr)]";

    public static readonly string JsTypeInfoFile = Application.dataPath +
                                                   "/Editor/JSBinding/JsTypeInfo.cs";

    public const string JsTypeGeneratorPath = "JSBExternalTools/JsTypeGenerator/JsTypeGenerator.exe";

    [MenuItem("JSB/Add SharpKit JsType Attribute for all Structs and Classes", false, 51)]
    public static void GenerateJsTypeInfo()
    {
        GenerateJsTypeInfo(true);
    }

    public static void GenerateJsTypeInfo(bool displayDialog)
    {
        var fileInfoSb = new StringBuilder();
        string workingDir = Application.dataPath.Replace("/Assets", "");
        var args = new args();

        // working dir
        args.AddFormat("/dir:\"{0}\"", workingDir);
        args.AddFormat("/out:\"{0}\"", JsTypeInfoFile);
        args.AddFormat("/template:\"{0}\"", JsTypeInfoFileTemplate);
        args.AddFormat("/format:\"{0}\"", JsTypeFormat);

        var exportList = GetExportFiles();
        var exportCount = exportList.Count;
        foreach (var filePath in exportList)
        {
            fileInfoSb.AppendLine(filePath.Replace(Application.dataPath, ""));
            args.Add("\"" + filePath.Replace(workingDir, ".") + "\"");
        }

        // 把参数写到文件中，然后把这个文件路径做为参数传递给 skc5.exe
        string argFile = GetTempFileNameFullPath("JsTypeGenerator_Args.txt");
        string strArgs = args.Format(args.ArgsFormat.Space);
        File.WriteAllText(argFile, strArgs);

        string addTypeInfoFile = GetTempFileNameFullPath(FilesToAddJsType);
        File.WriteAllText(addTypeInfoFile, fileInfoSb.ToString());
        AssetDatabase.Refresh();

        if (displayDialog)
        {
            if (!EditorUtility.DisplayDialog("TIP",
                    "Total: " + exportCount + "file prepare to Add [JsType]",
                    "OK",
                    "Cancel"))
            {
                Debug.Log("Operation canceled.");
                return;
            }

        }

#if UNITY_EDITOR_WIN
        string exePath = Path.Combine(workingDir, JsTypeGeneratorPath);
        string arguments = String.Format("/paramFile:\"{0}\"", argFile);
#else
        string exePath = "/usr/local/bin/mono";
        string arguments = Path.Combine(workingDir, JsTypeGeneratorPath) + string.Format(" /paramFile:\"{0}\"", argFile);
#endif
        var processInfo = new ProcessStartInfo
        {
            CreateNoWindow = true,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            FileName = exePath,
            Arguments = arguments
        };

        var process = Process.Start(processInfo);
        string outputLog = process.StandardOutput.ReadToEnd();
        string errorLog = process.StandardError.ReadToEnd();
        // 等待结束
        process.WaitForExit();

        Debug.LogError(outputLog);
        int exitCode = process.ExitCode;
        process.Close();
        if (exitCode != 0)
        {

            EditorUtility.DisplayDialog("JsTypeGenerator", "Generate failed. exit code = " + exitCode, "OK");
            if (!String.IsNullOrEmpty(errorLog))
                Debug.LogError(errorLog);
            throw new SystemException(errorLog);
        }

        //每次生成JsTypeInfo，清空一下JsTypeNameSet
        _jsTypeNameSet = null;
        if (displayDialog)
            EditorUtility.DisplayDialog("JsTypeGenerator", "GenerateJsTypeInfo success.", "OK");

        // 刷新前改一下文件格式，减少编译时间
        EditorHelper.SetFileFormatToUTF8_BOM(JsTypeInfoFile);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    public static List<string> GetExportFiles()
    {
        var csFiles = Directory.GetFiles(Application.dataPath, "*.cs", SearchOption.AllDirectories);
        var list = new List<string>();
        // filter files
        for (int i = 0; i < csFiles.Length; i++)
        {
            string file = csFiles[i];
            string filePath = file.Replace('\\', '/');
            bool export = true;

            //忽略Editor目录下的脚本
            if (filePath.Contains("/Editor/"))
            {
                continue;
            }

            //检查是否在忽略文件目录列表中
            for (int j = 0; j < JSBCodeGenSettings.PathsNotToJavaScript.Length; j++)
            {
                string dir = JSBCodeGenSettings.PathsNotToJavaScript[j];
                if (filePath.Contains(dir))
                {
                    export = false;
                    break;
                }
            }

            //检查是否在指定文件目录列表中
            if (!export && JSBCodeGenSettings.PathsToJavaScript != null)
            {
                for (int k = 0; k < JSBCodeGenSettings.PathsToJavaScript.Length; k++)
                {
                    string dir = JSBCodeGenSettings.PathsToJavaScript[k];
                    if (filePath.Contains(dir))
                    {
                        export = true;
                        break;
                    }
                }
            }
            if (export)
            {
                list.Add(filePath);
            }
        }

        return list;
    }

    [MenuItem("JSB/Delete SharpKit JsType Attribute for all Structs and Classes", false, 52)]
    public static void RemoveJsTypeAttribute()
    {
        if (!EditorUtility.DisplayDialog("TIP",
                "Will clean up JsTypeInfo.cs file",
                "OK",
                "Cancel"))
        {
            Debug.Log("Operation canceled.");
            return;
        }


        File.WriteAllText(JsTypeInfoFile, String.Format(JsTypeInfoFileTemplate, ""), new UTF8Encoding(true));

        EditorUtility.DisplayDialog("Tip", "RemoveJsTypeAttribute Success!", "OK");

        AssetDatabase.Refresh();
    }

    /// <summary>
    ///     生成JsType MonoBehaviour转JsCom配置信息
    ///     生成所有转换到JsType的类型信息
    /// </summary>
    [MenuItem("JSB/Generate Mono2JsComConfig and JsTypeConfig", false, 53)]
    public static void GenerateJsInfoConfig()
    {
        GenerateJsTypeInfoConfig();
        GenerateMono2JsComConfig();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    private static void GenerateJsTypeInfoConfig()
    {
        string filePath = JSPathSettings.JsTypeInfoConfig;
        File.WriteAllText(filePath, MiniJSON.jsonEncode(JsTypeNameSet.ToArray()));
        Debug.Log(String.Format("JsTypeInfoConfig:{0}\nOK. File: {1}", JsTypeNameSet.Count, filePath));
    }

    private static void GenerateMono2JsComConfig()
    {
        var mono2JsCom = new Dictionary<string, string>();

        Assembly logicCodeLib = Assembly.Load("Assembly-CSharp");
        if (logicCodeLib != null)
        {
            var types = logicCodeLib.GetExportedTypes();
            foreach (var t in types)
            {
                if (t.IsSubclassOf(typeof(MonoBehaviour)))
                {
                    if (WillTypeBeTranslatedToJavaScript(t))
                    {
                        string jsComponentName = JSComponentGenerator.GetJSComponentClassName(t);
                        mono2JsCom.Add(JSNameMgr.GetTypeFullName(t, false), jsComponentName);
                    }
                }
            }
        }
        else
        {
            Debug.LogError("Load Assembly-CSharp.dll failed");
        }

        string filePath = JSPathSettings.Mono2JsComConfig;
        File.WriteAllText(filePath, MiniJSON.jsonEncode(mono2JsCom));
        Debug.Log(String.Format("Mono2JsCom:{0}\nOK. File: {1}", mono2JsCom.Count, filePath));
    }

    /// <summary>
    /// Wills the type be translated to javascript.
    /// </summary>
    /// <param name="type">The type.</param>
    /// <returns></returns>
    public static bool WillTypeBeTranslatedToJavaScript(Type type)
    {
        if (type.IsDefined(typeof(JsTypeAttribute), false))
            return true;

        if (JsTypeNameSet.Contains(type.FullName))
            return true;

        return false;
    }

    #endregion

    #region Helper Func

    public static string GetTempFileNameFullPath(string shortPath)
    {
        Directory.CreateDirectory(Application.dataPath + "/Temp/");
        return Application.dataPath + "/Temp/" + shortPath;
    }

    public static string GetAllExportedMembersFile()
    {
        return GetTempFileNameFullPath("AllExportedMembers.txt");
    }

    #endregion

    [MenuItem("JSB/Others/Online Documents", false, 174)]
    public static void OpenHelp()
    {
        Application.OpenURL("http://www.cnblogs.com/answerwinner/p/4469021.html");
        // Application.OpenURL("http://www.cnblogs.com/answerwinner/p/4591144.html"); // English
    }
}