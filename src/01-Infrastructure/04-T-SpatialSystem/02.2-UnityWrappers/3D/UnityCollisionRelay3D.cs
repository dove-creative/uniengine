#if UNITY_2022_3_OR_NEWER
using System;
using U = UnityEngine;

namespace BlackThunder.UniEngine.SpatialSystem.Unity
{
    // Each host carries one collider of one world, and the relay is destroyed with the host.
    [U.AddComponentMenu("")]
    public class UnityCollisionRelay3D : U.MonoBehaviour
    {
        private UnitySpaceContext3D _owner;


        internal static Action Attach(U.GameObject host, UnitySpaceContext3D owner)
        {
            var relay = host.AddComponent<UnityCollisionRelay3D>();
            relay.hideFlags = U.HideFlags.HideInInspector;
            relay._owner = owner;
            return () => relay._owner = null;
        }

        private void OnTriggerEnter(U.Collider other) => _owner?.Trigger(gameObject, other, true);
        private void OnTriggerExit(U.Collider other) => _owner?.Trigger(gameObject, other, false);
    }
}
#endif
