// GameState.cs
// CYBERNOMAD -- Global game state machine.
// Single source of truth for what the game is doing RIGHT NOW.
// Every system checks GameState.Current before acting.

using System;
using UnityEngine;

namespace Plaga44
{
    public enum GamePhase
    {
        Splash,     // Splash screen -- head tracking only, no input
        MainMenu,   // Main menu -- UI input + head tracking, no gameplay
        Loading,    // Scene loading -- nothing active
        Playing,    // Gameplay -- everything active
        Paused,     // Pause menu -- UI input + head tracking, gameplay frozen
        Dead,       // Death screen -- limited input
    }

    public static class GameState
    {
        // ---- Current state ----

        public static GamePhase Current { get; private set; } = GamePhase.Splash;
        public static GamePhase Previous { get; private set; } = GamePhase.Splash;

        // ---- Events ----

        /// <summary>Fires AFTER state changes. Args: (oldState, newState)</summary>
        public static event Action<GamePhase, GamePhase> OnStateChanged;

        // ---- Transition ----

        public static void SetState(GamePhase newState)
        {
            if (newState == Current) return;

            Previous = Current;
            Current = newState;

            Debug.Log($"[PLAGA44] GameState: {Previous} -> {Current}");

            // Freeze/unfreeze time
            switch (newState)
            {
                case GamePhase.Playing:
                    Time.timeScale = 1f;
                    break;
                case GamePhase.Paused:
                case GamePhase.MainMenu:
                case GamePhase.Dead:
                    Time.timeScale = 0f;
                    break;
                // Splash and Loading keep timeScale as-is
            }

            OnStateChanged?.Invoke(Previous, newState);
        }

        // ---- Convenience queries ----

        /// <summary>True when gameplay systems should be active (locomotion, weapons, AI, etc.)</summary>
        public static bool IsPlaying => Current == GamePhase.Playing;

        /// <summary>True when UI input should work (menus, buttons, laser pointer)</summary>
        public static bool IsUIActive => Current == GamePhase.MainMenu ||
                                          Current == GamePhase.Paused ||
                                          Current == GamePhase.Dead;

        /// <summary>True when locomotion should work</summary>
        public static bool CanMove => Current == GamePhase.Playing;

        /// <summary>True when weapons/interactions should work</summary>
        public static bool CanInteract => Current == GamePhase.Playing;

        /// <summary>True when any menu is showing</summary>
        public static bool IsMenuOpen => Current == GamePhase.Paused ||
                                          Current == GamePhase.MainMenu;

        // ---- Shortcuts ----

        public static void Play() => SetState(GamePhase.Playing);
        public static void Pause() => SetState(GamePhase.Paused);
        public static void Resume() => SetState(GamePhase.Playing);
        public static void ShowMainMenu() => SetState(GamePhase.MainMenu);
        public static void Die() => SetState(GamePhase.Dead);

        public static void TogglePause()
        {
            if (Current == GamePhase.Playing) Pause();
            else if (Current == GamePhase.Paused) Resume();
        }
    }
}
