using UnityEngine;

// Sits on the Player (CharacterController). Two jobs:
//
//   1. When the player's capsule touches a cube on the `Cubes` layer, give
//      the cube a small push in the direction you were moving. That's how
//      "walking my hand into a cube nudges it" works — the capsule includes
//      the head/hands region.
//
//   2. Keep `CharacterController.stepOffset` moderate so a low cube is
//      climbable (walk on top of it) but a mid-size prop isn't.
//
// Player↔Cubes collision is LEFT ON here (we don't call IgnoreLayerCollision)
// because the user wants pushing + climb-on-top behavior. The push force is
// small and cubes are damped so the old "ping-pong shove-away" doesn't
// happen — cubes just gently slide.
[RequireComponent(typeof(CharacterController))]
public class playerPushScript : MonoBehaviour
{
    [Header("Push tuning")]
    [Tooltip("How hard the player nudges a cube on contact. Small = gentle slide, big = kick.")]
    public float pushPower = 1.0f;

    [Tooltip("Only push things on these layers. Leave empty = push everything with a Rigidbody.")]
    public LayerMask pushableLayers = ~0;

    [Header("CharacterController tuning")]
    [Tooltip("How tall a step the player can walk up. 0.2 = low cubes are climbable, tall props are walls.")]
    public float stepOffset = 0.2f;

    void Awake()
    {
        var cc = GetComponent<CharacterController>();
        if (cc != null) cc.stepOffset = stepOffset;
    }

    private void OnControllerColliderHit(ControllerColliderHit hit)
    {
        var body = hit.collider.attachedRigidbody;
        if (body == null || body.isKinematic) return;
        if ((pushableLayers.value & (1 << hit.gameObject.layer)) == 0) return;

        // Ignore hits under the feet (walking on the floor / on top of a cube).
        if (hit.moveDirection.y < -0.3f) return;

        Vector3 pushDir = new Vector3(hit.moveDirection.x, 0, hit.moveDirection.z);
        body.AddForceAtPosition(pushDir * pushPower, hit.point, ForceMode.Impulse);
    }
}
