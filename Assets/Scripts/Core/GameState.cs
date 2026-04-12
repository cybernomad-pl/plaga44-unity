// =============================================================================
// GameState.cs
// CYBERNOMAD -- Globalny automat stanow gry.
//
// DLACZEGO STATIC?
// GameState to "single source of truth" -- jeden punkt, z ktorego KAZDY system
// sprawdza co gra aktualnie robi. Dzieki temu nie trzeba przekazywac referencji
// do managera stanow miedzy komponentami. Kazdy skrypt moze po prostu sprawdzic:
//   if (!GameState.CanMove) return;
//
// FAZY GRY (GamePhase):
//   Splash    -- ekran ladowania, tylko head tracking
//   MainMenu  -- menu glowne, UI aktywne, gameplay wylaczony
//   Loading   -- ladowanie sceny, wszystko nieaktywne
//   Playing   -- rozgrywka, wszystkie systemy aktywne
//   Paused    -- pauza, UI aktywne, czas zamrozony (timeScale = 0)
//   Dead      -- ekran smierci, ograniczony input
//
// UZYCIE:
//   GameState.Play();                   // startuj rozgrywke
//   GameState.Pause();                  // pauzuj
//   GameState.TogglePause();            // toggle pauzy
//   if (GameState.CanMove) { ... }      // sprawdz czy lokomocja dozwolona
//   GameState.OnStateChanged += (o,n) => Debug.Log($"{o} -> {n}");
//
// UWAGA: SetState() automatycznie zarzadza Time.timeScale.
// W stanie Paused/MainMenu/Dead timeScale = 0, w Playing = 1.
// =============================================================================

using System;
using UnityEngine;

namespace Plaga44
{
    /// <summary>
    /// Enum opisujacy wszystkie mozliwe fazy gry.
    /// Kazda faza determinuje ktore systemy sa aktywne.
    /// </summary>
    public enum GamePhase
    {
        /// <summary>Splash screen -- tylko head tracking, bez inputu gracza.</summary>
        Splash,

        /// <summary>Menu glowne -- input UI + head tracking, bez gameplayu.</summary>
        MainMenu,

        /// <summary>Ladowanie sceny -- nic nie jest aktywne.</summary>
        Loading,

        /// <summary>Rozgrywka -- wszystko aktywne (lokomocja, bron, AI, itp.).</summary>
        Playing,

        /// <summary>Ekran ekwipunku -- UI aktywne, gameplay zamrozony, czas plynie (animacje modelu).</summary>
        Inventory,

        /// <summary>Menu pauzy -- input UI + head tracking, gameplay zamrozony.</summary>
        Paused,

        /// <summary>Ekran smierci -- ograniczony input.</summary>
        Dead,
    }

    /// <summary>
    /// Statyczny automat stanow gry. Jedyne zrodlo prawdy o tym, co gra
    /// aktualnie robi. Kazdy system powinien sprawdzac GameState przed dzialaniem.
    /// </summary>
    public static class GameState
    {
        // =====================================================================
        // Aktualny stan
        // =====================================================================

        private const string LOG = "[PLAGA44][GameState]";

        /// <summary>Aktualny stan gry.</summary>
        public static GamePhase Current { get; private set; } = GamePhase.Playing;

        /// <summary>Poprzedni stan gry (przydatne do powrotu np. z pauzy).</summary>
        public static GamePhase Previous { get; private set; } = GamePhase.Splash;

        // =====================================================================
        // Eventy
        // =====================================================================

        /// <summary>
        /// Odpala sie PO zmianie stanu. Argumenty: (starystan, nowyStan).
        /// Uzyj do reagowania na zmiane fazy, np. wlaczanie/wylaczanie UI.
        /// </summary>
        public static event Action<GamePhase, GamePhase> OnStateChanged;

        // =====================================================================
        // Tranzycja stanu
        // =====================================================================

