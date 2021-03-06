﻿using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SecureKeyExchange;
using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace SocketClient
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Please choose the type you want; (S)erver or (C)lient");
            string input = Console.ReadLine();

            switch (input.ToLower())
            {
                case "c":
                    Client();
                    break;
                case "s":
                    Server().Wait();
                    break;
                case "v":
                    CustomOperation.Custom();
                    break;
                default:
                    Console.WriteLine("Please enter either 'C' or 'S'");
                    break;
            }
        }

        static async Task Server()
        {
            TcpListener listener = new TcpListener(IPAddress.Any, 13356);

            Console.WriteLine("Awaiting Clients");
            listener.Start();

            while (true)
                HandleClient(await listener.AcceptTcpClientAsync());

            static void HandleClient(TcpClient client)
            {
                try
                {
                    using (client)
                    {
                        NetworkStream ns = client.GetStream();

                        using (DiffieHellman dh = new DiffieHellman())
                        {
                            byte[] buffer = new byte[client.ReceiveBufferSize];

                            var KeyData = dh.PublicKey.Select(b => (int)b).ToArray();
                            var serializedKeyData = JsonConvert.SerializeObject(KeyData);

                            var IVData = dh.IV.Select(b => (int)b).ToArray();
                            var serializedData = JsonConvert.SerializeObject(IVData);

                            var obj = new 
                            {
                                publicKey = serializedKeyData,
                                IV = serializedData
                            };

                            var jsonString = JsonConvert.SerializeObject(obj);

                            var byteArray = Encoding.UTF8.GetBytes(jsonString);

                            ns.Write(byteArray, 0, byteArray.Length);

                            while (true)
                            {
                                int read = ns.Read(buffer, 0, client.ReceiveBufferSize);
                                string text = Encoding.UTF8.GetString(buffer, 0, read);

                                //---convert the data received into a string---
                                var value = Encoding.UTF8.GetString(buffer);
                                dynamic recObj = JObject.Parse(value);

                                JValue RecData = (JValue)recObj["encryptedMessage"];
                                byte[] dataBytes = Encoding.UTF8.GetBytes((string)RecData.Value);

                                JValue RecIVData = (JValue)recObj["IV"];
                                byte[] IVDataBytes = Encoding.UTF8.GetBytes((string)RecIVData.Value);

                                var decrypted = dh.DecryptString(dataBytes, dh.PublicKey, IVDataBytes);

                                Console.WriteLine("Encrypted: " + text);
                                Console.WriteLine("Decrypted: " + decrypted);
                            }
                        }
                    };
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message);
                }
            }
        }

        static void Client()
        {
            TcpClient client = new TcpClient();

            IPAddress ip = IPAddress.Parse(GetAddress());
            IPEndPoint endpoint = new IPEndPoint(ip, int.Parse(GetPort()));

            client.Connect(endpoint);

            NetworkStream ns = client.GetStream();

            using (DiffieHellman dh = new DiffieHellman())
            {
                //---get the incoming data through a network stream---
                byte[] buffer = new byte[client.ReceiveBufferSize];

                //---read incoming stream---
                int bytesRead = ns.Read(buffer);

                //---convert the data received into a string---
                var value = Encoding.UTF8.GetString(buffer);
                JObject obj = JObject.Parse(value);

                JValue keyArrayValue = (JValue)obj["publicKey"];

                Console.WriteLine("Public key: " + (string)keyArrayValue.Value);

                var keyArray = JsonConvert.DeserializeObject<byte[]>((string)keyArrayValue.Value);

                //byte[] keyByteArray = keyArray.Select(b => b).ToArray();

                dh.PublicKey = keyArray;

                while (true)
                {
                    Console.Write("Write your message here: ");
                    string text = Console.ReadLine();
                    Console.WriteLine("Message: " + text);

                    var encrypted = dh.EncryptString(text, dh.PublicKey);
                    Console.WriteLine("Encrypted Message: " + Encoding.ASCII.GetString(encrypted));

                    var sendObj = new
                    {
                        encryptedMessage = encrypted,
                        IV = dh.IV
                    };

                    Console.WriteLine("IV used: " + Encoding.ASCII.GetString(sendObj.IV));

                    var jsonString = JsonConvert.SerializeObject(sendObj);

                    var byteArray = Encoding.UTF8.GetBytes(jsonString);

                    ns.Write(byteArray, 0, byteArray.Length);
                }
            }
        }

        private static string GetAddress()
        {
            Regex reg = new Regex(@"^(?:(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\.){3}(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)$");
            while (true)
            {
                Console.WriteLine($"Please pick an IP [127.0.0.1]");
                string input = Console.ReadLine();

                if (input == "")
                {
                    return "127.0.0.1";
                }
                else
                {
                    Match match = reg.Match("127.0.0.1");

                    if (match.Success)
                    {
                        return match.Value;
                    }
                }
            }
        }

        private static string GetPort()
        {
            Regex reg = new Regex(@"^([0-9]{1,4}|[1-5][0-9]{4}|6[0-4][0-9]{3}|65[0-4][0-9]{2}|655[0-2][0-9]|6553[0-5])$");
            
            while (true)
            {
                Console.WriteLine($"Please pick a Port [13356]");
                string input = Console.ReadLine();

                if (input == "")
                {
                    return "13356";
                }
                else
                {
                    Match match = reg.Match("13356");

                    if (match.Success)
                    {
                        return match.Value;
                    }
                }
            }

        }
    }
}
