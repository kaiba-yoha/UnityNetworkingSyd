using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Linq;
using System;
using System.IO;

public class NetworkManagerForTool_Client : NetworkManagerForTool_Base
{
    public string DOwnIP, TargetIP = "XXXX";
    public string m_TargetIP
    {
        get { return TargetIP; }
        set { TargetIP = value; }
    }

    UdpClient OwnUdpClient;
    TcpClient OwnTcpSocket;
    FTSocketContainer FTContainer;
    public int TcpPortNum = 8675, UdpPortNum = 8676, FTPortNum = 1130;

    public List<ClientDataContainer_ForTool> ConnectedClients;

    /// <summary>
    /// Object Dirctionary of Replicated by server.
    /// </summary>
    public Dictionary<string, IReplicatableObject> RepObjPairs = new Dictionary<string, IReplicatableObject>();
    int buffersize = 10240;
    byte[] databuffer;

    public delegate void ConnectionNotification(NetworkManagerForTool_Client client);
    public delegate void NetworkDataHandler(byte[] data);
    public delegate void MemberUpdateHandler(List<ClientDataContainer_ForTool> clientDatas);

    public event ConnectionNotification OnConnectedToServer;
    public event ConnectionNotification OnAssignedNetwork;
    public event MemberUpdateHandler OnMemberUpdated;
    public event ReplicatedObjectNotification OnNewRepObjectAdded;
    public event NetworkDataHandler OnTcpPacketReceived;
    public event NetworkDataHandler OnTcpMessageReceived;
    public event NetworkDataHandler OnUdpPacketReceived;
    public event FileTransmissionHandler OnUploadCompleted;
    public event FileTransmissionHandler OnDownloadCompleted;

    public string OwnUserName { get { return username; } set { username = value; } }
    string username;

    /// <summary>
    /// Set this variable before Call Launch()
    /// </summary>
    public NetworkRole networkRole = NetworkRole.Guest;

    public override void Initialize()
    {
        base.Initialize();
    }

    public override void Launch()
    {
        base.Launch();
        LaunchNetworkClient();
    }

    public override void ShutDown()
    {
        base.ShutDown();
        ShutDownClient();
    }

    void LaunchNetworkClient()
    {
        databuffer = new byte[buffersize];
        OwnTcpSocket = new TcpClient();
        OwnUdpClient = new UdpClient(UdpPortNum);
        FTContainer = new FTSocketContainer();
        FTContainer.FTSocket = new TcpClient();
        ConnectedClients = new List<ClientDataContainer_ForTool>();
        try
        {
            OwnTcpSocket.BeginConnect(TargetIP, TcpPortNum, ConnectedToServerCallback, 0);
        }
        catch
        {
            DebugLogging("Couldnt find Server");
            return;
        }
        LocalInst = this;
    }

    void ShutDownClient()
    {
        if (databuffer == null)
            return;
        databuffer = null;
        if (OwnTcpSocket != null && OwnTcpSocket.Connected)
            SendTcpPacket(encoding.GetBytes("Disconnect$"));
        OwnTcpSocket.Close();
        OwnTcpSocket = null;
        OwnUdpClient.Close();
        OwnUdpClient = null;
        FTContainer.FTSocket?.Close();
        FTContainer.FTSocket = null;
        RepObjPairs.Clear();
        RepObjPairs = null;
        LocalInst = null;
    }

    void ConnectedToServerCallback(System.IAsyncResult ar)
    {
        OwnTcpSocket.EndConnect(ar);
        if (OwnTcpSocket.Connected)
            DebugLogging("Client: Connected to Server");
        SendTcpPacket(encoding.GetBytes("Init," + OwnUserName + "," + (int)networkRole + "$"));
        FTContainer.FTSocket.Connect(TargetIP, FTPortNum);
        FTContainer.address = IPAddress.Parse(TargetIP);
        OnUploadCompleted += DequeueUpload;
        OnConnectedToServer?.Invoke(this);
        //OwnUdpClient.Connect(TargetIP, UdpPortNum);
    }

