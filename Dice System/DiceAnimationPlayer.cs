using System.Collections.Generic;
using System.Linq;
using Sirenix.OdinInspector;
using UnityEngine;

/// <summary>
/// Plays pre-recorded dice animations with visual result manipulation.
/// The parent transform plays the animation while the child visual transform
/// is rotated to show the desired dice result.
/// </summary>
public class DiceAnimationPlayer : SerializedMonoBehaviour
{
    #region Serialized Fields

    [BoxGroup("Setup")]
    [SerializeField] private Dictionary<int, List<Vector3>> resultToRotation = new();

    [BoxGroup("Setup")]
    [SerializeField] private Transform visualTransform;
    
    [BoxGroup("Setup")]
    [SerializeField] private MeshRenderer diceMeshRenderer;

    [BoxGroup("Data")]
    [Required] public DiceAnimationData animationData;

    [BoxGroup("Settings")]
    public bool playOnStart = false;

    [BoxGroup("Settings")]
    public bool loop = false;

    #endregion

    #region Inspector Debug Info

    [BoxGroup("Playback State")]
    [ReadOnly] public bool isPlaying = false;

    [BoxGroup("Playback State")]
    [ReadOnly, ShowIf(nameof(isPlaying))]
    public float currentTime = 0f;

    [BoxGroup("Playback State")]
    [ReadOnly, ShowIf(nameof(isPlaying))]
    public float duration = 0f;

    [BoxGroup("Playback State")]
    [ReadOnly, ShowIf(nameof(isPlaying))]
    public int currentDiceResult = -1;

    [BoxGroup("Playback State")]
    [ReadOnly, ShowIf(nameof(isPlaying))]
    public string currentAnimationName = "";

    #endregion

    #region Private State

    [BoxGroup("Loaded Data")]
    [ListDrawerSettings(Expanded = false, ShowPaging = true, NumberOfItemsPerPage = 5)]
    public List<DiceAnimation> loadedAnimations = new();

    [BoxGroup("Loaded Data")]
    [ValueDropdown(nameof(GetAnimationIndices))]
    public int currentAnimationIndex = 0;

    private float playbackTimer = 0f;
    private bool isPaused = false;

    #endregion

    #region Unity Lifecycle

    private void Start()
    {
        LoadAnimations();

        if (playOnStart && loadedAnimations.Count > 0)
        {
            PlayAnimation(0);
        }
    }

    private void Update()
    {
        if (ShouldUpdatePlayback())
        {
            UpdatePlayback();
        }
    }

    #endregion

    #region Initialization

    private void LoadAnimations()
    {
        if (animationData?.animations?.Count > 0)
        {
            LoadFromScriptableObject();
        }
        else
        {
            Debug.LogWarning($"[{nameof(DiceAnimationPlayer)}] No animation data available");
        }
    }

    public void Initialize(Material material)
    {
        if (diceMeshRenderer != null)
        {
            diceMeshRenderer.material = material;
        }
    }

    #endregion

    #region Playback Control

    public void PlayAnimation(int index)
    {
        if (!IsValidAnimationIndex(index))
        {
            Debug.LogWarning($"[{nameof(DiceAnimationPlayer)}] Invalid animation index: {index}");
            return;
        }

        currentAnimationIndex = index;
        playbackTimer = 0f;
        isPlaying = true;
        isPaused = false;
    }

    public void PlayAnimationByName(string animationName)
    {
        var index = loadedAnimations.FindIndex(a => a.animationName == animationName);
        
        if (index >= 0)
        {
            PlayAnimation(index);
        }
        else
        {
            Debug.LogWarning($"[{nameof(DiceAnimationPlayer)}] Animation not found: {animationName}");
        }
    }

    public void PlayRandomAnimation()
    {
        if (loadedAnimations.Count == 0)
        {
            Debug.LogWarning($"[{nameof(DiceAnimationPlayer)}] No animations loaded");
            return;
        }

        PlayAnimation(Random.Range(0, loadedAnimations.Count));
    }

    public void StopPlayback()
    {
        isPlaying = false;
        isPaused = false;
        playbackTimer = 0f;
        ResetPlaybackInfo();
    }

    public void TogglePause()
    {
        if (isPlaying)
        {
            isPaused = !isPaused;
        }
    }

    #endregion

    #region Visual Result Control

    /// <summary>
    /// Sets the visual child rotation to display a specific dice result (1-6).
    /// This is called AFTER setting the parent's final animation transform.
    /// </summary>
    [Button("Set Visual Result")]
    public void SetRotationFromResult(int result)
    {
        if (!IsValidResult(result))
        {
            Debug.LogWarning($"[{nameof(DiceAnimationPlayer)}] Invalid result: {result}");
            return;
        }

        if (!resultToRotation.TryGetValue(result, out var rotations) || rotations.Count == 0)
        {
            Debug.LogWarning($"[{nameof(DiceAnimationPlayer)}] No rotations for result {result}");
            return;
        }

        var randomRotation = rotations[Random.Range(0, rotations.Count)];
        visualTransform.eulerAngles = randomRotation;
    }

    private bool IsValidResult(int result) => result is >= 1 and <= 6;

    #endregion

    #region Playback Update Logic

    private bool ShouldUpdatePlayback()
    {
        return isPlaying 
            && !isPaused 
            && loadedAnimations.Count > 0 
            && IsValidAnimationIndex(currentAnimationIndex);
    }

