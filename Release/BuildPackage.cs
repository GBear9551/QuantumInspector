using System;
using System.IO;
using System.Linq;
using UnityEditor;

namespace FightSong.ReleaseChecks
{
    public static class BuildPackage
    {
        public static object Run()
        {
            if(EditorUtility.scriptCompilationFailed)throw new InvalidOperationException("Fix compilation before exporting.");
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            const string root="Assets/FightSongStudio/QuantumInspector";
            var paths=AssetDatabase.GetAllAssetPaths().Where(p=>p.StartsWith(root+"/")&&!AssetDatabase.IsValidFolder(p)&&!p.Contains("/Verification/")&&!p.Split('/').Any(part=>part.EndsWith("~"))).OrderBy(p=>p).ToArray();
            if(paths.Length==0)throw new InvalidOperationException("No package assets found.");
            Directory.CreateDirectory("output/package");
            const string output="output/package/QuantumInspector-1.0.0.unitypackage";
            AssetDatabase.ExportPackage(paths,output,ExportPackageOptions.Default);
            File.WriteAllLines("Release/PackageContents.txt",paths);
            return new{output,assets=paths.Length,bytes=new FileInfo(output).Length};
        }
    }
}
