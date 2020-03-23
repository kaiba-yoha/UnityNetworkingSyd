using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Text;
using System.Net;
using System.Net.Sockets;


public class SimpleUDPSender : MonoBehaviour
{
    UdpClient client;
    [SerializeField]
    string HostName;
    [SerializeField]
    int Port = 7995;
    // Start is called before the first frame update
    void Start()
    {
        client = new UdpClient(HostName, Port);
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    private void OnDestroy()
    {
        client.Close();
    }

    public void Send(string mes)
    {
        byte[] data = Encoding.ASCII.GetBytes(mes);
        client.Send(data,data.Length);
    }
}
