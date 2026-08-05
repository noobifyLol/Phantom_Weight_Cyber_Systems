// Shared, process-wide gateway to the ESP32 over serial.
//
// Previously every GrabDetector opened its own SerialPort on Start(). With
// four cubes in the scene that was four `esp.Open()` attempts on COM4 and
// four "port busy / access denied" warnings. This class owns the port for
// the whole app: any script that wants to talk to the ESP32 calls
// `Esp32Bridge.Send("Lift,30,Left")` and doesn't care whether the port is
// open, missing, or on a build target that doesn't support serial.
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

#if UNITY_EDITOR
    [UnityEditor.InitializeOnEnterPlayMode]
    private static void ResetOnPlay()
    {
        // Domain-reload-off safe: force a fresh open at each Play start.
        Close();
        s_initialised = false;
    }
#endif

    private static void Close()
    {
#if UNITY_EDITOR || UNITY_STANDALONE_WIN
        if (s_port != null)
        {
            try { if (s_port.IsOpen) s_port.Close(); } catch { /* ignore */ }
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