        /// <summary>
        /// Zmienia stan gry na podany. Ignoruje jesli juz jestesmy w tym stanie.
        /// Automatycznie zarzadza Time.timeScale (0 dla pauzy/menu/smierci, 1 dla gry).
        /// </summary>
        /// <param name="newState">Nowy stan gry.</param>
        public static void SetState(GamePhase newState)
        {
            // Nie robimy nic jesli stan sie nie zmienil -- zapobiega podwojnym eventom.
            if (newState == Current) return;

            Previous = Current;
            Current = newState;

            Debug.Log($"{LOG} {Previous} -> {Current} (timeScale={Time.timeScale})");

            // Zarzadzanie Time.timeScale:
            // W stanach "zamrozonych" (pauza, menu, smierc) ustawiamy timeScale na 0,
            // co zatrzymuje fizyke, animacje i wszelkie Time.deltaTime-zależne systemy.
            // W stanie Playing przywracamy timeScale do 1.
            switch (newState)
            {
                case GamePhase.Playing:
                    Time.timeScale = 1f;
                    break;
                case GamePhase.Inventory:
                    // Inventory: czas plynie (animacje obracania modelu), ale gameplay zamrozony.
                    Time.timeScale = 1f;
                    break;
                case GamePhase.Paused:
                case GamePhase.MainMenu:
                case GamePhase.Dead:
                    Time.timeScale = 0f;
                    break;
                // Splash i Loading nie zmieniaja timeScale -- zostawiamy jak jest.
            }

            // Powiadomienie subskrybentow o zmianie stanu.
            OnStateChanged?.Invoke(Previous, newState);
        }

        // =====================================================================
        // Wygodne zapytania (convenience queries)
        // Kazde zwraca bool na podstawie aktualnego stanu.
        // Systemy uzywaja tych property zamiast recznego porownywania z Current.
        // =====================================================================

        /// <summary>True gdy gameplay powinien byc aktywny (lokomocja, bron, AI, itp.).</summary>
        public static bool IsPlaying => Current == GamePhase.Playing;

        /// <summary>True gdy input UI powinien dzialac (menu, przyciski, laser pointer).</summary>
        public static bool IsUIActive => Current == GamePhase.MainMenu ||
                                          Current == GamePhase.Paused ||
                                          Current == GamePhase.Inventory ||
                                          Current == GamePhase.Dead;

        /// <summary>True gdy lokomocja jest dozwolona -- kluczowy guard dla LocomotionController.</summary>
        public static bool CanMove => Current == GamePhase.Playing;

        /// <summary>True gdy bron i interakcje powinny dzialac.</summary>
        public static bool CanInteract => Current == GamePhase.Playing;

        /// <summary>True gdy jakiekolwiek menu jest otwarte (pauza lub menu glowne).</summary>
        public static bool IsMenuOpen => Current == GamePhase.Paused ||
                                          Current == GamePhase.MainMenu ||
                                          Current == GamePhase.Inventory;

        // =====================================================================
        // Skroty (shortcuts)
        // Wygodne metody do najczestszych tranzycji.
        // =====================================================================

        /// <summary>Rozpocznij rozgrywke.</summary>
        public static void Play() => SetState(GamePhase.Playing);

        /// <summary>Pauzuj gre (zamraza czas).</summary>
        public static void Pause() => SetState(GamePhase.Paused);

        /// <summary>Wznow gre po pauzie.</summary>
        public static void Resume() => SetState(GamePhase.Playing);

        /// <summary>Pokaz menu glowne.</summary>
        public static void ShowMainMenu() => SetState(GamePhase.MainMenu);

        /// <summary>Gracz zginol.</summary>
        public static void Die() => SetState(GamePhase.Dead);

        /// <summary>Otworz ekran ekwipunku.</summary>
        public static void Inventory() => SetState(GamePhase.Inventory);

        /// <summary>
        /// Toggle pauzy: jesli gramy -- pauzuj, jesli pauza -- wznow.
        /// Uzywane przez przycisk Start na kontrolerze.
        /// </summary>
        public static void TogglePause()
        {
            Debug.Log($"{LOG} TogglePause: current={Current}");
            if (Current == GamePhase.Playing) Pause();
            else if (Current == GamePhase.Paused) Resume();
        }
    }
}
