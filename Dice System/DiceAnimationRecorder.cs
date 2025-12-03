using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using Sirenix.OdinInspector;

#if UNITY_EDITOR
using UnityEditor;
#endif

[System.Serializable]
public class DiceFrame
{
    public Vector3 position;
    public Quaternion rotation;
    public float timestamp;

    public DiceFrame(Vector3 pos, Quaternion rot, float time)
    {
        position = pos;
        rotation = rot;
        timestamp = time;
    }
}

[System.Serializable]
public class DiceAnimation
{
    public string animationName;
    public List<DiceFrame> frames = new();
    public float duration;
    public int diceResult = -1;
}

[System.Serializable]
public class AutoGenerateSettings
{
    [Header("Generation Settings")] public int numberOfAnimations = 10;
    public float stillnessThreshold = 0.02f;
    public float stillnessTime = 1.5f;
    public string saveFolder = "Assets/DiceAnimations";

    [Header("Physics Settings")] public float throwForceMin = 5f;
    public float throwForceMax = 12f;
    public float throwTorqueMin = 10f;
    public float throwTorqueMax = 20f;

    [Header("Starting Position")] public Vector3 startPosition = new Vector3(0, 3, 0);
    public Vector3 startRotationMin = Vector3.zero;
    public Vector3 startRotationMax = new(360, 360, 360);
}

public class DiceAnimationRecorder : MonoBehaviour
{
    [BoxGroup("Data Storage")] [Required] public DiceAnimationData animationData;

    [BoxGroup("Recording Settings")] [ReadOnly]
    public bool isRecording = false;

    [BoxGroup("Recording Settings")] public float recordingFrameRate = 60f;

    [BoxGroup("Auto Generation")] [InlineProperty, HideLabel]
    public AutoGenerateSettings autoGenSettings = new();

    [BoxGroup("Auto Generation")] [ReadOnly]
    public bool isAutoGenerating = false;

    [BoxGroup("Auto Generation")] [ReadOnly, ShowIf("isAutoGenerating")]
    public int currentGenerationIndex = 0;

    [BoxGroup("Controls")]
    [Button("Start Manual Recording", ButtonSizes.Large)]
    [EnableIf("@!isAutoGenerating && !isRecording")]
    private void StartManualRecordingButton() =>
        StartRecording($"ManualRecording_{System.DateTime.Now:yyyyMMdd_HHmmss}");

    [BoxGroup("Controls")]
    [Button("Stop Recording", ButtonSizes.Large)]
    [EnableIf("isRecording")]
    private void StopRecordingButton() => StopRecording();

    [BoxGroup("Controls")]
    [Button("Start Auto Generation", ButtonSizes.Large)]
    [EnableIf("@!isAutoGenerating && !isRecording")]
    private void StartAutoGenButton() => StartAutoGeneration();

    [BoxGroup("Controls")]
    [Button("Stop Auto Generation", ButtonSizes.Large)]
    [EnableIf("isAutoGenerating")]
    private void StopAutoGenButton() => StopAutoGeneration();

    [BoxGroup("Controls")]
    [Button("Save to ScriptableObject")]
    [EnableIf("@animationData != null && recordedAnimations.Count > 0 && !isRecording")]
    private void SaveToScriptableObjectButton() => SaveToScriptableObject();

    [Space(20)]
    [BoxGroup("Recorded Animations")]
    [ListDrawerSettings(Expanded = true, ShowPaging = true, NumberOfItemsPerPage = 5)]
    public List<DiceAnimation> recordedAnimations = new();

    // Private fields
    private Rigidbody rb;
    private float recordTimer = 0f;
    private DiceAnimation currentRecording;
    private float stillnessTimer = 0f;
    private Vector3 lastPosition;
    private Quaternion lastRotation;
    private Coroutine autoGenCoroutine;

    private void Start()
    {
        rb = GetComponent<Rigidbody>();
    }

    private void Update()
    {
        if (!isRecording) return;

        recordTimer += Time.deltaTime;
        if (recordTimer >= 1f / recordingFrameRate)
        {
            RecordFrame();
            recordTimer = 0f;
        }

        if (isAutoGenerating)
        {
            CheckStillness();
        }
    }

    private void CheckStillness()
    {
        var positionDelta = Vector3.Distance(transform.position, lastPosition);
        var rotationDelta = Quaternion.Angle(transform.rotation, lastRotation);

        if (positionDelta < autoGenSettings.stillnessThreshold &&
            rotationDelta < autoGenSettings.stillnessThreshold)
        {
            stillnessTimer += Time.deltaTime;

            if (stillnessTimer >= autoGenSettings.stillnessTime)
            {
                StopRecording();
                SaveCurrentAnimation();
                StartNextGenerationIfNeeded();
            }
        }
        else
        {
            stillnessTimer = 0f;
        }

        lastPosition = transform.position;
        lastRotation = transform.rotation;
    }

    private void StartNextGenerationIfNeeded()
    {
        currentGenerationIndex++;
        if (currentGenerationIndex < autoGenSettings.numberOfAnimations)
        {
            StartCoroutine(StartNextGeneration());
        }
        else
        {
            isAutoGenerating = false;
            Debug.Log($"Auto generation complete! Generated {autoGenSettings.numberOfAnimations} animations.");
        }
    }

    private IEnumerator StartNextGeneration()
    {
        yield return new WaitForSeconds(0.5f);
        ResetDiceForGeneration();
        yield return new WaitForFixedUpdate();

        var animName = $"DiceRoll_Auto_{currentGenerationIndex:D3}";
        StartRecording(animName);

        yield return new WaitForFixedUpdate();
        ApplyRandomForces();
    }

