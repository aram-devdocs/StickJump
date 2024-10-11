using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Audio;
using System;
using System.Net.Sockets;
using System.Threading;
using GameShared;
using System.Text;
using System.IO;
using System.Collections.Generic;

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
        private Texture2D _backgroundTexture;
        private Texture2D _baseTexture;
        private Texture2D _pipeTexture;
        private Dictionary<string, Texture2D> _birdTextures;
        private SpriteFont _font;
        private Vector2 _scoreTablePosition = new Vector2(600, 10); // Top-right corner

        // Animation
        private double _animationTimer;
        private int _animationFrame;

        public Game1()
        {
            _graphics = new GraphicsDeviceManager(this);
            Content.RootDirectory = "Content";
            IsMouseVisible = true;
            Window.Title = "Flappy Bird Multiplayer";
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
            _backgroundTexture = Content.Load<Texture2D>("sprites/background-day");
            _baseTexture = Content.Load<Texture2D>("sprites/base");
            _pipeTexture = Content.Load<Texture2D>("sprites/pipe-green");

            // Load bird textures
            _birdTextures = new Dictionary<string, Texture2D>
            {
                { "yellow", Content.Load<Texture2D>("sprites/yellowbird-midflap") },
                { "blue", Content.Load<Texture2D>("sprites/bluebird-midflap") },
                { "red", Content.Load<Texture2D>("sprites/redbird-midflap") }
            };

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

            // Update animation
            _animationTimer += gameTime.ElapsedGameTime.TotalMilliseconds;
            if (_animationTimer > 200) // Change frame every 200ms
            {
                _animationFrame = (_animationFrame + 1) % 3;
                _animationTimer = 0;
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

                // Draw background
                _spriteBatch.Draw(_backgroundTexture, Vector2.Zero, Color.White);

                // Draw pipes
                foreach (var obstacle in _gameState.Obstacles)
                {
                    // Upper pipe
                    _spriteBatch.Draw(
                        _pipeTexture,
                        new Rectangle((int)obstacle.Position.X, (int)(obstacle.Position.Y - 75 - 320), 52, 320),
                        null,
                        Color.White,
                        0,
                        Vector2.Zero,
                        SpriteEffects.FlipVertically,
                        0);

                    // Lower pipe
                    _spriteBatch.Draw(
                        _pipeTexture,
                        new Vector2(obstacle.Position.X, obstacle.Position.Y + 75),
                        Color.White);
                }

                // Draw base
                _spriteBatch.Draw(_baseTexture, new Vector2(0, 512 - 112), Color.White);

                // Draw players
                foreach (var player in _gameState.Players.Values)
                {
                    // Determine bird texture
                    Texture2D birdTexture = _birdTextures[player.Color];

                    // Apply animation frames if desired
                    _spriteBatch.Draw(birdTexture, player.Position, Color.White);
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
            scoreText.AppendLine("Player\tScore\tMax");

            foreach (var player in _gameState.Players.Values)
            {
                string playerIndicator = (player.PlayerId == _playerId) ? "*" : "";
                scoreText.AppendLine($"{playerIndicator}{player.PlayerId}\t{player.CurrentScore}\t{player.MaxScore}");
            }

            // Draw the text
            _spriteBatch.DrawString(_font, scoreText.ToString(), _scoreTablePosition, Color.Black);
        }

        private void OnGameExiting(object sender, EventArgs args)
        {
            // Cleanup
            _keepRunning = false;
            _client?.Close();
            _receiveThread?.Join();
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