    public void SendTcpPacket(byte[] data)
    {
        if (!IsNetworkAvailable)
            return;
        try
        {
            OwnTcpSocket.Client.Send(data);
        }
        catch (Exception ex)
        {
            DebugLogging(ex.Message + "   " + ex.StackTrace);
            DebugLogging("Connection Lost.");
        }
    }
    public void SendTcpPacket(string data)
    {
        if (!IsNetworkAvailable)
            return;
        try
        {
            OwnTcpSocket.Client.Send(encoding.GetBytes(data));
        }
        catch (Exception ex)
        {
            DebugLogging(ex.Message + "   " + ex.StackTrace);
            DebugLogging("Connection Lost.");
        }
    }
    private void DequeueUpload(FTSocketContainer fTSocket)
    {
        if (fTSocket.UploadQueue.Count < 1)
            return;
        ResourceData resource = ResourceManager.GetResource(fTSocket.UploadQueue.Dequeue());
        if (resource == null)
            return;
        UploadFile(resource);
    }

    public void UploadFile(ResourceData resource)
    {
        if (resource == null)
            return;

        try
        {
            if (FTContainer.Sending)
            {
                if (!FTContainer.UploadQueue.Contains(resource.Id))
                    FTContainer.UploadQueue.Enqueue(resource.Id);
                return;
            }
            DebugLogging("Start File Upload : " + resource.name + resource.extension);
            FTContainer.Sending = true;
            FTContainer.FTSocket.Client.Send(encoding.GetBytes("FT:" + resource.name + resource.extension + "|" + resource.Id + "$"));
            FTContainer.FTSocket.Client.BeginSendFile(resource.path, new AsyncCallback(SendFileCallback), resource);
        }
        catch (Exception ex)
        {
            DebugLogging(ex.Message + "   " + ex.StackTrace);
        }
    }

    void SendFileCallback(IAsyncResult result)
    {
        FTContainer.FTSocket.Client.EndSendFile(result);
        FTContainer.FTSocket.Client.Send(encoding.GetBytes("$End"));
        FTContainer.Sending = false;
        DebugLogging("File Uploaded");

        OnUploadCompleted?.Invoke(FTContainer);
    }

    public void RequestFile(string id)
    {
        if (FTContainer.Transdata != null)
            if (FTContainer.Transdata.Id == id)
                return;
        FTContainer.FTSocket.Client.Send(encoding.GetBytes("Req:" + id + "$"));
        DebugLogging("Request File : " + id);
    }

    void NetworkInitialize(byte NewId)
    {
        RepObjPairs.Values.ToList().ForEach((rep) =>
        {
            if (rep.GetInfoContainer().IsOwner())
                rep.GetInfoContainer().OwnerNetId = NewId;
        });
        NetworkId = NewId;
        OwnUdpClient.Send(encoding.GetBytes("InitRep$"), encoding.GetByteCount("InitRep$"), new IPEndPoint(IPAddress.Parse(TargetIP), UdpPortNum));
        OnAssignedNetwork?.Invoke(this);
    }

    void UpdateClientList(string[] data)
    {
        ConnectedClients.Clear();
        for (int i = 1; i < data.Length; i += 3)
        {
            ClientDataContainer_ForTool container = new ClientDataContainer_ForTool()
            {
                UserName = data[i],
                NetworkId = byte.Parse(data[i + 1]),
                role = (NetworkRole)Enum.Parse(typeof(NetworkRole), data[i + 2])
            };
            ConnectedClients.Add(container);
        }
        OnMemberUpdated?.Invoke(ConnectedClients);
    }

    public override void Tick(object state)
    {
        base.Tick(state);
        if (OwnTcpSocket == null || !OwnTcpSocket.Connected)
            return;
        if (OwnTcpSocket.Available > 0)
        {
            int length = OwnTcpSocket.Client.Receive(databuffer);
            byte[] vs = new byte[length];
            Buffer.BlockCopy(databuffer, 0, vs, 0, length);
            DebugLogging("Tcp Received : " + encoding.GetString(vs));
            DecompServerMessage(vs);
            if (OnTcpPacketReceived != null)
                OnTcpPacketReceived.Invoke(vs);
        }
        if (FTContainer.FTSocket.Available > 0)
        {
            databuffer = new byte[FTContainer.FTSocket.Client.ReceiveBufferSize];
            int length = FTContainer.FTSocket.Client.Receive(databuffer);
            byte[] trimmeddata = new byte[length];
            Buffer.BlockCopy(databuffer, 0, trimmeddata, 0, length);
            //DebugLogging("FTP Received : " + encoding.GetString(trimmeddata));
            ProcessFileTransmissionInfo(trimmeddata);
        }
        if (OwnUdpClient.Available > 0)
        {
            IPEndPoint endPoint = null;
            byte[] Udpdatabuffer = OwnUdpClient.Receive(ref endPoint);
            DecompReplicationData(Udpdatabuffer);
            if (OnUdpPacketReceived != null)
                OnUdpPacketReceived.Invoke(Udpdatabuffer);
        }
        ReplicateAutonomousObject();
    }



