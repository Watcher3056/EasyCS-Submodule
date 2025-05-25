// #if UNITY_EDITOR && ODIN_INSPECTOR
// using Sirenix.OdinInspector.Editor;
// #else

using TriInspector;
using System;
using UnityEngine;

namespace EasyCS
{
    [Serializable, InlineProperty]
    public class InterfaceReference<TInterface> where TInterface : class
    {
        public TInterface Value
        {
            get { return _object as TInterface; }
            set
            {
                if (value is TInterface)
                    _object = value as UnityEngine.Object;
                else
                    this.LogError(MessageWrongInput);
            }
        }

        private const string MessageWrongInput = "Wrong input";

        [SerializeField, HideInInspector]
        private UnityEngine.Object _object;

#if UNITY_EDITOR
        [ShowInInspector]
        [HideLabel]
#if ODIN_INSPECTOR
        [LabelText("@_EditorGetLabelName()")]
#endif
        [ValidateInput("_EditorValidateInput")]
        [InfoBox("Required!", TriMessageType.Error, visibleIf: "ParentHasRequiredAttribute")]
        private UnityEngine.Object _EditorValue
        {
            get { return _object; }
            set { EditorSetValue(value); }
        }

        public void EditorSetValue(UnityEngine.Object value)
        {
            _object = GetValueFromInput(value) as UnityEngine.Object;
        }

// #if ODIN_INSPECTOR
//         private bool ParentHasRequiredAttribute(InspectorProperty property)
//         {
//             bool isRequired = property.Parent?.Attributes.HasAttribute<RequiredAttribute>() ?? false;
//             if (isRequired)
//             {
//                 if (_object == null)
//                     return true;
//                 else
//                     return false;
//             }
//             else
//                 return false;
//         }
// #else
        private bool ParentHasRequiredAttribute() => false;

// #endif
        private string _EditorGetLabelName() => typeof(TInterface).Name;

        private TriValidationResult _EditorValidateInput()
        {
            TInterface input = GetValueFromInput(_EditorValue);

            if (_EditorValue == null)
                return TriValidationResult.Valid;
            else if (input == null && _EditorValue != null)
            {
                _object = null;
                return TriValidationResult.Error("Wrong Input!");
            }
            else
            {
                _object = input as UnityEngine.Object;
                return TriValidationResult.Valid;
            }
        }

        private TInterface GetValueFromInput(UnityEngine.Object obj)
        {
            TInterface result = default;
            if (obj is GameObject)
            {
                GameObject go = (GameObject)obj;
                result = go.GetComponent<TInterface>();
            }
            else if (obj is Component)
            {
                result = obj as TInterface;
            }
            else if (obj is ScriptableObject)
            {
                result = obj as TInterface;
            }

            return result;
        }
#endif
    }
}