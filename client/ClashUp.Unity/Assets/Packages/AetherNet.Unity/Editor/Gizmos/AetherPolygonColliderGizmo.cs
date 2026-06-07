using UnityEditor;
using UnityEngine;

namespace AetherNet.Editor
{
    [CustomEditor(typeof(AetherPolygonCollider))]
    [CanEditMultipleObjects]
    public sealed class AetherPolygonColliderGizmo : UnityEditor.Editor
    {
        private void OnSceneGUI()
        {
            var col = (AetherPolygonCollider)target;
            var so  = new SerializedObject(col);

            var  verts     = so.FindProperty("_vertices");
            bool isTrigger = so.FindProperty("_isTrigger").boolValue;
            int  count     = verts.arraySize;
            if (count < 2) return;

            Handles.color = isTrigger
                ? new Color(0.4f, 0.7f, 1f, 0.9f)
                : new Color(0.1f, 0.9f, 0.1f, 0.9f);

            Transform tf = col.transform;
            for (int i = 0; i < count; i++)
            {
                Vector2 a = verts.GetArrayElementAtIndex(i).vector2Value;
                Vector2 b = verts.GetArrayElementAtIndex((i + 1) % count).vector2Value;
                Handles.DrawLine(
                    tf.TransformPoint(GizmoHelper.To3D(a)),
                    tf.TransformPoint(GizmoHelper.To3D(b)));
            }
        }
    }
}
