using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace GameShared
{
    public class PlayerInput
    {
        public bool IsJumping { get; set; }
    }

    public class PlayerState
    {
        public int PlayerId { get; set; }
        public Vector2 Position { get; set; }
        public bool IsJumping { get; set; }

        // Add these properties
        public int CurrentScore { get; set; }
        public int MaxScore { get; set; }

        // New property for player color
        public string Color { get; set; }
    }

    public class Obstacle
    {
        public Vector2 Position { get; set; }
        public bool Passed { get; set; } // To track scoring
    }

    public class GameState
    {
        public List<Obstacle> Obstacles { get; set; } = new List<Obstacle>();
        public Dictionary<int, PlayerState> Players { get; set; } = new Dictionary<int, PlayerState>();
    }
}