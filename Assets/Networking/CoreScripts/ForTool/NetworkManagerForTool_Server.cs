using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Linq;
using System.Threading;
using System.IO;
using System;

public class ClientDataContainer_ForTool
{
    public IPAddress address;
    public TcpClient TcpSocket;
    public string UserName;
    public byte NetworkId;
    public NetworkRole role;
    public int UdpEndPoint = 8676;
}

public class FTSocketContainer
{
    public ResourceData Transdata;
    public FileStream filestream;
    public IPAddress address;
    public TcpClient FTSocket;
    public bool Sending = false;
    public bool IsReceivingUpdate = false;
    public Queue<string> UploadQueue = new Queue<string>();


    Encoding encoding = Encoding.UTF8;

}

public enum NetworkRole
{
    Host, Guest, Inspector
}



public class NetworkManagerForTool_Server : NetworkManagerForTool_Base
{
    public static NetworkManagerForTool_Server server;

    public string DOwnIP;
    TcpListener listener, ftplistner;
    UdpClient UdpSocket;
    public List<ClientDataContainer_ForTool> ClientDataList;
    public List<FTSocketContainer> FTSocketsList;
    public int TcpPortNum = 8675, UdpPortNum = 8676, FTPortNum = 1130;
    int buffersize = 10240;
    byte[] buffer;
    /// <summary>
    /// Dictionary of Replication targets Key=ReplicatorBase.Id
    /// </summary>
    public Dictionary<string, IReplicatableObject> RepObjPairs;
    /// <summary>
    /// List of Replication targets
    /// </summary>
    public List<IReplicatableObject> RepObjects;
    byte NetIdBuffer;

    int m_ServerAcceptInterval = 2000;
    public int ServerAcceptInterval
    {
        get { return m_ServerAcceptInterval; }
        set
        {
            m_ServerAcceptInterval = value;
            AcceptTimer?.Change(m_ServerAcceptInterval, m_ServerAcceptInterval);
        }
    }
    Timer AcceptTimer;

    public delegate void ClientNotification(ClientDataContainer_ForTool clientData);
    public delegate void NetworkDataHandler(byte[] data, ClientDataContainer_ForTool clientData);
    public event ReplicatedObjectNotification OnNewRepObjectAdded;
    public event ClientNotification OnNewClientConnected;
    public event ClientNotification OnNewClientInitialized;
    public event ClientNotification OnClientDisconnected;
    public event NetworkDataHandler OnTcpPacketReceived;
    public event NetworkDataHandler OnUdpPacketReceived;
    public event NetworkDataHandler OnTcpMessageReceived;
    public event FileTransmissionHandler OnUploadCompleted;
    public event FileTransmissionHandler OnDownloadCompleted;


    public override void Initialize()
    {
        base.Initialize();
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

    }

    public override void Launch()
    {
        base.Launch();
        LaunchNetworkServer();
    }

    public override void ShutDown()
    {
        base.ShutDown();
        ShutDownServer();
    }

    /// <summary>
    /// Launch Server System. Start Listen on TcpPortNum.
    /// </summary>
    void LaunchNetworkServer()
    {
        buffer = new byte[buffersize];
        RepObjPairs = new Dictionary<string, IReplicatableObject>();
        RepObjects = new List<IReplicatableObject>();
        ClientDataList = new List<ClientDataContainer_ForTool>();
        FTSocketsList = new List<FTSocketContainer>();
        NetIdBuffer = 1;
        try
        {
            listener = new TcpListener(IPAddress.Any, TcpPortNum);
            listener.Start();
            ftplistner = new TcpListener(IPAddress.Any, FTPortNum);
            ftplistner.Start();
            TimerCallback callback = new TimerCallback(CheckForNewClient);
            AcceptTimer = new Timer(callback, null, m_ServerAcceptInterval, m_ServerAcceptInterval);
        }
        catch
        {
            return;
        }
        UdpSocket = new UdpClient(UdpPortNum);
        server = this;
        LocalInst = this;
    }

    void ShutDownServer()
    {
        buffer = null;
        listener.Stop();
        listener = null;
        UdpSocket.Close();
        UdpSocket = null;
        ftplistner.Stop();
        ftplistner = null;
        server = null;
        LocalInst = null;
        AcceptTimer.Dispose();
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
        DebugLogging("ShutDown Server");
    }

