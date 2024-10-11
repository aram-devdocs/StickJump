// GameShared/GameModels.cs
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
    }

    public class Obstacle
    {
        public Vector2 Position { get; set; }
    }

    public class GameState
    {
        public List<Obstacle> Obstacles { get; set; } = new List<Obstacle>();
        public Dictionary<int, PlayerState> Players { get; set; } = new Dictionary<int, PlayerState>();
    }
}