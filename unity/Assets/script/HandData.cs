using System.Net;
using System.Text;
using System.Net.Sockets;
using System.Threading;
using UnityEngine;
using System;


public class HandData : MonoBehaviour
{
    public Hand hand;
    Thread receiveThread;
    UdpClient client;
    public int port = 5054;
    public string[] data;

    void Start()
    {
        receiveThread = new Thread(new ThreadStart(ReceiveData));
        receiveThread.IsBackground = true;
        receiveThread.Start();
    }

    private void ReceiveData()
    {
        client = new UdpClient(port);
        while (true)
        {
            try
            {
                IPEndPoint anyIP = new IPEndPoint(IPAddress.Any, 0);
                byte[] dataByte = client.Receive(ref anyIP);
                data = Encoding.UTF8.GetString(dataByte).Split(';');
                // Debug.Log(data[0]);
                hand.data = data[0];
            }
            catch (Exception e)
            {
                Debug.Log(e);
            }
        }
    }
    void Update()
    {

    }
}
