using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Text;
using System.Net;
using System.Net.Sockets;

public class SimpleUDPReceiver : MonoBehaviour
{
    UdpClient client;
    [SerializeField]
    int Port = 7995;
    // Start is called before the first frame update
    void Start()
    {
        client = new UdpClient(Port);
    }

    // Update is called once per frame
    void Update()
    {
        if (client.Available > 0)
        {
            IPEndPoint endPoint=null;
            byte[] data = client.Receive(ref endPoint);
            Debug.Log(Encoding.ASCII.GetString(data));
        }
    }

    private void OnDestroy()
    {
        client.Close();
    }
}
