// SaveData.cs
// CYBERNOMAD -- Serializable data classes for save/load system.

using System;

[Serializable]
public class SaveData
{
    public int version = 1;
    public string timestamp;
    public float playTime;

    // Player
    public float[] playerPosition; // xyz
    public float[] playerRotation; // xyzw quaternion

    // Terrain
    public float terrainSeed;
    public float terrainScale;
    public float terrainStrength;

    // Quality preset slot
    public int presetSlot;

    // NPC states
    public NPCSaveData[] npcs;

    // Spawned objects
    public ObjectSaveData[] objects;
}

[Serializable]
public class NPCSaveData
{
    public string name;
    public float[] position;
    public float health;
    public bool alive;
}

[Serializable]
public class ObjectSaveData
{
    public string prefabName;
    public float[] position;
    public float[] rotation;
    public float[] scale;
}
