using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

using Object = UnityEngine.Object;

namespace UnityExtensions
{

    internal class MeshIntersectionRectSelection
    {
        Vector2 m_SelectStartPoint;
        Vector2 m_SelectMousePoint;
        Object[] m_SelectionStart;
        bool m_RectSelecting;
        Dictionary<GameObject, bool> m_LastSelection;
        enum SelectionType { Normal, Additive, Subtractive }
        Object[] m_CurrentSelection = null;

        internal static Rect FromToRect(Vector2 start, Vector2 end)
        {
            Rect r = new Rect(start.x, start.y, end.x - start.x, end.y - start.y);
            if (r.width < 0)
            {
                r.x += r.width;
                r.width = -r.width;
            }
            if (r.height < 0)
            {
                r.y += r.height;
                r.height = -r.height;
            }
            return r;
        }

        internal static bool viewToolActive
        {
            get
            {
                // if (GUIUtility.hotControl != 0 && s_LockedViewTool == ViewTool.None)
                    return false;

                // return s_LockedViewTool != ViewTool.None || (current == 0) || Event.current.alt || (s_ButtonDown == 1) || (s_ButtonDown == 2);
            }
        }

        static GUIStyle selectionRect = null;

        public MeshIntersectionRectSelection() { }

        public void OnGUI()
        {
            if (selectionRect == null)
                selectionRect = new GUIStyle("SelectionRect");

            Event evt = Event.current;

            Handles.BeginGUI();

            Vector2 mousePos = evt.mousePosition;
            int id = GUIUtility.GetControlID(FocusType.Keyboard);

            switch (evt.GetTypeForControl(id))
            {
                case EventType.Layout:
                    if (!viewToolActive)
                        HandleUtility.AddDefaultControl(id);
                    break;

                case EventType.MouseDown:
                    if (HandleUtility.nearestControl == id && evt.button == 0)
                    {
                        GUIUtility.hotControl = id;
                        m_SelectStartPoint = mousePos;
                        m_SelectionStart = Selection.objects;
                        m_RectSelecting = false;
                    }
                    break;
                case EventType.MouseDrag:
                    if (GUIUtility.hotControl == id)
                    {
                        if (!m_RectSelecting && (mousePos - m_SelectStartPoint).magnitude > 6f)
                        {
                            EditorApplication.modifierKeysChanged += SendCommandsOnModifierKeys;
                            m_RectSelecting = true;
                            m_LastSelection = null;
                            m_CurrentSelection = null;
                        }
                        if (m_RectSelecting)
                        {
                            m_SelectMousePoint = new Vector2(Mathf.Max(mousePos.x, 0), Mathf.Max(mousePos.y, 0));
                            GameObject[] rectObjs = HandleUtility.PickRectObjects(FromToRect(m_SelectStartPoint, m_SelectMousePoint));
                            m_CurrentSelection = rectObjs;
                            bool setIt = false;
                            if (m_LastSelection == null)
                            {
                                m_LastSelection = new Dictionary<GameObject, bool>();
                                setIt = true;
                            }
                            setIt |= m_LastSelection.Count != rectObjs.Length;
                            if (!setIt)
                            {
                                Dictionary<GameObject, bool> set = new Dictionary<GameObject, bool>(rectObjs.Length);
                                foreach (GameObject g in rectObjs)
                                    set.Add(g, false);
                                foreach (GameObject g in m_LastSelection.Keys)
                                {
                                    if (!set.ContainsKey(g))
                                    {
                                        setIt = true;
                                        break;
                                    }
                                }
                            }
                            if (setIt)
                            {
                                m_LastSelection = new Dictionary<GameObject, bool>(rectObjs.Length);
                                foreach (GameObject g in rectObjs)
                                    m_LastSelection.Add(g, false);
                                if (rectObjs != null)
                                {
                                    if (evt.shift)
                                        UpdateSelection(m_SelectionStart, rectObjs, SelectionType.Additive, m_RectSelecting);
                                    else if (EditorGUI.actionKey)
                                        UpdateSelection(m_SelectionStart, rectObjs, SelectionType.Subtractive, m_RectSelecting);
                                    else
                                        UpdateSelection(m_SelectionStart, rectObjs, SelectionType.Normal, m_RectSelecting);
                                }
                            }
                        }
                        evt.Use();
                    }
                    break;

                case EventType.Repaint:
                    if (GUIUtility.hotControl == id && m_RectSelecting)
                        selectionRect.Draw(FromToRect(m_SelectStartPoint, m_SelectMousePoint), GUIContent.none, false, false, false, false);
                    break;

                case EventType.MouseUp:
                    if (GUIUtility.hotControl == id && evt.button == 0)
                    {
                        GUIUtility.hotControl = 0;
                        if (m_RectSelecting)
                        {
                            EditorApplication.modifierKeysChanged -= SendCommandsOnModifierKeys;
                            m_RectSelecting = false;
                            m_SelectionStart = new Object[0];
                            evt.Use();
                        }
                        else
                        {
                            if (evt.shift || EditorGUI.actionKey)
                            {
                                // For shift, we check if EXACTLY the active GO is hovered by mouse and then subtract. Otherwise additive.
                                // For control/cmd, we check if ANY of the selected GO is hovered by mouse and then subtract. Otherwise additive.
                                // Control/cmd takes priority over shift.
                                GameObject hovered = PickGameObject(evt.mousePosition, false);
                                if (EditorGUI.actionKey ? Selection.gameObjects.Contains(hovered) : Selection.activeGameObject == hovered)
                                    UpdateSelection(m_SelectionStart, hovered, SelectionType.Subtractive, m_RectSelecting);
                                else
                                    UpdateSelection(m_SelectionStart, PickGameObject(evt.mousePosition, true), SelectionType.Additive, m_RectSelecting);
                            }
                            else
                            {
                                GameObject picked = PickGameObject(evt.mousePosition, true);
                                UpdateSelection(m_SelectionStart, picked, SelectionType.Normal, m_RectSelecting);
                            }

                            evt.Use();
                        }
                    }
                    break;
                case EventType.ExecuteCommand:
                    if (id == GUIUtility.hotControl && evt.commandName == "ModifierKeysChanged")
                    {
                        if (evt.shift)
                            UpdateSelection(m_SelectionStart, m_CurrentSelection, SelectionType.Additive, m_RectSelecting);
                        else if (EditorGUI.actionKey)
                            UpdateSelection(m_SelectionStart, m_CurrentSelection, SelectionType.Subtractive, m_RectSelecting);
                        else
                            UpdateSelection(m_SelectionStart, m_CurrentSelection, SelectionType.Normal, m_RectSelecting);
                        evt.Use();
                    }
                    break;
            }

            Handles.EndGUI();
        }

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

