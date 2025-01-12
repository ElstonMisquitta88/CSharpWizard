using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;

namespace CSharpWizardApp;

public class StockTicker
{
    public string? symbol { get; set; }
    public char buySellIndicator { get; set; }
    public int quantity { get; set; }
    public int price { get; set; }
    public int packetSequence { get; set; }
}

enum APICallType
{
    StreamAllPackets = 1,
    ResendPacket = 2
}


public class Program
{
    static void Main(string[] args)
    {
        try
        {
            List<StockTicker> _tickerdata = new List<StockTicker>();
            HashSet<int> receivedSequences;

            // API Call - Stream all packets
            APICallHelper(APICallType.StreamAllPackets, 1, (byte)0, _tickerdata, out receivedSequences);

            // Identify missing sequences
            int maxSequence = receivedSequences.Max();
            List<int> missingSequences = new List<int>();
            for (int seq = 1; seq < maxSequence; seq++)
            {
                if (!receivedSequences.Contains(seq))
                {
                    missingSequences.Add(seq);
                }
            }

            // Request missing packets
            foreach (int missingSeq in missingSequences)
            {
                // API Call - Resend packet
                APICallHelper(APICallType.ResendPacket, 2, (byte)missingSeq, _tickerdata, out receivedSequences);
            }

            // Write the data to a file
            string _filepath = @"D:\\StockData\\StockTickerData.json";
            var mySortedList = _tickerdata.OrderBy(x => x.packetSequence);
            string json = Newtonsoft.Json.JsonConvert.SerializeObject(mySortedList);
            System.IO.File.WriteAllText(_filepath, json);

            Console.ForegroundColor = ConsoleColor.Blue;
            Console.WriteLine($"File Generated : {_filepath}");

            Console.ResetColor();
        }
        catch (SocketException ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("SocketException: " + ex.Message);
        }
        catch (IOException ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("IOException: " + ex.Message);
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("Exception: " + ex.Message);
        }
    }

    private static void APICallHelper(APICallType _apicall, byte _callType, byte _resendSeq, List<StockTicker> _tickerdata, out HashSet<int> receivedSequences)
    {
        try
        {
            string HOST = "127.0.0.1";
            int port = 3000;


            TcpClient client = new TcpClient();
            client.Connect(HOST, port);

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("Client Connection Open");


            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("Connected to server");

            // Create the payload
            byte[] payload = new byte[2];
            payload[0] = _callType;
            payload[1] = _resendSeq;


            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("Send Request");

            // Send message
            NetworkStream nwstream = client.GetStream();
            nwstream.Write(payload, 0, payload.Length);

            // Receive message
            byte[] buffer = new byte[client.ReceiveBufferSize];
            MemoryStream ms = new MemoryStream();
            int bytesRead;

            if (_apicall == APICallType.StreamAllPackets)
            {
                // Read all the data until the end of stream has been reached.
                while ((bytesRead = nwstream.Read(buffer, 0, buffer.Length)) > 0)
                {
                    ms.Write(buffer, 0, bytesRead);
                }
            }
            else
            {
                bytesRead = nwstream.Read(buffer, 0, buffer.Length);
                ms.Write(buffer, 0, bytesRead);
            }

            byte[] bytesToRead = ms.ToArray();
            ms.Close();

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("Data Received");


            // Process the response payload
            int packetSize = 17;
            receivedSequences = new HashSet<int>();
            for (int i = 0; i < bytesToRead.Length; i += packetSize)
            {
                string symbol = Encoding.ASCII.GetString(bytesToRead, i, 4);
                char buySellIndicator = (char)bytesToRead[i + 4];
                int quantity = BitConverter.ToInt32(bytesToRead.Skip(i + 5).Take(4).Reverse().ToArray(), 0);
                int price = BitConverter.ToInt32(bytesToRead.Skip(i + 9).Take(4).Reverse().ToArray(), 0);
                int packetSequence = BitConverter.ToInt32(bytesToRead.Skip(i + 13).Take(4).Reverse().ToArray(), 0);

                _tickerdata.Add(new StockTicker()
                {
                    symbol = symbol,
                    buySellIndicator = buySellIndicator,
                    quantity = quantity,
                    price = price,
                    packetSequence = packetSequence
                });

                receivedSequences.Add(packetSequence);
            }

            Console.ForegroundColor = ConsoleColor.Blue;
            Console.WriteLine("Data parse Done");


            client.Close();

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("Client Connection Closed");

        }
        catch
        {
            throw;
        }
    }
}
