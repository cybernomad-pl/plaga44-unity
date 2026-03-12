#if UNITY_EDITOR
using System.IO;
using Plaga44.AI;
using Plaga44.Gameplay;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.AI;

namespace Plaga44.Editor
{
    /// <summary>
    /// Editor tool do tworzenia Klaszczura i spawn pointów.
    ///
    /// Menu: CYBERNOMAD > NPC > ...
    ///
    /// Tworzy Klaszczura z:
    ///   - Capsule placeholder (do czasu integracii mesha z Blendera)
    ///   - NavMeshAgent
    ///   - Animator + AnimatorController (Idle/Patrol/Chase/Attack/Death)
    ///   - EnemyAI, HitTarget, HitZones na body parts
    ///   - Rigidbody (kinematic, dla ragdolla w przyszlosci)
    ///   - Tag "Enemy" + layer "Enemy"
    /// </summary>
    public static class NPCFactory
    {
        private const string LOG = "[PLAGA44][NPCFactory]";

        private const string ANIMATIONS_DIR      = "Assets/Animations/Klaszczur";
        private const string CONTROLLER_ASSET    = "Assets/Animations/Klaszczur/KlaszczurAnimatorController.controller";
        private const string PREFABS_DIR         = "Assets/Prefabs";
        private const string ENEMY_TAG           = "Enemy";
        private const string ENEMY_LAYER_NAME    = "Enemy";

        // Kolory dla wizualizacji placeholder
        private static readonly Color PlaceholderColor = new Color(0.55f, 0.15f, 0.15f);

        // =========================================================================
        // Menu items
        // =========================================================================

        [MenuItem("CYBERNOMAD/NPC/Create Klaszczur", priority = 100)]
        public static void CreateKlaszczur()
        {
            EnsureTagExists(ENEMY_TAG);
            int enemyLayer = EnsureLayerExists(ENEMY_LAYER_NAME);

            AnimatorController controller = GetOrCreateAnimatorController();

            GameObject root = BuildKlaszczurGameObject(controller, enemyLayer);

            // Pozycjonuj 3m przed kamera gracza
            PositionInFrontOfCamera(root.transform, 3f);

            // Rejestruj dla Undo
            Undo.RegisterCreatedObjectUndo(root, "Create Klaszczur");
            Selection.activeGameObject = root;

            Debug.Log($"{LOG} Klaszczur '{root.name}' utworzony na pozycji {root.transform.position}.");
            EditorGUIUtility.PingObject(root);
        }

        [MenuItem("CYBERNOMAD/NPC/Spawn Point", priority = 110)]
        public static void CreateSpawnPoint()
        {
            GameObject go = new GameObject("SpawnPoint");
            go.AddComponent<SpawnPointMarker>();

            PositionInFrontOfCamera(go.transform, 5f);

            Undo.RegisterCreatedObjectUndo(go, "Create Spawn Point");
            Selection.activeGameObject = go;

            Debug.Log($"{LOG} Spawn point utworzony: {go.name}");
            EditorGUIUtility.PingObject(go);
        }

        [MenuItem("CYBERNOMAD/NPC/Create Animator Controller", priority = 120)]
        public static void CreateAnimatorControllerMenu()
        {
            AnimatorController controller = CreateAnimatorController();
            if (controller != null)
            {
                EditorGUIUtility.PingObject(controller);
                Debug.Log($"{LOG} AnimatorController zapisany: {CONTROLLER_ASSET}");
            }
        }

        // =========================================================================
        // Budowanie GO Klaszczura
        // =========================================================================

