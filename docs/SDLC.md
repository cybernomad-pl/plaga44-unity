# PLAGA '44 -- Software Development Lifecycle (SDLC)

Proces rozwoju gry VR PLAGA '44 na Meta Quest 3.

## Git Flow

### Branche

| Branch | Cel | Kto merguje |
|--------|-----|-------------|
| `main` | Stabilna baza. Builduje się na Questa (SAFE preset). | Borys (po testach) |
| `bleeding-edge` | Branch rozwojowy. Nowe ficzery lecą tu. | Klaudia (PR -> merge) |
| `wrk1/nazwa-taska` ... `wrk20/nazwa-taska` | Worker branches -- 20 slotów na równoległe zadania. | Klaudia (PR -> bleeding-edge) |

### Zasady

1. **NIGDY nie merguj bleeding-edge do main bulk-em.** Zawsze one-by-one (cherry-pick lub merge pojedynczych commitów).
2. **NIGDY nie rób zmian bezpośrednio na testbedzie bez brancha.** Zmiany na branchu, push, potem checkout.
3. **Worker branches** mają format `wrkX/nazwa-taska` (np. `wrk14/body-rig-avatar`). NIGDY `klaudiaX/`.
4. **Testbed** domyślnie na `main` (stabilne). Editor play mode może być na `bleeding-edge`.
5. Build na Questa = z `main` (SAFE preset). Editor play mode = `bleeding-edge` (HI-END preset).

### Typowy workflow

```
# 1. Utwórz worker branch z bleeding-edge
git checkout bleeding-edge
git pull origin bleeding-edge
git checkout -b wrk14/nowa-funkcja

# 2. Pracuj, commituj
git add Assets/Scripts/NewFeature.cs
git commit -m "feat: add new feature"

# 3. Push na remote
git push -u origin wrk14/nowa-funkcja

# 4. Utwórz PR: wrk14/nowa-funkcja -> bleeding-edge
gh pr create --base bleeding-edge --title "feat: new feature" --body "..."

# 5. Review + merge PR

# 6. Cherry-pick z bleeding-edge do main (jeden po jednym, z testowaniem)
git checkout main
git cherry-pick <commit-hash>
# test w edytorze
git push origin main
```

## GitHub CLI (gh)

Klaudia używa `gh` CLI do pracy z GitHub:

```bash
# Tworzenie PR
gh pr create --base bleeding-edge --title "feat: opis" --body "## Summary\n- co\n- dlaczego"

# Lista PR-ek
gh pr list

# Review PR
gh pr view 123

# Merge PR
gh pr merge 123 --squash --delete-branch

# Issues
gh issue list
gh issue view 42
gh issue create --title "bug: opis" --body "..."
```

## Pull Requesty

### Tworzenie

1. Push brancha na remote (`git push -u origin wrk14/nazwa`)
2. `gh pr create --base bleeding-edge`
3. Tytuł: krótki (< 70 znaków), format `feat: / fix: / refactor: / docs:`
4. Body: `## Summary` + bullet points + `## Test plan`

### Review

- Klaudia tworzy, edytuje i merguje PR-ki samodzielnie
- Borys robi code review gdy potrzeba
- Testowanie = checkout brancha w testbedzie, NIE merge do main

### Merge

- Squash merge do bleeding-edge (czysta historia)
- Delete branch po merge
- Cherry-pick do main -- jeden commit na raz, z testowaniem

## Testy

### Unity EditMode Tests

Lokalizacja: `Assets/Tests/EditMode/`

```csharp
// Przykład testu EditMode
using NUnit.Framework;
using UnityEngine;

namespace Plaga44.Tests
{
    public class BodyTrackingTests
    {
        [Test]
        public void BodyCalibration_InvalidHeight_IsRejected()
        {
            var go = new GameObject("TestCalibration");
            var cal = go.AddComponent<BodyTracking.BodyCalibration>();
            cal.minValidHeadHeight = 1.0f;
            cal.maxValidHeadHeight = 2.5f;

            // Height 0.5m is below minimum -- should not calibrate
            Assert.IsFalse(cal.IsCalibrated);

            Object.DestroyImmediate(go);
        }

        [Test]
        public void MenuItemDef_CreateSubmenu_HasChildren()
        {
            var item = UI.MenuItemDef.CreateSubmenu("test", "TEST",
                new System.Collections.Generic.List<UI.MenuItemDef>());

            Assert.AreEqual(UI.MenuItemType.Submenu, item.Type);
            Assert.IsNotNull(item.Children);
        }
    }
}
```