    void CheckForNewClient(object o)
    {
        if (listener.Pending())
        {
            listener.BeginAcceptTcpClient(AcceptedClientCallback, listener);
        }
        if (ftplistner.Pending())
        {
            ftplistner.BeginAcceptTcpClient(AcceptedFileSocketCallback, ftplistner);
        }
    }

    void AcceptedClientCallback(System.IAsyncResult ar)
    {
        DebugLogging("Server: Client Connected");
        TcpClient client = (ar.AsyncState as TcpListener).EndAcceptTcpClient(ar);
        ClientDataContainer_ForTool c = new ClientDataContainer_ForTool() { TcpSocket = client, address = ((IPEndPoint)client.Client.RemoteEndPoint).Address, NetworkId = NetIdBuffer++ };
        DebugLogging("Client IPAddress : " + c.address);
        ClientDataList.Add(c);
        OnNewClientConnected?.Invoke(c);
    }

    void AcceptedFileSocketCallback(System.IAsyncResult ar)
    {
        DebugLogging("Server: Client FTPSocket Connected");
        TcpClient client = (ar.AsyncState as TcpListener).EndAcceptTcpClient(ar);
        IPAddress address = ((IPEndPoint)client.Client.RemoteEndPoint).Address;
        FTSocketContainer container = new FTSocketContainer()
        {
            FTSocket = client,
            address = address
        };
        OnUploadCompleted += DequeueUpload;
        FTSocketsList.Add(container);
    }

    private void DequeueUpload(FTSocketContainer fTSocket)
    {
        if (fTSocket.UploadQueue.Count < 1)
            return;
        ResourceData resource = ResourceManager.GetResource(fTSocket.UploadQueue.Dequeue());
        HandOutFile(fTSocket, resource);
    }

    void SendInitialMessage(ClientDataContainer_ForTool client, string name, int rolenum)
    {
        client.UserName = name;
        client.role = (NetworkRole)Enum.ToObject(typeof(NetworkRole), rolenum);
        OnNewClientInitialized?.Invoke(client);

        string InitRepData = "Assign," + client.NetworkId + "$";
        InitRepData += "CList";
        ClientDataList.ForEach((c) => InitRepData += "," + c.UserName + "," + c.NetworkId + "," + (int)c.role);
        SendTcpPacket(client, encoding.GetBytes(InitRepData));
    }
    public void SendTcpPacket(byte[] data)
    {
        ClientDataList.ForEach((client) =>
        {
            try
            {
                client.TcpSocket.Client.Send(data);
            }
            catch (Exception ex)
            {
                DebugLogging(ex.Message);
                ClientDisconnected(client);
            }
        });
    }
    public void SendTcpPacket(string data)
    {
        ClientDataList.ForEach((client) =>
        {
            try
            {
                SendTcpPacket(client, data);
            }
            catch (Exception ex)
            {
                DebugLogging(ex.Message);
                ClientDisconnected(client);
            }
        });
    }

    public void SendTcpPacket(ClientDataContainer_ForTool client, byte[] data)
    {
        try
        {
            client.TcpSocket.Client.Send(data);
        }
        catch (Exception ex)
        {
            DebugLogging(ex.Message);
            ClientDisconnected(client);
        }
    }
    public void SendTcpPacket(ClientDataContainer_ForTool client, string data)
    {
        try
        {
            SendTcpPacket(encoding.GetBytes(data));
        }
        catch (Exception ex)
        {
            DebugLogging(ex.Message);
            ClientDisconnected(client);
        }
    }

    public void RequestFile(string id, ClientDataContainer_ForTool clientData)
    {
        FTSocketContainer FTContainer = FTSocketsList.Find((ft) => ft.address.ToString() == clientData.address.ToString());
        if (FTContainer == null)
        {
            DebugLogging("FTP Socket not found");
            return;
        }
        if (FTContainer.Transdata != null)
        {
            if (FTContainer.Transdata.Id != id)
                FTContainer.FTSocket.Client.Send(encoding.GetBytes("Req:" + id + "$"));
        }
        else
            FTContainer.FTSocket.Client.Send(encoding.GetBytes("Req:" + id + "$"));

    }

    public void HandOutFile(FTSocketContainer client, ResourceData resource)
    {
        try
        {
            string filename = resource.name + resource.extension;
            DebugLogging("Start File Upload : " + filename);
            if (client.Sending)
            {
                client.UploadQueue.Enqueue(resource.Id);
                return;
            }
            client.Sending = true;
            client.FTSocket.Client.Send(encoding.GetBytes("FT:" + filename + "|" + resource.Id + "$"));
            client.FTSocket.Client.BeginSendFile(resource.path, new AsyncCallback(HandOutFileCallback), client);
        }
        catch (Exception ex)
        {
            DebugLogging(ex.Message);
            DebugLogging("Connection Lost.");
        }
    }