        private static GameObject BuildKlaszczurGameObject(AnimatorController controller, int enemyLayer)
        {
            // Root
            GameObject root = new GameObject("Klaszczur");
            root.tag = ENEMY_TAG;
            if (enemyLayer >= 0) root.layer = enemyLayer;

            // --- HitTarget (na root) ---
            root.AddComponent<HitTarget>();

            // --- Rigidbody (kinematic -- fizyka przez NavMesh, ragdoll w przyszlosci) ---
            var rb = root.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            // --- NavMeshAgent ---
            var agent = root.AddComponent<NavMeshAgent>();
            agent.height        = 1.8f;
            agent.radius        = 0.35f;
            agent.speed         = 3.5f;
            agent.angularSpeed  = 180f;
            agent.acceleration  = 8f;
            agent.stoppingDistance = 0.5f;
            agent.autoBraking   = true;

            // --- Animator ---
            var animator = root.AddComponent<Animator>();
            if (controller != null)
                animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false; // NavMeshAgent kontroluje ruch

            // --- EnemyAI ---
            var ai = root.AddComponent<EnemyAI>();
            ai.detectionRange  = 12f;
            ai.loseRange       = 18f;
            ai.attackRange     = 1.8f;
            ai.attackCooldown  = 1.4f;
            ai.patrolSpeed     = 1.8f;
            ai.chaseSpeed      = 4.2f;
            ai.maxHealth       = 100f;

            // --- Placeholder body (Capsule) ---
            BuildPlaceholderBody(root.transform, enemyLayer);

            return root;
        }

        // =========================================================================
        // Placeholder body z HitZones
        // =========================================================================

        private static void BuildPlaceholderBody(Transform parent, int layer)
        {
            // Glowny capsule collider na root (dla NavMesh obstacle)
            var mainCol = parent.gameObject.AddComponent<CapsuleCollider>();
            mainCol.height = 1.8f;
            mainCol.radius = 0.35f;
            mainCol.center = new Vector3(0f, 0.9f, 0f);

            // Visual placeholder -- capsule
            GameObject visual = CreatePrimitiveChild(parent, "Body_Visual",
                PrimitiveType.Capsule,
                new Vector3(0f, 0.9f, 0f),
                new Vector3(0.7f, 0.9f, 0.7f),
                layer, addCollider: false);

            ApplyPlaceholderMaterial(visual, PlaceholderColor);

            // --- HitZones (bez wizualnych mesh -- uzywamy colliderów) ---

            // GLOWA
            CreateHitZone(parent, "HitZone_Head", HitZoneType.Head,
                new Vector3(0f, 1.75f, 0f), layer,
                colType: HitColliderType.Sphere, sphereRadius: 0.12f);

            // TORS
            CreateHitZone(parent, "HitZone_Body", HitZoneType.Body,
                new Vector3(0f, 1.25f, 0f), layer,
                colType: HitColliderType.Capsule,
                capsuleRadius: 0.18f, capsuleHeight: 0.5f, capsuleDir: 1,
                detach: false); // torso nie odpada

            // LEWE RAMIE
            CreateHitZone(parent, "HitZone_LeftArm", HitZoneType.LeftArm,
                new Vector3(-0.5f, 1.35f, 0f), layer,
                colType: HitColliderType.Capsule,
                capsuleRadius: 0.06f, capsuleHeight: 0.55f, capsuleDir: 0);

            // PRAWE RAMIE
            CreateHitZone(parent, "HitZone_RightArm", HitZoneType.RightArm,
                new Vector3(0.5f, 1.35f, 0f), layer,
                colType: HitColliderType.Capsule,
                capsuleRadius: 0.06f, capsuleHeight: 0.55f, capsuleDir: 0);

            // LEWA NOGA
            CreateHitZone(parent, "HitZone_LeftLeg", HitZoneType.LeftLeg,
                new Vector3(-0.12f, 0.45f, 0f), layer,
                colType: HitColliderType.Capsule,
                capsuleRadius: 0.08f, capsuleHeight: 0.75f, capsuleDir: 1);

            // PRAWA NOGA
            CreateHitZone(parent, "HitZone_RightLeg", HitZoneType.RightLeg,
                new Vector3(0.12f, 0.45f, 0f), layer,
                colType: HitColliderType.Capsule,
                capsuleRadius: 0.08f, capsuleHeight: 0.75f, capsuleDir: 1);
        }

        private enum HitColliderType { Sphere, Capsule }

