using UnityEngine;

namespace EasyCS
{
    [ExecuteAlways]
    public class EntityEditorComponentFlag : MonoBehaviour
    {
        private void OnTransformChildrenChanged()
        {
            foreach (Transform child in transform)
                child.gameObject.TryGetElseSetComponent<Actor>();
        }
    }
}