    void HandOutFileCallback(IAsyncResult result)
    {
        FTSocketContainer ft = (FTSocketContainer)result.AsyncState;
        ft.FTSocket.Client.EndSendFile(result);
        ft.FTSocket.Client.Send(encoding.GetBytes("$End"));
        ft.Sending = false;
        DebugLogging("File Uploaded");

        DequeueUpload(ft);
        OnUploadCompleted?.Invoke(ft);
    }

    public void DisconnectClient(ClientDataContainer_ForTool client)
    {
        SendTcpPacket(client, "Kicked");
        if (ClientDataList.Contains(client))
            ClientDisconnected(client);
    }

    void ClientDisconnected(ClientDataContainer_ForTool client)
    {
        ClientDataList.Remove(client);
        OnClientDisconnected?.Invoke(client);
        DebugLogging("Client Disconnected : " + client.address);
    }

    public void RegistNewReplicationObject(IReplicatableObject replicator, string ObjId, ClientDataContainer_ForTool Register)
    {
        ReplicationInfoContainer repinfo = replicator.GetInfoContainer();
        RepObjects.Add(replicator);
        repinfo.Id = ObjId;
        repinfo.OwnerNetId = Register.NetworkId;
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
        RepObjPairs.Add(ObjId, replicator);
        OnNewRepObjectAdded?.Invoke(replicator);
    }

    byte[] CreateReplicationData(ClientDataContainer_ForTool client)
    {
        byte[] data = new byte[0];
        RepObjects.ForEach((obj) =>
        {
            if (obj.DoesClientNeedReplication(client))
            {
                byte[] vs = obj.GetReplicationData();
                if (vs != null)
                    data = data.Concat(encoding.GetBytes(obj.GetInfoContainer().Id + ":")).Concat(vs).Concat(encoding.GetBytes("$")).ToArray();
            }
        });
        return data;
    }

    /// <summary>
    /// Send ReplicationData to all client. Dont recommend manually call.
    /// </summary>
    public void Replicate()
    {
        try
        {
            ClientDataList.ForEach((client) =>
                    {
                        byte[] data = CreateReplicationData(client);
                        if (data.Length < 1)
                            return;

                        UdpSocket.Client.SendTo(data, data.Length, SocketFlags.None, new IPEndPoint(client.address, client.UdpEndPoint));
                        DebugLogging("Rep : " + client.address + " / " + data.Length + "  " + encoding.GetString(data));
                    });
            RepObjects.ForEach((obj) => obj.OnFinishedReplication());
        }
        catch (Exception ex)
        {
            DebugLogging(ex.Message + "\n" + ex.StackTrace);
        }
    }

    void DecompClientRequest(byte[] data, ClientDataContainer_ForTool client)
    {
        string datastr = encoding.GetString(data);
        string[] vs = datastr.Contains('$') ? datastr.Split('$') : new string[] { datastr };
        foreach (string s in vs)
        {
            ClientRequest(s, client);
        }
    }

    void ClientRequest(string request, ClientDataContainer_ForTool client)
    {
        string[] vs = request.Contains(',') ? request.Split(',') : new string[] { request };
        switch (vs[0])
        {
            case "Init":
                SendInitialMessage(client, vs[1], int.Parse(vs[2]));
                break;
            case "Disconnect":
                ClientDisconnected(client);
                break;
            case "RPC":
                ProcessRemoteProcedureCall(client, vs[1], (NetworkEventType)Enum.ToObject(typeof(NetworkEventType), int.Parse(vs[2])), encoding.GetBytes(request.Substring(7 + vs[1].Length)));
                break;
            default:
                OnTcpMessageReceived?.Invoke(encoding.GetBytes(request), client);
                break;
        }
    }