        private static void CreateHitZone(
            Transform parent,
            string partName,
            HitZoneType zoneType,
            Vector3 localPos,
            int layer,
            HitColliderType colType = HitColliderType.Capsule,
            float sphereRadius = 0.1f,
            float capsuleRadius = 0.1f,
            float capsuleHeight = 0.4f,
            int capsuleDir = 1,
            bool detach = true)
        {
            GameObject go = new GameObject(partName);
            go.transform.SetParent(parent);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.identity;
            if (layer >= 0) go.layer = layer;

            if (colType == HitColliderType.Sphere)
            {
                var col = go.AddComponent<SphereCollider>();
                col.radius = sphereRadius;
            }
            else
            {
                var col = go.AddComponent<CapsuleCollider>();
                col.radius    = capsuleRadius;
                col.height    = capsuleHeight;
                col.direction = capsuleDir;
            }

            var hz = go.AddComponent<HitZone>();
            hz.zoneType     = zoneType;
            hz.detachOnHit  = detach;
        }

        // =========================================================================
        // AnimatorController
        // =========================================================================

        private static AnimatorController GetOrCreateAnimatorController()
        {
            // Jesli juz istnieje -- wczytaj
            var existing = AssetDatabase.LoadAssetAtPath<AnimatorController>(CONTROLLER_ASSET);
            if (existing != null)
            {
                Debug.Log($"{LOG} Uzywam istniejacego AnimatorController: {CONTROLLER_ASSET}");
                return existing;
            }

            return CreateAnimatorController();
        }

        private static AnimatorController CreateAnimatorController()
        {
            // Upewnij sie ze folder istnieje
            if (!AssetDatabase.IsValidFolder(ANIMATIONS_DIR))
            {
                string parent = Path.GetDirectoryName(ANIMATIONS_DIR);
                string folder = Path.GetFileName(ANIMATIONS_DIR);
                AssetDatabase.CreateFolder(parent, folder);
            }

            var controller = AnimatorController.CreateAnimatorControllerAtPath(CONTROLLER_ASSET);

            // --- Parametry ---
            controller.AddParameter("Speed",   AnimatorControllerParameterType.Float);
            controller.AddParameter("Attack",  AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Death",   AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Grounded",AnimatorControllerParameterType.Bool);

            // --- Stany w Base Layer ---
            var rootStateMachine = controller.layers[0].stateMachine;

            var stateIdle    = rootStateMachine.AddState("Idle",   new Vector3(200f,  50f, 0f));
            var statePatrol  = rootStateMachine.AddState("Patrol", new Vector3(200f, 150f, 0f));
            var stateChase   = rootStateMachine.AddState("Chase",  new Vector3(400f, 100f, 0f));
            var stateAttack  = rootStateMachine.AddState("Attack", new Vector3(600f, 100f, 0f));
            var stateDeath   = rootStateMachine.AddState("Death",  new Vector3(400f, 250f, 0f));

            // Default state = Idle
            rootStateMachine.defaultState = stateIdle;

            // --- Tranzycje ---

            // Idle -> Patrol (Speed > 0.1)
            AddTransition(stateIdle, statePatrol, "Speed", AnimatorConditionMode.Greater, 0.1f, hasExitTime: false);

            // Patrol -> Chase (Speed > 2.0)
            AddTransition(statePatrol, stateChase, "Speed", AnimatorConditionMode.Greater, 2.0f, hasExitTime: false);

            // Chase -> Patrol (Speed < 2.0 && Speed > 0.1)
            AddTransition(stateChase, statePatrol, "Speed", AnimatorConditionMode.Less, 2.0f, hasExitTime: false);

            // Chase/Patrol -> Attack (trigger)
            AddTriggerTransition(stateChase,  stateAttack, "Attack");
            AddTriggerTransition(statePatrol, stateAttack, "Attack");

            // Attack -> Chase (exit time 0.9)
            var attackToChase = stateAttack.AddTransition(stateChase);
            attackToChase.hasExitTime = true;
            attackToChase.exitTime = 0.9f;
            attackToChase.duration = 0.15f;

            // Any State -> Death (trigger, z dowolnego stanu)
            var anyToDeath = rootStateMachine.AddAnyStateTransition(stateDeath);
            anyToDeath.AddCondition(AnimatorConditionMode.If, 0, "Death");
            anyToDeath.hasExitTime = false;
            anyToDeath.duration = 0.1f;
            anyToDeath.canTransitionToSelf = false;

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"{LOG} AnimatorController utworzony: {CONTROLLER_ASSET}");
            return controller;
        }

