using System.Collections;
using System.Collections.Generic;
using System.Windows;
using System.Linq;

public interface IReplicatableObject
{
    ReplicationInfoContainer GetInfoContainer();

    /// <summary>
    /// 
    /// Return Byte Array for Initial Replication.
    /// </summary>
    /// <returns>Byte Array for Initial Replication</returns>
    byte[] GetInitReplicationData();

    /// <summary>
    /// 
    /// Return Byte Array for Replication.
    /// </summary>
    /// <returns>Byte Array for Replication</returns>
    byte[] GetReplicationData();

    void OnFinishedReplication();

    /// <summary>
    /// 
    /// Initial Replicate From Server'sObjectData On Client
    /// </summary>
    /// <param name="repdata"></param>
    void ReceiveInitReplicationData(byte[] repdata);

    /// <summary>
    /// 
    /// Replicate From Server'sObjectData On Client
    /// </summary>
    /// <param name="repdata"></param>
    void ReceiveReplicationData(byte[] repdata, byte SourceNetId);

    void ReceiveNotify(byte[] data);

    bool DoesServerNeedReplication();

    bool DoesClientNeedReplication(ClientDataContainer_ForTool clientData);

    IUseResourceObjcet GetResourceObj();

}

public enum ReplicateObjectType
{
    Charactor = 0, Symbol = 1, SoundPlayer = 2, MoviePlayer = 3
}

public enum NetworkEventType
{
    ServerRPC = 0, MultiCastRPC = 1, MultiCastRPCWithoutLocal = 2, OtherClientRPC = 3, ReassignResource = 4
}

public class ReplicationInfoContainer
{
    /// <summary>
    /// Dont Change this variable
    /// </summary>
    public byte OwnerNetId;
    /// <summary>
    /// Dont Change this variable
    /// </summary>
    public string Id;

    public ReplicateObjectType objectType;

    public delegate void NetworkNotify(byte[] data);

    /// <summary>
    /// If Object registed to NetworkManager, RPCRequestHandler call Manager RPC Method.
    /// </summary>
    public event NetworkNotify RPCRequestHandler;
    public event NetworkNotify ReAssignResourcesRequestHandler;

    public bool IsOwner()
    {
        if (NetworkManagerForTool_Base.IsNetworkAvailable)
            return NetworkManagerForTool_Base.LocalInst.NetworkId == OwnerNetId;
        else
            return true;
    }
    public bool IsAutonomousObject()
    {
        return OwnerNetId != 0;
    }

    /// <summary>
    /// RPC Call RemoteHost's Object ReceiveNotify().
    /// </summary>
    /// <param name="eventType"></param>
    /// <param name="data"></param>
    public void RequestNetworkEvent(NetworkEventType eventType, byte[] data)
    {
        byte[] vs = null;
        switch (eventType)
        {
            case NetworkEventType.ServerRPC:
                vs = NetworkManagerForTool_Base.encoding.GetBytes((byte)eventType + ",").Concat(data).ToArray();
                break;
            case NetworkEventType.MultiCastRPC:
                vs = NetworkManagerForTool_Base.encoding.GetBytes((byte)eventType + ",").Concat(data).ToArray();
                break;
            case NetworkEventType.MultiCastRPCWithoutLocal:
                vs = NetworkManagerForTool_Base.encoding.GetBytes((byte)eventType + ",").Concat(data).ToArray();
                break;
            case NetworkEventType.OtherClientRPC:
                vs = NetworkManagerForTool_Base.encoding.GetBytes((byte)eventType + ",").Concat(data).ToArray();
                break;
            case NetworkEventType.ReassignResource:
                ReAssignResourcesRequestHandler?.Invoke(data);
                return;
            default:
                break;
        }
        NetworkManagerForTool_Base.LocalInst.SendPacket(vs);
    }
}