    void ProcessRemoteProcedureCall(ClientDataContainer_ForTool RPCRequester, string ObjId, NetworkEventType eventType, byte[] data)
    {
        if (!RepObjPairs.TryGetValue(ObjId, out IReplicatableObject replicatable))
            return;
        switch (eventType)
        {
            case NetworkEventType.ServerRPC:
                replicatable.ReceiveNotify(data);
                break;
            case NetworkEventType.MultiCastRPC:
                replicatable.ReceiveNotify(data);
                SendTcpPacket(encoding.GetBytes("RPC," + ObjId + ",").Concat(data).ToArray());
                break;
            case NetworkEventType.MultiCastRPCWithoutLocal:
                replicatable.ReceiveNotify(data);
                ClientDataList.ForEach((client) =>
                {
                    if (RPCRequester != client)
                        SendTcpPacket(client, encoding.GetBytes("RPC," + ObjId + ",").Concat(data).ToArray());
                });
                break;
            case NetworkEventType.OtherClientRPC:
                throw new NotImplementedException();
                //SendTcpPacket(encoding.GetBytes("RPC," + ObjId + ",").Concat(data).ToArray());
                break;
            default:
                break;
        }
    }

    public void ProcessFileTransmissionInfo(byte[] data, FTSocketContainer FTContainer)
    {
        if (data == null)
            return;

        string datastr = encoding.GetString(data);
        byte[] filedatabuffer;

        if (datastr.StartsWith("Req:"))
        {
            ResourceData resource = ResourceManager.GetResource(datastr.Substring(4, datastr.IndexOf("$") - 4));
            if (resource == null)
                return;
            if (!FTContainer.UploadQueue.Contains(resource.path))
                HandOutFile(FTContainer, resource);
            return;
        }

        if (datastr.StartsWith("FT:"))
        {
            int infodelim = datastr.IndexOf("$"), iddelim = datastr.IndexOf('|');
            string filename = datastr.Substring(3, iddelim - 3), Id = datastr.Substring(iddelim + 1, infodelim - iddelim - 1);
            if (ResourceManager.GetResource(Id) != null)
            {
                FTContainer.Transdata = ResourceManager.GetResource(Id);
                FTContainer.Transdata.ChangeResourcePath(ResourceManager.GetDLResourceFolderPath() + filename, UriKind.Relative);
                FTContainer.IsReceivingUpdate = true;
            }
            else
                FTContainer.Transdata = new ResourceData(ResourceManager.GetDLResourceFolderPath() + filename, Id, UriKind.Relative);
            string cstr = datastr.Substring(0, infodelim + 1);

            FTContainer.filestream = new FileStream(FTContainer.Transdata.path, FileMode.OpenOrCreate, FileAccess.Write);

            int clength = data.Length - encoding.GetByteCount(cstr);
            if (clength > 0)
            {
                filedatabuffer = new byte[clength];
                Buffer.BlockCopy(data, encoding.GetByteCount(cstr), filedatabuffer, 0, filedatabuffer.Length);
                FTContainer.filestream.Write(filedatabuffer, 0, filedatabuffer.Length);
            }
            else
                filedatabuffer = new byte[0];

            DebugLogging("File Receiving..." + FTContainer.Transdata.name + FTContainer.Transdata.extension + " Id : " + FTContainer.Transdata.Id + "\n Updating = " + FTContainer.IsReceivingUpdate);
            DebugLogging("Received File Data :" + FTContainer.filestream.Length + " ( +" + FTContainer.filestream.Length);

            if (datastr.EndsWith("$End"))
            {

                FTContainer.filestream.Close();
                if (FTContainer.IsReceivingUpdate)
                    FTContainer.Transdata.IssueFinishedUpdateNotify();
                else
                    ResourceManager.AddResource(FTContainer.Transdata);

                DebugLogging("File Received successfully " + FTContainer.Transdata.name + FTContainer.Transdata.extension + " Id : " + FTContainer.Transdata.Id);

                OnDownloadCompleted?.Invoke(FTContainer);

                FTContainer.IsReceivingUpdate = false;
                FTContainer.Transdata = null;
            }

        }
        else if (datastr.EndsWith("$End"))
        {
            if (FTContainer.filestream == null)
            {
                DebugLogging("Failed to Receive File " + FTContainer.Transdata.name + FTContainer.Transdata.extension + " Id : " + FTContainer.Transdata.Id);
                FTContainer.IsReceivingUpdate = false;
                FTContainer.Transdata = null;
                return;
            }

            int slength = data.Length - encoding.GetByteCount("$End");
            if (slength > 0)
            {
                filedatabuffer = new byte[slength];
                Buffer.BlockCopy(data, 0, filedatabuffer, 0, filedatabuffer.Length);
                //client.filebuffer = client.filebuffer.Concat(fdata).ToArray(); 
                FTContainer.filestream.Write(filedatabuffer, 0, filedatabuffer.Length);

                DebugLogging("Received File Data :" + FTContainer.filestream.Length + " ( +" + filedatabuffer.Length);
            }
            try
            {

                FTContainer.filestream.Close();
                if (FTContainer.IsReceivingUpdate)
                    FTContainer.Transdata.IssueFinishedUpdateNotify();
                else
                    ResourceManager.AddResource(FTContainer.Transdata);

                DebugLogging("File Received successfully " + FTContainer.Transdata.name + FTContainer.Transdata.extension + " Id : " + FTContainer.Transdata.Id);

                OnDownloadCompleted?.Invoke(FTContainer);

                FTContainer.IsReceivingUpdate = false;
                FTContainer.Transdata = null;
            }
            catch (Exception ex)
            {
                DebugLogging(ex.Message + "   " + ex.StackTrace);
            }
        }
        else
        {
            if (FTContainer.filestream == null)
            {
                FTContainer.IsReceivingUpdate = false;
                FTContainer.Transdata = null;
                return;
            }

            FTContainer.filestream.Write(data, 0, data.Length);

            DebugLogging("Received File Data :" + FTContainer.filestream.Length + " ( +" + data.Length);
        }
    }

