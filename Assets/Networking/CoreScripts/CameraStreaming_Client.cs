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
    byte[] ImageData;
    public int portnumber = 8;
    List<UdpClient> udpClients = new List<UdpClient>();
    public int streamwidth = 480, streamheight = 640;
    bool IsLengthMultiple;
    long start, amount, RestDataSize;
    byte[] databuffer;
    [SerializeField] int compressionquality = 0;

    // Start is called before the first frame update
    void Start()
    {
        texture = new Texture2D(streamwidth, streamheight, TextureFormat.RGB24, true);
        ImageData = texture.EncodeToPNG();
        IsLengthMultiple = ImageData.LongLength % portnumber == 0;
        amount = ImageData.LongLength / portnumber + (IsLengthMultiple ? 0 : 1);
        start = -amount;
        if (streamobj != null)
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
        if (!udpClients.TrueForAll((client) => client.Available > 0))
            return;
        long start = -amount, RestDataSize = ImageData.LongLength;
        ImageData = Array.Empty<byte>();
        IPEndPoint endPoint = null;
        for (int i = 0; i < udpClients.Count; i++)
        {
            start += amount;
            databuffer = udpClients[i].Receive(ref endPoint);
            ImageData = ImageData.Concat(databuffer).ToArray();
            //Array.Copy(databuffer, 0, ImageData, start, Mathf.Clamp(databuffer.Length, 0, (int)RestDataSize));
            Debug.Log("recv : start " + start);
            RestDataSize -= amount;
        }
        //texture.LoadRawTextureData(NativeTextureData);
        texture.LoadImage(ImageData);
        texture.Apply();
    }
}
