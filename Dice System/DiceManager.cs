using System;
using System.Collections.Generic;
using System.Linq;
using Enums;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Dice_Animations
{
    /// <summary>
    /// Manages dice animations for multiple players, ensuring unique animations
    /// and coordinating visual results with pre-recorded physics playback.
    /// </summary>
    public class DiceManager : SerializedMonoBehaviour
    {
        #region Serialized Fields

        [BoxGroup("Setup")]
        [SerializeField] private Dictionary<TeamColor, DiceAnimationPlayer> playerDices = new();
        
        [BoxGroup("Setup")]
        [SerializeField] private Dictionary<TeamColor, Material> diceMaterials;

        [BoxGroup("Data")]
        [Required] public DiceAnimationData animationData;

        #endregion

        #region Inspector Debug Info

        [BoxGroup("Runtime Info")]
        [ReadOnly, ShowInInspector]
        private Dictionary<TeamColor, DiceRollState> playerStates = new();

        [BoxGroup("Runtime Info")]
        [ReadOnly, ShowInInspector]
        private int AnimationsInUse => currentlyUsedAnimations.Count;

        #endregion

        #region Private State

        private readonly HashSet<string> currentlyUsedAnimations = new();
        private readonly Dictionary<TeamColor, string> activeAnimationNames = new();

        #endregion

        #region Events

        /// <summary>
        /// Invoked when a dice animation completes. Args: (playerColor, diceResult)
        /// </summary>
        public event Action<TeamColor, int> OnDiceAnimationComplete;

        #endregion

        #region Unity Lifecycle

        private void Start()
        {
            InitializeSystem();
        }

        private void Update()
        {
            UpdatePlayerStates();
        }

        #endregion

        #region Initialization

        private void InitializeSystem()
        {
            if (!ValidateSetup())
            {
                enabled = false;
                return;
            }

            InitializePlayerStates();
            InitializeDicePlayers();
        }

        private void InitializePlayerStates()
        {
            foreach (var color in playerDices.Keys)
            {
                playerStates[color] = new DiceRollState
                {
                    isRolling = false,
                    currentResult = -1,
                    animationName = string.Empty
                };
            }
        }

        private void InitializeDicePlayers()
        {
            foreach (var (color, player) in playerDices)
            {
                if (player != null)
                {
                    player.Hide();
                    if (diceMaterials.TryGetValue(color, out var material))
                    {
                        player.Initialize(material);
                    }
                }
            }
        }

        private bool ValidateSetup()
        {
            if (playerDices.Count == 0)
            {
                Debug.LogError($"[{nameof(DiceManager)}] No dice players assigned!");
                return false;
            }

            foreach (var (color, player) in playerDices)
            {
                if (player == null)
                {
                    Debug.LogError($"[{nameof(DiceManager)}] Missing player for {color}!");
                    return false;
                }
            }

            if (animationData == null)
            {
                Debug.LogError($"[{nameof(DiceManager)}] No animation data assigned!");
                return false;
            }

            if (animationData.animations.Count == 0)
            {
                Debug.LogWarning($"[{nameof(DiceManager)}] No animations available!");
                return false;
            }

            return true;
        }

        #endregion

        #region State Management

        private void UpdatePlayerStates()
        {
            foreach (var (color, player) in playerDices)
            {
                if (player == null) continue;

                var state = playerStates[color];
                var wasRolling = state.isRolling;
                var isNowRolling = player.isPlaying;

                state.isRolling = isNowRolling;

                // Animation just completed
                if (wasRolling && !isNowRolling)
                {
                    HandleAnimationComplete(color, player);
                }

                // Update current state while rolling
                if (isNowRolling)
                {
                    state.currentResult = player.currentDiceResult;
                    state.animationName = player.currentAnimationName;
                }
            }
        }

        private void HandleAnimationComplete(TeamColor color, DiceAnimationPlayer player)
        {
            var state = playerStates[color];
            
            // Release animation for reuse
            if (activeAnimationNames.TryGetValue(color, out var animName))
            {
                currentlyUsedAnimations.Remove(animName);
                activeAnimationNames.Remove(color);
            }

            // Hide dice and notify listeners
            player.Hide();
            OnDiceAnimationComplete?.Invoke(color, state.currentResult);
        }

        #endregion

        #region Public API

        /// <summary>
        /// Roll the dice for a specific player with a predetermined result from the server.
        /// </summary>
        public void RollDice(TeamColor playerColor, int result)
        {
            if (!ValidateRollRequest(playerColor, result)) return;

            var player = playerDices[playerColor];
            var animation = GetUniqueAnimation();

            if (animation == null)
            {
                Debug.LogWarning($"[{nameof(DiceManager)}] No available animations for {playerColor}");
                return;
            }

            ExecuteRoll(playerColor, player, animation, result);
        }

        /// <summary>
        /// Roll dice for all players simultaneously with the same result.
        /// </summary>
        public void RollAllPlayers(int result)
        {
            foreach (var color in playerDices.Keys)
            {
                RollDice(color, result);
            }
        }

        /// <summary>
        /// Stop a specific player's animation immediately.
        /// </summary>
        public void StopPlayer(TeamColor playerColor)
        {
            if (!playerDices.TryGetValue(playerColor, out var player)) return;

            player?.StopPlayback();
            player?.Hide();

            if (activeAnimationNames.TryGetValue(playerColor, out var animName))
            {
                currentlyUsedAnimations.Remove(animName);
                activeAnimationNames.Remove(playerColor);
            }

            playerStates[playerColor].Reset();
        }

        /// <summary>
        /// Stop all active dice animations.
        /// </summary>
        public void StopAll()
        {
            foreach (var color in playerDices.Keys)
            {
                StopPlayer(color);
            }

            currentlyUsedAnimations.Clear();
            activeAnimationNames.Clear();
        }

        #endregion

        #region Query Methods

        public bool IsAnyRolling() => playerStates.Values.Any(s => s.isRolling);
        
        public bool IsPlayerRolling(TeamColor player) => 
            playerStates.TryGetValue(player, out var state) && state.isRolling;
        
        public int GetPlayerResult(TeamColor player) => 
            playerStates.TryGetValue(player, out var state) ? state.currentResult : -1;
        
        public string GetPlayerAnimation(TeamColor player) => 
            playerStates.TryGetValue(player, out var state) ? state.animationName : string.Empty;

        #endregion

        #region Roll Execution

        private bool ValidateRollRequest(TeamColor playerColor, int result)
        {
            if (!playerDices.ContainsKey(playerColor))
            {
                Debug.LogError($"[{nameof(DiceManager)}] Unknown player color: {playerColor}");
                return false;
            }

            if (IsPlayerRolling(playerColor))
            {
                Debug.LogWarning($"[{nameof(DiceManager)}] {playerColor} is already rolling!");
                return false;
            }

            if (result is < 1 or > 6)
            {
                Debug.LogError($"[{nameof(DiceManager)}] Invalid dice result: {result}");
                return false;
            }

            return true;
        }

        private void ExecuteRoll(TeamColor color, DiceAnimationPlayer player, DiceAnimation animation, int result)
        {
            // Track animation usage
            currentlyUsedAnimations.Add(animation.animationName);
            activeAnimationNames[color] = animation.animationName;

            // Show dice and set up transforms
            player.Show();
            SetFinalTransform(player, animation);
            player.SetRotationFromResult(result);

            // Load and play animation
            if (!player.IsAnimationLoaded(animation.animationName))
            {
                player.LoadFromScriptableObject();
            }

            player.PlayAnimationByName(animation.animationName);

            // Update state
            playerStates[color].currentResult = result;
            playerStates[color].animationName = animation.animationName;
        }

        private void SetFinalTransform(DiceAnimationPlayer player, DiceAnimation animation)
        {
            if (animation?.frames == null || animation.frames.Count == 0) return;

            var finalFrame = animation.frames[^1];
            player.transform.position = finalFrame.position;
            player.transform.rotation = finalFrame.rotation;
        }

        private DiceAnimation GetUniqueAnimation()
        {
            if (animationData?.animations == null || animationData.animations.Count == 0)
                return null;

            var available = animationData.animations
                .Where(a => !currentlyUsedAnimations.Contains(a.animationName))
                .ToList();

            // Fall back to any animation if all are in use
            if (available.Count == 0)
            {
                Debug.LogWarning($"[{nameof(DiceManager)}] All animations in use, reusing one");
                return animationData.GetRandomAnimation();
            }

            return available[UnityEngine.Random.Range(0, available.Count)];
        }

        #endregion

        #region Inspector Buttons

        [BoxGroup("Controls")]
        [Button("Roll All Players", ButtonSizes.Large)]
        [EnableIf("@!IsAnyRolling()")]
        private void EditorRollAll() => RollAllPlayers(UnityEngine.Random.Range(1, 7));

        [BoxGroup("Controls")]
        [Button("Stop All")]
        [EnableIf("@IsAnyRolling()")]
        private void EditorStopAll() => StopAll();

        [BoxGroup("Debug")]
        [Button("Log Statistics")]
        private void EditorLogStats()
        {
            if (animationData == null) return;
            
            var stats = animationData.GetResultStatistics();
            var msg = $"Animations: {animationData.AnimationCount} total, {AnimationsInUse} in use\n";
            
            for (int i = 1; i <= 6; i++)
            {
                msg += $"Result {i}: {stats[i]} animations\n";
            }
            
            Debug.Log(msg);
        }

        #endregion

        #region Nested Types

        [Serializable]
        private class DiceRollState
        {
            public bool isRolling;
            public int currentResult;
            public string animationName;

            public void Reset()
            {
                isRolling = false;
                currentResult = -1;
                animationName = string.Empty;
            }
        }

        #endregion
    }
}