    void DecompClientReplicationData(byte[] data, ClientDataContainer_ForTool client)
    {
        string datastr = encoding.GetString(data);
        string[] vs = datastr.Contains('$') ? datastr.Split('$') : new string[] { datastr };
        foreach (string s in vs)
        {
            if (s.Length < 1)
                return;
            if (s.IndexOf(':') < 0)
                return;
            string Id = s.Substring(0, s.IndexOf(':'));
            HandOutReplicationData(Id, encoding.GetBytes(s.Substring(s.IndexOf(':') + 1)), client);
        }
    }

    void HandOutReplicationData(string Id, byte[] data, ClientDataContainer_ForTool client)
    {
        if (RepObjPairs.TryGetValue(Id, out IReplicatableObject Target))
        {
            Target.ReceiveReplicationData(data, client.NetworkId);
        }
    }

    /// <summary>
    /// manually call is not recommended
    /// </summary>
    public override void Tick(object state)
    {
        base.Tick(state);
        if (ClientDataList.Count < 1)
            return;

        try
        {
            ClientDataList.ForEach((c) =>
            {
                if (c.TcpSocket.Available > 0)
                {
                    int dleng = c.TcpSocket.Client.Receive(buffer);
                    byte[] vs = new byte[dleng];
                    Buffer.BlockCopy(buffer, 0, vs, 0, dleng);
                    DebugLogging("Tcp Received : " + encoding.GetString(vs));
                    DecompClientRequest(vs, c);
                    if (OnTcpPacketReceived != null)
                        OnTcpPacketReceived.Invoke(vs, c);
                }
            });
            FTSocketsList.ForEach((ft) =>
                    {
                        if (ft.FTSocket.Available > 0)
                        {
                            buffer = new byte[ft.FTSocket.Client.ReceiveBufferSize];
                            int length = ft.FTSocket.Client.Receive(buffer);
                            byte[] trimmeddata = new byte[length];
                            Buffer.BlockCopy(buffer, 0, trimmeddata, 0, length);
                            //DebugLogging("FTP Received : " + encoding.GetString(trimmeddata));
                            ProcessFileTransmissionInfo(trimmeddata, ft);
                        }
                    });
        }
        catch (Exception ex)
        {
            DebugLogging(ex.Message + "    " + ex.StackTrace);
        }

        if (UdpSocket.Available > 0)
        {
            try
            {
                IPEndPoint endPoint = null;
                byte[] Udpbuffer = UdpSocket.Receive(ref endPoint);
                string strdata = encoding.GetString(Udpbuffer);
                ClientDataContainer_ForTool client = ClientDataList.Find((c) => c.address.ToString() == endPoint.Address.ToString());
                DebugLogging("Udp Received : " + strdata + " " + endPoint.Address + " " + endPoint.Port + " " + client.NetworkId);
                if (strdata == "InitRep$")
                {
                    client.UdpEndPoint = endPoint.Port;
                    DebugLogging("Client UDP Assign : " + client.UserName + " " + client.address.ToString() + " " + client.UdpEndPoint + " ID: " + client.NetworkId);
                }

                DecompClientReplicationData(Udpbuffer, client);
                OnUdpPacketReceived?.Invoke(Udpbuffer, client);

            }
            catch (Exception ex)
            {
                DebugLogging(ex.Message + "  " + ex.StackTrace);
            }
        }
        Replicate();
    }

}
