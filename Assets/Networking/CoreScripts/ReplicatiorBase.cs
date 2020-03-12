using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ReplicatiorBase : MonoBehaviour
{
    /// <summary>
    /// Dont Change this variable
    /// </summary>
    public byte OwnerNetId = 0;
    /// <summary>
    /// Dont Change this variable
    /// </summary>
    public byte LocalHostNetId = 0;
    /// <summary>
    /// Dont Change this variable
    /// </summary>
    public int Id = -1;
    /// <summary>
    /// Dont Change this variable
    /// </summary>
    public string RepPrefabName;

    public bool IsAutonomousObject()
    {
        return OwnerNetId != 0;
    }
    bool IsServer()
    {
        return LocalHostNetId == 0;
    }

    /// <summary>
    /// For Server.
    /// Return Byte Array for Replication.
    /// </summary>
    /// <returns>Byte Array for Replication</returns>
    public virtual byte[] GetReplicationData()
    {
        return null;
    }

    /// <summary>
    /// For Client.
    /// Return Byte Array for Autonomously Replication.
    /// </summary>
    /// <returns></returns>
    public virtual byte[] GetAutonomousData()
    {
        return null;
    }

    /// <summary>
    /// For Client.
    /// Replicate From Server'sObjectData On Client
    /// </summary>
    /// <param name="repdata"></param>
    public virtual void ReceiveReplicationData(byte[] repdata)
    {

    }

    /// <summary>
    /// For Server.
    /// Replicate  AutonomousObject From Client'sObjectData On Server
    /// </summary>
    /// <param name="autodata"></param>
    public virtual void ReceiveAutonomousData(byte[] autodata)
    {

    }

    /// <summary>
    /// Available Only On Server. if override this function , Dont forget about AutonomousCheck{client.AutonomousObjects.Contains(this)}.
    /// </summary>
    /// <param name="client"></param>
    /// <returns></returns>
    public virtual bool DoesClientNeedReplication(ClientDataContainer client)
    {
        return !client.AutonomousObjects.Contains(this);
    }

    /// <summary>
    /// Available Only On Client. Only for AutonomousObject
    /// </summary>
    /// <returns></returns>
    public virtual bool DoesServerNeedReplication()
    {
        return true;
    }
}

/// <summary>
/// example class for Serialization
/// </summary>
public static class Serializer
{
    public static byte[] Vector3ToBytes(Vector3 vec)
    {
        return NetworkManagerBase.encoding.GetBytes(Vector3ToString(vec));
    }

    public static string Vector3ToString(Vector3 vec, int f = 1)
    {
        string s = vec.x.ToString("f" + f) + ",";
        s += vec.y.ToString("f" + f) + ",";
        s += vec.z.ToString("f" + f);
        return s;
    }

    public static Vector3 StringToVector3(string x, string y, string z)
    {
        return new Vector3(float.Parse(x), float.Parse(y), float.Parse(z));
    }

}
