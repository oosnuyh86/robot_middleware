using System.IO;
using Unity.EditorCoroutines.Editor;
using Unity.Robotics.UrdfImporter;
using UnityEditor;
using UnityEngine;

namespace RobotMiddleware.Editor
{
    public static class UR10eImporter
    {
        const string UrdfPath = "Assets/URDF/UR10e/ur10e.urdf";

        [MenuItem("Robot Middleware/Import UR10e URDF")]
        static void ImportUR10e()
        {
            string fullPath = Path.GetFullPath(UrdfPath);
            if (!File.Exists(fullPath))
            {
                EditorUtility.DisplayDialog("URDF Import Error",
                    $"URDF file not found at:\n{fullPath}", "OK");
                return;
            }

            var settings = new ImportSettings
            {
                chosenAxis = ImportSettings.axisType.yAxis,
                convexMethod = ImportSettings.convexDecomposer.vHACD,
                OverwriteExistingPrefabs = true
            };

            EditorCoroutineUtility.StartCoroutineOwnerless(
                UrdfRobotExtensions.Create(fullPath, settings, loadStatus: true));

            Debug.Log("[UR10eImporter] Started URDF import from: " + fullPath);
        }
    }
}
