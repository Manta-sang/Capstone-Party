using Unity.Netcode;
using UnityEngine;

// Winners keep ordinary owner movement; the other contestants hold a fallen pose behind them.
public sealed class FinalCelebrationPlayer : MonoBehaviour
{
    public Vector2 walkableX, walkableZ;
    public bool IsFallen { get; private set; }
    Rigidbody body;
    NetworkObject player;
    PlayerMovement movement;
    Animator[] animators;
    Collider[] colliders;
    bool configured;
    Vector3 restingPosition;
    Quaternion restingRotation;

    void Awake()
    {
        body = GetComponent<Rigidbody>(); player = GetComponent<NetworkObject>();
        movement = GetComponent<PlayerMovement>();
        animators = GetComponentsInChildren<Animator>(true);
        colliders = GetComponentsInChildren<Collider>(true);
    }
    public void Configure(bool winner, Vector3 position, Quaternion rotation)
    {
        if (configured && IsFallen == !winner) return;
        bool wasFallen=configured&&IsFallen;
        configured = true; IsFallen = !winner;
        restingPosition = position; restingRotation = rotation;
        if (movement != null) movement.enabled = winner;
        foreach (var animator in animators) animator.enabled = winner;
        foreach (var collider in colliders) collider.enabled = winner;
        if (body != null)
        {
            if (!body.isKinematic) { body.velocity = Vector3.zero; body.angularVelocity = Vector3.zero; }
            body.isKinematic = !winner; body.useGravity = winner;
        }
        if (!winner || wasFallen) transform.SetPositionAndRotation(restingPosition, restingRotation);
    }
    void LateUpdate()
    {
        // The same pose is calculated on every peer. Keep network interpolation from raising a fallen avatar.
        if (configured && IsFallen) transform.SetPositionAndRotation(restingPosition, restingRotation);
    }
    void FixedUpdate()
    {
        if (!configured || IsFallen || body == null || player == null || !player.IsOwner) return;
        var p = body.position;
        var bounded = new Vector3(Mathf.Clamp(p.x, walkableX.x, walkableX.y), p.y, Mathf.Clamp(p.z, walkableZ.x, walkableZ.y));
        if (p.y < -3) { bounded.y = 1; body.velocity = Vector3.zero; }
        if (bounded != p) body.position = bounded;
    }
}
