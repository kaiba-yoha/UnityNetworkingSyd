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
    [SerializeField] GameObject streamobj;
    [SerializeField] UnityEngine.UI.RawImage imageui;
    byte[] NativeTextureData;
    public int portnumber = 8;
    List<UdpClient> udpClients = new List<UdpClient>();
    public int streamwidth = 480, streamheight = 640;

    // Start is called before the first frame update
    void Start()
    {
        texture = new Texture2D(streamwidth, streamheight, TextureFormat.RGB24, true);
        NativeTextureData = new byte[texture.GetRawTextureData().Length];
        if(streamobj!=null)
        streamobj.GetComponent<MeshRenderer>().material.mainTexture = texture;
        imageui.texture = texture;
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
        bool IsLengthMultiple = texture.GetRawTextureData().LongLength % portnumber == 0;
        long amount = texture.GetRawTextureData().LongLength / portnumber + (IsLengthMultiple ? 0 : 1);
        long start = -amount,RestDataSize=texture.GetRawTextureData().LongLength;
        byte[] data;
        IPEndPoint endPoint = null;
        for (int i = 0; i < udpClients.Count; i++)
        {
            start += amount;
            if (udpClients[i].Available > 0)
            {
                data = udpClients[i].Receive(ref endPoint);
                Array.Copy(data, 0, NativeTextureData, start, Mathf.Clamp(data.Length,0,(int)RestDataSize));
                Debug.Log("recv : start " + start);
            }
            RestDataSize -= amount;
        }
        texture.LoadRawTextureData(NativeTextureData);
        texture.Apply();
    }
}
