using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class GhostReplayFrame
{
    public float time;
    public Vector3 position;
    public float rotationZ;
    public List<GhostLineRendererFrame> lineRenderers = new List<GhostLineRendererFrame>();
    public string stateJson;
}

[Serializable]
public class GhostLineRendererFrame
{
    public string path;
    public List<Vector3> positions = new List<Vector3>();
}

[Serializable]
public class GhostReplayRecording
{
    public string recordingName = "GhostReplay";
    public float duration;
    public List<GhostReplayFrame> frames = new List<GhostReplayFrame>();
}

public interface IGhostStateSource
{
    string CaptureGhostState();
}

public interface IGhostStateReceiver
{
    void ApplyGhostState(string stateJson);
}