        private static GameObject PickGameObject(Vector2 position, bool selectPrefabRoot)
        {
            var mouseRay = default(Ray);
            var gameObject = default(GameObject);
            if (GUIPointToWorldRay(position, out mouseRay))
            {
                var meshRenderers =
                    Object
                    .FindObjectsOfType<MeshRenderer>()
                    .Where(IsVisibleMeshRenderer);

                var meshIntersection = default(MeshIntersection);

                meshIntersection.Raycast(mouseRay, meshRenderers);

                gameObject = meshIntersection.gameObject;
                if (gameObject != null && selectPrefabRoot)
                    gameObject = FindSelectionBase(gameObject) ?? gameObject;
            }
            return gameObject;
        }

        internal static GameObject FindSelectionBase(GameObject go)
        {
            if (go == null)
                return null;

            // Find prefab based base
            Transform prefabBase = null;
            PrefabType pickedType = PrefabUtility.GetPrefabType(go);
            if (pickedType == PrefabType.PrefabInstance || pickedType == PrefabType.ModelPrefabInstance)
            {
                prefabBase = PrefabUtility.FindPrefabRoot(go).transform;
            }

            // Find attribute based base
            Transform tr = go.transform;
            while (tr != null)
            {
                // If we come across the prefab base, no need to search further down.
                if (tr == prefabBase)
                    return tr.gameObject;

                // If this one has the attribute, return this one.
                if (GameObjectContainsAttribute<SelectionBaseAttribute>(tr.gameObject))
                    return tr.gameObject;

                tr = tr.parent;
            }

            // There is neither a prefab or attribute based selection root, so return null
            return null;
        }

        internal static bool GameObjectContainsAttribute<T>(GameObject go) where T : Attribute
        {
            var behaviours = go.GetComponents(typeof(Component));
            for (var index = 0; index < behaviours.Length; index++)
            {
                var behaviour = behaviours[index];
                if (behaviour == null)
                    continue;

                var behaviourType = behaviour.GetType();
                if (behaviourType.GetCustomAttributes(typeof(T), true).Length > 0)
                    return true;
            }
            return false;
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

        private static void UpdateSelection(Object[] existingSelection, Object newObject, SelectionType type, bool isRectSelection)
        {
            Object[] objs;
            if (newObject == null)
            {
                objs = new Object[0];
            }
            else
            {
                objs = new Object[1];
                objs[0] = newObject;
            }

            UpdateSelection(existingSelection, objs, type, isRectSelection);
        }

        private static void UpdateSelection(Object[] existingSelection, Object[] newObjects, SelectionType type, bool isRectSelection)
        {
            Object[] newSelection;
            switch (type)
            {
                case SelectionType.Additive:
                    if (newObjects.Length > 0)
                    {
                        newSelection = new Object[existingSelection.Length + newObjects.Length];
                        System.Array.Copy(existingSelection, newSelection, existingSelection.Length);
                        for (int i = 0; i < newObjects.Length; i++)
                            newSelection[existingSelection.Length + i] = newObjects[i];
                        if (!isRectSelection)
                            Selection.activeObject = newObjects[0];
                        else
                            Selection.activeObject = newSelection[0];

                        Selection.objects = newSelection;
                    }
                    else
                    {
                        Selection.objects = existingSelection;
                    }
                    break;
                case SelectionType.Subtractive:
                    Dictionary<Object, bool> set = new Dictionary<Object, bool>(existingSelection.Length);
                    foreach (Object g in existingSelection)
                        set.Add(g, false);
                    foreach (Object g in newObjects)
                    {
                        if (set.ContainsKey(g))
                            set.Remove(g);
                    }
                    newSelection = new Object[set.Keys.Count];
                    set.Keys.CopyTo(newSelection, 0);
                    Selection.objects = newSelection;
                    break;
                case SelectionType.Normal:
                default:
                    Selection.objects = newObjects;
                    break;
            }
        }

        // When rect selecting, we update the selected objects based on which modifier keys are currently held down,
        // so the window needs to repaint.
        internal void SendCommandsOnModifierKeys()
        {
            var window = SceneView.currentDrawingSceneView;
            var @event = EditorGUIUtility.CommandEvent("ModifierKeysChanged");
            window.SendEvent(@event);
        }
    }

}