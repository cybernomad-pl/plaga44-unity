using UnityEngine;
using System;
using System.Collections.Generic;
using System.IO;

namespace Plaga44.Core
{
    /// <summary>
    /// PoseRecorder -- GLOBALNY. Jeden w scenie, lapie WSZYSTKIE NPC naraz.
    ///
    /// AUDYT:    automatyczny (PoseableBone robi po kazdym grab/release, per kosc)
    /// KEYFRAME: manualny -- przycisk A/X = zapis PELNEJ POZY WSZYSTKICH NPC w scenie
    ///
    /// Haptic feedback po zapisie.
    /// Export: JSON z timestampami -- keyframes + audit log.
    /// </summary>
    public class PoseRecorder : MonoBehaviour
    {
        [Header("Keyframe Config")]
        [Tooltip("Przycisk do manualnego keyframe (A na prawym)")]
        public OVRInput.Button keyframeButton = OVRInput.Button.One; // A

        [Tooltip("Backup: X na lewym")]
        public OVRInput.Button keyframeButtonAlt = OVRInput.Button.Three; // X

        [Header("Haptics")]
        public float hapticDuration = 0.15f;
        public float hapticFrequency = 0.8f;
        public float hapticAmplitude = 0.6f;

        [Header("State")]
        public int keyframeCount;

        // --- KEYFRAME DATA ---

        public static List<SceneKeyframe> Keyframes = new List<SceneKeyframe>();
        public static event Action<SceneKeyframe> OnKeyframeRecorded;

        [Serializable]
        public struct BonePose
        {
            public string boneName;
            public int boneIndex;
            public Vector3 localPosition;
            public Quaternion localRotation;
        }

        [Serializable]
        public struct NPCPose
        {
            public string npcName;
            public Vector3 worldPosition;
            public Quaternion worldRotation;
            public BonePose[] bones;
        }

        [Serializable]
        public struct SceneKeyframe
        {
            public int index;
            public float timestamp;
            public NPCPose[] npcs;
        }

        private float _lastKeyframeTime;

        void Start()
        {
            var allBones = FindObjectsByType<PoseableBone>(FindObjectsSortMode.None);
            var npcRoots = new HashSet<string>();
            foreach (var b in allBones)
            {
                Transform t = b.transform;
                while (t.parent != null) t = t.parent;
                npcRoots.Add(t.name);
            }

            Debug.Log($"[KEYFRAME] PoseRecorder ready -- {npcRoots.Count} NPC(s), {allBones.Length} total bones. " +
                      $"Press A or X to record keyframe of ALL NPCs.");
        }

        void Update()
        {
            if (Time.time - _lastKeyframeTime < 0.3f) return;

            bool pressed = OVRInput.GetDown(keyframeButton) || OVRInput.GetDown(keyframeButtonAlt);
            if (pressed)
            {
                RecordKeyframe();
                _lastKeyframeTime = Time.time;
            }
        }

        public void RecordKeyframe()
        {
            // Znajdz WSZYSTKIE NPC z PoseableBone w scenie
            var allBones = FindObjectsByType<PoseableBone>(FindObjectsSortMode.None);

            // Grupuj po root NPC
            var npcBones = new Dictionary<Transform, List<PoseableBone>>();
            foreach (var bone in allBones)
            {
                Transform root = bone.transform;
                while (root.parent != null) root = root.parent;

                if (!npcBones.ContainsKey(root))
                    npcBones[root] = new List<PoseableBone>();
                npcBones[root].Add(bone);
            }

            // Zapisz poze kazdego NPC
            var npcPoses = new NPCPose[npcBones.Count];
            int npcIdx = 0;
            foreach (var kvp in npcBones)
            {
                var root = kvp.Key;
                var bones = kvp.Value;

                var bonePoses = new BonePose[bones.Count];
                for (int i = 0; i < bones.Count; i++)
                {
                    bonePoses[i] = new BonePose
                    {
                        boneName = bones[i].boneName,
                        boneIndex = bones[i].boneIndex,
                        localPosition = bones[i].transform.localPosition,
                        localRotation = bones[i].transform.localRotation,
                    };
                }

                npcPoses[npcIdx] = new NPCPose
                {
                    npcName = root.name,
                    worldPosition = root.position,
                    worldRotation = root.rotation,
                    bones = bonePoses,
                };
                npcIdx++;
            }

            var snapshot = new SceneKeyframe
            {
                index = keyframeCount,
                timestamp = Time.time,
                npcs = npcPoses,
            };

            Keyframes.Add(snapshot);
            keyframeCount++;

            OnKeyframeRecorded?.Invoke(snapshot);

            // Haptic -- obie rece
            OVRInput.SetControllerVibration(hapticFrequency, hapticAmplitude, OVRInput.Controller.RTouch);
            OVRInput.SetControllerVibration(hapticFrequency, hapticAmplitude, OVRInput.Controller.LTouch);
            Invoke(nameof(StopHaptics), hapticDuration);

            // Log
            Debug.Log($"[KEYFRAME #{keyframeCount - 1}] t={snapshot.timestamp:F2} -- {npcPoses.Length} NPC(s):");
            foreach (var npc in npcPoses)
            {
                Debug.Log($"  {npc.npcName} ({npc.bones.Length} bones) @ world=({npc.worldPosition.x:F2},{npc.worldPosition.y:F2},{npc.worldPosition.z:F2})");
            }
        }

