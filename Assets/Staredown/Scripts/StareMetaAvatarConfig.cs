using UnityEngine;

namespace Staredown
{
    [CreateAssetMenu(menuName = "Staredown/Meta Avatar Configuration")]
    public sealed class StareMetaAvatarConfig : ScriptableObject
    {
        [Tooltip("Public Quest application ID. Never put an app secret or access token here.")]
        public string MetaAppId = "";
        public GameObject ManagerPrefab;
    }
}
