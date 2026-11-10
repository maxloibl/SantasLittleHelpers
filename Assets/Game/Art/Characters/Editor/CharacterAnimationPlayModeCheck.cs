using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class CharacterAnimationPlayModeCheck
{
    private const string RunKey = "CharacterAnimationPlayModeCheck.Run";
    private const string ResultKey = "CharacterAnimationPlayModeCheck.Result";
    private static readonly int IdleState = Animator.StringToHash("Base Layer.Idle");
    private static readonly int MoveState = Animator.StringToHash("Base Layer.Move");
    private static readonly int JumpState = Animator.StringToHash("Base Layer.Jump");

    private enum Phase { Idle, Move, Jump, ReturnToIdle }
    private static Animator animator;
    private static Phase phase;
    private static float phaseStarted;
    private static Vector3 rootPosition;
    private static Quaternion[] idlePose;
    private static float movePoseDelta;
    private static float jumpPoseDelta;
    private static bool sawIdle;
    private static bool sawMove;
    private static bool sawJump;

    static CharacterAnimationPlayModeCheck()
    {
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    public static void Run()
    {
        SessionState.SetBool(RunKey, true);
        SessionState.SetBool(ResultKey, false);
        EditorSceneManager.OpenScene("Assets/Game/Art/Characters/Scenes/CharacterShowcase.unity");
        EditorApplication.EnterPlaymode();
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(RunKey, false)) return;
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            animator = GameObject.Find("Elf (Idle to Move to Action)")?.GetComponentInChildren<Animator>();
            if (animator == null)
            {
                Finish(false, "showcase Animator not found");
                return;
            }
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.Rebind();
            rootPosition = animator.transform.position;
            animator.ResetTrigger("Action");
            animator.SetFloat("MoveSpeed", 0f);
            Debug.Log($"CHARPLAYMODE animator enabled={animator.enabled} avatarHuman={animator.avatar != null && animator.avatar.isHuman} controller={animator.runtimeAnimatorController.name} humanBones={CapturePose().Length}");
            phase = Phase.Idle;
            phaseStarted = Time.time;
            EditorApplication.update += UpdateCheck;
        }
        else if (state == PlayModeStateChange.EnteredEditMode)
        {
            EditorApplication.update -= UpdateCheck;
            var passed = SessionState.GetBool(ResultKey, false);
            SessionState.SetBool(RunKey, false);
            Debug.Log($"CHARPLAYMODE result={(passed ? "PASS" : "FAIL")}");
            EditorApplication.Exit(passed ? 0 : 1);
        }
    }

    private static void UpdateCheck()
    {
        if (animator == null || Time.time - phaseStarted > 5f)
        {
            Finish(false, "timed out before observing all states");
            return;
        }

        var state = animator.GetCurrentAnimatorStateInfo(0).fullPathHash;
        switch (phase)
        {
            case Phase.Idle when state == IdleState:
                sawIdle = true;
                idlePose = CapturePose();
                animator.SetFloat("MoveSpeed", 1f);
                phase = Phase.Move;
                phaseStarted = Time.time;
                break;
            case Phase.Move when state == MoveState && Time.time - phaseStarted > .25f:
                sawMove = true;
                movePoseDelta = PoseDelta(idlePose, CapturePose());
                Debug.Log($"CHARPLAYMODE move clip={string.Join(",", Array.ConvertAll(animator.GetCurrentAnimatorClipInfo(0), info => info.clip.name))} boneDelta={movePoseDelta:F2}deg");
                animator.SetFloat("MoveSpeed", 0f);
                animator.SetTrigger("Action");
                phase = Phase.Jump;
                phaseStarted = Time.time;
                break;
            case Phase.Jump when state == JumpState:
                sawJump = true;
                jumpPoseDelta = PoseDelta(idlePose, CapturePose());
                Debug.Log($"CHARPLAYMODE jump clip={string.Join(",", Array.ConvertAll(animator.GetCurrentAnimatorClipInfo(0), info => info.clip.name))} boneDelta={jumpPoseDelta:F2}deg");
                phase = Phase.ReturnToIdle;
                phaseStarted = Time.time;
                break;
            case Phase.ReturnToIdle when state == IdleState && Time.time - phaseStarted > .1f:
                var rootDelta = Vector3.Distance(rootPosition, animator.transform.position);
                var valid = sawIdle && sawMove && sawJump && movePoseDelta > .1f && jumpPoseDelta > .1f && rootDelta < .01f && !animator.applyRootMotion;
                Finish(valid, $"idle={sawIdle} move={sawMove} jump={sawJump} moveBoneDelta={movePoseDelta:F2}deg jumpBoneDelta={jumpPoseDelta:F2}deg rootDelta={rootDelta:F4}m applyRootMotion={animator.applyRootMotion}");
                break;
        }
    }

    private static Quaternion[] CapturePose()
    {
        var pose = new List<Quaternion>();
        for (var i = 0; i < (int)HumanBodyBones.LastBone; i++)
        {
            var bone = animator.GetBoneTransform((HumanBodyBones)i);
            if (bone != null) pose.Add(bone.localRotation);
        }
        return pose.ToArray();
    }

    private static float PoseDelta(Quaternion[] reference, Quaternion[] current)
    {
        var count = Math.Min(reference.Length, current.Length);
        var degrees = 0f;
        for (var i = 0; i < count; i++) degrees += Quaternion.Angle(reference[i], current[i]);
        return degrees;
    }

    private static void Finish(bool passed, string detail)
    {
        EditorApplication.update -= UpdateCheck;
        SessionState.SetBool(ResultKey, passed);
        Debug.Log($"CHARPLAYMODE {(passed ? "PASS" : "FAIL")}: {detail}");
        EditorApplication.ExitPlaymode();
    }
}
