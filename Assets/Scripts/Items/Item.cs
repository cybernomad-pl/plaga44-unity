// =============================================================================
// Item.cs
// CYBERNOMAD -- "property itemu": komponent na prefabie itemu trzymajacy jego
// transformacje w roznych kontekstach trzymania ORAZ punkty chwytu (grab points).
//
// Konteksty (kazdy niezalezny, z flaga Defined):
//   - npcRightHand / npcLeftHand : offset wzgledem BONE dloni NPC (doczep broni)
//   - playerGripRight / playerGripLeft : offset wzgledem GRIP dloni gracza
//   - grabPoints : gdzie item mozna zlapac (np. strzelba: trigger + loze),
//                  z ograniczeniem ktora dlon; zrodlo pod ISDK HandGrabPose
//
// ZERO FALLBACKOW: brak konfiguracji danego kontekstu -> Defined=false.
// Caller sprawdza Defined i decyduje; NIE zgadujemy offsetu ani pozy.
// =============================================================================

using System;
using UnityEngine;

namespace Plaga44.Items
{
    /// <summary>Ktora dlon moze zlapac dany grab point.</summary>
    public enum GrabHand
    {
        Any,       // dowolna dlon
        LeftOnly,  // tylko lewa
        RightOnly  // tylko prawa
    }

    /// <summary>Offset trzymania w jednym kontekscie (bone NPC albo grip gracza).
    /// Klasa (nie struct) dla domyslnych wartosci scale=1 przy dodaniu w inspektorze.</summary>
    [Serializable]
    public class HoldPose
    {
        [Tooltip("Czy ten kontekst jest skonfigurowany. false = nieznany, NIE uzywaj (zero fallback).")]
        public bool defined;

        [Tooltip("Lokalna pozycja itemu wzgledem anchora (bone/grip), w metrach.")]
        public Vector3 localPosition = Vector3.zero;

        [Tooltip("Lokalna rotacja itemu wzgledem anchora, w stopniach (Euler).")]
        public Vector3 localEuler = Vector3.zero;

        [Tooltip("Mnoznik skali itemu w tym kontekscie. 1 = bez zmiany.")]
        public float scale = 1f;

        public Quaternion LocalRotation => Quaternion.Euler(localEuler);
    }

    /// <summary>Punkt chwytu itemu -- gdzie i ktora dlonia mozna go zlapac.
    /// Zrodlo danych pod generowanie ISDK HandGrabPose przy budowie prefabu.</summary>
    [Serializable]
    public class ItemGrabPoint
    {
        [Tooltip("Etykieta punktu, np. 'trigger', 'loze', 'raczka'. Do czytelnosci/debugu.")]
        public string label = "";

        [Tooltip("Lokalna pozycja grab pointu wzgledem roota itemu, w metrach.")]
        public Vector3 localPosition = Vector3.zero;

        [Tooltip("Lokalna rotacja grab pointu wzgledem roota itemu, w stopniach (Euler).")]
        public Vector3 localEuler = Vector3.zero;

        [Tooltip("Ktora dlon moze zlapac ten punkt.")]
        public GrabHand hand = GrabHand.Any;

        public Quaternion LocalRotation => Quaternion.Euler(localEuler);
    }

    /// <summary>Metadane itemu -- transformacje trzymania w kontekstach + grab points.</summary>
    [DisallowMultipleComponent]
    public class Item : MonoBehaviour
    {
        private const string LOG = "[PLAGA44][Item]";

        [Tooltip("Nazwa itemu = klucz. Domyslnie nazwa prefabu (bez '(Clone)').")]
        public string itemName = "";

        [Header("Trzymanie -- dlon NPC (doczep do bone)")]
        public HoldPose npcRightHand = new HoldPose();
        public HoldPose npcLeftHand = new HoldPose();

        [Header("Trzymanie -- grip gracza")]
        public HoldPose playerGripRight = new HoldPose();
        public HoldPose playerGripLeft = new HoldPose();

        [Tooltip("Czy item jest trzymany dwiema dlonmi jednoczesnie (dual wield / bron dwureczna).")]
        public bool dualWield;

        [Header("Grab points -- gdzie mozna zlapac (ISDK)")]
        [Tooltip("Punkty chwytu. Pusto = brak zdefiniowanych punktow (item lapany gdziekolwiek fallbackiem ISDK -- decyzja przy budowie prefabu, nie tu).")]
        public ItemGrabPoint[] grabPoints = Array.Empty<ItemGrabPoint>();

        /// <summary>Offset do trzymania w danej dloni NPC. false => nieskonfigurowany, NIE doczepiaj.</summary>
        public bool TryGetNpcHand(bool rightHand, out HoldPose hold)
        {
            hold = rightHand ? npcRightHand : npcLeftHand;
            return hold != null && hold.defined;
        }

        /// <summary>Offset do gripu danej dloni gracza. false => nieskonfigurowany, NIE snapuj.</summary>
        public bool TryGetPlayerGrip(bool rightHand, out HoldPose hold)
        {
            hold = rightHand ? playerGripRight : playerGripLeft;
            return hold != null && hold.defined;
        }

        /// <summary>Grab points dostepne dla danej dloni (Any + pasujace ograniczenie).</summary>
        public System.Collections.Generic.List<ItemGrabPoint> GrabPointsFor(bool rightHand)
        {
            var result = new System.Collections.Generic.List<ItemGrabPoint>();
            if (grabPoints == null) return result;
            foreach (var gp in grabPoints)
            {
                if (gp == null) continue;
                if (gp.hand == GrabHand.Any
                    || (rightHand && gp.hand == GrabHand.RightOnly)
                    || (!rightHand && gp.hand == GrabHand.LeftOnly))
                    result.Add(gp);
            }
            return result;
        }

        private void Reset()
        {
            // Explicit init nazwy z prefabu -- nie fallback, wygoda w edytorze.
            if (string.IsNullOrEmpty(itemName)) itemName = gameObject.name;
        }
    }
}
