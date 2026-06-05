# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Language

- 모든 응답, 설명, 코드 리뷰는 반드시 **한국어**로 작성한다.
- 코드 주석도 한국어로 작성한다.
- 에러 메시지 분석, 아키텍처 설명, 리팩토링 제안 등 모든 커뮤니케이션에 한국어를 사용한다.
- 기술 용어(예: SyncVar, Command, ClientRpc 등)는 영문 그대로 사용하되, 설명은 한국어로 한다.

## Project Overview

**Push & Pull** — a 2-player cooperative (and PVP) side-scrolling platformer built in Unity 2022.3.62f3. The core mechanics are a charged push (PushGlove) and a directional grab/pull (Grab) that the two players use to solve physics puzzles across 12 stages. Multiplayer runs over Steam P2P via Mirror + FizzySteamworks.

## Build & CI

Builds are automated via GitHub Actions (`.github/workflows/`). They trigger on push to `develop`, use `game-ci/unity-builder`, and target `StandaloneWindows64`. There is no local build command — open the project in Unity Editor and use **File → Build Settings** for local builds.

Unity version: **2022.3.62f3** (check `ProjectSettings/ProjectVersion.txt`).

## Architecture

### Networking
All networked behaviours inherit from Mirror's `NetworkBehaviour`. The standard pattern is:
- `[SyncVar(hook = nameof(OnXChanged))]` for state that must replicate to all clients.
- `[Command]` for client→server calls, `[ClientRpc]` for server→all-clients broadcasts.
- Only the server mutates authoritative state; clients request via Commands.

### Core Systems

| System | Location | Notes |
|---|---|---|
| Player controller | `Assets/Scripts/Player/InputPlayer.cs` | Rigidbody2D movement, networked |
| Push mechanic | `Assets/Scripts/Player/PushGlove.cs` | Charge levels, force application |
| Pull/grab mechanic | `Assets/Scripts/Player/Grab.cs` | Directional aim with angle snapping |
| Animation | `Assets/Scripts/Player/PlayerAnim.cs` | Driven by movement state from InputPlayer |
| Steam name sync | `Assets/Scripts/Player/PlayerID.cs` | SyncVar + TextMeshPro |
| Key/Door puzzle | `Assets/Scripts/Mechanics/Key.cs`, `Door.cs` | Event-driven via KeyCounter |
| Color-inversion gimmick | `Assets/Scripts/Mechanics/TurnColorObject.cs` | Shader-based; drives platform activation |
| Sound | `Assets/Scripts/Manager/SoundManager.cs` | Singleton; two AudioMixerGroups: BGM / SFX |
| Input rebinding | `Assets/Scripts/Manager/KeyManager.cs`, `RebindingManager.cs` | New Input System `InputActionAsset` |

### Singleton pattern
`SoundManager` and `KeyManager` expose a static `Instance`. Awake sets it; do not create additional instances.

### Event pattern
`KeyCounter` publishes `OnKeyCountChanged` (a C# `event Action<int>`). UI subscribes; no polling.

### Input
Uses **Unity Input System 1.14.0** with an `InputActionAsset`. Rebindable actions are enumerated in `KeyAction` (LEFT, RIGHT, Jump, PUSH, PULL). Gamepad and keyboard paths are stored per-player and persisted via `PlayerPrefs`.

## Scenes

Scenes live under `Assets/Scenes/InGameScenes/`. Stages are numbered Stage1–Stage12. Additional scenes: Main (title), SelectScene (stage select), SettingScene, and PVP maps.

## Coding Conventions

- Class names: PascalCase; private fields: `_camelCase` (underscore prefix); `[SerializeField]` fields: `camelCase`.
- Comments are written in Korean.
- Inspector-facing values use `[SerializeField]`; avoid public fields unless required by Mirror.
- New scripts that need network replication must inherit `NetworkBehaviour`, not `MonoBehaviour`.
- 새 스크립트 작성 시 기존 패턴(SyncVar, Command, ClientRpc)을 따른다.
- 매직 넘버는 상수(`const` 또는 `[SerializeField]`)로 분리한다.

## Key External Packages

- **Mirror** — networking backbone (not in `manifest.json`; installed as a Unity package asset).
- **FizzySteamworks** — Steam transport layer for Mirror.
- **Steamworks.NET** — `Assets/Scripts/Steamworks.NET/`.
- **Cinemachine 2.10.3** — camera follow.
- **URP 14.0.12** — rendering pipeline; materials use URP shaders including a custom color-inversion shader.
- **TextMeshPro 3.0.7** — all UI text.

## AI 작업 가이드라인

- 기존 코드 수정 시 반드시 변경 이유를 한국어로 설명한다.
- 버그 수정 전 원인 분석을 먼저 제시한다.
- 새 기능 추가 시 네트워크 동기화 필요 여부를 항상 검토한다.
- 리팩토링 제안 시 성능 또는 유지보수 측면의 근거를 명시한다.
- 코드 생성은 명시적으로 요청받은 경우에만 출력한다.s