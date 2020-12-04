using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Linq;
using System.Text;
using System.Threading;
using System;
using System.IO;

public class NetworkManagerForTool_Base
{
    public static NetworkManagerForTool_Base LocalInst;
    public IPAddress OwnIP;
    public byte NetworkId;
    /// <summary>
    /// Encoding class of Server and Replicators
    /// </summary>
    public static Encoding encoding = Encoding.UTF8;

    public delegate void ReplicatedObjectNotification(IReplicatableObject replicatior);
    public delegate void FileTransmissionHandler(FTSocketContainer fTSocket);

    Timer Updatetimer;
    int m_UpdateInterval = 25;

    public event EventHandler<string> DebugHandle;

    public static bool IsNetworkAvailable
    {
        get { return LocalInst != null; }
    }

    public static bool IsServer
    {
        get { return LocalInst is NetworkManagerForTool_Server; }
    }

    public int UpdateInterval
    {
        get { return m_UpdateInterval; }
        set
        {
            m_UpdateInterval = value;
            Updatetimer?.Change(m_UpdateInterval, m_UpdateInterval);
        }
    }

    public virtual void Initialize()
    {
        TimerCallback callback = new TimerCallback(Tick);
        Updatetimer = new Timer(callback, null, 1000, m_UpdateInterval);
        DebugHandle += NetworkManagerBase_DebugHandle;
    }

    private void NetworkManagerBase_DebugHandle(object sender, string e)
    {

    }

    public virtual void Tick(object state)
    {

    }

    public virtual void Launch()
    {

    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="data"></param>
    /// <param name="option">For Server. Id List of user who dont send</param>
    public virtual void SendPacket(byte[] data,byte[] option = null)
    {

    }

    public virtual void ShutDown()
    {
        Updatetimer.Change(-1, -1);
        Updatetimer.Dispose();
    }
    public virtual void Destroy(IReplicatableObject replicatior)
    {

    }

    public void DebugLogging(string log)
    {
        DebugHandle?.Invoke(this, log);
    }
}
