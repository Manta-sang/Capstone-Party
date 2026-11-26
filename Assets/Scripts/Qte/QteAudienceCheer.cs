using UnityEngine;

/// <summary>Starts a scene-only crowd animation with a different phase for each spectator.</summary>
public sealed class QteAudienceCheer : MonoBehaviour
{
    public Animator animator;
    [Range(0, 1)] public float phase;
    [Range(.5f, 1.5f)] public float playbackSpeed = 1;
    private bool started;
    private static readonly int CheerState = Animator.StringToHash("Cheer");

    private void Start() { Begin(); }
    public void Begin()
    {
        if (started || animator == null || !animator.isActiveAndEnabled) return;
        started = true;
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
        animator.speed = playbackSpeed;
        animator.Play(CheerState, 0, phase);
    }
}
