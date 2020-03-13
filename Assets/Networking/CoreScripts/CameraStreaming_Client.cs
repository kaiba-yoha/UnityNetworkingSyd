using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Linq;
using System.Net.Sockets;
using System.Net;

public class CameraStreaming_Client : MonoBehaviour
{
    [SerializeField] NetworkManager_Client netclient;
    [SerializeField] Texture2D texture;
    byte[] NativeTextureData;
    public int portnumber = 8;
    public int Interval = 3;
    [SerializeField] int CaptureCount;
    List<UdpClient> udpClients = new List<UdpClient>();
    public int streamwidth = 480, streamheight = 640;

    // Start is called before the first frame update
    void Start()
    {
        texture = new Texture2D(streamwidth, streamheight);
        NativeTextureData = new byte[texture.GetRawTextureData().Length];
        OpenUDPSockets();
    }

    private void Update()
    {
        ReceiveTexture();
    }

    void OpenUDPSockets()
    {
        for (int i = 1; i < portnumber + 1; i++)
        {
            UdpClient client = new UdpClient(netclient.UdpPortNum + i);
            udpClients.Add(client);
        }
    }

    void ReceiveTexture()
    {
        int size=texture.GetRawTextureData().Length / portnumber;
        byte[] data;
        int start = 0;
        IPEndPoint endPoint=null;
        for (int i = 0; i < udpClients.Count; i++)
        {
            if (udpClients[i].Available > 0)
            {
                start = (i * size);
                data =udpClients[i].Receive(ref endPoint);
                Array.Copy(data, 0, NativeTextureData, start, data.Length);
            }
        }
        texture.LoadRawTextureData(NativeTextureData);
        texture.Apply();
    }
}
