using UnityEngine;
using TMPro;

public class PingUI : MonoBehaviour
{
    public UdpGameClient client;
    public TMP_Text pingText;

    void Update()
    {
        if (client == null || pingText == null) return;

        if (client.IsConnected)
        {
            pingText.text = $"Ping: {client.CurrentPingMs} ms";
        }
        else
        {
            pingText.text = "Ping: Disconnected";
        }
    }
}