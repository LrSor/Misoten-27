using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

public class SmartphoneTestServer : MonoBehaviour
{
    private HttpListener listener;
    private Thread serverThread;

    private bool pressed = false;

    void Start()
    {
        listener = new HttpListener();

        // スマホなど他端末から接続できるようにする
        listener.Prefixes.Add("http://*:8080/");
        listener.Start();

        serverThread = new Thread(ServerLoop);
        serverThread.Start();

        Debug.Log(
            "Webサーバー開始\n" +
            "http://" +Dns.GetHostAddresses(Dns.GetHostName())[3].ToString() + ":8080");
    }

    void ServerLoop()
    {
        while (listener.IsListening)
        {
            try
            {
                HttpListenerContext context = listener.GetContext();

                string path = context.Request.Url.AbsolutePath;

                if (path == "/press")
                {
                    pressed = true;

                    SendHtml(context,
                        "<h1>Unityに送信しました！</h1>");
                }
                else
                {
                    SendHtml(context, @"
<html>
<head>
<meta name='viewport' content='width=device-width, initial-scale=1'>
</head>

<body style='text-align:center; padding-top:100px;'>

<h1>Unity Controller</h1>

<button
    style='font-size:50px; padding:40px;'
    onclick=""location.href='/press'"">
    押す
</button>

</body>
</html>");
                }
            }
            catch
            {
                // 終了時など
            }
        }
    }

    void SendHtml(HttpListenerContext context, string html)
    {
        byte[] data = Encoding.UTF8.GetBytes(html);

        context.Response.ContentType = "text/html; charset=utf-8";
        context.Response.ContentLength64 = data.Length;

        context.Response.OutputStream.Write(data, 0, data.Length);
        context.Response.Close();
    }

    void Update()
    {
        if (pressed)
        {
            pressed = false;

            Debug.Log("スマホからボタンが押された！");
        }
    }

    void OnDestroy()
    {
        listener?.Stop();
        serverThread?.Abort();
    }
}
