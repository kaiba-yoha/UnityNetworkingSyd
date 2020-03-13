using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using System.Net.Sockets;
using System.Net;

public class CameraStreaming_Server : MonoBehaviour
{
    [SerializeField] NetworkManager_Server netserver;
    [SerializeField] Texture2D texture;
    public int portnumber=8;
    public int Interval = 3;
    [SerializeField] int CaptureCount;
    List<UdpClient> udpClients=new List<UdpClient>();
    public int streamwidth=480, streamheight=640;
    
    // Start is called before the first frame update
    void Start()
    {
        OpenUDPSockets();
    }

    void OnPostRender()
    {
        if (++CaptureCount % Interval != 0)
            return;
        texture = new Texture2D(Mathf.Clamp(streamwidth,1,Screen.width), Mathf.Clamp(streamheight,1,Screen.height));
        texture.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0);
        texture.Apply();
        if (netserver.ClientDataList.Count > 0)
            SendScreen();
    }

    void OpenUDPSockets()
    {
        for(int i = 1; i < portnumber+1; i++)
        {
            UdpClient client = new UdpClient(netserver.UdpPortNum + i);
            udpClients.Add(client);
        }
    }

    void SendScreen()
    {
        byte[] data=new byte[texture.GetRawTextureData().Length / portnumber];
        int start=0;
        for(int i = 0; i < udpClients.Count; i++)
        {
        IPEndPoint endPoint = new IPEndPoint(netserver.ClientDataList[0].address,netserver.UdpPortNum+i+1);
            start = (i * data.Length/portnumber);
            texture.GetRawTextureData().CopyTo(data, start);
            udpClients[i].Send(data, data.Length, endPoint);
        }
    }
}
