using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class NetworkDebugger : MonoBehaviour
{
    [SerializeField] NetworkManager_Client client;
    bool Connected = false;

    private void Awake()
    {
        Application.logMessageReceived += Application_logMessageReceived;
        client.OnAssignedNetwork += (s) => { Connected = true; };
    }

    private void Application_logMessageReceived(string condition, string stackTrace, LogType type)
    {
        if (Connected)
            client.SendTcpPacket(condition + " / " + stackTrace);
    }
}
