using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

using Object = UnityEngine.Object;

namespace UnityExtensions
{

    public static class MeshIntersectionTestGUI
    {

        [InitializeOnLoadMethod]
        private static void InitializeOnLoad()
        {
            SceneView.onSceneGUIDelegate += OnSceneGUI;
        }

        private static void OnSceneGUI(SceneView sceneView)
        {
            switch (sceneView.cameraMode.drawMode) {
                case DrawCameraMode.Wireframe:
                case DrawCameraMode.TexturedWire:
                    DrawMeshIntersectionGUI();
                    break;
                default: break;
            }
        }

        private static Ray MouseRay;
        private static MeshIntersection MouseOverIntersection;

        public static void DrawMeshIntersectionGUI()
        {
            var @event = Event.current;
            var mousePosition = @event.mousePosition;

            if (@event.type == EventType.MouseMove)
            {
                var mouseRay = HandleUtility.GUIPointToWorldRay(mousePosition);

                var meshRenderers =
                    Object
                    .FindObjectsOfType<MeshRenderer>()
                    .Where(IsVisibleMeshRenderer);

                MouseRay = mouseRay;
                MouseOverIntersection.Raycast(mouseRay, meshRenderers);

                SceneView.RepaintAll();
                return;
            }

            if (@event.type == EventType.Repaint)
            {
                var normal = MouseRay.direction;
                var position = MouseRay.origin + MouseRay.direction * 2f;
                var size = HandleUtility.GetHandleSize(position) / 4f;
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

                    Handles.color = Color.red;
                    Handles.DrawSolidDisc(v0, normal, size / 2);

                    Handles.color = Color.green;
                    Handles.DrawSolidDisc(v1, normal, size / 2);

                    Handles.color = Color.Lerp(Color.blue, Color.cyan, 0.5f);
                    Handles.DrawSolidDisc(v2, normal, size / 2);

                    Handles.color = Color.Lerp(Color.yellow, Color.clear, 0.25f);
                    Handles.DrawAAConvexPolygon(v0, v1, v2);

                    var p0 = MouseOverIntersection.position;
                    var n = MouseOverIntersection.normal;
                    var p1 = p0 + n * size * 10;
                    Handles.color = Color.magenta;
                    Handles.DrawSolidDisc(p0, n, size);
                    Handles.DrawLine(p0, p1);

                    go = PrefabUtility.FindPrefabRoot(go);
                    Handles.BeginGUI();
                    GUILabel(p1, go.name);
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