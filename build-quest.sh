#!/bin/bash
# ============================================================
# PLAGA '44 -- Build & Deploy Quest APK
# ============================================================
# One-command build: Unity batch mode -> APK -> ADB deploy -> logcat.
# Backs up every build with timestamp to builds/.
#
# Usage:
#   bash build-quest.sh                # build + deploy + logcat
#   bash build-quest.sh --no-install   # build + backup only
#   bash build-quest.sh --no-logcat    # build + deploy, skip logcat
#   bash build-quest.sh --clean        # delete Library before build
#   bash build-quest.sh --method NAME  # use custom build method
#
# Requires:
#   - Unity 6000.x installed via Unity Hub
#   - Android SDK with platform-tools (for adb)
#   - Project must have BuildScript.cs with Build() method
# ============================================================

set -e

# --- Config ---
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
PROJECT_PATH="$SCRIPT_DIR"
UNITY_PATH="/mnt/c/Program Files/Unity/Hub/Editor/6000.3.7f1/Editor/Unity.exe"
BUILDS_DIR="$PROJECT_PATH/Builds"
BACKUP_DIR="/mnt/c/Users/boris/NordLocker_8592730/PLAGA44/builds"
TIMESTAMP=$(date +%Y%m%d-%H%M%S)
BUILD_METHOD="Plaga44.Editor.BuildScript.Build"

# --- Detect git info ---
BRANCH=$(cd "$PROJECT_PATH" && git branch --show-current 2>/dev/null || echo "detached")
COMMIT=$(cd "$PROJECT_PATH" && git log --oneline -1 2>/dev/null || echo "unknown")
BRANCH_SAFE=$(echo "$BRANCH" | tr '/' '-')
APK_NAME="plaga44-${BRANCH_SAFE}-${TIMESTAMP}.apk"
APK_OUTPUT="${BUILDS_DIR}/plaga44.apk"
LOG_FILE="${BUILDS_DIR}/build-${BRANCH_SAFE}-${TIMESTAMP}.log"

# --- Parse args ---
INSTALL=true
LOGCAT=true
CLEAN=false

for arg in "$@"; do
    case "$arg" in
        --no-install) INSTALL=false ;;
        --no-logcat)  LOGCAT=false ;;
        --clean)      CLEAN=true ;;
        --method)     shift; BUILD_METHOD="$1" ;;
    esac
done

# --- Convert paths for Windows Unity ---
win_path() {
    echo "$1" | sed 's|/mnt/c/|C:\\|' | sed 's|/|\\|g'
}

WIN_PROJECT=$(win_path "$PROJECT_PATH")
WIN_LOG=$(win_path "$LOG_FILE")

# --- Header ---
echo "============================================================"
echo " PLAGA '44 -- Quest APK Build"
echo " Branch:    ${BRANCH}"
echo " Commit:    ${COMMIT}"
echo " Timestamp: ${TIMESTAMP}"
echo " Method:    ${BUILD_METHOD}"
echo " Install:   ${INSTALL}"
echo " Logcat:    ${LOGCAT}"
echo "============================================================"
echo ""

# --- Step 1: Clean (optional) ---
if [ "$CLEAN" = true ]; then
    echo "[1/7] Cleaning Library cache..."
    rm -rf "${PROJECT_PATH}/Library" 2>/dev/null || true
    echo "      Library deleted."
else
    echo "[1/7] Skipping clean."
fi

# --- Step 2: Kill Unity ---
echo "[2/7] Ensuring Unity is closed..."
powershell.exe -Command "Stop-Process -Name Unity -Force -ErrorAction SilentlyContinue" 2>/dev/null || true
powershell.exe -Command "Stop-Process -Name UnityShaderCompiler -Force -ErrorAction SilentlyContinue" 2>/dev/null || true
sleep 2
rm -f "${PROJECT_PATH}/Temp/UnityLockfile" 2>/dev/null || true

# --- Step 3: Create output dirs ---
echo "[3/7] Preparing output directories..."
mkdir -p "$BUILDS_DIR"
mkdir -p "$BACKUP_DIR" 2>/dev/null || true

