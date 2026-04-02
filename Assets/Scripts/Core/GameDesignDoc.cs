/// <summary>
/// PLAGA '44 -- Game Design Notes (code-embedded, survives everything)
/// Last updated: 2026-04-02
/// </summary>
public static class GameDesignDoc
{
    // =========================================================================
    // MOVEMENT & ACTIONS
    // =========================================================================
    //
    // JUMP:
    //   - B button = jump (hands empty OR holding one-handed items)
    //   - DOUBLE JUMP in air = salto (flip animation)
    //   - Max 2 jumps, reset on ground contact
    //   - Two-handed items: NO jump unless weapon lowered (barrel down)
    //
    // SPRINT:
    //   - L3 (left thumbstick press) = sprint
    //   - Auto-sprint if weapon held barrel-down (like Pavlov)
    //   - Two-handed weapons must be lowered to sprint
    //
    // CROUCH:
    //   - Physical crouch (headset Y position)
    //   - Already implemented (VRCrouch.cs)
    //
    // STAMINA:
    //   - Tied to WYTRZYMALOSC (endurance) stat
    //   - Every hostile action against player reduces endurance
    //   - Low endurance = slower sprint, weaker jumps, shaky aim
    //
    // =========================================================================
    // WEAPONS
    // =========================================================================
    //
    // M249 DISASSEMBLY:
    //   - Index finger trigger = highlight part (cyan glow)
    //   - Grip = detach part (becomes grabbable physics object)
    //   - Bring part near origin = snap reattach
    //   - B button while holding weapon = drop magazine
    //   - Parts: grip_trigger, handguard, magazine, receiver, stock, bipod
    //
    // WEAPON HANDLING:
    //   - One-handed: pistol, knife -- can jump/sprint freely
    //   - Two-handed: M249, rifle -- must lower to sprint/jump
    //   - Barrel-down detection: automatic (like Pavlov)
    //
    // =========================================================================
    // BUILD PROFILES
    // =========================================================================
    //
    // PCVR BASELINE (Steam VR):
    //   - Full quality, no compromises
    //   - Body tracking if available
    //   - High-res textures, full shadows, post-processing
    //
    // QUEST 2 OPTIMIZED:
    //   - Foveated rendering level 3
    //   - 72Hz refresh rate
    //   - Reduced shadow distance
    //   - ASTC texture compression
    //   - Stripped terrain (no trees, no grass, no buildings)
    //   - SAFE preset auto-loaded
    //
    // =========================================================================
    // SAFE MODE DEFAULTS (from Quest testing session 2026-03-31)
    // =========================================================================
    //
    // Render Scale=1.2  |  Eye Texture Scale=1.2  |  MSAA=2
    // Shadow Distance=150  |  Shadow Resolution=4096
    // Light Intensity=3.2  |  Light RGB=(1.0, 0.9, 0.96)
    // Fog Density=0.1  |  Fog End=400  |  Fog RGB=(0.38, 0.44, 0.44)
    // Texture Quality=3 (mip)  |  LOD Bias=2
    // Exposure=1.2  |  Contrast=50  |  Saturation=10
    // Sky Tint=(1.4, 1.55, 1.85)  |  Sky Exposure=0.3  |  Sky Rotation=181
    // Ambient Intensity=1.3  |  Ambient RGB=(0.12, 0.53, 1.0)
    // Foveated Render=3  |  Refresh Rate=72
    // Near Clip=0.01  |  Far Clip=2000
    // Water: RGB=(0.318, 0.381, 0.404) Metallic=0.21 Smoothness=0.423
    //        Scroll=0.007 WaveHeight=0.05 WaveFreq=0.8
    //        Emission=0.312 Reflection=1.793 Fresnel=7.01
    //        UVDensity=3.7 Transparency=0.707
    //        Foam: Depth=0.61 Strength=0.27 RGB=(0.772, 0.836, 0.764)
    // Sky Rotation Speed=0.31
    //
    // =========================================================================
    // AVATAR
    // =========================================================================
    //
    // PINEA_rigged.fbx = base avatar for player AND bots
    // 21 bones, Unity Humanoid mapping
    // Player: head hidden, hands=controllers, legs=IK
    // Bots: AI-driven, same rig, same animations
    //
    // =========================================================================

    public const string Version = "0.2.0-dev";
    public const string BuildDate = "2026-04-02";
}
