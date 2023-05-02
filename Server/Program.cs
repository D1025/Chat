



using System.Net.Sockets;
using System.Net;
using System.Text;

namespace Server
{


    class MainServer
    {
        public static List<Event> Events = new List<Event>();
        public static List<Lobby> Lobbys = new List<Lobby>();
        public static List<Message> Output = new List<Message>();

        public class Event
        { 
            public Event() { }
            public virtual void resolve()
            {

            }
        }
        public class CreateRoom : Event
        {
            private Lobby lobby { get; set; }
            public CreateRoom(Lobby lobby)
            {
                this.lobby = lobby;

            }
            public int returnId()
            {
                return lobby.Id;
            }
            override
            public void resolve()
            {
                Lobbys.Add(lobby);
            }
        }
        public class JoinRoom : Event
        {
            private int id { get; set; }
            private Client client { get; set; }
            public JoinRoom(int id, Client client)
            {
                this.id = id;
                this.client = client;
            }
            override
            public void resolve()
            {
                for (int i = 0; i < Lobbys[this.id].clients.Count; i++)
                {
                    Output.Add(new Message(Lobbys[this.id].clients[i].tcpClient, "J " + client.username));
                }
                Lobbys[id].addtoLobby(this.client);
                for (int i = 0; i < Lobbys[this.id].clients.Count; i++)
                {
                    Output.Add(new Message(this.client.tcpClient, "J " + Lobbys[this.id].clients[i].username));
                }
            }
        }
        public class Message
        {
            public TcpClient client;
            private string message;

            public Message(TcpClient client, string message)
            {
                this.client = client;
                this.message = message;
            }

            public void send()
            {
                if(client.Connected)
                {
                    byte[] messageBytes = Encoding.ASCII.GetBytes(this.message);
                    NetworkStream stream = this.client.GetStream();
                    stream.Write(messageBytes, 0, messageBytes.Length);
                }

            }
        }

        public class Lobby
        {
            public int Id;
            private static int nextId = 0;
            public string Name;
            public Client owner;
            public List<Client> clients = new List<Client>();

            public Lobby(string Name, Client owner)
            {
                Id = nextId;
                nextId++;
                this.Name = Name;
                this.owner = owner;
            }

            public void addtoLobby(Client client)
            {
                clients.Add(client);
            }
            
            public void removefromLobby(Client client)
            {
                clients.Remove(client);
            }

        }

        public class Client
        {
            private static int nextId = 0;
            public int Id;
            public TcpClient tcpClient;
            public string? username; 

            public Client(TcpClient tcpClient)
            {
                this.tcpClient = tcpClient; 
                Id = nextId;
                nextId++;

            }
            public int getLobbyId(string name)
            {
                for (int i = 0; i<Lobbys.Count; i++)
                {
                    if (Lobbys[i].Name == name) return i;
                }
                return -1;
            }
            public void handleClient()
            {
                Console.WriteLine($"Client connected from {tcpClient.Client.RemoteEndPoint}");
                    try
                    {
                        // Receive data in chunks until the client closes the connection
                        byte[] buffer = new byte[1024]; // Use a 1 KB buffer size
                        int bytesRead;
                        while ((bytesRead = tcpClient.GetStream().Read(buffer, 0, buffer.Length)) > 0)
                        {
                            byte[] message = new byte[bytesRead];
                            Array.Copy(buffer, message, bytesRead);
                            string strMessage = Encoding.UTF8.GetString(message); 
                            Console.WriteLine($"Received message from {tcpClient.Client.RemoteEndPoint}: {strMessage}");
                            string[] parts = strMessage.Split();
                            Output.Add(new Message(tcpClient, "OK"));
                            if (parts[0].Equals("S"))
                            {
                                username = parts[1];
                            }
                            if (parts[0].Equals("C"))
                            {
                                Events.Add(new CreateRoom(new Lobby(parts[1], this)));
                            }
                            if (parts[0].Equals("J"))
                            {
                                int id = getLobbyId(parts[1]);
                                if (id == -1)
                                {
                                    Output.Add(new Message(tcpClient, "FJ donotexist"));
                                }
                                else
                                {
                                    Events.Add(new JoinRoom(id, this));
                                } 
                            }


                        }
                        Console.WriteLine($"Connection closed by {tcpClient.Client.RemoteEndPoint}.");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error handling client {tcpClient.Client.RemoteEndPoint}: {ex.Message}");
                    }
                    finally
                    {
                        tcpClient.Close();
                    }
                
            }
            
        }
        public class Listener
        {
            private static IPAddress hostIPAddress = IPAddress.Parse("127.0.0.1");
            private static int portNumber = 7891;
            public TcpListener listener = new TcpListener(hostIPAddress, portNumber);
            public void handleRequests()
            {
                listener.Start();
                Console.WriteLine($"Listening on {hostIPAddress}:{portNumber}...");
                while (true)
                {
                    Client client = new Client(listener.AcceptTcpClient());
                    Thread thread = new Thread(client.handleClient);
                    thread.Start();
                }
            }
        }

        public class EventListener
        {
            public EventListener()
            {
                Thread thread = new Thread(this.handleEvents);
                thread.Start();
            }
            public void handleEvents()
            {
                while (true)
                {
                    if (Events.Count==0)
                    {
                        continue;
                    }
                    try
                    {
                        Events.ElementAt(0).resolve();
                        Events.RemoveAt(0);
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine("Error in resolving event" + Events.Count);
                    }
                    
                }
            }
        }

        public class Sender
        {
            public Sender()
            {
                Thread thread = new Thread(this.handleOutput);
                thread.Start();
            }
            public void handleOutput()
            {
                while (true)
                {
                    if (!Output.Any())
                    {
                        continue;
                    }
                    try
                    {
                        Output.ElementAt(0).send();
                        Output.RemoveAt(0);
                    }
                    catch (Exception e)
                    {
                        Output[0].client.Close();
                    }


                }
            }

        }

        public static void Main(string[] args) 
        {
            Console.WriteLine("Server Started...");
            Listener listener = new Listener();
            Thread thread = new Thread(listener.handleRequests);
            thread.Start();
            EventListener eventListener = new EventListener();
            Sender sender = new Sender();
            

        }
    }
}