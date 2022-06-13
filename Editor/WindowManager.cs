using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor;
using UnityEditor.SceneManagement;
using System;

namespace Reallusion.Import
{
    [InitializeOnLoad]
    public static class WindowManager
    {
        public static Scene currentScene;
        public static Scene previewSceneHandle;
        public static PreviewScene previewScene;
        public static bool openedInPreviewScene;
        public static bool showPlayer = true;
        public static bool showRetarget = false;
        
        static WindowManager()  
        {
            // Even if update is not the most elegant. Using hierarchyWindowChanged for CPU sake will not work in all cases, because when hierarchyWindowChanged is called, Time's values might be all higher than current values. Why? Because current values are set at the first frame. If you keep reloading the same scene, this case happens.
            EditorApplication.update += WindowManager.MonitorScene;
            showPlayer = Importer.ANIMPLAYER_ON_BY_DEFAULT;
            currentScene = EditorSceneManager.GetActiveScene();

            previewScene = PreviewScene.FetchPreviewScene(currentScene);
            if (previewScene.IsValidPreviewScene)
            {
                openedInPreviewScene = true;
                previewSceneHandle = currentScene;
            }
            else
            {
                previewScene = default;
                previewSceneHandle = default;
            }            
        }        

        public static PreviewScene OpenPreviewScene(GameObject prefab)
        {
            if (!prefab) return default;
            if (!IsPreviewScene && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return default;

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
            GameObject.Instantiate(Util.FindPreviewScenePrefab(), Vector3.zero, Quaternion.identity);

            previewSceneHandle = scene;
            previewScene = PreviewScene.FetchPreviewScene(scene);

            previewScene.PostProcessingAndLighting();
            previewScene.ShowPreviewCharacter(prefab);
            
            return previewScene;
        }        

        public static bool IsPreviewScene
        {
            get { return (EditorSceneManager.GetActiveScene() == previewSceneHandle && previewScene.IsValidPreviewScene); }
        }

        public static PreviewScene GetPreviewScene() 
        {
            if (IsPreviewScene) 
            {
                return previewScene;
            }            

            return default;
        }
                
        private static void MonitorScene() 
        {
            Scene activeScene = EditorSceneManager.GetActiveScene();
            if (currentScene != activeScene)
            {
                currentScene = activeScene;                
                previewScene = GetPreviewScene();
            }

            bool isPlayerShown = AnimPlayerGUI.IsPlayerShown();            

            if (IsPreviewScene) 
            {                
                if (showPlayer && !isPlayerShown)
                {
                    ShowAnimationPlayer();                    
                }
                else if (!showPlayer && isPlayerShown)
                {
                    HideAnimationPlayer(false);
                }
            }
            else if (openedInPreviewScene)
            {
                if (isPlayerShown)
                {
                    HideAnimationPlayer(false);                    
                }
            }
        }

        public static void DoSceneViewOrbit()
        {
            if (!isSceneViewOrbit)
            {
                trackTarget = null;
                EditorApplication.update += SceneViewOrbitUpdate;
                isSceneViewOrbit = true;
            }
            else
            {
                trackTarget = null;
                EditorApplication.update -= SceneViewOrbitUpdate;
                isSceneViewOrbit = false;
            }
        }

        public static void StopSceneViewOrbit()
        {
            if (isSceneViewOrbit)
            {
                EditorApplication.update -= SceneViewOrbitUpdate;
                trackTarget = null;
                isSceneViewOrbit = true;
            }
        }

        public static void DoSceneViewOrbitTracking()
        {            
            if (!isSceneViewOrbit)
            {
                trackTarget = Selection.activeTransform;
                if (trackTarget)
                {
                    pivotDisplacement = SceneView.lastActiveSceneView.pivot - trackTarget.position;
                    EditorApplication.update += SceneViewOrbitUpdate;
                    isSceneViewOrbit = true;
                }
            }
            else
            {
                EditorApplication.update -= SceneViewOrbitUpdate;
                isSceneViewOrbit = false;
            }

        }

        private static bool isSceneViewOrbit;
        private static Vector3 pivotDisplacement;
        private static Transform trackTarget;

        static void SceneViewOrbitUpdate()
        {
            if (isSceneViewOrbit)
            {
                SceneView scene = SceneView.lastActiveSceneView;
                Vector3 pivot = scene.pivot;
                Vector3 pos = scene.camera.transform.position;
                Vector3 boom = pos - pivot;
                if (trackTarget)
                {
                    pivot = trackTarget.position + pivotDisplacement;
                }
                float fov = scene.camera.fieldOfView;
                float dist = scene.cameraDistance;
                float size = Mathf.Sin(Mathf.Deg2Rad * fov / 2f) * dist;

                boom = Quaternion.AngleAxis(0.1f, Vector3.up) * boom;

                SceneView.lastActiveSceneView.LookAtDirect(pivot, Quaternion.LookRotation(-boom, Vector3.up), size);
                SceneView.RepaintAll();
            }
        }

        public static void DoMatchSceneCameraOnce()
        {
            if (isMatchSceneViewCamera) StopMatchSceneCamera();

            isMatchSceneViewCamera = true;
            MatchSceneCameraUpdate();
            isMatchSceneViewCamera = false;
        }

        public static void DoMatchSceneCamera()
        {
            if (!isMatchSceneViewCamera)
            {
                EditorApplication.update += MatchSceneCameraUpdate;
                isMatchSceneViewCamera = true;
            }
            else
            {
                EditorApplication.update -= MatchSceneCameraUpdate;
                isMatchSceneViewCamera = false;
            }
        }

        public static void DoSceneToggleOffAll()
        {
            if (isMatchSceneViewCamera)
            {
                EditorApplication.update -= MatchSceneCameraUpdate;
                isMatchSceneViewCamera = false;
            }

            if (isSceneViewOrbit)
            {                
                EditorApplication.update -= SceneViewOrbitUpdate;
                isSceneViewOrbit = false;
                trackTarget = null;
            }
        }

        public static void StopMatchSceneCamera()
        {
            if (isSceneViewOrbit)
            {
                EditorApplication.update -= MatchSceneCameraUpdate;
                isMatchSceneViewCamera = true;
            }
        }        

        private static bool isMatchSceneViewCamera;
        
        static void MatchSceneCameraUpdate()
        {
            if (isMatchSceneViewCamera)
            {
                Transform cam = previewScene.GetCamera();
                SceneView scene = SceneView.lastActiveSceneView;                
                Vector3 pos = scene.camera.transform.position;
                Quaternion rot = scene.camera.transform.rotation;
                float fov = scene.camera.fieldOfView;                
                cam.position = pos;
                cam.rotation = rot;
                Camera camera = cam.GetComponent<Camera>();
                if (camera)
                {
                    camera.fieldOfView = fov;
                }
            }
        }

        public static void TakeScreenShot()
        {
            string dateStamp = DateTime.Now.ToString("yyMMdd-hhmmss");
            string fileName = "Screenshot-" + dateStamp + ".png";
            Debug.Log("Saving screenshot to: " + fileName);
            ScreenCapture.CaptureScreenshot(fileName);
        }

        public static void ShowAnimationPlayer()
        {
            GameObject currentCharacterFbx;

            if (IsPreviewScene)
                currentCharacterFbx = GetPreviewScene().GetPreviewCharacter();
            else
                currentCharacterFbx = Selection.activeGameObject;

            AnimPlayerGUI.OpenPlayer(currentCharacterFbx);
            openedInPreviewScene = IsPreviewScene;

            if (showRetarget) ShowAnimationRetargeter();

            showPlayer = true;
        }

        public static void HideAnimationPlayer(bool updateShowPlayer)
        {
            if (AnimPlayerGUI.IsPlayerShown()) AnimPlayerGUI.ResetFace();

            AnimPlayerGUI.ClosePlayer();

            HideAnimationRetargeter(false);

            if (updateShowPlayer)
                showPlayer = false;
        }

        public static void ShowAnimationRetargeter()
        {
            if (AnimPlayerGUI.IsPlayerShown())
            {
                AnimRetargetGUI.OpenRetargeter();
                showRetarget = true;
            }
        }

        public static void HideAnimationRetargeter(bool updateShowRetarget)
        {
            AnimRetargetGUI.CloseRetargeter();
            
            if (updateShowRetarget)
                showRetarget = false;
        }        
    }
}
