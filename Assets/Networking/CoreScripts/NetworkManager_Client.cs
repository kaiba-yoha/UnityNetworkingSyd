using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Linq;


public class NetworkManager_Client : NetworkManagerBase
{
    public string DOwnIP, TargetIP;
    public string m_TargetIP
    {
        get { return TargetIP; }
        set { TargetIP = value; }
    }

    UdpClient OwnUdpClient;
    TcpClient OwnTcpSocket;
    [SerializeField]
    bool LaunchOnStart;
    [SerializeField]
    int TcpPortNum = 7890, UdpPortNum = 7891;
    /// <summary>
    /// Object List of Autonomous Object.
    /// </summary>
    List<ReplicatiorBase> AutonomausObjects;
    /// <summary>
    /// Object Dirctionary of Replicated by server.
    /// </summary>
    Dictionary<int, ReplicatiorBase> RepObjPairs;
    int buffersize = 512;
    byte[] databuffer;
    [SerializeField]
    float ClientUpdateInterval = 0.033f;

    public delegate void ConnectionNotification(NetworkManager_Client client);
    public delegate void NetworkDataHandler(byte[] data);

    public ConnectionNotification OnConnectedToServer;
    public ConnectionNotification OnAssignedNetwork;
    public ReplicatedObjectNotification OnNewRepObjectAdded;
    public ReplicatedObjectNotification OnNewAutonomousObjectAdmitted;
    public NetworkDataHandler OnTcpPacketReceived;
    public NetworkDataHandler OnTcpMessageReceived;
    public NetworkDataHandler OnUdpPacketReceived;

    // Start is called before the first frame update
    void Start()
    {
        if (LaunchOnStart)
        {
            LaunchNetworkClient();
        }
    }

    public override void Launch()
    {
        LaunchNetworkClient();
    }

    public override void ShutDown()
    {
        ShutDownClient();
    }

    public void LaunchNetworkClient()
    {
        databuffer = new byte[buffersize];
        OwnTcpSocket = new TcpClient();
        OwnUdpClient = new UdpClient(UdpPortNum);
        AutonomausObjects = new List<ReplicatiorBase>();
        RepObjPairs = new Dictionary<int, ReplicatiorBase>();
        try
        {
            OwnTcpSocket.BeginConnect(TargetIP, TcpPortNum, ConnectedToServerCallback, 0);
        }
        catch
        {
            Debug.Log("Couldnt find Server");
            return;
        }
        LocalInst = this;
    }

    public void ShutDownClient()
    {
        databuffer = null;
        CancelInvoke();
        if(OwnTcpSocket.Connected)
        SendTcpPacket(encoding.GetBytes("Disconnect$"));
        OwnTcpSocket.Close();
        OwnTcpSocket = null;
        OwnUdpClient.Close();
        OwnUdpClient = null;
        AutonomausObjects.Clear();
        AutonomausObjects = null;
        RepObjPairs.Clear();
        RepObjPairs = null;
        LocalInst = null;
    }

    void ConnectedToServerCallback(System.IAsyncResult ar)
    {
        OwnTcpSocket.EndConnect(ar);
        if (OwnTcpSocket.Connected)
            Debug.Log("Client: Connected to Server");
        SendTcpPacket(encoding.GetBytes("Init$"));
        OwnUdpClient.Send(encoding.GetBytes("InitRep$"), encoding.GetByteCount("InitRep$"), new IPEndPoint(IPAddress.Parse(TargetIP), UdpPortNum));
        if (OnConnectedToServer != null)
            OnConnectedToServer.Invoke(this);
        InvokeRepeating("ClientTick", ClientUpdateInterval, ClientUpdateInterval);
        //OwnUdpClient.Connect(TargetIP, UdpPortNum);
    }

    public void SendTcpPacket(byte[] data)
    {
        try
        {
            OwnTcpSocket.Client.Send(data);
        }
        catch
        {
            Debug.Log("Connection Lost.");
        }
    }
    public void SendTcpPacket(string data)
    {
        try
        {
            OwnTcpSocket.Client.Send(encoding.GetBytes(data));
        }
        catch
        {
            Debug.Log("Connection Lost.");
        }
    }

    void NetworkInitialize(byte NewId)
    {
        NetworkId = NewId;
        if (OnAssignedNetwork != null)
            OnAssignedNetwork.Invoke(this);
    }

    public void ClientTick()
    {
        if (OwnTcpSocket == null || !OwnTcpSocket.Connected)
            return;
        if (OwnTcpSocket.Available > 0)
        {
            OwnTcpSocket.Client.Receive(databuffer);
            Debug.Log("Tcp Received :" + encoding.GetString(databuffer));
            DecompServerMessage(databuffer);
            if (OnTcpPacketReceived != null)
                OnTcpPacketReceived.Invoke(databuffer);
        }
        if (OwnUdpClient.Available > 0)
        {
            IPEndPoint endPoint = null;
            byte[] Udpdatabuffer = OwnUdpClient.Receive(ref endPoint);
            Debug.Log("Udp Received : " + encoding.GetString(Udpdatabuffer));
            DecompReplicationData(Udpdatabuffer);
            if (OnUdpPacketReceived != null)
                OnUdpPacketReceived.Invoke(Udpdatabuffer);
        }
        ReplicateAutonomousObject();
    }

