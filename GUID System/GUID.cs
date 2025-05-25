using TriInspector;
using UnityEngine;

namespace EasyCS
{
    public class GUID : MonoBehaviour
    {
        [SerializeField, HideLabel]
        private ComponentGUID guid = new ComponentGUID();

        public ComponentGUID Guid => guid;
    }
}