        private static void AddTransition(
            AnimatorState from, AnimatorState to,
            string paramName, AnimatorConditionMode mode, float threshold,
            bool hasExitTime = false)
        {
            var t = from.AddTransition(to);
            t.hasExitTime = hasExitTime;
            t.duration    = 0.1f;
            t.AddCondition(mode, threshold, paramName);
        }

        private static void AddTriggerTransition(AnimatorState from, AnimatorState to, string triggerName)
        {
            var t = from.AddTransition(to);
            t.hasExitTime = false;
            t.duration    = 0.05f;
            t.AddCondition(AnimatorConditionMode.If, 0, triggerName);
        }

        // =========================================================================
        // Helpers
        // =========================================================================

        private static GameObject CreatePrimitiveChild(
            Transform parent,
            string name,
            PrimitiveType type,
            Vector3 localPos,
            Vector3 localScale,
            int layer,
            bool addCollider = false)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent);
            go.transform.localPosition = localPos;
            go.transform.localScale    = localScale;
            if (layer >= 0) go.layer = layer;

            if (!addCollider)
                Object.DestroyImmediate(go.GetComponent<Collider>());

            return go;
        }

        private static void ApplyPlaceholderMaterial(GameObject go, Color color)
        {
            var rend = go.GetComponent<Renderer>();
            if (rend == null) return;

            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");

            var mat = new Material(shader)
            {
                color = color
            };
            rend.sharedMaterial = mat;
        }

        private static void PositionInFrontOfCamera(Transform t, float distance)
        {
            var sceneView = UnityEditor.SceneView.lastActiveSceneView;
            if (sceneView != null && sceneView.camera != null)
            {
                Camera cam = sceneView.camera;
                t.position = cam.transform.position + cam.transform.forward * distance;
                // Obróc twarzą do kamery
                Vector3 dir = cam.transform.position - t.position;
                dir.y = 0f;
                if (dir != Vector3.zero)
                    t.rotation = Quaternion.LookRotation(-dir);
            }
            else
            {
                t.position = new Vector3(0f, 0f, distance);
            }
        }

        private static void EnsureTagExists(string tag)
        {
            SerializedObject tagManager = new SerializedObject(
                AssetDatabase.LoadAssetAtPath<Object>("ProjectSettings/TagManager.asset")
            );
            SerializedProperty tagsProp = tagManager.FindProperty("tags");

            for (int i = 0; i < tagsProp.arraySize; i++)
            {
                if (tagsProp.GetArrayElementAtIndex(i).stringValue == tag)
                    return;
            }

            tagsProp.InsertArrayElementAtIndex(tagsProp.arraySize);
            tagsProp.GetArrayElementAtIndex(tagsProp.arraySize - 1).stringValue = tag;
            tagManager.ApplyModifiedProperties();

            Debug.Log($"{LOG} Tag '{tag}' dodany do projektu.");
        }

        private static int EnsureLayerExists(string layerName)
        {
            // Sprawdz czy layer juz istnieje
            int existing = LayerMask.NameToLayer(layerName);
            if (existing >= 0) return existing;

            SerializedObject tagManager = new SerializedObject(
                AssetDatabase.LoadAssetAtPath<Object>("ProjectSettings/TagManager.asset")
            );
            SerializedProperty layersProp = tagManager.FindProperty("layers");

            // Szukaj wolnego slotu (od 8 w gore -- 0-7 zarezerwowane)
            for (int i = 8; i < layersProp.arraySize; i++)
            {
                SerializedProperty sp = layersProp.GetArrayElementAtIndex(i);
                if (string.IsNullOrEmpty(sp.stringValue))
                {
                    sp.stringValue = layerName;
                    tagManager.ApplyModifiedProperties();
                    Debug.Log($"{LOG} Layer '{layerName}' dodany na slot {i}.");
                    return i;
                }
            }

            Debug.LogWarning($"{LOG} Brak wolnego slotu na layer '{layerName}'. Uzywam domyslnego (0).");
            return 0;
        }
    }
}
#endif