    public void AddNewReplicateObject(IReplicatableObject replicator, string Id, byte OwnerId)
    {
        if (replicator == null)
        {
            DebugLogging("Replicator is null. AddNewReplicatedObject Failed");
            return;
        }
        if (RepObjPairs.ContainsKey(Id))
            return;

        ReplicationInfoContainer repinfo = replicator.GetInfoContainer();
        repinfo.Id = Id;
        repinfo.OwnerNetId = OwnerId;
        repinfo.RPCRequestHandler += (data) => SendTcpPacket(encoding.GetBytes("RPC," + repinfo.Id + ",").Concat(data).ToArray());
        repinfo.ReAssignResourcesRequestHandler += (data) =>
        {
            string resIddata = "";
            replicator.GetResourceObj().GetAllDependResources().ForEach((res) =>
            {
                resIddata += "," + res.Id;
            });
            SendTcpPacket("ResUp," + repinfo.Id + resIddata);
        };
        DebugLogging("Regist New RepObj : " + repinfo.Id);
        RepObjPairs.Add(Id, replicator);
        OnNewRepObjectAdded?.Invoke(replicator);
    }


    void DestroyReplicatedObject(string Id)
    {
        if (RepObjPairs.TryGetValue(Id, out IReplicatableObject replicatior))
        {
            RepObjPairs.Remove(Id);
            Destroy(replicatior);
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
            case "End":
                DebugLogging("Server Shutdown");
                ShutDownClient();
                break;
            case "Kicked":
                DebugLogging("Kicked From Server");
                ShutDownClient();
                break;
            case "Assign":
                NetworkInitialize(byte.Parse(vs[1]));
                break;
            case "CList":
                UpdateClientList(vs);
                break;
            case "RPC":
                ProcessRemoteProcedureCall(vs[1], encoding.GetBytes(mes.Substring(5 + vs[1].Length)));
                break;
            default:
                OnTcpMessageReceived?.Invoke(encoding.GetBytes(mes));
                break;
        }
    }
    void ProcessRemoteProcedureCall(string ObjId, byte[] data)
    {
        if (!RepObjPairs.TryGetValue(ObjId, out IReplicatableObject replicatable))
            return;
        replicatable.ReceiveNotify(data);
    }

