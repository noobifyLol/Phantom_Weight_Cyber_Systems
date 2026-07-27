using System.Net.Sockets;
using System.Text;
using UnityEngine;

public class ESP32Manager : MonoBehaviour
{
    // Static Singleton instance so any script can find it instantly
    public static ESP32Manager Instance { get; private set; }

    private UdpClient udpClient;
    [SerializeField] private string esp32IP = "192.168.1.50"; // ESP32 Local IP
    [SerializeField] private int port = 12345;

    private void Awake()
    {
        if (Instance == null) { Instance = this; DontDestroyOnLoad(gameObject); }
        else { Destroy(gameObject); }

        udpClient = new UdpClient();
    }

    // Call this function from ANY script to send a command to the hardware
    public void SendActionToHardware(string commandKey)
    {
        try
        {
            byte[] data = Encoding.UTF8.GetBytes(commandKey);
            udpClient.Send(data, data.Length, esp32IP, port);
            Debug.Log($"[ESP32] Sent Action Command: {commandKey}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[ESP32] Send failed: {e.Message}");
        }
    }

    private void OnApplicationQuit()
    {
        udpClient?.Close();
    }
}