    private void ResetDiceForGeneration()
    {
        rb.isKinematic = false;
        rb.velocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        transform.position = autoGenSettings.startPosition;

        var randomRot = new Vector3(
            Random.Range(autoGenSettings.startRotationMin.x, autoGenSettings.startRotationMax.x),
            Random.Range(autoGenSettings.startRotationMin.y, autoGenSettings.startRotationMax.y),
            Random.Range(autoGenSettings.startRotationMin.z, autoGenSettings.startRotationMax.z)
        );
        transform.rotation = Quaternion.Euler(randomRot);
    }

    private void ApplyRandomForces()
    {
        var throwForce = Random.Range(autoGenSettings.throwForceMin, autoGenSettings.throwForceMax);
        var throwTorque = Random.Range(autoGenSettings.throwTorqueMin, autoGenSettings.throwTorqueMax);

        var forceDirection = new Vector3(
            Random.Range(-1f, 1f),
            Random.Range(-0.2f, 0.3f),
            Random.Range(-1f, 1f)
        ).normalized;

        rb.AddForce(forceDirection * throwForce, ForceMode.Impulse);
        rb.AddTorque(Random.rotation * Vector3.one * throwTorque, ForceMode.Impulse);
    }

    private void RecordFrame()
    {
        if (currentRecording == null) return;

        var frame = new DiceFrame(
            transform.position,
            transform.rotation,
            Time.time - currentRecording.duration
        );

        currentRecording.frames.Add(frame);
    }

    public void StartRecording(string animationName)
    {
        rb.isKinematic = false;

        currentRecording = new DiceAnimation
        {
            animationName = animationName,
            duration = Time.time
        };

        isRecording = true;
        recordTimer = 0f;
        stillnessTimer = 0f;
        lastPosition = transform.position;
        lastRotation = transform.rotation;

        Debug.Log($"Started recording: {animationName}");
    }

    public void StopRecording()
    {
        if (!isRecording || currentRecording == null) return;

        isRecording = false;
        currentRecording.duration = Time.time - currentRecording.duration;

        recordedAnimations.Add(currentRecording);

        if (animationData != null)
        {
            animationData.AddAnimation(currentRecording);
        }

        Debug.Log($"Stopped recording: {currentRecording.animationName} " +
                  $"({currentRecording.frames.Count} frames, {currentRecording.duration:F2}s, " +
                  $"Result: {(currentRecording.diceResult == -1 ? "Not Set" : currentRecording.diceResult.ToString())})");

        currentRecording = null;
        rb.isKinematic = true;
    }

    public void StartAutoGeneration()
    {
        if (isAutoGenerating) return;

        isAutoGenerating = true;
        currentGenerationIndex = 0;
        recordedAnimations.Clear();

        Debug.Log($"Starting auto generation of {autoGenSettings.numberOfAnimations} animations...");

#if UNITY_EDITOR
        if (!Directory.Exists(autoGenSettings.saveFolder))
        {
            Directory.CreateDirectory(autoGenSettings.saveFolder);
            AssetDatabase.Refresh();
        }
#endif

        if (autoGenCoroutine != null) StopCoroutine(autoGenCoroutine);
        autoGenCoroutine = StartCoroutine(StartNextGeneration());
    }

    public void StopAutoGeneration()
    {
        isAutoGenerating = false;

        if (autoGenCoroutine != null)
        {
            StopCoroutine(autoGenCoroutine);
            autoGenCoroutine = null;
        }

        StopRecording();
        Debug.Log($"Auto generation stopped. Generated {currentGenerationIndex} animations.");
    }

    private void SaveCurrentAnimation()
    {
#if UNITY_EDITOR
        if (recordedAnimations.Count == 0) return;

        var lastAnimation = recordedAnimations[^1];
        var json = JsonUtility.ToJson(lastAnimation, true);
        var path = Path.Combine(autoGenSettings.saveFolder, $"{lastAnimation.animationName}.json");

        File.WriteAllText(path, json);
        AssetDatabase.Refresh();

        Debug.Log($"Saved animation: {lastAnimation.animationName} " +
                  $"({lastAnimation.frames.Count} frames, {lastAnimation.duration:F2}s, " +
                  $"Result: {(lastAnimation.diceResult == -1 ? "Not Set" : lastAnimation.diceResult.ToString())})");
#endif
    }

    public void SaveToScriptableObject()
    {
        if (animationData == null)
        {
            Debug.LogWarning("No ScriptableObject assigned!");
            return;
        }

        int savedCount = 0;
        foreach (var anim in recordedAnimations)
        {
            if (animationData.GetAnimationByName(anim.animationName) == null)
            {
                animationData.AddAnimation(anim);
                savedCount++;
            }
        }

        Debug.Log($"Saved {savedCount} new animations to ScriptableObject. Total: {animationData.AnimationCount}");
    }

    [ContextMenu("Test Force Manually")]
    public void TestForceManually()
    {
        rb.isKinematic = false;
        transform.position = autoGenSettings.startPosition;
        rb.velocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        var force = new Vector3(0.5f, 0.1f, 0.5f).normalized * 10f;
        var torque = new Vector3(500, 1000, 750);

        rb.AddForce(force, ForceMode.Impulse);
        rb.AddTorque(torque, ForceMode.Impulse);

        Debug.Log($"Applied test force: {force}, torque: {torque}");
    }

    public void ClearRecordings() => recordedAnimations.Clear();
}