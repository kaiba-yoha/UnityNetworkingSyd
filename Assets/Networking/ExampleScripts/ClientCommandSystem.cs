using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ClientCommandSystem : MonoBehaviour
{
    [SerializeField]
    NetworkManager_Client client;

    private void Start()
    {
        client.OnTcpMessageReceived += ReceivedServerCommand;
    }

    void ReceivedServerCommand(byte[] data)
    {
        string s = NetworkManagerBase.encoding.GetString(data);
    }

    public void SendNotify_StartingContent()
    {
        client.SendTcpPacket("Started");
    }
    public void SendNotify_EndContent()
    {
        client.SendTcpPacket("Started");
    }
}