    void ProcessFileTransmissionInfo(byte[] data)
    {

        string datastr = encoding.GetString(data);
        byte[] filedatabuffer;

        if (datastr.StartsWith("Req:"))
        {
            ResourceData resource = ResourceManager.GetResource(datastr.Substring(4, datastr.IndexOf("$") - 4));
            if (!FTContainer.UploadQueue.Contains(resource.path))
                UploadFile(resource);
            return;
        }

        if (datastr.StartsWith("FT:"))
        {
            int infodelim = datastr.IndexOf("$"), iddelim = datastr.IndexOf('|');
            string filename = datastr.Substring(3, iddelim - 3), Id = datastr.Substring(iddelim + 1, infodelim - iddelim - 1);
            if (ResourceManager.GetResource(Id) != null)
            {
                FTContainer.Transdata = ResourceManager.GetResource(Id);
                FTContainer.Transdata.ChangeResourcePath(Path.GetDirectoryName(FTContainer.Transdata.path) + filename, FTContainer.Transdata.URI.IsAbsoluteUri ? UriKind.Absolute : UriKind.Relative);
                FTContainer.IsReceivingUpdate = true;
            }
            else
                FTContainer.Transdata = new ResourceData(ResourceManager.GetDLResourceFolderPath() + filename, Id, UriKind.Relative);
            string cstr = datastr.Substring(0, infodelim + 1);

            FTContainer.filestream = new FileStream(FTContainer.Transdata.path, FileMode.Create);

            int clength = data.Length - encoding.GetByteCount(cstr);
            if (clength > 0)
            {
                filedatabuffer = new byte[clength];
                Buffer.BlockCopy(data, encoding.GetByteCount(cstr), filedatabuffer, 0, filedatabuffer.Length);
                FTContainer.filestream.Write(filedatabuffer, 0, filedatabuffer.Length);
            }
            else
                filedatabuffer = new byte[0];

            DebugLogging("File Receiving..." + FTContainer.Transdata.name + FTContainer.Transdata.extension + " Id : " + FTContainer.Transdata.Id);
            DebugLogging("Received File Data :" + FTContainer.filestream.Length + " ( +" + FTContainer.filestream.Length);

            if (datastr.EndsWith("$End"))
            {

                FTContainer.filestream?.Close();
                if (FTContainer.IsReceivingUpdate)
                    FTContainer.Transdata.IssueFinishedUpdateNotify();
                else
                    ResourceManager.AddResource(FTContainer.Transdata);
                OnDownloadCompleted?.Invoke(FTContainer);

                DebugLogging("File Received successfully " + FTContainer.Transdata.name + FTContainer.Transdata.extension + " Id : " + FTContainer.Transdata.Id);

                FTContainer.IsReceivingUpdate = false;
                FTContainer.Transdata = null;
            }
        }
        else if (datastr.EndsWith("$End"))
        {
            int slength = data.Length - encoding.GetByteCount("$End");
            if (slength > 0)
            {
                byte[] fdata = new byte[data.Length - encoding.GetByteCount("$End")];
                Buffer.BlockCopy(data, 0, fdata, 0, fdata.Length);
                //FTContainer.filebuffer = FTContainer.filebuffer.Concat(fdata).ToArray();
                FTContainer.filestream.Write(fdata, 0, fdata.Length);

                DebugLogging("Received File Data :" + FTContainer.filestream.Length + " ( +" + fdata.Length);
            }
            try
            {
                FTContainer.filestream?.Close();
                if (FTContainer.IsReceivingUpdate)
                    FTContainer.Transdata.IssueFinishedUpdateNotify();
                else
                    ResourceManager.AddResource(FTContainer.Transdata);
                OnDownloadCompleted?.Invoke(FTContainer);

                DebugLogging("File Received successfully " + FTContainer.Transdata.name + FTContainer.Transdata.extension + " Id : " + FTContainer.Transdata.Id);

                FTContainer.IsReceivingUpdate = false;
                FTContainer.Transdata = null;
            }
            catch (Exception ex)
            {
                DebugLogging(ex.Message + "\n" + ex.StackTrace);
            }
        }
        else
        {
            if (!FTContainer.filestream.CanWrite)
                return;

            FTContainer.filestream.Write(data, 0, data.Length);
            //FTContainer.filebuffer = FTContainer.filebuffer.Concat(data).ToArray();

            DebugLogging("Received File Data :" + FTContainer.filestream.Length + " ( +" + data.Length);
        }
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
            string Id = s.Substring(0, s.IndexOf(':'));
            HandOutReplicationData(Id, encoding.GetBytes(s.Substring(s.IndexOf(':') + 1)));
        }
    }

    void HandOutReplicationData(string id, byte[] data)
    {
        IReplicatableObject Target;
        if (RepObjPairs.TryGetValue(id, out Target))
        {
            Target.ReceiveReplicationData(data, 0);
        }
    }

    byte[] CreateReplicationData()
    {
        byte[] data = new byte[0];
        RepObjPairs.Values.ToList().ForEach((obj) =>
        {
            if (obj.DoesServerNeedReplication())
            {
                byte[] vs = obj.GetReplicationData();
                if (vs != null)
                    data = data.Concat(encoding.GetBytes(obj.GetInfoContainer().Id + ":")).Concat(vs).Concat(encoding.GetBytes("$")).ToArray();
            }
        });
        return data;
    }

    void ReplicateAutonomousObject()
    {
        byte[] data = CreateReplicationData();
        if (data.Length < 1)
            return;
        OwnUdpClient.Send(data, data.Length, new IPEndPoint(IPAddress.Parse(TargetIP), UdpPortNum));
        RepObjPairs.Values.ToList().ForEach((obj) => obj.OnFinishedReplication());
        DebugLogging("Rep : " + TargetIP + " / " + data.Length + "  " + encoding.GetString(data));
    }

}
