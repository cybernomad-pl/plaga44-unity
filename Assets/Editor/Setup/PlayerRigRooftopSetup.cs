// =============================================================================
// PlayerRigRooftopSetup.cs
// Jednym kliknieciem: konfiguruje CALY player rig od nowa (przez PlayerRigSetup)
// i sadza OVRCameraRig na DACHU budynku (raycast na collider dachu + fallback
// na renderer-bounds). Menu: CYBERNOMAD/Player Rig on Rooftop.
// =============================================================================
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Plaga44.Editor.Setup
{
    public static class PlayerRigRooftopSetup
    {
        private const string LOG = "[PLAGA44][RooftopSetup]";
        private const string OvrRigName = "OVRCameraRig";
        private const string ConfigPath = "Assets/PLAGA44/Config/BootstrapConfig_Quest.asset";

        // Kandydaci na budynek -- pierwszy znaleziony z rendererami wygrywa.
        // Nazwy z Scene_A (Flooded_Grounds). Jak brak -- fallback do dowolnego GO
        // z "Building" w nazwie majacego renderery.
        private static readonly string[] BuildingNames = { "ScienceBuilding", "_FloodedBuilding2", "FloodedGrounds" };

        // Ile nad dachem spawnujemy rig. Maly zapas, zeby CharacterController nie
        // klipowal w geometrie dachu; grawitacja/CC osadzi na powierzchni.
        private const float RoofClearance = 0.15f;

        [MenuItem("CYBERNOMAD/Player Rig on Rooftop", false, 2)]
        public static void Run()
        {
            // 0. Zapewnij OVRCameraRig -- jak brak (usuniety/nigdy nie byl), instancjonuj z prefabu.
            //    To najczestszy powod "nic nie dziala": brak kamery VR w scenie.
            var rig = EnsureOvrCameraRig();
            if (rig == null) return; // blad juz zalogowany

            // 1. Skonfiguruj CALY rig od nowa (CC, Locomotion, SmoothTurn, PlayerAvatar,
            //    defaultRig). Reuse istniejacego setupu -- zero duplikacji logiki.
            var cfg = AssetDatabase.LoadAssetAtPath<BootstrapConfig>(ConfigPath);
            if (cfg != null)
            {
                PlayerRigSetup.Run(cfg);
                Debug.Log($"{LOG} Rig skonfigurowany przez PlayerRigSetup (komponenty OK).");
            }
            else
            {
                Debug.LogWarning($"{LOG} Brak {ConfigPath} -- pomijam konfiguracje komponentow, tylko pozycjonuje na dach.");
            }

            // 2. Znajdz budynek.
            GameObject building = FindBuilding();
            if (building == null)
            {
                Debug.LogError($"{LOG} Nie znalazlem zadnego budynku ({string.Join(", ", BuildingNames)} ani GO z 'Building'). "
                    + "Czy Scene_A (Flooded_Grounds) jest zaladowana?");
                return;
            }

            // 3. Policz bounds budynku (ze wszystkich rendererow).
            if (!TryComputeBounds(building, out Bounds b))
            {
                Debug.LogError($"{LOG} Budynek '{building.name}' nie ma rendererow -- nie policze dachu.");
                return;
            }

            // 4. Ustal punkt dachu: raycast w dol na collider (pewna powierzchnia),
            //    fallback na renderer-bounds.max.y (jak brak collidera dachu).
            Vector3 roof = ResolveRoofPoint(building, b);

            // 5. Ustaw rig na dachu.
            Vector3 target = new Vector3(roof.x, roof.y + RoofClearance, roof.z);
            Undo.RecordObject(rig.transform, "Player Rig on Rooftop");
            rig.transform.position = target;
            EditorUtility.SetDirty(rig);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(rig.scene);

            Debug.Log($"{LOG} [OK] Rig na dachu '{building.name}' -> pos={target} (dach y={roof.y:F2}).");
            Selection.activeGameObject = rig;
            SceneView.FrameLastActiveSceneView();
        }

        // Zwraca istniejacy OVRCameraRig, albo instancjonuje z prefabu Meta SDK core.
        private static GameObject EnsureOvrCameraRig()
        {
            var existing = GameObject.Find(OvrRigName);
            if (existing != null)
            {
                Debug.Log($"{LOG} OVRCameraRig juz istnieje.");
                return existing;
            }

            // Znajdz prefab OVRCameraRig (Meta core -- konczy sie /OVRCameraRig.prefab,
            // pomijamy warianty typu OVRCameraRigInteraction).
            string prefabPath = null;
            foreach (var guid in AssetDatabase.FindAssets("OVRCameraRig t:Prefab"))
            {
                var p = AssetDatabase.GUIDToAssetPath(guid);
                if (p.EndsWith("/OVRCameraRig.prefab")) { prefabPath = p; break; }
            }
            if (prefabPath == null)
            {
                Debug.LogError($"{LOG} Nie znalazlem prefabu OVRCameraRig (Meta SDK core). Czy pakiet com.meta.xr.sdk.core jest zainstalowany?");
                return null;
            }

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                Debug.LogError($"{LOG} Nie zaladowalem prefabu z {prefabPath}.");
                return null;
            }

            var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            inst.name = OvrRigName;
            Undo.RegisterCreatedObjectUndo(inst, "Create OVRCameraRig");
            Debug.Log($"{LOG} [CREATED] OVRCameraRig z {prefabPath}");
            return inst;
        }

        private static GameObject FindBuilding()
        {
            // Priorytetowe nazwy.
            foreach (var n in BuildingNames)
            {
                var go = GameObject.Find(n);
                if (go != null && HasRenderers(go)) return go;
            }
            // Fallback: dowolny aktywny GO z "Building" w nazwie majacy renderery.
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
            {
                if (t.name.IndexOf("Building", System.StringComparison.OrdinalIgnoreCase) >= 0
                    && HasRenderers(t.gameObject))
                    return t.gameObject;
            }
            return null;
        }

        private static bool HasRenderers(GameObject go)
            => go.GetComponentInChildren<Renderer>() != null;

        private static bool TryComputeBounds(GameObject go, out Bounds bounds)
        {
            bounds = default;
            var renderers = go.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return false;
            bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            return true;
        }

        // Raycast w dol znad srodka budynku. Jak trafi w collider nalezacy do
        // budynku -> to jest pewny dach (gracz nie przeleci). Inaczej fallback
        // na renderer-top (moze brakowac collidera -- ostrzegamy).
        private static Vector3 ResolveRoofPoint(GameObject building, Bounds b)
        {
            Vector3 origin = new Vector3(b.center.x, b.max.y + 5f, b.center.z);
            var hits = Physics.RaycastAll(origin, Vector3.down, 20f);
            float bestY = float.NegativeInfinity;
            bool found = false;
            foreach (var h in hits)
            {
                // tylko collidery nalezace do tego budynku
                if (!h.collider.transform.IsChildOf(building.transform) && h.collider.gameObject != building) continue;
                if (h.point.y > bestY) { bestY = h.point.y; found = true; }
            }
            if (found)
            {
                Debug.Log($"{LOG} Dach z colliderem (raycast): y={bestY:F2}.");
                return new Vector3(b.center.x, bestY, b.center.z);
            }
            Debug.LogWarning($"{LOG} Brak collidera dachu na '{building.name}' -- uzywam renderer-top y={b.max.y:F2}. "
                + "Jak gracz przelatuje przez dach, budynek potrzebuje MeshCollidera.");
            return new Vector3(b.center.x, b.max.y, b.center.z);
        }
    }
}
#endif
