using UnityEngine;

namespace BlackoutProtocol.Core
{
    /// <summary>
    /// Project tag names. Use these constants instead of string literals.
    /// </summary>
    public static class GameTags
    {
        public const string Player = "Player";
        public const string Enemy = "Enemy";
        public const string Interactable = "Interactable";
        public const string CoverPoint = "CoverPoint";
    }

    /// <summary>
    /// Project physics layers (Project Settings → Tags and Layers).
    /// </summary>
    public static class GameLayers
    {
        public const int Player = 6;
        public const int Enemy = 7;
        public const int Interactable = 8;
        public const int Obstacle = 9;
        public const int Ground = 10;

        /// <summary>Layers that block line of sight (walls, cover, doors).</summary>
        public static readonly LayerMask SightBlockers = (1 << Obstacle) | (1 << Interactable) | (1 << Ground);

        /// <summary>Layers a weapon raycast can hit.</summary>
        public static readonly LayerMask Shootable = (1 << Enemy) | (1 << Obstacle) | (1 << Interactable) | (1 << Ground);
    }
}
