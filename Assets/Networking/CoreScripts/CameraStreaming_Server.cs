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
    public int streamwidth = 640, streamheight = 360;
    Rect PixelRect;
    byte[] imagedata, databuffer;
    bool IsLengthMultiple;
    long start, amount, RestDataSize;
    [SerializeField] int compressionquality = 0;

    // Start is called before the first frame update
    void Start()
    {
        streamheight = Mathf.Clamp(streamheight, 1, Screen.height);
        streamwidth = Mathf.Clamp(streamwidth, 1, Screen.width);
        PixelRect = new Rect(0, 0, streamwidth, streamheight);
        texture = new Texture2D(streamwidth, streamheight, TextureFormat.RGB24, true);
        imagedata = texture.EncodeToPNG();
        //imagedata = texture.EncodeToJPG(compressionquality);
        OpenUDPSockets();
    }

    void OnPostRender()
    {
        if (++CaptureCount % Interval != 0)
            return;
        texture.ReadPixels(PixelRect, 0, 0);

        CaptureCount = 0;
        if (netserver.ClientDataList.Count > 0)
        {
            imagedata = texture.EncodeToPNG();
            IsLengthMultiple = imagedata.LongLength % portnumber == 0;
            amount = imagedata.LongLength / portnumber + (IsLengthMultiple ? 0 : 1);
            databuffer = new byte[amount];
            DivideBytesToSockets(imagedata);
        }
    }

    void OpenUDPSockets()
    {
        for (int i = 1; i < portnumber + 1; i++)
        {
            UdpClient client = new UdpClient(netserver.UdpPortNum + i);
            udpClients.Add(client);
        }
    }

    void DivideBytesToSockets(byte[] data)
    {
        start = -amount;
        RestDataSize = data.LongLength;
        Debug.Log("SendDataLength : " + data.Length + " DatasizePerSocket:" + databuffer.Length);

        for (int i = 0; i < udpClients.Count; i++)
        {
            IPEndPoint endPoint = new IPEndPoint(netserver.ClientDataList[0].address, netserver.UdpPortNum + i + 1);
            start += amount;
            Array.Copy(data, start, databuffer, 0, amount <= RestDataSize ? amount : RestDataSize);
            udpClients[i].Send(databuffer, databuffer.Length, endPoint);
            RestDataSize -= amount;
        }
    }
}
