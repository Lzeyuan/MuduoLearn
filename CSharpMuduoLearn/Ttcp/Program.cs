using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.CommandLine;
using System;

namespace Ttcp;

struct SessionMessage
{
    public int Count { get; set; }
    public int Size { get; set; }

    public byte[] ToBytes()
    {
        List<byte> ret = [.. BitConverter.GetBytes(Count).Reverse(), .. BitConverter.GetBytes(Size).Reverse()];
        return ret.ToArray();
    }
}

struct PayloadMessage
{
    public int Size { get; set; }
    public byte[] Data { get; set; }

    public byte[] GenerateBytes()
    {
        Data = new byte[Size];
        for (int i = 0; i < Size; ++i)
        {
            Data[i] = (byte)(i % 16);
        }
        List<byte> ret = [.. BitConverter.GetBytes(Size).Reverse(), .. Data];
        return ret.ToArray();
    }
};


internal class Program
{
    static int SendAll(Socket socket, byte[] bytes, int len)
    {
        int written = 0;
        while (written < len)
        {
            int nw = socket.Send(bytes, written, len - written, SocketFlags.None);
            if (nw > 0)
            {
                written += nw;
            }
            else
            {
                break;
            }
        }
        return written;
    }


    static void Transmit(IPEndPoint iPEndPoint, int bufferSize, int sendCount)
    {
        Socket socket = new(IPAddress.Any.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
        socket.Connect(iPEndPoint);
        socket.NoDelay = true;

        Stopwatch stopwatch = new Stopwatch();
        stopwatch.Start();
        // 发送长度信息
        SessionMessage sessionMessage = new SessionMessage
        {
            Size = bufferSize,
            Count = sendCount,
        };
        byte[] sessionMessageBytes = sessionMessage.ToBytes();
        if (SendAll(socket, sessionMessageBytes, sessionMessageBytes.Length)
            != sessionMessageBytes.Length)
        {
            throw new Exception("信息头，发送不完全");
        }

        // 发送信息
        PayloadMessage payload = new()
        {
            Size = bufferSize
        };

        double totalMib = 1.0 * bufferSize * sendCount / 1024 / 1024;
        Console.WriteLine($"{totalMib:F3} MiB in total");
        var payloadMessageBytes = payload.GenerateBytes();

        for (int i = 0; i < sendCount; ++i)
        {
            int nw = SendAll(socket, payloadMessageBytes, payloadMessageBytes.Length);
            if (nw != payloadMessageBytes.Length)
            {
                throw new Exception("负载信息，发送不完全");
            }

            int ack = 0;
            byte[] receiveBuffer = new byte[4];
            int nr = socket.Receive(receiveBuffer);
            if (nr != receiveBuffer.Length)
            {
                throw new Exception("ACK，接收不完全");
            }
            ack = BitConverter.ToInt32(receiveBuffer, 0);
            if (nr != receiveBuffer.Length)
            {
                throw new Exception("ACK，对方接收不完全");
            }
        }

        stopwatch.Stop();
        double elapsed = stopwatch.Elapsed.TotalSeconds;
        Console.WriteLine($"{elapsed:F3} seconds\n{totalMib / elapsed:F3} MiB / s\n");
        socket.Close();
    }

    static int ReceiveAll(Socket socket, byte[] bytes, int len)
    {
        int received = 0;
        while (received < len)
        {
            int nw = socket.Receive(bytes, received, len - received, SocketFlags.None);
            if (nw > 0)
            {
                received += nw;
            }
            else
            {
                break;
            }
        }
        return received;
    }

    static void Receive(int port)
    {
        Socket serverSocket;

        // 创建终结点
        IPEndPoint localEndPoint = new IPEndPoint(IPAddress.Any, port);
        serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        serverSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
        serverSocket.Bind(localEndPoint);
        serverSocket.Listen(10);
        Socket clientSocket = serverSocket.Accept();
        // 4 + 4 (Size, Count)
        byte[] sessionMessageBuffer = new byte[8];
        int receiveCount = ReceiveAll(clientSocket, sessionMessageBuffer, sessionMessageBuffer.Length);
        clientSocket.NoDelay = true;
        if (receiveCount < 8)
        {
            throw new Exception("接收信息头出错");
        }

        Span<byte> sessionMessageBytes = sessionMessageBuffer;
        var countBytes = sessionMessageBytes.Slice(0, 4);
        countBytes.Reverse();
        int count = BitConverter.ToInt32(countBytes.ToArray(), 0);
        var sizeBytes = sessionMessageBytes.Slice(4, 4);
        sizeBytes.Reverse();
        int size = BitConverter.ToInt32(sizeBytes.ToArray(), 0);
        SessionMessage sessionMessage = new SessionMessage()
        {
            Count = count,
            Size = size,
        };
        Console.WriteLine($"receive buffer size = {size}\nreceive count of buffers = {count}");

        double totalMiB = 1.0 * sessionMessage.Count * sessionMessage.Size / 1024 / 1024;
        Console.WriteLine($"{totalMiB:F3} MiB in total");

        int receiveBufferLength = 4 + sessionMessage.Size;
        byte[] receiveBuffer = new byte[receiveBufferLength];
        byte[] sendBuffer = new byte[4];
        PayloadMessage payload = new PayloadMessage();

        Stopwatch stopwatch = Stopwatch.StartNew();
        for (int i = 0; i < sessionMessage.Count; i++)
        {
            payload.Size = 0;
            receiveCount = ReceiveAll(clientSocket, receiveBuffer, receiveBuffer.Length);
            if (receiveCount < receiveBufferLength)
            {
                throw new Exception("接收负载出错");
            }
            Span<byte> receiveBufferSpan = receiveBuffer;
            sizeBytes = receiveBufferSpan.Slice(0, 4);
            sizeBytes.Reverse();
            size = BitConverter.ToInt32(sizeBytes.ToArray(), 0);
            payload.Size = size;
            int ack = size;
            sendBuffer = BitConverter.GetBytes(ack).Reverse().ToArray();
            int sendCount = SendAll(clientSocket, sendBuffer, sendBuffer.Length);
            if (sendCount < 4)
            {
                throw new Exception("发送ACK出错");
            }
        }
        stopwatch.Stop();
        double elapsed = stopwatch.Elapsed.TotalSeconds;
        Console.WriteLine($"{elapsed:F3} seconds\n{(totalMiB / elapsed):F3} MiB/s\n");
    }

    static void StartTest(bool recv, int port, string trans, int bufferSize, int sendCount)
    {
        if (recv == !string.IsNullOrEmpty(trans))
        {
            Console.WriteLine("Either -t or -r must be specified.\n");
            return;
        }

        if (recv)
        {
            Receive(port);
        }

        if (!string.IsNullOrEmpty(trans))
        {
            IPEndPoint iPEndPoint = new(IPAddress.Parse(trans), port);
            Transmit(iPEndPoint, bufferSize, sendCount);
        }
    }

    static int ParseCommandLine(string[] args)
    {
        var receiveOption = new Option<bool>(
            aliases: ["--recv", "-r"],
            description: "Receive."
        );

        var portOption = new Option<int>(
            aliases: ["--port", "-p"],
            getDefaultValue: () => 5001,
            description: "Set port. Default to 5001."
        );

        var transmitOption = new Option<string>(
            aliases: ["--trans", "-t"],
            description: "Transmit."
        );

        var sizeOption = new Option<int>(
            aliases: ["--size", "-s"],
            getDefaultValue: () => 1 << 16,
            description: "Transmit."
        );

        var countOption = new Option<int>(
            aliases: ["--count", "-c"],
            getDefaultValue: () => 1 << 13,
            description: "Transmit."
        );

        var startCommand = new Command("start", "Start test.")
        {
            receiveOption,
            portOption,
            transmitOption,
            sizeOption,
            countOption
        };

        var rootCommand = new RootCommand("TTcp command line.");
        rootCommand.AddCommand(startCommand);

        startCommand.SetHandler(StartTest, receiveOption, portOption, transmitOption, sizeOption, countOption);
        return rootCommand.Invoke(args);
    }

    static int Main(string[] args)
    {
        Console.WriteLine("Hello, TTCP!");
        return ParseCommandLine(args);
    }
}
