using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using System;
using System.Net.Sockets;
using System.Net;

public class CameraStreaming_Server : MonoBehaviour
{
    [SerializeField] NetworkManager_Server netserver;
    [SerializeField] Texture2D texture;
    public int portnumber = 8;
    public int Interval = 3;
    [SerializeField] int CaptureCount;
    List<UdpClient> udpClients = new List<UdpClient>();
    public int streamwidth = 480, streamheight = 640;

    // Start is called before the first frame update
    void Start()
    {
        streamheight = Mathf.Clamp(streamheight, 1, Screen.height);
        streamwidth = Mathf.Clamp(streamwidth, 1, Screen.width);
        OpenUDPSockets();
    }

    void OnPostRender()
    {
        if (++CaptureCount % Interval != 0)
            return;
        texture = new Texture2D(streamwidth, streamheight, TextureFormat.RGB24, true);
        texture.ReadPixels(new Rect(0, 0, streamwidth, streamheight), 0, 0);
        texture.Apply();
        CaptureCount = 0;
        if (netserver.ClientDataList.Count > 0)
            SendScreen();
    }

    void OpenUDPSockets()
    {
        for (int i = 1; i < portnumber + 1; i++)
        {
            UdpClient client = new UdpClient(netserver.UdpPortNum + i);
            udpClients.Add(client);
        }
    }

    void SendScreen()
    {
        bool IsLengthMultiple = texture.GetRawTextureData().LongLength % portnumber == 0;
        long amount = texture.GetRawTextureData().LongLength / portnumber + (IsLengthMultiple ? 0 : 1);
        long start = -amount;
        byte[] data = new byte[amount];

        Debug.Log("RawDataLength : " + texture.GetRawTextureData().Length + " DatasizePerSocket:" + data.Length);

        for (int i = 0; i < udpClients.Count; i++)
        {
            IPEndPoint endPoint = new IPEndPoint(netserver.ClientDataList[0].address, netserver.UdpPortNum + i + 1);
            start += amount - (!IsLengthMultiple && i == udpClients.Count - 1 ? 1 : 0);
            Array.Copy(texture.GetRawTextureData(), start, data, 0, amount);
            udpClients[i].Send(data, data.Length, endPoint);
        }
    }
}
