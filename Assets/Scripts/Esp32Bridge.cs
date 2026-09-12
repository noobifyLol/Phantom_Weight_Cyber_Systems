// Shared, process-wide gateway to the ESP32 over serial.
//
// Guarded by `UNITY_EDITOR || UNITY_STANDALONE_WIN` because
// System.IO.Ports.SerialPort isn't available on Android (Quest) builds.

using UnityEngine;

#if UNITY_EDITOR || UNITY_STANDALONE_WIN
using System.IO.Ports;
#endif

public static class Esp32Bridge
{
    // UPDATE THIS TO MATCH YOUR ESP32 COM PORT (Device Manager → Ports)!
    public static string PortName = "COM9";
    public static int BaudRate = 115200;

    private static bool s_initialised;
    private static bool s_available;

    // Centralized state tracking for both hands
    private static bool s_leftGrabbing = false;
    private static bool s_rightGrabbing = false;
    private static float s_leftWeight = 0f;
    private static float s_rightWeight = 0f;

#if UNITY_EDITOR || UNITY_STANDALONE_WIN
    private static SerialPort s_port;
#endif

    private static void EnsureOpen()
    {
        if (s_initialised) return;
        s_initialised = true;

#if UNITY_EDITOR || UNITY_STANDALONE_WIN
        try
        {
            s_port = new SerialPort(PortName, BaudRate);
            s_port.ReadTimeout = 100;
            s_port.WriteTimeout = 100;
            s_port.Open();
            s_available = true;
            Debug.Log($"[Esp32Bridge] Connected on {PortName}.");
        }
        catch (System.Exception e)
        {
            s_available = false;
            // One-time warning, not per-detector spam.
            Debug.LogWarning($"[Esp32Bridge] {PortName} unavailable — running without EMS. ({e.Message})");
        }
#endif
    }

    // Best-effort send. Silently no-ops if the port never opened.
    public static void Send(string payload)
    {
        EnsureOpen();
        if (!s_available) return;

#if UNITY_EDITOR || UNITY_STANDALONE_WIN
        try
        {
            s_port.WriteLine(payload);
            Debug.Log($"[Esp32Bridge] Sent to ESP32: {payload}");
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[Esp32Bridge] Send failed, disabling port: {e.Message}");
            s_available = false;
        }
#endif
    }

    // Grab state manager — call this from GrabDetector instead of Send().
    public static void SetGrabState(bool isLeftHand, bool isGrabbing, float weight = 0f)
    {
        if (isLeftHand)
        {
            s_leftGrabbing = isGrabbing;
            s_leftWeight = isGrabbing ? weight : 0f;
        }
        else
        {
            s_rightGrabbing = isGrabbing;
            s_rightWeight = isGrabbing ? weight : 0f;
        }

        EvaluateAndSendState();
    }

    // Sends the combined state of both hands. Weights are rounded so the
    // firmware always receives whole pulse counts.
    private static void EvaluateAndSendState()
    {
        if (s_leftGrabbing && s_rightGrabbing)
        {
            int maxWeight = Mathf.RoundToInt(Mathf.Max(s_leftWeight, s_rightWeight));
            Send($"Lift,{maxWeight},both");
        }
        else if (s_leftGrabbing)
        {
            Send($"Lift,{Mathf.RoundToInt(s_leftWeight)},left");
            Send("Release,0,right"); // Explicitly drop right
        }
        else if (s_rightGrabbing)
        {
            Send($"Lift,{Mathf.RoundToInt(s_rightWeight)},right");
            Send("Release,0,left"); // Explicitly drop left
        }
        else
        {
            Send("Release,0,both");
        }
    }

#if UNITY_EDITOR
    [UnityEditor.InitializeOnEnterPlayMode]
    private static void ResetOnPlay()
    {
        // Domain-reload-off safe: force a fresh open and clean state at each Play start.
        Close();
        s_initialised = false;
        s_leftGrabbing = false;
        s_rightGrabbing = false;
        s_leftWeight = 0f;
        s_rightWeight = 0f;
    }
#endif

    private static void Close()
    {
#if UNITY_EDITOR || UNITY_STANDALONE_WIN
        if (s_port != null)
        {
            // Final kill switch so the EMS unit never stays on after quitting
            if (s_port.IsOpen)
            {
                try { s_port.WriteLine("Release,0,both"); } catch { }
                try { s_port.Close(); } catch { /* ignore */ }
            }
            s_port = null;
        }
#endif
        s_available = false;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnLoad()
    {
        Close();
        s_initialised = false;
    }

    // Application quit hook
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void InstallQuitHook()
    {
        Application.quitting -= Close;
        Application.quitting += Close;
    }
}
