using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Linq;
using UnityEngine;

public class ClientDataContainer
{
    public IPAddress address;
    public TcpClient TcpSocket;
    public List<ReplicatiorBase> AutonomousObjects;
    public string UserName;
    public byte NetworkId;
    public int UdpEndPoint = 8676;
}

public class NetworkManager_Server : NetworkManagerBase
{
    public static NetworkManager_Server server;

    public string DOwnIP;
    TcpListener listener;
    UdpClient UdpSocket;
    public List<ClientDataContainer> ClientDataList;
    [SerializeField]
    bool LaunchOnStart;
    public int TcpPortNum = 7890, UdpPortNum = 7891;
    int buffersize = 512;
    byte[] buffer;
    /// <summary>
    /// Dictionary of Replication targets Key=ReplicatorBase.Id
    /// </summary>
    public Dictionary<int, ReplicatiorBase> RepObjPairs;
    /// <summary>
    /// List of Replication targets
    /// </summary>
    public List<ReplicatiorBase> RepObjects;
    int ObjIdBuffer;
    byte NetIdBuffer;
    [SerializeField]
    float ServerUpdateInterval = 0.025f;
    [SerializeField]
    float ServerAcceptInterval = 2f;

    public delegate void ClientNotification(ClientDataContainer clientData);
    public delegate void NetworkDataHandler(byte[] data, ClientDataContainer clientData);
    public ReplicatedObjectNotification OnNewRepObjectAdded;
    public ReplicatedObjectNotification OnNewAutonomousObjectAdded;
    public ClientNotification OnNewClientConnected;
    public ClientNotification OnClientDisconnected;
    public NetworkDataHandler OnTcpPacketReceived;
    public NetworkDataHandler OnUdpPacketReceived;
    public NetworkDataHandler OnTcpMessageReceived;

    // Start is called before the first frame update
    void Start()
    {
        try
        {
            foreach (var I in Dns.GetHostAddresses(Dns.GetHostName()))
            {
                if (I.AddressFamily == AddressFamily.InterNetwork)
                {
                    OwnIP = I;
                    DOwnIP = I.ToString();
                }
            }
        }
        catch
        {
            OwnIP = IPAddress.Parse(DOwnIP);
        }

        if (LaunchOnStart)
        {
            LaunchNetworkServer();
        }
    }

    public override void Launch()
    {
        LaunchNetworkServer();
    }

    public override void ShutDown()
    {
        ShutDownServer();
    }

    /// <summary>
    /// Launch Server System. Start Listen on TcpPortNum.
    /// </summary>
    public void LaunchNetworkServer()
    {
        buffer = new byte[buffersize];
        RepObjPairs = new Dictionary<int, ReplicatiorBase>();
        RepObjects = new List<ReplicatiorBase>();
        ClientDataList = new List<ClientDataContainer>();
        ObjIdBuffer = 0;
        NetIdBuffer = 1;
        try
        {
            listener = new TcpListener(IPAddress.Any, TcpPortNum);
            listener.Start();
            InvokeRepeating("CheckForNewClient", ServerAcceptInterval, ServerAcceptInterval);
            InvokeRepeating("ServerTick", ServerUpdateInterval, ServerUpdateInterval);
            Debug.Log("Launch Server Successfully!");
        }
        catch
        {
            Debug.Log("Couldnt Launch Server");
            return;
        }
        UdpSocket = new UdpClient(UdpPortNum);
        server = this;
        LocalInst = this;
    }

    public void ShutDownServer()
    {
        buffer = null;
        listener.Stop();
        listener = null;
        UdpSocket.Close();
        UdpSocket = null;
        server = null;
        LocalInst = null;
        CancelInvoke();
        ClientDataList.ForEach((c) =>
        {
            SendTcpPacket(c, encoding.GetBytes("End"));
            c.TcpSocket.Close();
        });
        ClientDataList.Clear();
        RepObjPairs.Clear();
        RepObjPairs = null;
        RepObjects.Clear();
        RepObjects = null;
        Debug.Log("ShutDown Server");
    }

    void CheckForNewClient()
    {
        if (listener.Pending())
        {
            Debug.Log("Accepting New Client...");
            listener.BeginAcceptTcpClient(AcceptedClientCallback, listener);
        }
    }