    /// <summary>
    /// Send creating autonomous object request to server <!waring!> Autonomous Object must have One and Only Name!
    /// </summary>
    /// <param name="replicatior">Autonomous obj</param>
    /// <param name="ObjName">Identity of autonomous obj</param>
    /// <param name="ReplicatedPrefabName">Name of prefab replicated on others</param>
    /// <param name="pos">replicated prefab initial position</param>
    /// <param name="eular">replicated prefab initial eularAngle</param>
    /// <param name="ParentName">replicated prefab initial parent name</param>
    public void RequestCreatingNewAutonomousObject(ReplicatiorBase replicatior, string ReplicatedPrefabName, Vector3 pos, Vector3 eular, string ParentName)
    {
        SendTcpPacket(encoding.GetBytes("NewAutoObj," + ReplicatedPrefabName + "," + replicatior.gameObject.name + "," + Serializer.Vector3ToString(pos) +
            "," + Serializer.Vector3ToString(eular) + "," + ParentName));
    }

    public void DestroyAutonomousObject(ReplicatiorBase replicatior)
    {
        SendTcpPacket(encoding.GetBytes("DestAutoObj," + replicatior.Id));
        RepObjPairs.Remove(replicatior.Id);
        AutonomausObjects.Remove(replicatior);
        Destroy(replicatior.gameObject);
    }

    public void RequestRPCOnServer(ReplicatiorBase RPCTarget, string MethodName, string arg)
    {
        SendTcpPacket(encoding.GetBytes("RPCOS," + RPCTarget.Id + "," + MethodName + "," + arg));
    }

    public void RequestRPCOnOtherClient(ReplicatiorBase RPCTarget, string MethodName, string arg, byte ClientId)
    {
        if (ClientId == NetworkId)
            ProcessRPC(RPCTarget.Id, MethodName, arg);
        else
        {
            SendTcpPacket(encoding.GetBytes("RPCOC," + ClientId + "," + RPCTarget.Id + "," + MethodName + "," + arg));
        }
    }

    public void RequestRPCMultiCast(ReplicatiorBase RPCTarget, string MethodName, string arg)
    {
        SendTcpPacket(encoding.GetBytes("RPCMC," + RPCTarget.Id + "," + MethodName + "," + arg));
    }

    public void RequestRPCOnServer_Wide(string ServerObjectName, string MethodName, string arg)
    {
        SendTcpPacket(encoding.GetBytes("RPWCOS," + ServerObjectName + "," + MethodName + "," + arg));
    }

    public void RequestRPCOnOtherClient_Wide(string ObjectName, string MethodName, string arg, byte ClientId)
    {
        if (ClientId == NetworkId)
            ProcessRPC_Wide(ObjectName, MethodName, arg);
        else
        {
            SendTcpPacket(encoding.GetBytes("RPWCOC," + ClientId + "," + ObjectName + "," + MethodName + "," + arg));
        }
    }

    public void RequestRPCMultiCast_Wide(string ObjectName, string MethodName, string arg)
    {
        SendTcpPacket(encoding.GetBytes("RPWCMC," + ObjectName + "," + MethodName + "," + arg));
    }

    void AddNewReplicatedObject(ReplicatiorBase replicatior, int Id, byte OwnerId)
    {
        replicatior.Id = Id;
        replicatior.OwnerNetId = OwnerId;
        replicatior.LocalHostNetId = NetworkId;
        RepObjPairs.Add(Id, replicatior);
        if (OnNewRepObjectAdded != null)
            OnNewRepObjectAdded.Invoke(replicatior);
    }

    void AddAdmittedAutonomousObject(string ObjName, int ObjId)
    {
        GameObject obj = GameObject.Find(ObjName);

        if (obj == null)
            return;

        ReplicatiorBase replicatior = obj.GetComponent<ReplicatiorBase>();
        AddNewReplicatedObject(replicatior, ObjId, NetworkId);
        AutonomausObjects.Add(replicatior);
        if (OnNewAutonomousObjectAdmitted != null)
            OnNewAutonomousObjectAdmitted.Invoke(replicatior);

        Debug.Log("New Autonomous Object : " + ObjName);
    }

    void DestroyReplicatedObject(int Id)
    {
        if (RepObjPairs.TryGetValue(Id, out ReplicatiorBase replicatior))
        {
            RepObjPairs.Remove(Id);
            Destroy(replicatior.gameObject);
        }
    }

