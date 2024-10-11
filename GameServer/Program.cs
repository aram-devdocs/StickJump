// GameServer/Program.cs
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using GameShared;
using Microsoft.Xna.Framework;
using System.Text;

namespace GameServer
{
    class Program
    {
        private static TcpListener _listener = new TcpListener(IPAddress.Any, 5001);
        private static Dictionary<int, TcpClient> _clients = new Dictionary<int, TcpClient>();
        private static GameState _gameState = new GameState();
        private static int _nextPlayerId = 1;
        private static object _lock = new object();

        static void Main(string[] args)
        {
            Console.WriteLine("Starting server...");


            _listener.Start();

            Thread acceptClientsThread = new Thread(AcceptClients);
            acceptClientsThread.Start();

            // Game loop
            while (true)
            {
                lock (_lock)
                {
                    UpdateGameState();
                    BroadcastGameState();
                }
                Thread.Sleep(16); // ~60 FPS
            }
        }

        private static void AcceptClients()
        {
            while (true)
            {
                var client = _listener.AcceptTcpClient();
                int playerId = _nextPlayerId++;
                lock (_lock)
                {
                    _clients.Add(playerId, client);
                    _gameState.Players.Add(playerId, new PlayerState
                    {
                        PlayerId = playerId,
                        Position = new Vector2(100, 300),
                        IsJumping = false
                    });
                }
                Console.WriteLine($"Player {playerId} connected.");

                Thread clientThread = new Thread(() => HandleClient(client, playerId));
                clientThread.Start();
            }
        }

        private static void HandleClient(TcpClient client, int playerId)
        {
            NetworkStream stream = client.GetStream();
            byte[] buffer = new byte[1024];

            while (client.Connected)
            {
                try
                {
                    int bytesRead = stream.Read(buffer, 0, buffer.Length);
                    if (bytesRead == 0) continue;

                    string data = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                    var playerInput = Serializer.Deserialize<PlayerInput>(data);

                    lock (_lock)
                    {
                        // Update player's input state
                        if (_gameState.Players.ContainsKey(playerId) && playerInput != null)
                        {
                            _gameState.Players[playerId].IsJumping = playerInput.IsJumping;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error with player {playerId}: {ex.Message}");
                    break;
                }
            }

            lock (_lock)
            {
                _clients.Remove(playerId);
                _gameState.Players.Remove(playerId);
            }
            Console.WriteLine($"Player {playerId} disconnected.");
        }

        private static void UpdateGameState()
        {
            // Update obstacles
            foreach (var obstacle in _gameState.Obstacles)
            {
                var position = obstacle.Position;
                position.X -= 5f; // Move obstacle to the left
                obstacle.Position = position;
            }

            // Remove off-screen obstacles
            _gameState.Obstacles.RemoveAll(o => o.Position.X < -50);

            // Add new obstacle if needed
            if (_gameState.Obstacles.Count == 0 || _gameState.Obstacles[_gameState.Obstacles.Count - 1].Position.X < 400)
            {
                _gameState.Obstacles.Add(new Obstacle
                {
                    Position = new Vector2(800, 300)
                });
            }

            // Update players
            foreach (var player in _gameState.Players.Values)
            {
                var position = player.Position;

                if (player.IsJumping)
                {
                    position.Y -= 10f; // Move up
                }
                else
                {
                    position.Y += 5f; // Apply gravity
                }

                // Clamp position
                if (position.Y > 300)
                {
                    position.Y = 300;
                }
                if (position.Y < 200)
                {
                    position.Y = 200;
                }

                player.Position = position;

                // Reset jumping state
                player.IsJumping = false;
            }
        }

        private static void BroadcastGameState()
        {
            string gameStateJson = Serializer.Serialize(_gameState);
            byte[] data = Encoding.UTF8.GetBytes(gameStateJson);

            List<int> disconnectedPlayers = new List<int>();

            foreach (var kvp in _clients)
            {
                try
                {
                    NetworkStream stream = kvp.Value.GetStream();
                    stream.Write(data, 0, data.Length);
                }
                catch (Exception)
                {
                    disconnectedPlayers.Add(kvp.Key);
                }
            }

            // Remove disconnected players
            foreach (int playerId in disconnectedPlayers)
            {
                _clients.Remove(playerId);
                _gameState.Players.Remove(playerId);
                Console.WriteLine($"Player {playerId} disconnected.");
            }
        }
    }
}