using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Collider2D))]
public class GhostReplayTrigger : MonoBehaviour
{
    [Header("Interaction")]
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private float replayCooldown = 0.5f;

    [Header("Recording Source")]
    [SerializeField] private TextAsset recordingJson;

    [Header("Ghost Playback")]
    [SerializeField] private GameObject ghostVisualPrefab;
    [SerializeField] private bool faceAlongRecording = true;
    [SerializeField] private Vector3 visualOffset = Vector3.zero;

    [Header("Ghost Visual")]
    [SerializeField] private Color ghostTint = new Color(0.35f, 0.35f, 0.35f, 0.65f);
    [SerializeField] private bool disableGameplayComponents = true;

    private GhostReplayRecording recording;
    private bool playerInRange;
    private float cooldownTimer;
    private Coroutine playbackCoroutine;
    private GameObject ghostInstance;
    private IGhostStateReceiver stateReceiver;
    private Dictionary<string, LineRenderer> lineRenderersByPath = new Dictionary<string, LineRenderer>();
    private LineRenderer[] lineRenderersByIndex = new LineRenderer[0];

    private void Awake()
    {
        LoadRecording();
    }

    private void Reset()
    {
        Collider2D trigger = GetComponent<Collider2D>();
        if (trigger != null)
        {
            trigger.isTrigger = true;
        }
    }

    private void Update()
    {
        if (cooldownTimer > 0f)
        {
            cooldownTimer -= Time.deltaTime;
        }

        if (!playerInRange || cooldownTimer > 0f)
        {
            return;
        }

        if (WasReplayPressedThisFrame())
        {
            PlayGhost();
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag(playerTag))
        {
            playerInRange = true;
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag(playerTag))
        {
            playerInRange = false;
        }
    }

    [ContextMenu("Reload Recording")]
    public void LoadRecording()
    {
        if (recordingJson == null || string.IsNullOrWhiteSpace(recordingJson.text))
        {
            recording = null;
            return;
        }

        recording = JsonUtility.FromJson<GhostReplayRecording>(recordingJson.text);
    }

    [ContextMenu("Play Ghost")]
    public void PlayGhost()
    {
        if (recording == null || recording.frames == null || recording.frames.Count == 0)
        {
            LoadRecording();
        }

        if (recording == null || recording.frames == null || recording.frames.Count == 0 || ghostVisualPrefab == null)
        {
            return;
        }

        EnsureGhostInstance();

        if (playbackCoroutine != null)
        {
            StopCoroutine(playbackCoroutine);
        }

        cooldownTimer = replayCooldown;
        playbackCoroutine = StartCoroutine(PlayGhostRoutine());
    }

    private IEnumerator PlayGhostRoutine()
    {
        float duration = Mathf.Max(0.01f, recording.duration);
        float elapsed = 0f;

        ghostInstance.SetActive(true);
        ApplyFrame(0f);

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            ApplyFrame(elapsed);
            yield return null;
        }

