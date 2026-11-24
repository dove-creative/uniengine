#if UNITY_2022_3_OR_NEWER
using System;
using U = UnityEngine;

namespace BlackThunder.UniEngine.SpatialSystem.Unity
{
    // Each host carries one collider of one world, and the relay is destroyed with the host.
    [U.AddComponentMenu("")]
    public class UnityCollisionRelay2D : U.MonoBehaviour
    {
        private UnitySpaceContext2D _owner;


        internal static Action Attach(U.GameObject host, UnitySpaceContext2D owner)
        {
            var relay = host.AddComponent<UnityCollisionRelay2D>();
            relay.hideFlags = U.HideFlags.HideInInspector;
            relay._owner = owner;
            return () => relay._owner = null;
        }

        private void OnTriggerEnter2D(U.Collider2D other) => _owner?.Trigger(gameObject, other, true);
        private void OnTriggerExit2D(U.Collider2D other) => _owner?.Trigger(gameObject, other, false);
        private void OnCollisionEnter2D(U.Collision2D collision) => Contact(collision, true);
        private void OnCollisionExit2D(U.Collision2D collision) => Contact(collision, false);

        private void Contact(U.Collision2D collision, bool entering) =>
            _owner?.Contact(gameObject, collision.collider, collision.otherCollider, entering);
    }
}
#endif
