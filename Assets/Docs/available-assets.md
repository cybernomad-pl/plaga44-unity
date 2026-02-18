# Meta XR SDK - Available Assets Report

Generated: 2026-02-16
Package: com.meta.xr.sdk.interaction@271c5f55628f

## Environment Prefabs

### Main Environments
- **LocomotionEnvironment.prefab** - Full test environment for locomotion features
- **RoomEnvironment.prefab** - Basic room setup
- **SmallRoomEnvironment.prefab** - Compact test space
- **LargeRoom.prefab** - Larger test space
- **Desk.prefab** - MR desk setup

### Info/Tutorial Elements
- BasicGrabInfoFrames.prefab
- BasicPokeInfoFrames.prefab
- ComplexGrabInfoFrames.prefab
- DistanceGrabInfoCards.prefab
- InfoFrame.prefab
- DebugScreen.prefab
- Screen.prefab

## Interaction Templates (QuickActions/Templates)

### Interactors
- Template_HandGrabInteractor.prefab
- Template_HandPokeInteractor.prefab
- Template_HandRayInteractor.prefab
- Template_HandDistanceGrabInteractor.prefab
- Template_HandTeleportInteractorGroup.prefab
- Template_ControllerGrabInteractor.prefab
- Template_ControllerPokeInteractor.prefab
- Template_ControllerRayInteractor.prefab
- Template_ControllerDistanceGrabInteractor.prefab
- Template_ControllerTeleportInteractorGroup.prefab

### Interactions
- Template_HandGrabInteraction.prefab
- Template_PokeInteraction.prefab
- Template_RayInteraction.prefab
- Template_RayGrabInteraction.prefab
- Template_TeleportInteraction.prefab
- Template_DistanceGrabInteraction_HandToInteractable.prefab
- Template_DistanceGrabInteraction_InteractableToHand.prefab
- Template_DistanceGrabInteraction_AnchorAtHand.prefab
- Template_DistanceGrabSnapZone.prefab

### Base Prefab
- BaseInteractors.prefab - Foundation interactor setup

## Props (Sample Objects)

### Grabbable Objects
- **Box.prefab** - Basic box for testing grab
- **BigStone.prefab** - Heavy object
- **ChessPiece.prefab** - Small precision object
- **Doll.prefab** - Complex shaped object
- **Mug.prefab** - Handle-based grab
- **Torch.prefab** - Tool-like object
- **Key.prefab** - Small key object

### Stone Polyhedra (Geometric shapes)
- StoneCube.prefab
- StoneTetrahedron.prefab
- StoneOctahedron.prefab
- StoneDodecahedron.prefab
- StoneIcosahedron.prefab
- StonePolyhedron.prefab (base)

### Interactive Elements
- **BigRedButton.prefab** - Poke-able button
- **Keypad.prefab** - Multi-button interface
- **KeypadButton.prefab** - Single button component
- **MapWithPins.prefab** - Pin placement demo
- **Pin.prefab** - Individual pin
- **PictureFrame.prefab** - Wall-mountable
- **Picture.prefab** - Picture component
- **PingPongBall.prefab** - Physics-based

### UI Elements
- FlatUnityCanvas.prefab
- SampleCanvas.prefab
- HoverButtons.prefab
- IconBox.prefab

## UI Set (HorizonOS Style)

### Buttons
- PrimaryButton_IconAndLabel
- SecondaryButton_IconAndLabel
- BorderlessButton_IconAndLabel
- DestructiveButton_IconAndLabel
- TextTileButton variants
- ToggleButton (Checkbox, Radio, Switch)

### Dialogs
- Dialog1Button_IconAndText
- Dialog2Button_IconAndText
- Dialog1Button_TextOnly
- Dialog2Button_TextOnly
- Dialog2Button_ImageVideoAndText

### Dropdowns
- DropDown1LineTextOnly
- DropDownIconAnd1LineText
- DropDownIconAnd2LineText
- ContextMenu variants

### Sliders
- SmallSlider, MediumSlider, LargeSlider
- Variants with labels and icons

### Input
- TextInputField.prefab
- SearchBar.prefab

### Patterns (Complete UI Examples)
- ContentUIExample1-3
- ContentUIExample-HorizonOS1-3
- ContentUIExample-VideoPlayer
- GridMenuExample2x4
- GridMenuExample3x3

### Other
- EmptyUIBackplateWithCanvas.prefab
- Tooltip.prefab

## Materials

Available in Runtime/Materials/:
- OculusHand.mat - Hand rendering
- OculusHandWire.mat - Debug wireframe
- OculusHandDebug.mat - Debug visualization
- RoundedBoxUnlit.mat - UI boxes
- UIDefaultOverlay.mat - UI rendering
- TransparentStandard.mat - Transparency
- PointerMaterial.mat - Ray pointers
- TeleportReticleMaterial.mat - Teleport visuals
- LocomotionIndicatorMaterial.mat
- And many more debug/UI materials

## Meshes

Available in Runtime/Meshes/:
- OVRHand_L.fbx, OVRHand_R.fbx - Hand models
- OpenXRLeftHand.fbx, OpenXRRightHand.fbx - OpenXR hands
- HandLeft.asset, HandRight.asset - Hand data
- OculusHandPinchArrowBlended.fbx - Gesture indicator
- Locomotion/ folder - Locomotion-related meshes

## Recommendations for PLAGA '44 Test Scene

### Minimal Setup (Krok 2)
1. Use **SmallRoomEnvironment.prefab** or **RoomEnvironment.prefab** as base
2. If prefab import issues - create simple procedural room with Unity primitives

### Basic Interactions (Krok 3)
Use these simple props for testing:
1. **Box.prefab** - Basic grab test
2. **BigRedButton.prefab** - Poke interaction test
3. **Mug.prefab** - Handle-based grab test

Alternative if prefabs fail:
- Create Unity Cube/Sphere primitives
- Add Meta XR interaction components manually via templates

## Package Location
Path: `/Library/PackageCache/com.meta.xr.sdk.interaction@271c5f55628f/`

Key folders:
- `Runtime/Sample/Objects/` - Props and environments
- `Runtime/Prefabs/` - Base interactor setups
- `Editor/QuickActions/Templates/` - Interaction templates
- `Runtime/Materials/` - Materials
- `Runtime/Meshes/` - 3D models