        ApplyFrame(duration);
        ghostInstance.SetActive(false);
        playbackCoroutine = null;
    }

    private void ApplyFrame(float time)
    {
        List<GhostReplayFrame> frames = recording.frames;
        if (frames.Count == 1)
        {
            ApplyPose(frames[0], frames[0], 0f);
            return;
        }

        GhostReplayFrame previous = frames[0];
        GhostReplayFrame next = frames[frames.Count - 1];

        for (int i = 1; i < frames.Count; i++)
        {
            if (frames[i].time >= time)
            {
                previous = frames[i - 1];
                next = frames[i];
                break;
            }
        }

        float interval = Mathf.Max(0.0001f, next.time - previous.time);
        float t = Mathf.Clamp01((time - previous.time) / interval);
        ApplyPose(previous, next, t);
    }

    private void ApplyPose(GhostReplayFrame fromFrame, GhostReplayFrame toFrame, float t)
    {
        ghostInstance.transform.position = Vector3.Lerp(fromFrame.position, toFrame.position, t) + visualOffset;

        if (faceAlongRecording)
        {
            float rotationZ = Mathf.LerpAngle(fromFrame.rotationZ, toFrame.rotationZ, t);
            ghostInstance.transform.rotation = Quaternion.Euler(0f, 0f, rotationZ);
        }

        if (stateReceiver != null)
        {
            string stateJson = t < 0.5f ? fromFrame.stateJson : toFrame.stateJson;
            stateReceiver.ApplyGhostState(stateJson);
        }

        ApplyLineRenderers(fromFrame, toFrame, t);
    }

    private void EnsureGhostInstance()
    {
        if (ghostInstance == null)
        {
            ghostInstance = Instantiate(ghostVisualPrefab);
            ghostInstance.name = ghostVisualPrefab.name + " (Ghost Replay)";
            PrepareGhostInstance(ghostInstance);
            CacheLineRenderers(ghostInstance);
            stateReceiver = FindStateReceiver(ghostInstance);
        }
    }

    private void ApplyLineRenderers(GhostReplayFrame fromFrame, GhostReplayFrame toFrame, float t)
    {
        if (fromFrame.lineRenderers == null || toFrame.lineRenderers == null)
        {
            return;
        }

        int count = Mathf.Min(fromFrame.lineRenderers.Count, toFrame.lineRenderers.Count);
        for (int i = 0; i < count; i++)
        {
            GhostLineRendererFrame fromLine = fromFrame.lineRenderers[i];
            GhostLineRendererFrame toLine = toFrame.lineRenderers[i];
            LineRenderer targetLineRenderer = FindLineRenderer(fromLine.path, i);

            if (targetLineRenderer == null || fromLine.positions == null || toLine.positions == null)
            {
                continue;
            }

            int positionCount = Mathf.Min(fromLine.positions.Count, toLine.positions.Count);
            targetLineRenderer.positionCount = positionCount;
            targetLineRenderer.useWorldSpace = true;

            for (int pointIndex = 0; pointIndex < positionCount; pointIndex++)
            {
                Vector3 localPoint = Vector3.Lerp(fromLine.positions[pointIndex], toLine.positions[pointIndex], t);
                targetLineRenderer.SetPosition(pointIndex, ghostInstance.transform.TransformPoint(localPoint));
            }
        }
    }

    private LineRenderer FindLineRenderer(string path, int index)
    {
        if (path != null && lineRenderersByPath.TryGetValue(path, out LineRenderer lineRenderer))
        {
            return lineRenderer;
        }

        if (index >= 0 && index < lineRenderersByIndex.Length)
        {
            return lineRenderersByIndex[index];
        }

        return null;
    }

    private void CacheLineRenderers(GameObject instance)
    {
        lineRenderersByPath.Clear();
        lineRenderersByIndex = instance.GetComponentsInChildren<LineRenderer>(true);

        for (int i = 0; i < lineRenderersByIndex.Length; i++)
        {
            string path = GetRelativePath(instance.transform, lineRenderersByIndex[i].transform);
            if (!lineRenderersByPath.ContainsKey(path))
            {
                lineRenderersByPath.Add(path, lineRenderersByIndex[i]);
            }
        }
    }

    private void PrepareGhostInstance(GameObject instance)
    {
        ApplyGhostTint(instance);

        if (!disableGameplayComponents)
        {
            return;
        }

        Collider2D[] colliders = instance.GetComponentsInChildren<Collider2D>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            colliders[i].enabled = false;
        }

        Rigidbody2D[] rigidbodies = instance.GetComponentsInChildren<Rigidbody2D>(true);
        for (int i = 0; i < rigidbodies.Length; i++)
        {
            rigidbodies[i].simulated = false;
        }

        MonoBehaviour[] behaviours = instance.GetComponentsInChildren<MonoBehaviour>(true);
        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] is IGhostStateReceiver)
            {
                continue;
            }

            behaviours[i].enabled = false;
        }
    }

    private void ApplyGhostTint(GameObject instance)
    {
        SpriteRenderer[] spriteRenderers = instance.GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < spriteRenderers.Length; i++)
        {
            spriteRenderers[i].color = MultiplyColor(spriteRenderers[i].color, ghostTint);
        }

        LineRenderer[] lineRenderers = instance.GetComponentsInChildren<LineRenderer>(true);
        for (int i = 0; i < lineRenderers.Length; i++)
        {
            lineRenderers[i].startColor = MultiplyColor(lineRenderers[i].startColor, ghostTint);
            lineRenderers[i].endColor = MultiplyColor(lineRenderers[i].endColor, ghostTint);
        }
    }

    private static Color MultiplyColor(Color baseColor, Color tint)
    {
        return new Color(
            baseColor.r * tint.r,
            baseColor.g * tint.g,
            baseColor.b * tint.b,
            baseColor.a * tint.a);
    }

    private static IGhostStateReceiver FindStateReceiver(GameObject targetObject)
    {
        if (targetObject == null)
        {
            return null;
        }

        MonoBehaviour[] behaviours = targetObject.GetComponentsInChildren<MonoBehaviour>(true);
        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] is IGhostStateReceiver receiver)
            {
                return receiver;
            }
        }

        return null;
    }

    private static string GetRelativePath(Transform root, Transform child)
    {
        if (root == null || child == null || root == child)
        {
            return string.Empty;
        }

        string path = child.name;
        Transform current = child.parent;

        while (current != null && current != root)
        {
            path = current.name + "/" + path;
            current = current.parent;
        }

        return path;
    }

    private bool WasReplayPressedThisFrame()
    {
        if (Keyboard.current != null)
        {
            if (Keyboard.current.eKey.wasPressedThisFrame || Keyboard.current.xKey.wasPressedThisFrame)
            {
                return true;
            }
        }

        if (Gamepad.current != null)
        {
            if (Gamepad.current.buttonWest.wasPressedThisFrame || Gamepad.current.buttonNorth.wasPressedThisFrame)
            {
                return true;
            }
        }

        return false;
    }
}