    void AcceptedClientCallback(System.IAsyncResult ar)
    {
        Debug.Log("Server: Client Connected");
        TcpClient client = (ar.AsyncState as TcpListener).EndAcceptTcpClient(ar);
        ClientDataContainer c = new ClientDataContainer() { TcpSocket = client, address = ((IPEndPoint)client.Client.RemoteEndPoint).Address, AutonomousObjects = new List<ReplicatiorBase>(), NetworkId = NetIdBuffer++ };
        Debug.Log("Client IPAddress : " + c.address);
        ClientDataList.Add(c);
        OnNewClientConnected?.Invoke(c);
    }

    void SendInitialMessage(ClientDataContainer client)
    {
        string InitRepData = "Assign," + client.NetworkId + "$";
        RepObjects.ForEach((obj) =>
        {
            InitRepData += "NewRepObj" + "," + obj.RepPrefabName + "," + Serializer.Vector3ToString(obj.transform.position) + "," +
            Serializer.Vector3ToString(obj.transform.eulerAngles) + "," + obj.transform.parent.gameObject.name + "," + obj.Id + "," + obj.OwnerNetId + "$";
        });
        SendTcpPacket(client, encoding.GetBytes(InitRepData));
    }

    public void SendTcpPacket(ClientDataContainer client, byte[] data)
    {
        try
        {
            client.TcpSocket.Client.Send(data);
        }
        catch
        {
            ClientDisconnected(client);
        }
    }
    public void SendTcpPacket(ClientDataContainer client, string data)
    {
        try
        {
            client.TcpSocket.Client.Send(encoding.GetBytes(data));
        }
        catch
        {
            ClientDisconnected(client);
        }
    }

    void SendFile(ClientDataContainer client, string FilePath)
    {
        client.TcpSocket.Client.SendFile(FilePath);
    }

    public void DisconnectClient(ClientDataContainer client)
    {
        SendTcpPacket(client, "Kicked");
        if(ClientDataList.Contains(client))
        ClientDisconnected(client);
    }

    void ClientDisconnected(ClientDataContainer client)
    {
        ClientDataList.Remove(client);
        OnClientDisconnected?.Invoke(client);
        client.AutonomousObjects.ForEach((r) =>
        {
            DestroyReplicatedObject(r.Id);
        });
        Debug.Log("Client Disconnected : " + client.address);
    }

    void RegistNewReplicationObject(ReplicatiorBase replicatior, string PrefabName)
    {
        RepObjects.Add(replicatior);
        replicatior.Id = ObjIdBuffer;
        replicatior.RepPrefabName = PrefabName;
        RepObjPairs.Add(ObjIdBuffer++, replicatior);
        OnNewRepObjectAdded?.Invoke(replicatior);
    }

    void RegistNewAutonomousObject(ClientDataContainer client, ReplicatiorBase replicatior, string PrefabName)
    {
        RegistNewReplicationObject(replicatior, PrefabName);
        replicatior.OwnerNetId = client.NetworkId;
        client.AutonomousObjects.Add(replicatior);
        OnNewAutonomousObjectAdded?.Invoke(replicatior);
    }

    byte[] CreateReplicationData(ClientDataContainer client)
    {
        byte[] data = new byte[0];
        RepObjects.ForEach((obj) =>
        {
            if (obj.DoesClientNeedReplication(client))
            {
                byte[] vs = obj.GetReplicationData();
                if (vs != null)
                    data = data.Concat(encoding.GetBytes(obj.Id + ":")).Concat(vs).Concat(encoding.GetBytes("$")).ToArray();
            }
        });
        return data;
    }

    /// <summary>
    /// Send ReplicationData to all client. Dont recommend manually call.
    /// </summary>
    public void Replicate()
    {
        ClientDataList.ForEach((client) =>
        {
            byte[] data = CreateReplicationData(client);
            if (data.Length < 1)
                return;
            UdpSocket.Send(data, data.Length, new IPEndPoint(client.address, UdpPortNum));
            Debug.Log("Rep : " + client.address + " / " + data.Length + "  " + encoding.GetString(data));
        });
    }

    void DecompClientRequest(byte[] data, ClientDataContainer client)
    {
        string datastr = encoding.GetString(data);
        string[] vs = datastr.Contains('$') ? datastr.Split('$') : new string[] { datastr };
        foreach (string s in vs)
        {
            ClientRequest(s, client);
        }
    }