    void DecompServerMessage(byte[] data)
    {
        string datastr = encoding.GetString(data);
        string[] vs = datastr.Contains('$') ? datastr.Split('$') : new string[] { datastr };
        foreach (string s in vs)
        {
            ProcessServerMessage(s);
        }
    }

    void ProcessServerMessage(string mes)
    {
        string[] vs = mes.Contains(',') ? mes.Split(',') : new string[] { mes };
        switch (vs[0])
        {
            case "NewRepObj":
                CreateReplicatedPrefab(vs[1], Serializer.StringToVector3(vs[2], vs[3], vs[4]), Serializer.StringToVector3(vs[5], vs[6], vs[7]), vs[8], int.Parse(vs[9]), byte.Parse(vs[10]));
                break;
            case "AutoObjAdded":
                AddAdmittedAutonomousObject(vs[1], int.Parse(vs[2]));
                break;
            case "Dest":
                DestroyReplicatedObject(int.Parse(vs[1]));
                break;
            case "End":
                Debug.Log("Server Shutdown");
                ShutDownClient();
                break;
            case "Kicked":
                Debug.Log("Kicked From Server");
                ShutDownClient();
                break;
            case "RPCOC":
                ProcessRPC(int.Parse(vs[1]), vs[2], vs[3]);
                break;
            case "RPWCOC":
                ProcessRPC_Wide(vs[1], vs[2], vs[3]);
                break;
            case "Assign":
                NetworkInitialize(byte.Parse(vs[1]));
                break;
            default:
                OnTcpMessageReceived?.Invoke(encoding.GetBytes(mes));
                break;
        }
    }

    GameObject CreateReplicatedPrefab(string PrefabName, Vector3 pos, Vector3 eular, string ParentObj, int ObjId, byte OwnerId)
    {
        if (RepObjPairs.TryGetValue(ObjId, out ReplicatiorBase r))
            return null;
        string path = "Prefabs/" + PrefabName;
        GameObject Pobj = (GameObject)Resources.Load(path), parentobj = GameObject.Find(ParentObj), obj;
        if (parentobj != null)
            obj = Instantiate(Pobj, pos, Quaternion.Euler(eular.x, eular.y, eular.z), parentobj.transform);
        else
            obj = Instantiate(Pobj, pos, Quaternion.Euler(eular.x, eular.y, eular.z));
        ReplicatiorBase replicatior = obj.GetComponent<ReplicatiorBase>();
        AddNewReplicatedObject(replicatior, ObjId, OwnerId);
        return obj;
    }

    void ProcessRPC(int ObjectId, string MethodName, string arg)
    {
        if (RepObjPairs.TryGetValue(ObjectId, out ReplicatiorBase replicatior))
        {
            replicatior.SendMessage(MethodName, arg, SendMessageOptions.DontRequireReceiver);
        }
        else
        {
            Debug.Log("Object Id is invalid. RPC failed.");
            return;
        }
    }

    void ProcessRPC_Wide(string ObjectName, string MethodName, string arg)
    {
        GameObject obj = GameObject.Find(ObjectName);
        if (obj == null)
        {
            Debug.Log("Object couldnt find. WideRange RPC failed.");
            return;
        }
        obj.SendMessage(MethodName, arg, SendMessageOptions.DontRequireReceiver);
    }

    void DecompReplicationData(byte[] data)
    {
        string[] vs = encoding.GetString(data).Split('$');
        foreach (string s in vs)
        {
            if (s.Length < 1)
                return;
            if (s.IndexOf(':') < 0)
                return;
            int Id = int.Parse(s.Substring(0, s.IndexOf(':')));
            HandOutReplicationData(Id, encoding.GetBytes(s.Substring(s.IndexOf(':') + 1)));
        }
    }

    void HandOutReplicationData(int id, byte[] data)
    {
        ReplicatiorBase Target;
        if (RepObjPairs.TryGetValue(id, out Target))
        {
            Target.ReceiveReplicationData(data);
        }
    }

    byte[] CreateAutonomousData()
    {
        byte[] data = new byte[0];
        AutonomausObjects.ForEach((obj) =>
        {
            if (obj.DoesServerNeedReplication())
            {
                byte[] vs = obj.GetAutonomousData();
                if (vs != null)
                    data = data.Concat(encoding.GetBytes(obj.Id + ":")).Concat(vs).Concat(encoding.GetBytes("$")).ToArray();
            }
        });
        return data;
    }

    void ReplicateAutonomousObject()
    {
        byte[] data = CreateAutonomousData();
        if (data.Length < 1)
            return;
        OwnUdpClient.Send(data, data.Length, new IPEndPoint(IPAddress.Parse(TargetIP), UdpPortNum));
        Debug.Log("Rep : " + TargetIP + " / " + data.Length + "  " + encoding.GetString(data));
    }

    private void OnDestroy()
    {
        if (OwnTcpSocket != null)
            OwnTcpSocket.Close();
        if (OwnUdpClient != null)
            OwnUdpClient.Close();
    }
}
