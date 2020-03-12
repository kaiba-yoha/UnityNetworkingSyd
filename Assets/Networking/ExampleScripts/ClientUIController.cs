using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ClientUIController : MonoBehaviour
{
    public Text Nametext;
    [SerializeField] InputField CmdInputF;
    public ClientDataContainer clientDataContainer;
    NetworkManager_Server Server;
    // Start is called before the first frame update
    public void Initialize(NetworkManager_Server server, ClientDataContainer clientData)
    {
        clientDataContainer = clientData;
        Server = server;
    }

    void Start()
    {
        Nametext.text = clientDataContainer.address + " : Id " + clientDataContainer.NetworkId;
    }

    public void Kick()
    {
        Server.DisconnectClient(clientDataContainer);
    }

    public void SendCommand()
    {
        Server.SendTcpPacket(clientDataContainer, CmdInputF.text);
    }
}
