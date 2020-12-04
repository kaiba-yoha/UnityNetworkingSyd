using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ReplicationManager
{
    private static ReplicationManager instance;

    public Dictionary<string, IReplicatableObject> ReplicationDict;

    public delegate void ReplicatedObjectEvent(IReplicatableObject rep);
    public event ReplicatedObjectEvent OnNewObejctRegisted;

    public static ReplicationManager Instance
    {
        get
        {
            if (instance == null)
                instance = new ReplicationManager();
            return instance;
        }
    }

    public ReplicationManager()
    {
        ReplicationDict = new Dictionary<string, IReplicatableObject>();

    }

    public void RegistReplicationObject(IReplicatableObject replicator,string Id,byte OwnerId)
    {
        if (replicator == null)
        {
            UnityEngine.Debug.Log("Replicator is null. RegistReplicatedObject Failed");
            return;
        }
        if (ReplicationDict.ContainsKey(Id))
            return;

        ReplicationInfoContainer repinfo = replicator.GetInfoContainer();
        repinfo.Id = Id;
        repinfo.OwnerNetId = OwnerId;
        UnityEngine.Debug.Log("Regist New RepObj : " + repinfo.Id);
        ReplicationDict.Add(Id, replicator);
        OnNewObejctRegisted?.Invoke(replicator);
    }
}
