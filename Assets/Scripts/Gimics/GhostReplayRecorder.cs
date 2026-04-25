using System.IO;
using System.Text;
using System;
using UnityEngine;
using UnityEngine.InputSystem;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class GhostReplayRecorder : MonoBehaviour
{
    [Header("Recording Source")]
    [SerializeField] private Transform target;

    [Header("Recording Settings")]
    [SerializeField] private string recordingName = "ghost_demo";
    [SerializeField] private float maxDuration = 5f;
    [SerializeField] private float sampleInterval = 0.05f;
    [SerializeField] private bool captureLineRenderers = true;
    [SerializeField] private bool captureOnPlay = false;

    [Header("Developer Shortcuts")]
    [SerializeField] private KeyCode toggleRecordingKey = KeyCode.F8;
    [SerializeField] private KeyCode saveRecordingKey = KeyCode.F9;

    [Header("Output")]
    [SerializeField] private string resourcesSubFolder = "GhostRecordings";
    [SerializeField] private bool autoSaveWhenRecordingStops = true;
    [SerializeField] private bool generateUniqueFileName = true;
    [SerializeField, TextArea(8, 20)] private string latestJson;
    [SerializeField] private string latestSavedPath;

    private GhostReplayRecording currentRecording;
    private IGhostStateSource stateSource;
    private bool isRecording;
    private float recordingTime;
    private float sampleTimer;

    public string LatestJson => latestJson;
    public bool IsRecording => isRecording;
    public string LatestSavedPath => latestSavedPath;

    private void Awake()
    {
        if (target == null)
        {
            target = transform;
        }

        stateSource = FindStateSource(target);
    }

    private void Start()
    {
        if (captureOnPlay)
        {
            StartRecording();
        }
    }

    private void Update()
    {
        if (WasPressedThisFrame(toggleRecordingKey))
        {
            if (isRecording)
            {
                StopRecording();
            }
            else
            {
                StartRecording();
            }
        }

        if (WasPressedThisFrame(saveRecordingKey))
        {
            SaveLatestRecordingToResources();
        }

    }

    private void LateUpdate()
    {
        if (!isRecording)
        {
            return;
        }

        float interval = Mathf.Max(0.01f, sampleInterval);

        recordingTime += Time.deltaTime;
        sampleTimer += Time.deltaTime;

        while (sampleTimer >= interval)
        {
            sampleTimer -= interval;
            CaptureFrame(recordingTime);
        }

        if (recordingTime >= maxDuration)
        {
            StopRecording();
        }
    }

    [ContextMenu("Start Recording")]
    public void StartRecording()
    {
        if (target == null)
        {
            target = transform;
        }

        stateSource = FindStateSource(target);

        currentRecording = new GhostReplayRecording
        {
            recordingName = recordingName,
            duration = 0f
        };

        recordingTime = 0f;
        sampleTimer = 0f;
        isRecording = true;

        CaptureFrame(0f);
    }

    [ContextMenu("Stop Recording")]
    public void StopRecording()
    {
        if (!isRecording || currentRecording == null)
        {
            return;
        }

        currentRecording.duration = recordingTime;
        CaptureFrame(recordingTime);
        latestJson = JsonUtility.ToJson(currentRecording, true);
        isRecording = false;

        if (autoSaveWhenRecordingStops)
        {
            SaveLatestRecordingToResources();
        }
    }

    [ContextMenu("Save Latest Recording To Resources")]
    public void SaveLatestRecordingToResources()
    {
        if (string.IsNullOrWhiteSpace(latestJson))
        {
            return;
        }

        string assetsPath = Application.dataPath;
        string folderPath = Path.Combine(assetsPath, "Resources", resourcesSubFolder);
        Directory.CreateDirectory(folderPath);

        string fileName = CreateRecordingFileName();
        string fullPath = Path.Combine(folderPath, fileName);
        File.WriteAllText(fullPath, latestJson, Encoding.UTF8);
        latestSavedPath = fullPath;

#if UNITY_EDITOR
        AssetDatabase.Refresh();
        EditorUtility.SetDirty(this);
#endif

        Debug.Log("Ghost recording saved: " + fullPath, this);
    }

    private string CreateRecordingFileName()
    {
        string baseName = SanitizeFileName(recordingName);
        if (!generateUniqueFileName)
        {
            return baseName + ".json";
        }

        string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string shortGuid = Guid.NewGuid().ToString("N").Substring(0, 8);
        return baseName + "_" + timestamp + "_" + shortGuid + ".json";
    }

    private void CaptureFrame(float time)
    {
        if (currentRecording == null || target == null)
        {
            return;
        }

        GhostReplayFrame frame = new GhostReplayFrame
        {
            time = time,
            position = target.position,
            rotationZ = target.eulerAngles.z,
            stateJson = stateSource != null ? stateSource.CaptureGhostState() : string.Empty
        };

        if (captureLineRenderers)
        {
            CaptureLineRenderers(frame);
        }

        currentRecording.frames.Add(frame);
    }

    private void CaptureLineRenderers(GhostReplayFrame frame)
    {
        LineRenderer[] lineRenderers = target.GetComponentsInChildren<LineRenderer>(true);
        for (int i = 0; i < lineRenderers.Length; i++)
        {
            LineRenderer lineRenderer = lineRenderers[i];
            GhostLineRendererFrame lineFrame = new GhostLineRendererFrame
            {
                path = GetRelativePath(target, lineRenderer.transform)
            };

            for (int pointIndex = 0; pointIndex < lineRenderer.positionCount; pointIndex++)
            {
                Vector3 point = lineRenderer.GetPosition(pointIndex);
                Vector3 worldPoint = lineRenderer.useWorldSpace ? point : lineRenderer.transform.TransformPoint(point);
                lineFrame.positions.Add(target.InverseTransformPoint(worldPoint));
            }

            frame.lineRenderers.Add(lineFrame);
        }
    }

    private static IGhostStateSource FindStateSource(Transform sourceTarget)
    {
        if (sourceTarget == null)
        {
            return null;
        }

        MonoBehaviour[] behaviours = sourceTarget.GetComponents<MonoBehaviour>();
        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] is IGhostStateSource ghostStateSource)
            {
                return ghostStateSource;
            }
        }

        return null;
    }

    private static string SanitizeFileName(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return "ghost_demo";
        }

        foreach (char invalidChar in Path.GetInvalidFileNameChars())
        {
            raw = raw.Replace(invalidChar, '_');
        }

        return raw.Trim();
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

    private static bool WasPressedThisFrame(KeyCode key)
    {
        if (Keyboard.current == null)
        {
            return false;
        }

        Key inputSystemKey = ConvertKeyCode(key);
        if (inputSystemKey == Key.None)
        {
            return false;
        }

        return Keyboard.current[inputSystemKey].wasPressedThisFrame;
    }

    private static Key ConvertKeyCode(KeyCode keyCode)
    {
        switch (keyCode)
        {
            case KeyCode.F8:
                return Key.F8;
            case KeyCode.F9:
                return Key.F9;
            default:
                return Key.None;
        }
    }
}
