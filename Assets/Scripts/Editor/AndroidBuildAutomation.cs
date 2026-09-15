using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace CarShowcase.Editor
{
    public static class AndroidBuildAutomation
    {
        private const string OutputDirectory = "Builds/Android";
        private const string OutputPath = OutputDirectory + "/AutoScanAR.apk";

        [MenuItem("Tools/AR/Generar APK de prueba")]
        public static void BuildTestApk()
        {
            ConfigureAndroid();
            Directory.CreateDirectory(OutputDirectory);

            string[] scenes = EditorBuildSettings.scenes
                .Where(scene => scene.enabled)
                .Select(scene => scene.path)
                .ToArray();

            if (scenes.Length == 0)
                throw new InvalidOperationException("No hay escenas habilitadas en Build Settings.");

            if (!EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android))
                throw new InvalidOperationException("Unity no pudo cambiar la plataforma activa a Android.");

            BuildPlayerOptions options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = OutputPath,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = BuildOptions.None
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new InvalidOperationException(
                    $"La compilación Android terminó con estado {report.summary.result} y " +
                    $"{report.summary.totalErrors} errores.");
            }

            Debug.Log(
                $"[AR BUILD] APK generada correctamente: {Path.GetFullPath(OutputPath)} " +
                $"({report.summary.totalSize / (1024f * 1024f):0.0} MB)");
        }

        private static void ConfigureAndroid()
        {
            PlayerSettings.companyName = "UTN";
            PlayerSettings.productName = "AutoScan AR";
            PlayerSettings.bundleVersion = "1.0.0";
            PlayerSettings.Android.bundleVersionCode = 1;
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.utn.autoscanar");
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel25;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            EditorUserBuildSettings.buildAppBundle = false;
        }
    }
}
