using AssetsTools.NET;
using AssetsTools.NET.Extra;
using BepInEx;
using BepInEx.Logging;
using Mono.Cecil;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace Tobey.ChainedEchoes.UltrawideSupport.Unlocker
{
    public static class UltrawideSupportUnlocker
    {
        public static IEnumerable<string> TargetDLLs { get; } = new string[0];
        public static void Patch(AssemblyDefinition _) { }

        public static void Initialize()
        {
            Assembly assembly = Assembly.GetExecutingAssembly();
            var resourceName = assembly.GetManifestResourceNames().SingleOrDefault(name => name.EndsWith("lzma.tpk"));

            using (Stream stream = assembly.GetManifestResourceStream(resourceName))
            {
                try
                {
                    EnableUltrawideSupport(stream);
                    Log.LogInfo("Ultrawide Support enabled.");
                }
                catch (Exception e)
                {
                    Log.LogError(e);
                    Log.LogWarning("Failed to enable Ultrawide Support!");
                }
            }
        }

        private static readonly ManualLogSource Log = Logger.CreateLogSource("Ultrawide Support");

        private static void EnableUltrawideSupport(Stream classPackageStream)
        {
            string globalGameManagersFileName = "globalgamemanagers";
            string dataDir = Path.GetFullPath(Path.Combine(Paths.ManagedPath, ".."));
            string globalGameManagersPath = Path.Combine(dataDir, globalGameManagersFileName);

            var manager = new AssetsManager();
            manager.LoadClassPackage(classPackageStream);
            AssetsFileInstance globalGameManagersInstance = manager.LoadAssetsFile(globalGameManagersPath, false);
            manager.LoadClassDatabaseFromPackage(globalGameManagersInstance.file.Metadata.UnityVersion);

            AssetFileInfo playerSettings = globalGameManagersInstance.file.GetAssetInfo(1);
            var baseField = manager.GetBaseField(globalGameManagersInstance, playerSettings);

            foreach (var aspectRatio in baseField["m_SupportedAspectRatios"].Children)
            {
                aspectRatio.AsBool = true;
            }

            // write changes to temp file
            string tempPath = Path.Combine(dataDir, $"{globalGameManagersFileName}.tmp");
            using (var writer = new AssetsFileWriter(tempPath))
            {
                globalGameManagersInstance.file.Write(writer, 0, new List<AssetsReplacer>
                {
                    new AssetsReplacerFromMemory(globalGameManagersInstance.file, playerSettings, baseField)
                });
            }
            globalGameManagersInstance.file.Close();

            // finally, overwrite original with the modified file
            File.Delete(globalGameManagersPath);
            File.Move(tempPath, globalGameManagersPath);
        }
    }
}
