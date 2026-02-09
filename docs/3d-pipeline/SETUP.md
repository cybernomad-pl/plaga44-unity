# Setup Guide - cbrnmd.3D

Kompletna instrukcja instalacji wszystkich narzędzi do fotogrametrii.

## 📋 Spis treści

1. [Przygotowanie systemu](#przygotowanie-systemu)
2. [COLMAP - Instalacja](#colmap-instalacja)
3. [Meshroom - Instalacja](#meshroom-instalacja)
4. [FFmpeg - Instalacja](#ffmpeg-instalacja)
5. [Python i zależności](#python-i-zależności)
6. [Weryfikacja instalacji](#weryfikacja-instalacji)

## Przygotowanie systemu

### Ubuntu/Debian

```bash
sudo apt update
sudo apt upgrade -y

# Podstawowe narzędzia
sudo apt install -y \
    build-essential \
    cmake \
    git \
    wget \
    curl \
    python3 \
    python3-pip \
    python3-venv
```

### Fedora/RHEL

```bash
sudo dnf update -y
sudo dnf install -y \
    gcc \
    gcc-c++ \
    cmake \
    git \
    wget \
    curl \
    python3 \
    python3-pip
```

### Windows

1. Zainstaluj [Python 3.10+](https://www.python.org/downloads/)
2. Zainstaluj [Git](https://git-scm.com/download/win)
3. Zainstaluj [CMake](https://cmake.org/download/)
4. Zainstaluj [Visual Studio 2022 Community](https://visualstudio.microsoft.com/)

### macOS

```bash
# Zainstaluj Homebrew jeśli nie masz
/bin/bash -c "$(curl -fsSL https://raw.githubusercontent.com/Homebrew/install/HEAD/install.sh)"

# Narzędzia
brew install cmake git python@3.11 wget
```

## COLMAP - Instalacja

**COLMAP** jest najlepszym darmowym narzędziem do fotogrametrii.

### Metoda 1: Instalacja z paczki (najłatwiejsza)

#### Ubuntu 22.04+

```bash
sudo apt install -y \
    colmap \
    colmap-cuda  # Opcjonalnie, jeśli masz GPU NVIDIA
```

#### Windows

1. Pobierz najnowszą wersję: https://github.com/colmap/colmap/releases
2. Rozpakuj do `C:\Program Files\COLMAP`
3. Dodaj do PATH: `C:\Program Files\COLMAP\bin`

#### macOS

```bash
brew install colmap
```

### Metoda 2: Kompilacja ze źródeł (zaawansowane)

#### Linux

```bash
# Zależności
sudo apt install -y \
    libboost-all-dev \
    libeigen3-dev \
    libfreeimage-dev \
    libmetis-dev \
    libgoogle-glog-dev \
    libgflags-dev \
    libsqlite3-dev \
    libglew-dev \
    qtbase5-dev \
    libqt5opengl5-dev \
    libcgal-dev \
    libceres-dev

# Klonowanie i kompilacja
git clone https://github.com/colmap/colmap.git
cd colmap
mkdir build
cd build

cmake .. -DCMAKE_CUDA_ARCHITECTURES=native  # Z GPU
# lub
cmake ..  # Bez GPU

make -j$(nproc)
sudo make install
```

#### Windows (Visual Studio)

```powershell
# W PowerShell jako Administrator
git clone https://github.com/colmap/colmap.git
cd colmap
mkdir build
cd build

cmake .. -G "Visual Studio 17 2022" -A x64
cmake --build . --config Release
cmake --install . --prefix "C:\Program Files\COLMAP"
```

## Meshroom - Instalacja

**Meshroom** (AliceVision) - GUI tool z node-based pipeline.

### Windows/Linux

1. Pobierz najnowszą wersję: https://github.com/alicevision/Meshroom/releases
2. Rozpakuj archiwum
3. Uruchom `Meshroom.exe` (Windows) lub `Meshroom` (Linux)

**UWAGA:** Meshroom wymaga GPU NVIDIA z CUDA dla pełnej funkcjonalności!

### Linux - Instalacja z source

```bash
# Zależności
sudo apt install -y \
    libsuitesparse-dev \
    libeigen3-dev \
    libceres-dev \
    libopencv-dev \
    libpng-dev \
    libjpeg-dev \
    libtiff-dev

# AliceVision
git clone --recursive https://github.com/alicevision/AliceVision.git
cd AliceVision
mkdir build && cd build
cmake .. -DCMAKE_BUILD_TYPE=Release
make -j$(nproc)
sudo make install

# Meshroom GUI
cd ../..
git clone --recursive https://github.com/alicevision/Meshroom.git
cd Meshroom
pip3 install -r requirements.txt
```

### Weryfikacja Meshroom

```bash
# Linux
./Meshroom

# Windows
Meshroom.exe
```

## FFmpeg - Instalacja

**FFmpeg** służy do ekstrakcji klatek z wideo.

### Ubuntu/Debian

```bash
sudo apt install -y ffmpeg
```

### Fedora/RHEL

```bash
sudo dnf install -y ffmpeg
```

### Windows

1. Pobierz z: https://ffmpeg.org/download.html#build-windows
2. Rozpakuj do `C:\ffmpeg`
3. Dodaj `C:\ffmpeg\bin` do PATH

### macOS

```bash
brew install ffmpeg
```

### Weryfikacja

```bash
ffmpeg -version
```

## Python i zależności

### Utwórz wirtualne środowisko

```bash
cd cbrnmd.3D

# Stwórz venv
python3 -m venv venv

# Aktywuj venv
# Linux/macOS:
source venv/bin/activate

# Windows:
venv\Scripts\activate
```

### Zainstaluj zależności Python

```bash
pip install --upgrade pip
pip install -r tools/requirements.txt
```

Plik `requirements.txt` zawiera:
- opencv-python
- numpy
- pillow
- pyyaml
- trimesh
- pymeshlab
- open3d

## Weryfikacja instalacji

Uruchom skrypt weryfikacyjny:

```bash
cd cbrnmd.3D/tools
python3 verify_installation.py
```

Skrypt sprawdzi:
- ✅ COLMAP
- ✅ FFmpeg
- ✅ Python packages
- ✅ Meshroom (opcjonalnie)

### Ręczna weryfikacja

```bash
# COLMAP
colmap -h

# FFmpeg
ffmpeg -version

# Python packages
python3 -c "import cv2, numpy, PIL, yaml, trimesh; print('OK')"
```

## Opcjonalne: GPU CUDA Support

Jeśli masz GPU NVIDIA, zainstaluj CUDA dla przyspieszenia:

### Linux

```bash
# CUDA Toolkit
wget https://developer.download.nvidia.com/compute/cuda/repos/ubuntu2204/x86_64/cuda-keyring_1.1-1_all.deb
sudo dpkg -i cuda-keyring_1.1-1_all.deb
sudo apt update
sudo apt install -y cuda-toolkit-12-3

# cuDNN
sudo apt install -y libcudnn8 libcudnn8-dev
```

### Windows

1. Pobierz CUDA Toolkit: https://developer.nvidia.com/cuda-downloads
2. Pobierz cuDNN: https://developer.nvidia.com/cudnn
3. Zainstaluj zgodnie z instrukcjami NVIDIA

### Weryfikacja CUDA

```bash
nvidia-smi
nvcc --version
```

## Rozwiązywanie problemów

### COLMAP nie wykrywa GPU

```bash
# Sprawdź czy CUDA jest wykrywane
colmap -h | grep -i cuda

# Jeśli nie, przekompiluj z flagą CUDA
cmake .. -DCMAKE_CUDA_ARCHITECTURES=86  # Dla RTX 30xx
# Sprawdź swoją architekturę: https://developer.nvidia.com/cuda-gpus
```

### Meshroom error: CUDA not available

Meshroom wymaga **GPU NVIDIA** z CUDA. Jeśli nie masz:
- Użyj COLMAP zamiast Meshroom
- Lub użyj wersji CPU AliceVision (wolniejsze)

### FFmpeg codec errors

```bash
# Zainstaluj dodatkowe kodeki
sudo apt install -y ubuntu-restricted-extras  # Ubuntu
# lub
brew install ffmpeg --with-all  # macOS
```

### Python package conflicts

```bash
# Wyczyść cache pip
pip cache purge

# Reinstaluj w czystym venv
rm -rf venv
python3 -m venv venv
source venv/bin/activate
pip install -r tools/requirements.txt
```

## Następne kroki

Po instalacji przejdź do:
- [TURNTABLE-GUIDE.md](TURNTABLE-GUIDE.md) - Nagrywanie wideo turntable
- [../pipeline/README.md](../pipeline/README.md) - Uruchomienie pipeline'u

## 📚 Dodatkowe zasoby

- [COLMAP Documentation](https://colmap.github.io/)
- [Meshroom Documentation](https://meshroom-manual.readthedocs.io/)
- [FFmpeg Documentation](https://ffmpeg.org/documentation.html)

---

**Potrzebujesz pomocy?** Otwórz issue w repozytorium projektu.
