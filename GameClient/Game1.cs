// GameClient/Game1.cs
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Net.Sockets;
using System.Threading;
using GameShared;
using System.Text;

namespace GameClient
{
    public class Game1 : Game
    {
        private GraphicsDeviceManager _graphics;
        private SpriteBatch _spriteBatch;

        // Networking
        private TcpClient _client;
        private NetworkStream _stream;
        private Thread _receiveThread;

        // Game State
        private GameState _gameState = new GameState();
        private int _playerId;
        private object _lock = new object();
        private volatile bool _keepRunning = true;

        // Textures
        private Texture2D _playerTexture;
        private Texture2D _obstacleTexture;

        public Game1()
        {
            _graphics = new GraphicsDeviceManager(this);
            Content.RootDirectory = "Content";
            IsMouseVisible = true;
            Window.Title = "Stick Man Game";
        }

        protected override void Initialize()
        {
            // Connect to server
            try
            {
                _client = new TcpClient("127.0.0.1", 5001);
                _stream = _client.GetStream();

                // Start receiving game state
                _receiveThread = new Thread(ReceiveGameState);
                _receiveThread.Start();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Unable to connect to server: {ex.Message}");
                Exit();
            }

            base.Initialize();
        }

        protected override void LoadContent()
        {
            _spriteBatch = new SpriteBatch(GraphicsDevice);

            // Load textures
            _playerTexture = new Texture2D(GraphicsDevice, 50, 50);
            Color[] playerData = new Color[50 * 50];
            for (int i = 0; i < playerData.Length; ++i) playerData[i] = Color.Blue;
            _playerTexture.SetData(playerData);

            _obstacleTexture = new Texture2D(GraphicsDevice, 50, 50);
            Color[] obstacleData = new Color[50 * 50];
            for (int i = 0; i < obstacleData.Length; ++i) obstacleData[i] = Color.Red;
            _obstacleTexture.SetData(obstacleData);
        }

        protected override void Update(GameTime gameTime)
        {
            if (!IsActive)
                return;

            // Exit game
            if (Keyboard.GetState().IsKeyDown(Keys.Escape))
                Exit();

            // Handle input
            var input = new PlayerInput();
            if (Keyboard.GetState().IsKeyDown(Keys.Space))
            {
                input.IsJumping = true;
            }

            // Send input to server
            string inputJson = Serializer.Serialize(input);
            byte[] data = Encoding.UTF8.GetBytes(inputJson);
            try
            {
                _stream.Write(data, 0, data.Length);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error sending data to server: {ex.Message}");
                Exit();
            }

            base.Update(gameTime);
        }

        private void ReceiveGameState()
        {
            byte[] buffer = new byte[1024 * 10];
            while (_client.Connected && _keepRunning)
            {
                try
                {
                    int bytesRead = _stream.Read(buffer, 0, buffer.Length);
                    if (bytesRead == 0) continue;

                    string data = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                    var gameState = Serializer.Deserialize<GameState>(data);

                    if (gameState != null)
                    {
                        lock (_lock)
                        {
                            _gameState = gameState;
                        }
                    }
                }
                catch (Exception ex)
                {
                    if (_keepRunning)
                    {
                        Console.WriteLine($"Error receiving data from server: {ex.Message}");
                    }
                    break;
                }
            }

            Exit();
        }

        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(Color.CornflowerBlue);

            lock (_lock)
            {
                _spriteBatch.Begin();

                // Draw obstacles
                foreach (var obstacle in _gameState.Obstacles)
                {
                    _spriteBatch.Draw(_obstacleTexture, obstacle.Position, Color.White);
                }

                // Draw players
                foreach (var player in _gameState.Players.Values)
                {
                    Color color = (player.PlayerId == _playerId) ? Color.Green : Color.Gray;
                    _spriteBatch.Draw(_playerTexture, player.Position, color);
                }

                _spriteBatch.End();
            }

            base.Draw(gameTime);
        }

        protected override void OnExiting(object sender, ExitingEventArgs args)
        {
            base.OnExiting(sender, args);

            // Cleanup
            _keepRunning = false;
            _client?.Close();
            _receiveThread?.Join();
        }
    }
}