    void ClientRequest(string request, ClientDataContainer client)
    {
        string[] vs = request.Contains(',') ? request.Split(',') : new string[] { request };
        switch (vs[0])
        {
            case "NewAutoObj":
                CreateAutonomousPrefab(vs[1], vs[2], Serializer.StringToVector3(vs[3], vs[4], vs[5]), Serializer.StringToVector3(vs[6], vs[7], vs[8]), vs[9], client);
                break;
            case "DestAutoObj":
                RepObjPairs.TryGetValue(int.Parse(vs[1]), out ReplicatiorBase replicatior);
                if (client.AutonomousObjects.Contains(replicatior))
                    DestroyReplicatedObject(int.Parse(vs[1]));
                break;
            case "Init":
                SendInitialMessage(client);
                break;
            case "RPWCOS": //WideRange RPC On Server
                ProcessRPC_Wide(vs[1], vs[2], vs[3]);
                break;
            case "RPWCOC": //WideRange RPC On Client
                HandOutRPC_Wide(byte.Parse(vs[1]), vs[2], vs[3], vs[4]);
                break;
            case "RPWCMC": //MultiCast WideRange RPC
                MultiCastRPC_Wide(vs[1], vs[2], vs[3]);
                break;
            case "RPCOS": //RPC On Server
                ProcessRPC(int.Parse(vs[1]), vs[2], vs[3]);
                break;
            case "RPCOC": //RPC On Client
                HandOutRPC(byte.Parse(vs[1]), vs[2], vs[3], vs[4]);
                break;
            case "RPCMC": //MultiCast RPC
                MultiCastRPC(vs[1], vs[2], vs[3]);
                break;
            case "Disconnect":
                ClientDisconnected(client);
                break;
            default:
                OnTcpMessageReceived?.Invoke(encoding.GetBytes(request), client);
                break;
        }
    }

    void DecompClientAutonomousData(byte[] data, ClientDataContainer client)
    {
        string datastr = encoding.GetString(data);
        string[] vs = datastr.Contains('$') ? datastr.Split('$') : new string[] { datastr };
        foreach (string s in vs)
        {
            if (s.Length < 1)
                return;
            if (s.IndexOf(':') < 0)
                return;
            int Id = int.Parse(s.Substring(0, s.IndexOf(':')));
            HandOutAutonomousData(Id, encoding.GetBytes(s.Substring(s.IndexOf(':') + 1)));
        }
    }

    void HandOutAutonomousData(int Id, byte[] data)
    {
        if (RepObjPairs.TryGetValue(Id, out ReplicatiorBase Target))
        {
            Target.ReceiveAutonomousData(data);
        }
    }
    /// <summary>
    /// Create Gameobject replicated On all Client <!warning> Prefab must contain Replicator!
    /// </summary>
    /// <param name="PrefabName">Resources/Prefabs/...</param>
    /// <param name="pos"></param>
    /// <param name="eular"></param>
    /// <param name="ParentObjName"></param>
    /// <returns></returns>
    public GameObject CreateNetworkPrefab(string PrefabName, Vector3 pos, Vector3 eular, string ParentObjName)
    {
        string path = "Prefabs/" + PrefabName;
        GameObject Pobj = (GameObject)Resources.Load(path), parentobj = GameObject.Find(ParentObjName), obj;
        if (parentobj != null)
            obj = Instantiate(Pobj, pos, Quaternion.Euler(eular.x, eular.y, eular.z), parentobj.transform);
        else
            obj = Instantiate(Pobj, pos, Quaternion.Euler(eular.x, eular.y, eular.z));
        ReplicatiorBase replicatior = obj.GetComponent<ReplicatiorBase>();
        if (replicatior == null)
        {
            Debug.Log("CreatingNetworkPrefab Request Refused! Attach Replicator To Prefab!");
            Destroy(obj);
            return null;
        }
        RegistNewReplicationObject(replicatior, PrefabName);
        ClientDataList.ForEach((c) =>
        {
            SendTcpPacket(c, encoding.GetBytes("NewRepObj," + PrefabName + "," + Serializer.Vector3ToString(pos) + "," +
                Serializer.Vector3ToString(eular) + "," + ParentObjName + "," + replicatior.Id + "," + replicatior.OwnerNetId));
        });
        return obj;
    }