# --- Step 4: Build ---
echo "[4/7] Building APK (batch mode)..."
echo "      This may take 5-15 minutes..."
START_TIME=$(date +%s)

"$UNITY_PATH" -quit -batchmode \
    -projectPath "$WIN_PROJECT" \
    -executeMethod "$BUILD_METHOD" \
    -logFile "$WIN_LOG" 2>&1 || true

END_TIME=$(date +%s)
DURATION=$((END_TIME - START_TIME))

# --- Step 5: Verify build ---
echo "[5/7] Verifying build result..."

if [ ! -f "$APK_OUTPUT" ]; then
    echo ""
    echo "[FAIL] APK not found at: ${APK_OUTPUT}"
    echo "       Build failed after ${DURATION}s."
    echo ""
    echo "Last errors from log:"
    grep -i "error CS\|BUILD FAILED\|compiler errors\|Exception" "$LOG_FILE" 2>/dev/null | tail -15
    echo ""
    echo "Full log: ${LOG_FILE}"
    exit 1
fi

APK_SIZE=$(du -h "$APK_OUTPUT" | cut -f1)
APK_MTIME=$(stat -c %Y "$APK_OUTPUT" 2>/dev/null || echo "0")
NOW=$(date +%s)
APK_AGE=$((NOW - APK_MTIME))

if [ "$APK_AGE" -gt 300 ]; then
    echo "[WARNING] APK is ${APK_AGE}s old -- build may have failed silently."
    echo "          Check log: ${LOG_FILE}"
fi

echo "      APK: ${APK_SIZE} (built in ${DURATION}s)"

# --- Step 6: Backup ---
echo "[6/7] Backing up..."
cp "$APK_OUTPUT" "${BUILDS_DIR}/${APK_NAME}"
echo "      Local:  ${BUILDS_DIR}/${APK_NAME}"

if [ -d "$BACKUP_DIR" ]; then
    cp "$APK_OUTPUT" "${BACKUP_DIR}/${APK_NAME}"
    echo "      Backup: ${BACKUP_DIR}/${APK_NAME}"
fi

# --- Step 7: Install + Logcat ---
if [ "$INSTALL" = true ]; then
    echo "[7/7] Installing on Quest..."

    # Check ADB connection
    if powershell.exe -Command "adb devices" 2>/dev/null | grep -q "device$"; then
        WIN_APK=$(win_path "${BUILDS_DIR}/${APK_NAME}")
        powershell.exe -Command "adb install -r '${WIN_APK}'" 2>&1

        # Get package name from ProjectSettings
        PACKAGE=$(grep "applicationIdentifier:" "${PROJECT_PATH}/ProjectSettings/ProjectSettings.asset" 2>/dev/null | grep Android | sed 's/.*: //' | tr -d ' ')
        if [ -z "$PACKAGE" ]; then
            PACKAGE="com.cybernomad.plaga44"
        fi

        echo "      Launching ${PACKAGE}..."
        powershell.exe -Command "adb shell monkey -p ${PACKAGE} -c android.intent.category.LAUNCHER 1" 2>&1 | tail -1

        if [ "$LOGCAT" = true ]; then
            echo ""
            echo "============================================================"
            echo " LOGCAT (Ctrl+C to stop)"
            echo "============================================================"
            sleep 2
            powershell.exe -Command "adb logcat -s Unity:V ActivityManager:I *:S" 2>&1
        fi
    else
        echo "      [SKIP] No Quest connected. Connect USB and enable ADB."
        echo "      Manual install: adb install -r '${BUILDS_DIR}/${APK_NAME}'"
    fi
else
    echo "[7/7] Skipping install (--no-install)."
fi

echo ""
echo "============================================================"
echo " BUILD COMPLETE"
echo " APK:     ${APK_NAME} (${APK_SIZE})"
echo " Branch:  ${BRANCH}"
echo " Commit:  ${COMMIT}"
echo " Time:    ${DURATION}s"
echo " Log:     ${LOG_FILE}"
echo "============================================================"
