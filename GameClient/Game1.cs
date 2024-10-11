// GameClient/Game1.cs
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Net.Sockets;
using System.Threading;
using GameShared;
using System.Text;
using System.IO;

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

        // Add at the top of the class
        private SpriteFont _font;
        private Vector2 _scoreTablePosition = new Vector2(600, 10); // Top-right corner

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

                // Read the player ID from the server
                StreamReader reader = new StreamReader(_stream, Encoding.UTF8, false, 1024, true);
                string idString = reader.ReadLine();
                _playerId = int.Parse(idString);
                Console.WriteLine($"Assigned Player ID: {_playerId}");

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

            // Load font
            _font = Content.Load<SpriteFont>("Default");
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
            byte[] inputData = Encoding.UTF8.GetBytes(inputJson);
            try
            {
                // Send length-prefixed input
                byte[] lengthPrefix = BitConverter.GetBytes(inputData.Length);
                _stream.Write(lengthPrefix, 0, lengthPrefix.Length);
                _stream.Write(inputData, 0, inputData.Length);
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
            while (_client.Connected && _keepRunning)
            {
                try
                {
                    // Read the length prefix
                    byte[] lengthPrefix = new byte[4];
                    int bytesReceived = 0;
                    while (bytesReceived < 4)
                    {
                        int read = _stream.Read(lengthPrefix, bytesReceived, 4 - bytesReceived);
                        if (read == 0) throw new Exception("Disconnected");
                        bytesReceived += read;
                    }

                    int messageLength = BitConverter.ToInt32(lengthPrefix, 0);

                    // Read the full message
                    byte[] messageData = new byte[messageLength];
                    bytesReceived = 0;
                    while (bytesReceived < messageLength)
                    {
                        int read = _stream.Read(messageData, bytesReceived, messageLength - bytesReceived);
                        if (read == 0) throw new Exception("Disconnected");
                        bytesReceived += read;
                    }

                    string data = Encoding.UTF8.GetString(messageData);
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

                // Draw score table
                DrawScoreTable();

                _spriteBatch.End();
            }

            base.Draw(gameTime);
        }

        private void DrawScoreTable()
        {
            // Prepare the score table text
            StringBuilder scoreText = new StringBuilder();
            scoreText.AppendLine("Scores:");
            scoreText.AppendLine("Player  Score  Max");

            foreach (var player in _gameState.Players.Values)
            {
                string playerIndicator = (player.PlayerId == _playerId) ? "*" : "";
                scoreText.AppendLine($"{playerIndicator}{player.PlayerId}      {player.CurrentScore}      {player.MaxScore}");
            }

            // Draw the text
            _spriteBatch.DrawString(_font, scoreText.ToString(), _scoreTablePosition, Color.Black);
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