### Jak uruchamiać testy

**Z Unity Editora:**
1. Window > General > Test Runner
2. EditMode tab
3. Run All / Run Selected

**Z linii komend (batch mode):**
```bash
Unity.exe -batchmode -runTests -testPlatform EditMode \
    -projectPath "C:\path\to\plaga44-unity" \
    -testResults "TestResults.xml"
```

### Konwencje testów

- Nazwa pliku: `*Tests.cs` (np. `BodyTrackingTests.cs`)
- Namespace: `Plaga44.Tests`
- Nazwa metody: `Component_Scenario_ExpectedResult`
- Nie testuj Meta XR SDK (wymaga urządzenia). Testuj logikę gry.

## CI/CD

### Obecny stan

- **Build**: ręczny z menu Unity (`CYBERNOMAD > Build > Build APK (Quest)`) lub batch mode
- **Deploy**: ADB install przez USB/WiFi
- **Backup**: builds z timestampem do `C:\Users\boris\NordLocker_8592730\PLAGA44\builds\`

### Build scripts

| Script | Cel |
|--------|-----|
| `Assets/Editor/BuildScript.cs` | Menu item `CYBERNOMAD/Build/Build APK (Quest)` + batch mode entry `BuildScript.Build` |
| `Assets/Editor/BuildQuest.cs` | Prosty build z hardcoded sceną (legacy) |
| `Assets/Editor/BuildInfoWriter.cs` | Pre-build: zapisuje git branch + commit hash do `Resources/BuildInfo.txt` |
| `build-quest.sh` | Bash: batch mode build + ADB deploy + backup + logcat |

### Batch mode build

```bash
# Z WSL
bash build-quest.sh              # build + deploy + logcat
bash build-quest.sh --no-install # build only
bash build-quest.sh --clean      # clean Library przed buildem
```

### Docelowy CI/CD (GitHub Actions)

```yaml
# .github/workflows/build.yml (docelowy)
name: Build Quest APK
on:
  push:
    branches: [main]
  pull_request:
    branches: [bleeding-edge]

jobs:
  build:
    runs-on: ubuntu-latest  # wymaga Unity license + Android SDK
    steps:
      - uses: actions/checkout@v4
      - uses: game-ci/unity-builder@v4
        with:
          targetPlatform: Android
          buildMethod: BuildScript.Build
      - uses: actions/upload-artifact@v4
        with:
          name: plaga44-apk
          path: Builds/*.apk
```

**Stan**: nie wdrożone. Wymaga self-hosted runnera z Unity license lub game-ci.
Przeszkody: brak Unity license na CI, duży cache Library (~5GB).

## Struktura katalogów

```
plaga44-unity/
  Assets/
    Editor/         # Editor-only scripts (menu items, build, setup)
    Scripts/
      AI/           # Enemy AI, spawners
      Audio/        # Spatial audio
      BodyTracking/ # OVRBody, PlayerBody, calibration
      Core/         # Quality menu, presets, save/load
      Gameplay/     # Hit detection, ragdoll, death
      IK/           # Foot IK, crouch, lean, seat
      UI/           # VR menu, HUD, debug overlay
      Performance/  # FPS monitor, quality scaler
      ...
    Prefabs/        # Plaga44Rig.prefab
    Scenes/         # PLAGA44_Demo.unity, testbed.unity
    Resources/      # Runtime-loaded assets
  Builds/           # APK output (gitignored)
  docs/             # Documentation
  tools/            # Python converters, CLI tools
  ProjectSettings/  # Unity project settings (committed)
```

## Konwencje kodu

- **Namespace**: `Plaga44.{Modul}` (np. `Plaga44.BodyTracking`, `Plaga44.UI`)
- **Conditional compilation**: `#if HAS_META_XR` dla Meta XR SDK, `#if UNITY_EDITOR` dla edytora
- **Logging**: `private const string LOG = "[ComponentName]";` + `Debug.Log($"{LOG} ...")`
- **Singleton**: `public static T Instance { get; private set; }` w `Awake()`
- **Auto-init**: `[RuntimeInitializeOnLoadMethod]` dla systemów, które muszą istnieć w każdej scenie
- **Menu items**: pod `CYBERNOMAD/` w menu Unity