    /// <summary>
    /// Create Gameobject replicated On all Client. On LocalHost, Object beact as LocalPrefab. On Client, Object beact as NetworkPrefab. <!warning> Prefab must contain Replicator!
    /// </summary>
    /// <paramref name="LocalPrefabName"/> Local Replicated Object. Search in Resources/Prefabs/...
    /// <param name="NetworkPrefabName"> Replicated Object On Networking. Search in Resources/Prefabs/... </param>
    /// <param name="pos"></param>
    /// <param name="eular"></param>
    /// <param name="ParentObjName"></param>
    /// <returns></returns>
    public GameObject CreateNetworkPrefab(string LocalPrefabName, string NetworkPrefabName, Vector3 pos, Vector3 eular, string ParentObjName)
    {
        string path = "Prefabs/" + LocalPrefabName;
        GameObject Pobj = (GameObject)Resources.Load(path), parentobj = GameObject.Find(ParentObjName), obj;
        if (parentobj != null)
            obj = Instantiate(Pobj, pos, Quaternion.Euler(eular.x, eular.y, eular.z), parentobj.transform);
        else
            obj = Instantiate(Pobj, pos, Quaternion.Euler(eular.x, eular.y, eular.z));
        ReplicatiorBase replicatior = obj.GetComponent<ReplicatiorBase>();
        if (replicatior == null)
        {
            Debug.Log("CreatingNetworkPrefab Request Refused! Attach Replicator To Prefab!");
            Destroy(obj);
            return null;
        }
        RegistNewReplicationObject(replicatior, NetworkPrefabName);
        ClientDataList.ForEach((c) =>
        {
            SendTcpPacket(c, encoding.GetBytes("NewRepObj," + NetworkPrefabName + "," + Serializer.Vector3ToString(pos) + "," +
                Serializer.Vector3ToString(eular) + "," + ParentObjName + "," + replicatior.Id + "," + replicatior.OwnerNetId));
        });
        return obj;
    }

    /// <summary>
    /// Replicate Object as RepPrefabObj
    /// </summary>
    /// <param name="replicatior"></param>
    /// <param name="RepPrefabName"></param>
    public void StartReplicateObject(ReplicatiorBase replicatior, string RepPrefabName)
    {
        RegistNewReplicationObject(replicatior, RepPrefabName);
        ClientDataList.ForEach((c) =>
        {
            SendTcpPacket(c, encoding.GetBytes("NewRepObj," + RepPrefabName + "," + Serializer.Vector3ToString(replicatior.transform.position) + "," +
                Serializer.Vector3ToString(replicatior.transform.eulerAngles) + "," + replicatior.transform.parent.gameObject.name + "," + replicatior.Id + "," + replicatior.OwnerNetId));
        });
    }

    void CreateAutonomousPrefab(string PrefabName, string ObjName, Vector3 pos, Vector3 eular, string ParentObjName, ClientDataContainer Owner)
    {
        string path = "Prefabs/" + PrefabName;
        GameObject Pobj = (GameObject)Resources.Load(path), parentobj = GameObject.Find(ParentObjName), obj;
        if (parentobj != null)
            obj = Instantiate(Pobj, pos, Quaternion.Euler(eular.x, eular.y, eular.z), parentobj.transform);
        else
            obj = Instantiate(Pobj, pos, Quaternion.Euler(eular.x, eular.y, eular.z));
        ReplicatiorBase replicatior = obj.GetComponent<ReplicatiorBase>();
        if (replicatior == null)
        {
            Debug.Log("CreatingAutonomousPrefab Request Refused! Attach Replicator To Prefab!");
            Destroy(obj);
            return;
        }

        RegistNewAutonomousObject(Owner, replicatior, PrefabName);
        ClientDataList.ForEach((c) =>
        {
            if (c.TcpSocket != Owner.TcpSocket)
                SendTcpPacket(c, encoding.GetBytes("NewRepObj," + PrefabName + "," + Serializer.Vector3ToString(pos) + "," + Serializer.Vector3ToString(eular) + "," + ParentObjName + "," + replicatior.Id));
            else
                SendTcpPacket(c, encoding.GetBytes("AutoObjAdded," + ObjName + "," + replicatior.Id));
        });
    }

