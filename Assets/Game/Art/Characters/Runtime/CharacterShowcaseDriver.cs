using UnityEngine;

/// <summary>Cycles a visual-only demo through in-place locomotion and jump states.</summary>
public sealed class CharacterShowcaseDriver : MonoBehaviour
{
    [SerializeField] private Animator movingAnimator;
    [SerializeField] private float cycleSeconds = 8f;
    private float elapsed;

    private void Update()
    {
        if (movingAnimator == null) return;
        elapsed += Time.deltaTime;
        var phase = elapsed % cycleSeconds;
        movingAnimator.SetFloat("MoveSpeed", phase > 2f && phase < 5.5f ? 1f : 0f);
        if (phase > 6f && phase < 6f + Time.deltaTime) movingAnimator.SetTrigger("Action");
    }
}