    private void UpdatePlayback()
    {
        var animation = loadedAnimations[currentAnimationIndex];
        
        if (animation?.frames == null || animation.frames.Count == 0)
            return;

        playbackTimer += Time.deltaTime;
        UpdateDebugInfo(animation);

        var normalizedTime = playbackTimer / animation.duration;

        if (normalizedTime >= 1f)
        {
            HandlePlaybackEnd(animation);
        }
        else
        {
            ApplyInterpolatedFrame(animation, normalizedTime);
        }
    }

    private void UpdateDebugInfo(DiceAnimation animation)
    {
        currentTime = playbackTimer;
        duration = animation.duration;
        currentAnimationName = animation.animationName;
        currentDiceResult = animation.diceResult;
    }

    private void HandlePlaybackEnd(DiceAnimation animation)
    {
        if (loop)
        {
            playbackTimer = 0f;
        }
        else
        {
            ApplyFinalFrame(animation);
            StopPlayback();
        }
    }

    private void ApplyFinalFrame(DiceAnimation animation)
    {
        var finalFrame = animation.frames[^1];
        transform.position = finalFrame.position;
        transform.rotation = finalFrame.rotation;
    }

    private void ApplyInterpolatedFrame(DiceAnimation animation, float normalizedTime)
    {
        var frameCount = animation.frames.Count;
        var targetIndex = Mathf.FloorToInt(normalizedTime * (frameCount - 1));
        targetIndex = Mathf.Clamp(targetIndex, 0, frameCount - 1);

        if (targetIndex < frameCount - 1)
        {
            InterpolateBetweenFrames(animation, targetIndex);
        }
        else
        {
            ApplyFrame(animation.frames[targetIndex]);
        }
    }

    private void InterpolateBetweenFrames(DiceAnimation animation, int fromIndex)
    {
        var frameDuration = animation.duration / (animation.frames.Count - 1);
        var progress = (playbackTimer % frameDuration) / frameDuration;

        var fromFrame = animation.frames[fromIndex];
        var toFrame = animation.frames[fromIndex + 1];

        var position = Vector3.Lerp(fromFrame.position, toFrame.position, progress);
        var rotation = Quaternion.Slerp(fromFrame.rotation, toFrame.rotation, progress);

        transform.position = position;
        transform.rotation = rotation;
    }

    private void ApplyFrame(DiceFrame frame)
    {
        transform.position = frame.position;
        transform.rotation = frame.rotation;
    }

    #endregion

    #region Data Management

    public void LoadFromScriptableObject()
    {
        if (animationData == null)
        {
            Debug.LogWarning($"[{nameof(DiceAnimationPlayer)}] No ScriptableObject assigned");
            return;
        }

        loadedAnimations.Clear();
        loadedAnimations.AddRange(animationData.animations);
    }

    #endregion

    #region Visibility Control

    public void Show()
    {
        gameObject.SetActive(true);
    }

    public void Hide()
    {
        gameObject.SetActive(false);
        ResetTransforms();
    }

    private void ResetTransforms()
    {
        transform.localPosition = Vector3.zero;
        transform.localEulerAngles = Vector3.zero;
        visualTransform.localPosition = Vector3.zero;
        visualTransform.localEulerAngles = Vector3.zero;
    }

    public bool IsVisible => gameObject.activeInHierarchy;

    #endregion

    #region Query Methods

    public bool IsAnimationLoaded(string animationName) =>
        loadedAnimations.Any(a => a.animationName == animationName);

    public DiceAnimation GetCurrentAnimation() =>
        IsValidAnimationIndex(currentAnimationIndex) 
            ? loadedAnimations[currentAnimationIndex] 
            : null;

    public int GetDiceResult() =>
        IsValidAnimationIndex(currentAnimationIndex) 
            ? loadedAnimations[currentAnimationIndex].diceResult 
            : -1;

    public float GetPlaybackProgress() =>
        isPlaying && IsValidAnimationIndex(currentAnimationIndex)
            ? playbackTimer / loadedAnimations[currentAnimationIndex].duration
            : 0f;

    private bool IsValidAnimationIndex(int index) =>
        index >= 0 && index < loadedAnimations.Count;

    #endregion

    #region Inspector Helpers

    private void ResetPlaybackInfo()
    {
        currentTime = 0f;
        duration = 0f;
        currentAnimationName = "";
        currentDiceResult = -1;
    }

    private System.Collections.IEnumerable GetAnimationIndices()
    {
        var list = new ValueDropdownList<int>();
        
        for (int i = 0; i < loadedAnimations.Count; i++)
        {
            var displayName = loadedAnimations[i] != null
                ? $"{i}: {loadedAnimations[i].animationName}"
                : $"{i}: (null)";
            list.Add(displayName, i);
        }

        return list;
    }

    #endregion

    #region Inspector Buttons

    [BoxGroup("Controls")]
    [Button("Load Animations")]
    [EnableIf("@animationData != null && !isPlaying")]
    private void EditorLoadAnimations() => LoadFromScriptableObject();

    [BoxGroup("Controls")]
    [Button("Play Current")]
    [EnableIf("@!isPlaying && loadedAnimations.Count > 0")]
    private void EditorPlayCurrent() => PlayAnimation(currentAnimationIndex);

    [BoxGroup("Controls")]
    [Button("Play Random")]
    [EnableIf("@!isPlaying && loadedAnimations.Count > 0")]
    private void EditorPlayRandom() => PlayRandomAnimation();

    [BoxGroup("Controls")]
    [Button("Stop")]
    [EnableIf(nameof(isPlaying))]
    private void EditorStop() => StopPlayback();

    #endregion
}