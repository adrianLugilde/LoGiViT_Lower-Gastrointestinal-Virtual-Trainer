using UnityEngine;

namespace CustomUI
{
    [CreateAssetMenu(menuName = "LoGiViT/UI/XR Keybind Display Profile")]
    public class XRKeybindDisplayProfile : ScriptableObject
    {
        public XRControllerKeybindProfile leftControllerProfile;
        public XRControllerKeybindProfile rightControllerProfile;
    }
}