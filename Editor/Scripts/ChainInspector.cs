using UnityEditor;
using UnityEngine;

namespace Preliy.Chain.Editor
{
    [CustomEditor(typeof(Chain))]
    [CanEditMultipleObjects]
    public class ChainInspector : UnityEditor.Editor 
    {
        private Chain _chain;

        private void OnEnable()
        {
            _chain = target as Chain;
        }

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            if (GUILayout.Button("Instantiate Prefabs")) _chain.InstantiatePrefabs();
            if (GUILayout.Button("Destroy Prefabs")) _chain.DestroyPrefabs();
        }
    }
}
