using System;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;

public class JSDicCompare : EditorWindow
{
    private static JSDicCompare instance;

    [MenuItem("JSB/DicCompare")]
    public static void ShowWindow()
    {
        if (instance == null)
        {
            var window = GetWindow<JSDicCompare>(false, "JSDicCompare", true);
            window.minSize = new Vector2(872f, 680f);
            window.Show();
        }
        else
        {
            instance.Close();
            instance = null;
        }
    }

    private void OnEnable()
    {
        instance = this;
    }

    private string oldDic1Path;
    private string newDic1Path;
    private void OnGUI()
    {
        EditorGUILayout.BeginHorizontal();
        {
            EditorGUILayout.BeginVertical();
            if (GUILayout.Button("加载oldDic1Path", "LargeButton", GUILayout.Height(50f)))
            {
                oldDic1Path = LoadResConfigFilePanel();
            }
            GUILayout.Label(string.Format("oldDic1 {0}", oldDic1Path));
            EditorGUILayout.EndVertical();

            EditorGUILayout.BeginVertical();
            if (GUILayout.Button("newDic1Path", "LargeButton", GUILayout.Height(50f)))
            {
                newDic1Path = LoadResConfigFilePanel();
            }
            GUILayout.Label(string.Format("newDic1 {0}", newDic1Path));
            EditorGUILayout.EndVertical();
        }
        EditorGUILayout.EndHorizontal();

        if (GUILayout.Button("生成Dic1差异", GUILayout.Height(50f)))
        {
            EditorApplication.delayCall += () =>
            {
                string[] oldDic1 = File.ReadAllLines(oldDic1Path);
                string[] newDic1 = File.ReadAllLines(newDic1Path);
                IEnumerable<string> intersect =  oldDic1.Intersect(newDic1);
                IEnumerable<string> except = newDic1.Except(oldDic1);

                StringBuilder compareStr = new StringBuilder();
                compareStr.AppendLine("交集");
                foreach (string str in intersect)
                {
                    compareStr.AppendLine(str);
                }

                //compareStr.AppendLine("###############################################");
                //compareStr.AppendLine("差集");
                //foreach (string str in except)
                //{
                //    compareStr.AppendLine(str);
                //}
                DateTime dateTime = DateTime.Now;

                File.WriteAllText(string.Format("{0}2{1}", Path.GetFileName(oldDic1Path), Path.GetFileName(newDic1Path)), compareStr.ToString());
                EditorUtility.DisplayDialog("提示", "生成差异完成", "OK");
            };
        }
    }
    private static string LoadResConfigFilePanel()
    {
        string filePath = EditorUtility.OpenFilePanel("加载版本资源配置信息", "", "");
        return filePath;
    }
}
