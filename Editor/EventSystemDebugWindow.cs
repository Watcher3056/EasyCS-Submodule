using EasyCS.EventSystem;
using UnityEditor;
using UnityEngine;

namespace EasyCS.Editor
{
    public class EventSystemDebugWindow : EditorWindow
    {
        private Vector2 _scroll;
        private IEventSystem _eventSystem;

        //[MenuItem("EasyCS/Event Debugger")]
        public static void ShowWindow()
        {
            var window = GetWindow<EventSystemDebugWindow>("Event Debugger");
            window.Show();
        }

        private void OnEnable()
        {
            _eventSystem = new DefaultEventSystem(); // Replace with your global instance if needed
        }

        private void OnGUI()
        {
            if (_eventSystem == null)
            {
                EditorGUILayout.HelpBox("EventSystem is null.", MessageType.Warning);
                return;
            }

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            GUILayout.Label("Event Subscriptions", EditorStyles.boldLabel);

            var map = _eventSystem.GetSubscriberMap();

            foreach (var entry in map)
            {
                GUILayout.Space(5);
                GUILayout.Label(entry.Key.ToString(), EditorStyles.largeLabel);

                foreach (var listener in entry.Value)
                {
                    bool isOneShot = false;
                    string label = isOneShot ? "    ⚡ " : "    - ";
                    label += listener.GetType().Name;
                    GUILayout.Label(label);
                }
            }

            EditorGUILayout.EndScrollView();
        }
    }
}
