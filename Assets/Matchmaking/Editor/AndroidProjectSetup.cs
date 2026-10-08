using System;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace Sid2026.Matchmaking.Editor
{
    [InitializeOnLoad]
    public static class AndroidProjectSetup
    {
        private const string PackageId = "com.upb.sid2026";
        private const string ExpectedDatabaseProject = "sistemasfisicosdistribui-c957b";
        private const string SessionKey = "SID2026_MATCHMAKING_VALIDATED";

        static AndroidProjectSetup()
        {
            EditorApplication.delayCall += ApplySafeDefaultsOnce;
        }

        [MenuItem("SID2026/1. Configurar proyecto para Android", priority = 1)]
        public static void ConfigureAndroid()
        {
            ApplyPlayerSettings();
            ValidateGoogleServices(true);

            bool switched = EditorUserBuildSettings.SwitchActiveBuildTarget(
                BuildTargetGroup.Android, BuildTarget.Android);
            if (switched)
                Debug.Log("[SID2026] Proyecto configurado y migrado a Android correctamente.");
            else
                Debug.LogWarning("[SID2026] No se pudo cambiar a Android. Verifica que Android Build Support esté instalado en Unity Hub.");
        }

        [MenuItem("SID2026/2. Validar configuración de Firebase", priority = 2)]
        public static void ValidateFirebaseMenu()
        {
            ValidateGoogleServices(true);
        }

        [MenuItem("SID2026/3. Abrir escena principal", priority = 3)]
        public static void OpenMainScene()
        {
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Matchmaking/Scenes/Main.unity");
        }

        private static void ApplySafeDefaultsOnce()
        {
            if (SessionState.GetBool(SessionKey, false)) return;
            SessionState.SetBool(SessionKey, true);
            ApplyPlayerSettings();
            ValidateGoogleServices(false);
        }

        private static void ApplyPlayerSettings()
        {
            PlayerSettings.companyName = "UPB";
            PlayerSettings.productName = "Actividad 6 - Matchmaking Firebase";
            PlayerSettings.bundleVersion = "1.0.0";
            PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, PackageId);
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel23;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64 | AndroidArchitecture.ARMv7;
            PlayerSettings.Android.bundleVersionCode = 1;
            PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);
            EditorUserBuildSettings.androidBuildSystem = AndroidBuildSystem.Gradle;
            AssetDatabase.SaveAssets();
        }

        private static bool ValidateGoogleServices(bool showDialog)
        {
            string path = Path.Combine(Application.dataPath, "google-services.json");
            if (!File.Exists(path))
            {
                Report(false,
                    "No existe Assets/google-services.json. Descárgalo desde Firebase Console para el paquete " +
                    PackageId + ".", showDialog);
                return false;
            }

            string json = File.ReadAllText(path);
            string package = Capture(json, "\\\"package_name\\\"\\s*:\\s*\\\"([^\\\"]+)\\\"");
            string projectId = Capture(json, "\\\"project_id\\\"\\s*:\\s*\\\"([^\\\"]+)\\\"");
            string firebaseUrl = Capture(json, "\\\"firebase_url\\\"\\s*:\\s*\\\"([^\\\"]+)\\\"");

            bool packageMatches = string.Equals(package, PackageId, StringComparison.Ordinal);
            bool projectMatches = string.Equals(projectId, ExpectedDatabaseProject, StringComparison.OrdinalIgnoreCase) ||
                                  firebaseUrl.IndexOf(ExpectedDatabaseProject, StringComparison.OrdinalIgnoreCase) >= 0;

            if (!packageMatches || !projectMatches)
            {
                string message = "El google-services.json adjunto no corresponde a esta entrega. " +
                                 "Encontrado: paquete '" + package + "', proyecto '" + projectId + "'. " +
                                 "Esperado: paquete '" + PackageId + "' y proyecto de la base '" +
                                 ExpectedDatabaseProject + "'. Descarga el archivo correcto y reemplázalo sin cambiar su nombre.";
                Report(false, message, showDialog);
                return false;
            }

            Report(true, "google-services.json coincide con el paquete Android y la Realtime Database.", showDialog);
            return true;
        }

        private static string Capture(string input, string pattern)
        {
            Match match = Regex.Match(input, pattern);
            return match.Success ? match.Groups[1].Value : "no encontrado";
        }

        private static void Report(bool valid, string message, bool showDialog)
        {
            if (valid) Debug.Log("[SID2026] " + message);
            else Debug.LogError("[SID2026] " + message);

            if (showDialog)
            {
                EditorUtility.DisplayDialog(valid ? "Configuración válida" : "Configuración incompatible",
                    message, "Entendido");
            }
        }
    }
}
