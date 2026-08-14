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
    public static string PortName = "COM4";
    public static int BaudRate = 115200;

    private static bool s_initialised;
    private static bool s_available;

    // --- NEW: Centralized State Tracking ---
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
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[Esp32Bridge] send failed, disabling: {e.Message}");
            s_available = false;
        }
#endif
    }

    // --- NEW: Grab State Manager ---
    // Call this from your GrabDetector instead of calling Send() directly.
    public static void SetGrabState(bool isLeftHand, bool isGrabbing, float weight = 0f)
    {
        // 1. Update the internal state for the specific hand
        if (isLeftHand)
        {
            s_leftGrabbing = isGrabbing;
            s_leftWeight = weight;
        }
        else
        {
            s_rightGrabbing = isGrabbing;
            s_rightWeight = weight;
        }

        // 2. Evaluate the combined state and send the correct payload
        EvaluateAndSendState();
    }

    private static void EvaluateAndSendState()
    {
        // SAFETY OVERRIDE: Both hands are grabbing! 
        // Force a total reset to zero.
        if (s_leftGrabbing && s_rightGrabbing)
        {
            int maxWeight = Mathf.RoundToInt(Mathf.Max(s_leftWeight, s_rightWeight));
            Send($"Lift,{maxWeight},both");
        }
        // ONLY Left hand is grabbing
        else if (s_leftGrabbing)
        {
            Send($"Lift,{s_leftWeight},left");
            Send("Release,0,right"); // Make sure right is completely off
        }
        // ONLY Right hand is grabbing
        else if (s_rightGrabbing)
        {
            Send($"Lift,{s_rightWeight},right");
            Send("Release,0,left"); // Make sure left is completely off
        }
        // NEITHER hand is grabbing
        else
        {
            Send("Release,0,both");
        }
    }
    // ------------------------------------

#if UNITY_EDITOR
    [UnityEditor.InitializeOnEnterPlayMode]
    private static void ResetOnPlay()
    {
        // Domain-reload-off safe: force a fresh open at each Play start.
        Close();
        s_initialised = false;
        
        // Reset our grab states when hitting Play
        s_leftGrabbing = false;
        s_rightGrabbing = false;
    }
#endif

    private static void Close()
    {
#if UNITY_EDITOR || UNITY_STANDALONE_WIN
        if (s_port != null)
        {
            // Send a final kill switch to the ESP32 on application quit
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

    // Application quit hook via a hidden GameObject.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void InstallQuitHook()
    {
        Application.quitting -= Close;
        Application.quitting += Close;
    }
}