    public void DestroyReplicatedObject(int Id)
    {
        if (RepObjPairs.TryGetValue(Id, out ReplicatiorBase replicatior))
        {
            ClientDataList.ForEach((c) =>
                    {
                        if (!c.AutonomousObjects.Contains(replicatior))
                            SendTcpPacket(c, encoding.GetBytes("Dest," + Id));
                        else
                            c.AutonomousObjects.Remove(replicatior);
                    });
            Debug.Log("Destroy NetworkObject : " + replicatior.gameObject.name);
            RepObjects.Remove(replicatior);
            RepObjPairs.Remove(replicatior.Id);
            Destroy(replicatior.gameObject);
        }
    }

    void ProcessRPC(int ObjId, string MethodName, string arg)
    {
        if (RepObjPairs.TryGetValue(ObjId, out ReplicatiorBase replicatior))
        {
            replicatior.SendMessage(MethodName, arg, SendMessageOptions.DontRequireReceiver);
        }
        else
        {
            Debug.Log("Object Id is invalid. RPC failed");
            return;
        }
    }

    void HandOutRPC(byte ClientId, string ObjId, string MethodName, string arg)
    {
        if (ClientId == 0)
            ProcessRPC(int.Parse(ObjId), MethodName, arg);
        else
        {
            SendTcpPacket(ClientDataList.Find((c) => c.NetworkId == ClientId), encoding.GetBytes("RPCOC," + ObjId + "," + MethodName + "," + arg));
        }
    }

    void MultiCastRPC(string ObjId, string MethodName, string arg)
    {
        ProcessRPC(int.Parse(ObjId), MethodName, arg);
        ClientDataList.ForEach((c) => SendTcpPacket(c, encoding.GetBytes("RPCOC," + ObjId + "," + MethodName + "," + arg)));
    }

    void ProcessRPC_Wide(string ObjName, string MethodName, string arg)
    {
        GameObject obj = GameObject.Find(ObjName);
        if (obj == null)
        {
            Debug.Log("Object couldnt find. WideRange RPC failed");
            return;
        }
        obj.SendMessage(MethodName, arg, SendMessageOptions.DontRequireReceiver);
    }

    void HandOutRPC_Wide(byte ClientId, string ObjName, string MethodName, string arg)
    {
        if (ClientId == 0)
            ProcessRPC_Wide(ObjName, MethodName, arg);
        else
        {
            SendTcpPacket(ClientDataList.Find((c) => c.NetworkId == ClientId), encoding.GetBytes("RPWCOC," + ObjName + "," + MethodName + "," + arg));
        }
    }

    void MultiCastRPC_Wide(string ObjName, string MethodName, string arg)
    {
        ProcessRPC_Wide(ObjName, MethodName, arg);
        ClientDataList.ForEach((c) => SendTcpPacket(c, encoding.GetBytes("RPWCOC," + ObjName + "," + MethodName + "," + arg)));
    }

    /// <summary>
    /// manually call is not recommended
    /// </summary>
    public void ServerTick()
    {
        if (ClientDataList.Count < 1)
            return;

        ClientDataList.ForEach((c) =>
        {
            if (c.TcpSocket.Available > 0)
            {
                c.TcpSocket.Client.Receive(buffer);
                Debug.Log("Tcp Received : " + encoding.GetString(buffer));
                DecompClientRequest(buffer, c);
                if (OnTcpPacketReceived != null)
                    OnTcpPacketReceived.Invoke(buffer, c);
            }

        });
        if (UdpSocket.Available > 0)
        {
            IPEndPoint endPoint = null;
            byte[] Udpbuffer = UdpSocket.Receive(ref endPoint);
            Debug.Log("Udp Received : " + encoding.GetString(Udpbuffer));
            ClientDataContainer client = ClientDataList.Find((c) => c.address == endPoint.Address);
            DecompClientAutonomousData(Udpbuffer, client);
            if (OnUdpPacketReceived != null)
                OnUdpPacketReceived.Invoke(Udpbuffer, client);
        }
        Replicate();
    }

    private void OnDestroy()
    {
        if (ClientDataList != null)
            ClientDataList.ForEach((s) => s.TcpSocket.Close());
        if (UdpSocket != null)
            UdpSocket.Close();
    }
}