        private void StopHaptics()
        {
            OVRInput.SetControllerVibration(0, 0, OVRInput.Controller.RTouch);
            OVRInput.SetControllerVibration(0, 0, OVRInput.Controller.LTouch);
        }

        // =========================================================================
        // Export JSON -- keyframes + audit
        // =========================================================================

        public static string ExportJSON()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("{");

            // --- KEYFRAMES ---
            sb.AppendLine("  \"keyframes\": [");
            for (int k = 0; k < Keyframes.Count; k++)
            {
                var kf = Keyframes[k];
                sb.AppendLine("    {");
                sb.AppendLine($"      \"index\": {kf.index},");
                sb.AppendLine($"      \"timestamp\": {kf.timestamp:F3},");
                sb.AppendLine("      \"npcs\": [");

                for (int n = 0; n < kf.npcs.Length; n++)
                {
                    var npc = kf.npcs[n];
                    sb.AppendLine("        {");
                    sb.AppendLine($"          \"name\": \"{npc.npcName}\",");
                    sb.AppendLine($"          \"worldPos\": [{npc.worldPosition.x:F4},{npc.worldPosition.y:F4},{npc.worldPosition.z:F4}],");
                    sb.AppendLine($"          \"worldRot\": [{npc.worldRotation.x:F4},{npc.worldRotation.y:F4},{npc.worldRotation.z:F4},{npc.worldRotation.w:F4}],");
                    sb.AppendLine("          \"bones\": [");

                    for (int b = 0; b < npc.bones.Length; b++)
                    {
                        var bone = npc.bones[b];
                        sb.Append($"            {{\"name\":\"{bone.boneName}\",\"idx\":{bone.boneIndex}," +
                                  $"\"pos\":[{bone.localPosition.x:F4},{bone.localPosition.y:F4},{bone.localPosition.z:F4}]," +
                                  $"\"rot\":[{bone.localRotation.x:F4},{bone.localRotation.y:F4},{bone.localRotation.z:F4},{bone.localRotation.w:F4}]}}");
                        sb.AppendLine(b < npc.bones.Length - 1 ? "," : "");
                    }

                    sb.AppendLine("          ]");
                    sb.Append("        }");
                    sb.AppendLine(n < kf.npcs.Length - 1 ? "," : "");
                }

                sb.AppendLine("      ]");
                sb.Append("    }");
                sb.AppendLine(k < Keyframes.Count - 1 ? "," : "");
            }
            sb.AppendLine("  ],");

            // --- AUDIT LOG ---
            sb.AppendLine("  \"audit\": [");
            for (int a = 0; a < PoseableBone.AuditLog.Count; a++)
            {
                var e = PoseableBone.AuditLog[a];
                sb.Append($"    {{\"t\":{e.timestamp:F3},\"npc\":\"{e.npcName}\",\"bone\":\"{e.boneName}\"," +
                          $"\"pos\":[{e.localPosition.x:F4},{e.localPosition.y:F4},{e.localPosition.z:F4}]," +
                          $"\"rot\":[{e.localRotation.x:F4},{e.localRotation.y:F4},{e.localRotation.z:F4},{e.localRotation.w:F4}]," +
                          $"\"dPos\":[{e.deltaPosition.x:F4},{e.deltaPosition.y:F4},{e.deltaPosition.z:F4}]}}");
                sb.AppendLine(a < PoseableBone.AuditLog.Count - 1 ? "," : "");
            }
            sb.AppendLine("  ]");

            sb.AppendLine("}");
            return sb.ToString();
        }

        /// <summary>
        /// Zapisz JSON do pliku na urzadzeniu (persistentDataPath)
        /// </summary>
        public static void SaveToFile(string filename = null)
        {
            if (filename == null)
                filename = $"pose_recording_{DateTime.Now:yyyyMMdd_HHmmss}.json";

            string path = Path.Combine(Application.persistentDataPath, filename);
            File.WriteAllText(path, ExportJSON());
            Debug.Log($"[KEYFRAME] Saved to: {path} ({Keyframes.Count} keyframes, {PoseableBone.AuditLog.Count} audit entries)");
        }

        /// <summary>
        /// Auto-save przy wyjsciu z appki
        /// </summary>
        void OnApplicationQuit()
        {
            if (Keyframes.Count > 0 || PoseableBone.AuditLog.Count > 0)
            {
                SaveToFile();
            }
        }
    }
}
