using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Linq;
using System;

public class SimpleOSCSender_UDP : MonoBehaviour
{
    UdpClient client;
    [SerializeField]
    string HostName;
    [SerializeField]
    int Port = 7995;

    [SerializeField]
    string address;
    [SerializeField]
    float value;

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

    public void send()
    {
        SendFloat(address, value);
    }

    void IncludeZeroBytes(byte[] bytes, int size, ref int index)
    {
        var zeroCount = 4 - size % 4;
        for (var i = 0; i < zeroCount; i++)
        {
            bytes[index] = 0;
            index++;
        }
    }

    public void SendFloat(string address,float value)
    {
        byte[] buffer=new byte[512];
        byte[] addressdata = Encoding.ASCII.GetBytes(address);
        byte[] tagdata = Encoding.ASCII.GetBytes(",f");
        byte[] valuedata = BitConverter.GetBytes(value);

        Array.Copy(addressdata, 0, buffer, 0, addressdata.Length);
        int index = addressdata.Length;
        IncludeZeroBytes(buffer, addressdata.Length, ref index);

        Array.Copy(tagdata, 0, buffer, index, tagdata.Length);
        index += tagdata.Length;
        IncludeZeroBytes(buffer, tagdata.Length, ref index);

        if (BitConverter.IsLittleEndian)
            Array.Reverse(valuedata);
        Array.Copy(valuedata, 0, buffer, index, valuedata.Length);
        index += valuedata.Length;

        client.Send(buffer,buffer.Length);
    }
}
