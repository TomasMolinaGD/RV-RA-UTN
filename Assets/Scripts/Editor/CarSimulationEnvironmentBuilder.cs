using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.XR.Simulation;

namespace CarShowcase.Editor
{
    public static class CarSimulationEnvironmentBuilder
    {
        private const string EnvironmentDirectory = "Assets/XR/Simulation";
        private const string EnvironmentPath = EnvironmentDirectory + "/CarMarkerSimulationEnvironment.prefab";
        private const string PreferencesPath = "Assets/XR/UserSimulationSettings/Resources/XRSimulationPreferences.asset";

        private static readonly string[] MarkerPaths =
        {
            "Assets/Art/Images/VW/VWLogo1.png",
            "Assets/Art/Images/Ford/LogoF3.png",
            "Assets/Art/Images/Audi/LogoAudi1.png"
        };

        [MenuItem("Tools/AR/Crear entorno de prueba de marcadores")]
        public static void CreateOrUpdate()
        {
            Directory.CreateDirectory(EnvironmentDirectory);

            GameObject root = new GameObject("Car Marker Simulation Environment");

            try
            {
                AddSimulationEnvironmentComponent(root);
                AddMarkers(root.transform);

                GameObject environmentPrefab = PrefabUtility.SaveAsPrefabAsset(root, EnvironmentPath);
                SelectEnvironment(environmentPrefab);

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Selection.activeObject = environmentPrefab;

                Debug.Log($"[AR SIMULACIÓN] Entorno creado y seleccionado: {EnvironmentPath}");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void AddSimulationEnvironmentComponent(GameObject root)
        {
            Type environmentType = typeof(SimulatedTrackedImage).Assembly.GetType(
                "UnityEngine.XR.Simulation.SimulationEnvironment");

            if (environmentType == null)
                throw new InvalidOperationException("No se encontró SimulationEnvironment en AR Foundation.");

            Component environment = root.AddComponent(environmentType);
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;

            environmentType.GetField("m_CameraStartingPose", flags)?.SetValue(
                environment,
                new Pose(new Vector3(0f, 0.42f, -0.42f), Quaternion.Euler(38f, 0f, 0f)));

            environmentType.GetField("m_CameraMovementBounds", flags)?.SetValue(
                environment,
                new Bounds(new Vector3(0f, 0.75f, 0f), new Vector3(3f, 2.5f, 3f)));
        }

        private static void AddMarkers(Transform parent)
        {
            const float horizontalSpacing = 0.22f;

            for (int index = 0; index < MarkerPaths.Length; index++)
            {
                Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(MarkerPaths[index]);
                if (texture == null)
                    throw new InvalidOperationException($"No se encontró el marcador: {MarkerPaths[index]}");

                GameObject marker = new GameObject($"Marker_{texture.name}");
                marker.transform.SetParent(parent, false);
                marker.transform.localPosition = new Vector3(
                    (index - 1f) * horizontalSpacing,
                    0f,
                    0.12f);
                marker.transform.localRotation = Quaternion.identity;

                SimulatedTrackedImage simulatedImage = marker.AddComponent<SimulatedTrackedImage>();
                SerializedObject serializedImage = new SerializedObject(simulatedImage);
                serializedImage.FindProperty("m_Image").objectReferenceValue = texture;
                serializedImage.FindProperty("m_ImagePhysicalSizeMeters").vector2Value = new Vector2(0.1f, 0.1f);
                serializedImage.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(simulatedImage);
            }
        }

        private static void SelectEnvironment(GameObject environmentPrefab)
        {
            ScriptableObject preferences = AssetDatabase.LoadAssetAtPath<ScriptableObject>(PreferencesPath);
            if (preferences == null)
                throw new InvalidOperationException($"No se encontraron las preferencias XR: {PreferencesPath}");

            SerializedObject serializedPreferences = new SerializedObject(preferences);
            SerializedProperty environmentProperty = serializedPreferences.FindProperty("m_EnvironmentPrefab");

            if (environmentProperty == null)
                throw new InvalidOperationException("XRSimulationPreferences no contiene m_EnvironmentPrefab.");

            environmentProperty.objectReferenceValue = environmentPrefab;
            serializedPreferences.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(preferences);
        }
    }
}
