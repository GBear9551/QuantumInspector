using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FightSong.ReleaseChecks
{
    // Publisher tooling only. Reflects the installed validator package, never Unity internals.
    public static class RunStoreValidator
    {
        public static object Run()
        {
            for(int i=0;i<SceneManager.sceneCount;i++)
                if(SceneManager.GetSceneAt(i).isDirty||string.IsNullOrEmpty(SceneManager.GetSceneAt(i).path))
                    return new{ran=false,reason="An open scene is unsaved; preserve it and validate the exported package in a clean project."};
            var setup=EditorSceneManager.GetSceneManagerSetup();
            var selection=Selection.activeObject;
            try
            {
                var assembly=AppDomain.CurrentDomain.GetAssemblies().First(a=>a.GetType("AssetStoreTools.Validator.CurrentProjectValidator")!=null);
                var settingsType=assembly.GetType("AssetStoreTools.Validator.Data.CurrentProjectValidationSettings");
                var settings=Activator.CreateInstance(settingsType);
                settingsType.GetField("Category").SetValue(settings,"tools/utilities");
                settingsType.GetField("ValidationPaths").SetValue(settings,new List<string>{"Assets/FightSongStudio/QuantumInspector"});
                var validationType=settingsType.GetField("ValidationType");
                validationType.SetValue(settings,Enum.Parse(validationType.FieldType,"UnityPackage"));
                var validator=Activator.CreateInstance(assembly.GetType("AssetStoreTools.Validator.CurrentProjectValidator"),settings);
                var result=validator.GetType().GetMethod("Validate").Invoke(validator,null);
                var rows=new List<Row>();
                foreach(var test in (IEnumerable)result.GetType().GetField("Tests").GetValue(result))
                {
                    var value=test.GetType().GetField("Result").GetValue(test);
                    var count=(int)value.GetType().GetProperty("MessageCount").GetValue(value);
                    var messages=new List<string>();
                    for(int i=0;i<count;i++)
                    {
                        var message=value.GetType().GetMethod("GetMessage").Invoke(value,new object[]{i});
                        messages.Add((string)message.GetType().GetMethod("GetText").Invoke(message,null));
                    }
                    rows.Add(new Row{title=(string)test.GetType().GetField("Title").GetValue(test),status=value.GetType().GetField("Status").GetValue(value).ToString(),messages=messages.ToArray()});
                }
                var report=new Report{status=result.GetType().GetField("Status").GetValue(result).ToString(),tests=rows.ToArray()};
                File.WriteAllText("Release/AssetStoreValidator.json",JsonUtility.ToJson(report,true));
                return new{ran=true,status=report.status,total=rows.Count,results=rows.GroupBy(r=>r.status).Select(g=>new{status=g.Key,count=g.Count()}).ToArray(),issues=rows.Where(r=>r.status!="Pass"&&r.status!="NotApplicable").ToArray()};
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                EditorSceneManager.RestoreSceneManagerSetup(setup);
                Selection.activeObject=selection;
            }
        }
        [Serializable] public class Row {public string title,status;public string[] messages;}
        [Serializable] public class Report {public string status;public Row[] tests;}
    }
}
