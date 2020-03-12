using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Net;
using System;

public class ServerCommandSystem : MonoBehaviour
{
    [SerializeField]
    NetworkManager_Server server;
    [SerializeField]
    GameObject ClientUIPrefab, ContentPanel;
    List<ClientUIController> clientUIs=new List<ClientUIController>();
    // Start is called before the first frame update
    void Start()
    {
        server.OnNewClientConnected += OnClientConnected;
        server.OnClientDisconnected += OnClientDisconnected;
        server.OnTcpMessageReceived += OnReceivedMessage;
    }

    private void OnReceivedMessage(byte[] data, ClientDataContainer clientData)
    {
        Debug.Log("new");
        GameObject obj = Instantiate(ClientUIPrefab, ContentPanel.transform);
        ClientUIController uIController = obj.GetComponent<ClientUIController>();
        uIController.Initialize(server, clientData);
        clientUIs.Add(uIController);
    }

    void OnClientConnected(ClientDataContainer client)
    {
        UpdateUI();
    }
    void OnClientDisconnected(ClientDataContainer client)
    {
        ClientUIController uIController= clientUIs.Find((ui) => ui.clientDataContainer.address == client.address);
        clientUIs.Remove(uIController);
        Destroy(uIController.gameObject);
    }

    public void UpdateUI()
    {
        clientUIs.ForEach((ui) => Destroy(ui.gameObject));
        clientUIs.Clear();
        server.ClientDataList.ForEach((client) =>
        {
            Debug.Log("new");
            GameObject obj = Instantiate(ClientUIPrefab, ContentPanel.transform);
            ClientUIController uIController = obj.GetComponent<ClientUIController>();
            uIController.Initialize(server, client);
            clientUIs.Add(uIController);
        });
    }
}
