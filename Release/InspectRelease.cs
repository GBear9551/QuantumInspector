using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace FightSong.ReleaseChecks
{
    public static class InspectRelease
    {
        public static object Run()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var roots=new[]{"Assets/FightSongStudio/QuantumInspector"};
            var paths=AssetDatabase.GetAllAssetPaths().Where(p=>roots.Any(r=>p==r||p.StartsWith(r+"/"))).Where(p=>!AssetDatabase.IsValidFolder(p)).ToArray();
            var dependencies=AssetDatabase.GetDependencies(paths,true);
            var report=new Report{
                unityVersion=Application.unityVersion,
                assetCount=paths.Length,
                missingMeta=paths.Where(p=>!File.Exists(p+".meta")).ToArray(),
                longPaths=paths.Where(p=>p.Length>=150).ToArray(),
                externalAssets=dependencies.Where(p=>p.StartsWith("Assets/")&&!roots.Any(r=>p.StartsWith(r+"/"))).ToArray(),
                packageDependencies=dependencies.Where(p=>p.StartsWith("Packages/")).Select(p=>string.Join("/",p.Split('/').Take(2))).Distinct().ToArray(),
                executables=paths.Where(p=>new[]{".dll",".exe",".apk",".zip"}.Contains(Path.GetExtension(p).ToLowerInvariant())).ToArray(),
                scriptsWithoutNamespace=paths.Where(p=>p.EndsWith(".cs")&&!File.ReadAllText(p).Contains("namespace ")&&File.ReadAllLines(p).Any(line=>!string.IsNullOrWhiteSpace(line)&&!line.TrimStart().StartsWith("//"))).ToArray(),
                compileFailed=EditorUtility.scriptCompilationFailed
            };
            File.WriteAllText("Release/LocalAudit.json",JsonUtility.ToJson(report,true));
            return report;
        }
        public static object GraphCredit()
        {
            var type=typeof(FightSong.InspectorTool.InspectorWheelInformationVerification);
            var window=(FightSong.InspectorTool.InspectorWheelWindow)type.GetField("window",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static).GetValue(null);
            window.SetGraphMode(true);
            return new{ready=true};
        }
        public static object CheckGraphCredit()
        {
            var type=typeof(FightSong.InspectorTool.InspectorWheelInformationVerification);
            var window=(FightSong.InspectorTool.InspectorWheelWindow)type.GetField("window",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static).GetValue(null);
            var label=window.rootVisualElement.Q<Label>("developerCredit");
            if(label==null||label.resolvedStyle.display==DisplayStyle.None||label.worldBound.height<20||label.worldBound.yMax>window.rootVisualElement.worldBound.yMax+1)throw new Exception("Graph credit missing or clipped.");
            return new{passed=true,text=label.text,width=label.worldBound.width,height=label.worldBound.height};
        }
        [Serializable] public class Report
        {
            public string unityVersion;
            public int assetCount;
            public bool compileFailed;
            public string[] missingMeta,longPaths,externalAssets,packageDependencies,executables,scriptsWithoutNamespace;
        }
    }
}
