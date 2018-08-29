using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

using Object = UnityEngine.Object;

namespace UnityExtensions
{

    public static class MeshIntersectionSceneGUI
    {

        private static MeshIntersectionRectSelection m_RectSelection;

        [InitializeOnLoadMethod]
        private static void InitializeOnLoad()
        {
            m_RectSelection = new MeshIntersectionRectSelection();
            SceneView.onSceneGUIDelegate += OnSceneGUI;
        }

        private const string EnableMeshPickerKey =
            "UnityExtensions.MeshIntersection.EnableMeshPicker";
        private static bool? EnableMeshPickerCached = null;
        private static bool EnableMeshPicker
        {
            get
            {
                if (EnableMeshPickerCached == null)
                    EnableMeshPickerCached =
                        EditorPrefs.GetBool(
                            EnableMeshPickerKey,
                            false);

                return EnableMeshPickerCached.Value;
            }
            set
            {
                EnableMeshPickerCached = value;
                EditorPrefs.SetBool(EnableMeshPickerKey, value);
            }
        }

        private static GUIContent MeshPickerLabel = null;

        private static void OnSceneGUI(SceneView sceneView)
        {
            if (MeshPickerLabel == null)
                MeshPickerLabel = new GUIContent(
                    text: " Pixel Select",
                    tooltip:
                        " Enable pixel select mode to select \n"+
                        " visible geometry only. \n\n"+
                        " NOTE: You will not be able to click on \n"+
                        " a nav mesh when pixel select mode is \n"+
                        " enabled. "
                );
            Handles.BeginGUI();
            {
                var rect = sceneView.camera.pixelRect;
                var pixelsPerPoint = EditorGUIUtility.pixelsPerPoint;
                rect.width /= pixelsPerPoint;
                rect.height /= pixelsPerPoint;
                rect.xMax -= 6;
                rect.xMin = rect.xMax - 90;
                rect.y += 118;
                rect.height = EditorGUIUtility.singleLineHeight;
                if (Event.current.type == EventType.Repaint)
                {
                    var rect2 = rect;
                    rect2.yMin -= 1;
                    rect2.yMax += 1;
                    var style = EditorStyles.helpBox;
                    style.Draw(rect2, false, false, false, false);
                    style.Draw(rect2, false, false, false, false);
                }
                rect.x += 2;
                EnableMeshPicker =
                    EditorGUI.ToggleLeft(rect, MeshPickerLabel, EnableMeshPicker);
            }
            Handles.EndGUI();

            switch (sceneView.cameraMode.drawMode) {
                case DrawCameraMode.Wireframe:
                case DrawCameraMode.TexturedWire:
                    DrawMeshIntersectionGUI();
                    break;
                default: break;
            }

            if (EnableMeshPicker)
                DoMeshPickerGUI();
        }

        private static Ray MouseRay;
        private static MeshIntersection MouseOverIntersection;

        private static bool GUIPointToWorldRay(Vector2 position, out Ray ray)
        {
            try {
                ray = HandleUtility.GUIPointToWorldRay(position);
                return true;
            } catch {
                // Screen position out of view frustum
                ray = default(Ray);
                return false;
            }
        }

        public static void DoMeshPickerGUI()
        {
            m_RectSelection.OnGUI();
        }

        public static void DrawMeshIntersectionGUI()
        {
            var @event = Event.current;
            var mousePosition = @event.mousePosition;

            if (@event.type == EventType.MouseMove)
            {
                var mouseRay = default(Ray);
                if (GUIPointToWorldRay(mousePosition, out mouseRay))
                {
                    var meshRenderers =
                        Object
                        .FindObjectsOfType<MeshRenderer>()
                        .Where(IsVisibleMeshRenderer);

                    MouseRay = mouseRay;
                    MouseOverIntersection.Raycast(mouseRay, meshRenderers);
                }
                SceneView.RepaintAll();
                return;
            }

            if (@event.type == EventType.Repaint)
            {
                var normal = MouseRay.direction;
                var position = MouseRay.origin + MouseRay.direction * 2f;
                var size = HandleUtility.GetHandleSize(position) / 20f;
                Handles.color = Color.black;

                if (MouseOverIntersection.found)
                {
                    var go = MouseOverIntersection.gameObject;
                    var meshRenderer = go.GetComponent<MeshRenderer>();
                    var bounds = meshRenderer.bounds;
                    var c = bounds.center;
                    Handles.color = Color.magenta;
                    Handles.DrawWireCube(c, bounds.size);

                    var v0 = MouseOverIntersection.vertex0;
                    var v1 = MouseOverIntersection.vertex1;
                    var v2 = MouseOverIntersection.vertex2;

                    var vsize = size / 4;

                    Handles.color = Color.red;
                    Handles.DrawSolidDisc(v0, normal, vsize);

                    Handles.color = Color.green;
                    Handles.DrawSolidDisc(v1, normal, vsize);

                    Handles.color = Color.Lerp(Color.blue, Color.cyan, 0.5f);
                    Handles.DrawSolidDisc(v2, normal, vsize);

                    Handles.color = Color.Lerp(Color.yellow, Color.clear, 0.5f);
                    Handles.DrawAAConvexPolygon(v0, v1, v2);

                    var p0 = MouseOverIntersection.position;
                    var n = MouseOverIntersection.normal;
                    var p1 = p0 + n * size * 10;
                    var psize = size / 2;
                    Handles.color = Color.magenta;
                    Handles.DrawSolidDisc(p0, n, psize);
                    Handles.DrawLine(p0, p1);

                    go = PrefabUtility.FindPrefabRoot(go);
                    Handles.BeginGUI();
                    var labelPosition = mousePosition;
                    labelPosition.y -= 16;
                    GUILabel(labelPosition, go.name);
                    Handles.EndGUI();
                }
            }
        }

        private static bool IsNavMeshRenderer(MeshRenderer meshRenderer)
        {
            var material = meshRenderer.sharedMaterial;
            var materialName = material?.name ?? string.Empty;
            return materialName.Contains("navmesh_mat");
        }

        private static bool IsVisibleMeshRenderer(MeshRenderer meshRenderer)
        {
            if (!meshRenderer.enabled)
                return false;
            if (!meshRenderer.isVisible)
                return false;
            return !IsNavMeshRenderer(meshRenderer);
        }

        private static GUIContent TempContent = new GUIContent();

        private static void
        GUILabel(Vector2 center, string label, GUIStyle style = null)
        {
            if (style == null)
                style = EditorStyles.helpBox;
            var content = TempContent;
            content.text = label;
            var size = style.CalcSize(content);
            var origin = center - size / 2;
            var rect = new Rect(origin, size);
            rect.width += 4;
            GUI.Label(rect, label, style);
        }

        private static void
        GUILabel(Vector3 center, string label, GUIStyle style = null)
        {
            GUILabel(GUIPoint(center), label, style);
        }

        private static Vector2
        GUIPoint(Vector3 worldPoint)
        {
            return HandleUtility.WorldToGUIPoint(worldPoint);
        }

        private static Vector3
        GUIPoint3(Vector3 worldPoint)
        {
            var p = GUIPoint(worldPoint);
            return new Vector3(p.x, p.y);
        }

    }

}