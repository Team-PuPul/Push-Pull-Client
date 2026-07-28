# 네트워크 구조 평가 및 개선 계획

- **작성일**: 2026-07-28
- **대상 브랜치**: `fix/errors`
- **기준 커밋**: `bed458f`
- **스택**: Unity 2022.3.62f3 / Mirror 96.0.1 / FizzySteamworks (Steam P2P)
- **범위**: `Assets/Scripts` 내 네트워크 관련 스크립트 21개 + 관련 프리팹/씬 설정

---

## 0. 요약

| 항목 | 판정 |
|---|---|
| 로비/세션 계층 | 🟢 양호 — 그대로 유지 |
| 기믹 상태 동기화 패턴 | 🟢 양호 — 일관되고 교과서적 |
| 물리/권한 모델 | 🔴 문제 — 권한이 코드가 아니라 프리팹 인스펙터에 존재 |
| 사망/리스폰 파이프라인 | 🔴 문제 — 게스트가 영구히 멈출 수 있음 |
| 플레이어 코어 (`InputPlayer`) | 🔴 문제 — 768줄 God object, 리팩토링 미완 상태 |
| 인원 수 확장 (PVP 4인) | 🔴 불가 — 2인 하드코딩이 NetworkManager 레벨에 존재 |
| 회귀 탐지 수단 | 🔴 없음 — 자동 테스트 0, 멀티 테스트 마찰 큼 |

**핵심 결론**: 아래 나열된 버그들은 개별 실수가 아니라 **권한 모델이 코드로 표현되지 않은 구조의 필연적 산출물**이다.
버그만 수정하면 같은 종류가 재발한다. 권한 규칙을 타입과 에디터 검증으로 승격시키는 작업이 함께 필요하다.

**방향**: 전면 재작성이 아니라, 현재의 하이브리드 권한 모델(플레이어 = 클라 권한 / 나머지 = 서버 권한)을
**명시적으로 코드에 고정**하는 방향. 총 3주(1인 기준), 5단계로 분할하며 각 단계 종료 시 항상 플레이 가능 상태를 유지한다.

---

## 1. 현재 구조

```
[Steam 로비]   ─ 초대 / 방코드 ─┐
[룸 서버 REST] ─ 코드↔LobbyId, 하트비트 ─┤→ SteamLobby → NetworkManager.StartHost/StartClient
                                        └→ Mirror(FizzySteamworks) → WaitingRoom → StageN
```

로비 계층(`SteamLobby` + `RoomServerService`)과 게임플레이 계층(Mirror)은 잘 분리되어 있다.
실패 경로(`RollbackJoin`, `HandleUnexpectedClientDisconnect`, Steam 콜백 `Dispose`)까지 처리되어 있으며 **이 계층은 손댈 필요가 없다.**

문제는 게임플레이 계층에 집중되어 있다.

### 실측 설정값

| 프리팹 | NT syncDirection | 실제 이동 주체 | 판정 |
|---|---|---|---|
| `Player_White/Black.prefab` | 1 (ClientToServer) | 소유 클라 | ✅ |
| `BoomBox.prefab` | 0 (ServerToClient) | 서버 물리 | ✅ |
| `Ball.prefab` | **1 (ClientToServer)** | 각 클라 물리 | ❌ |
| `MoveTileHorizontal.prefab` | **1 (ClientToServer)** | 서버 코루틴 | ❌ |

- `NetworkManager`: `sendRate: 60`, `bufferTimeMultiplier: 2`, `maxConnections`는 `Awake`에서 4로 덮어씀
- 플레이어 프리팹당 `NetworkTransformReliable` **4개** (root / GrabGlove / PushGlove / ID), 전부 `syncInterval: 0` → 60Hz
- 플레이어 Rigidbody2D: `bodyType: 0 (Dynamic)`, `gravityScale: 3` — 원격 프록시도 동일

---

## 2. 핵심 진단

`InputPlayer.cs:573` 과 `BoomBox.cs:172` 에 **동일한 함수가 2벌** 존재한다.

```csharp
private static bool IsServerAuthoritativePhysics(NetworkIdentity identity)
{
    NetworkTransformBase netTransform = identity.GetComponent<NetworkTransformBase>();
    return netTransform != null && netTransform.syncDirection == SyncDirection.ServerToClient;
}
```

**프리팹 인스펙터의 드롭다운 하나가 게임 로직 분기를 결정한다.**
프리팹을 건드리면 물리 규칙이 조용히 바뀌며, 컴파일러도 에디터도 이를 검증하지 않는다.
이미 `Ball.prefab`에서 사고가 발생한 상태다(3-A 참조).

---

## 3. 문제 목록 (심각도순)

### 🔴 A. Ball / MoveTile의 NetworkTransform이 `ClientToServer`인데 소유자가 없다

씬 오브젝트는 owner가 없으므로 `ClientToServer`는 의미가 없다.
동기화가 되는 것은 우연이다 — `NetworkIdentity.cs:881` 의 `ServerDirtyMasks` 가
`syncMode == Observers`(현재 설정값)일 때 **syncDirection과 무관하게** observer에게 브로드캐스트하기 때문이다.

**결과**:
- `IsServerAuthoritativePhysics(Ball)` → `false` → Push/Grab이 `RpcApplyPush` / `RpcMoveTarget` 경로로 진입
  → **모든 클라이언트가 각자 공에 힘을 가한다**
- 동시에 서버가 NT 스냅샷으로 같은 transform을 매 프레임 덮어쓴다
- 로컬 물리 vs 스냅샷 보간이 같은 오브젝트를 두고 경합 → **공 튐·러버밴딩의 직접 원인**

**수정**: Ball·MoveTile의 NT를 `ServerToClient`로 변경 + Ball에 `BoomBox.cs:50` 의 `TryEnableServerAuthorityMode()` 상당 처리 추가.

---

### 🔴 B. 원격 플레이어 프록시의 Rigidbody2D가 Dynamic 그대로다

`InputPlayer.OnStartLocalPlayer` (`:141`) 은 **로컬 플레이어만** 처리하고 원격 인스턴스는 조치가 없다.

**결과**: 모든 피어에서 상대 플레이어 몸이 중력을 받으며 물리 시뮬레이션을 돌리는 동시에 NT가 매 프레임 위치를 덮어쓴다.
- 프록시가 유령처럼 박스/공을 밀어냄
- `Trampoline`(비네트워크)이 프록시 충돌에도 반응 → 효과음/애니 중복
- `BreakTile`, 버튼류의 서버측 충돌 판정 불안정
- 지터

`BoomBox`는 이 문제를 정확히 인지하고 해결해 두었으나(`BoomBox.cs:37-45` 주석) 정작 플레이어에는 같은 처리가 없다.

**수정**: `OnStartClient` 에서 `!isLocalPlayer` 일 때 Kinematic으로 전환.

---

### 🔴 C. 사망/재시작 경로가 네트워크에서 깨진다

`Obstacle.cs` 는 순수 MonoBehaviour다.

```csharp
player.Die();               // isLocalPlayer만 통과
bs.FadeOut();               // 로컬에서 실행됨
levelLoader.LoadScene(...); // → ChangeScene: 클라에선 경고만 찍고 no-op
```

게스트가 가시에 닿으면 **자기 화면은 페이드아웃되지만 씬 리로드는 호스트만 수행 가능하다.**
호스트 시뮬레이션에서 게스트 프록시가 가시에 닿지 않으면(문제 B로 인해 충분히 가능) **게스트는 검은 화면에 영구히 갇힌다.**

`Monster.cs` 도 동일 패턴이며, 추가로 판정 로직이 **전부 주석 처리**되어 현재 아무 동작도 하지 않는다.

**수정**: 서버 판정 → `ClientRpc` 로 연출 → `ServerChangeScene`. 3-Phase 2의 `StageFlowController`로 통합.

---

### 🟠 D. 서버 권한 트리거 + 클라 권한 이동 = 구조적 입력 지연

버튼·문·색반전·폭발이 전부 `[ServerCallback] OnTriggerEnter2D` 다.
그러나 서버가 보는 게스트 위치는 **스냅샷 보간된 과거 위치**다 (`bufferTimeMultiplier: 2` + RTT).

게스트 입장에서 "분명 밟았는데 안 눌렸다"가 구조적으로 발생한다. 정밀 플랫포머라 체감이 크다.

**수정 방향**: 로컬 예측 후 서버 확정, 또는 서버 판정 시 히트박스 여유 보정.

---

### 🟠 E. 같은 정보를 SyncVar와 ClientRpc로 이중 전송

`InputPlayer.cs:333-338`:

```csharp
[Command] private void CmdForceGroundedAnimatorState(bool isMoving)
{
    SetSyncLocomotionAnimatorState(isMoving, false, false); // SyncVar 3개 → hook이 비로컬 클라에 적용
    RpcForceGroundedAnimatorState(isMoving);                // 비로컬 클라에 또 적용
}
```

대역폭 2배 + 적용 순서 경쟁. 둘 중 하나만 남겨야 한다.

`Cleared()` (`:653`) 도 동일 — `Door.RpcPlayClearStart` 가 이미 전 클라에서 모든 플레이어의 `Cleared()` 를 호출하는데
그 안에서 다시 Cmd/Rpc를 발생시켜 불필요한 왕복이 여러 번 발생한다.

---

### 🟠 F. 플레이어당 NetworkTransform 4개, 그중 하나는 다른 시스템과 경합

| 대상 | 필요 여부 | 사유 |
|---|---|---|
| `root` | ✅ 필요 | 플레이어 위치 |
| `GrabGlove` | ✅ 필요 | `Grab.DOGrab` 이 `isLocalPlayer` 만 실행 |
| `PushGlove` | ❌ **경합** | NT와 `RpcPunchAnim` → `DoPunchAnim()` 코루틴이 **같은 `transform.localPosition`을 조작** |
| `ID` | ❌ 불필요 | `PlayerID.LateUpdate` 가 로컬에서 스케일 계산 |

**수정**: 4개 → 2개로 축소.

---

### 🟠 G. 당기는 동안 매 프레임 Command

`Grab.cs:52` — `Update` 에서 `player.SyncMoveTarget(...)` 을 매 프레임 호출한다.
60Hz Command + 60Hz Rpc 브로드캐스트.
상대 플레이어를 당길 때는 `RpcMoveTarget` 이 **소유자 클라의 위치까지 강제로 덮어써서** 소유자 본인의 이동과 충돌한다.

`PlayerEyes` (`:107`) 도 20Hz Command를 상시 전송한다. 순수 장식이므로 `syncInterval` 을 지정한 SyncVar 단독으로 충분하다.
추가로 `PlayerEyes.Awake:50` 은 스폰 전에 SyncVar에 쓰고 있어 아무 효과가 없다.

---

### 🟡 H. 씬 전환 후 스폰 재배치 경쟁

`PushPullNetworkManager.cs:124` 는 **서버 쪽 identity만** 대기한다.
플레이어 NT가 `ClientToServer` 이므로 `ServerTeleport` 후에도 클라가 자기 위치를 계속 전송한다.
클라 씬 로드가 늦으면 텔레포트가 되돌려질 수 있다.

**수정**: 클라 ready 시점 기준으로 대기.

---

### 🟡 I. 연결 끊김 시 상태 정리 부재

- `Door.enteredPlayers` 는 netId를 담는데, 플레이어가 트리거 안에서 나가면 `OnTriggerExit2D` 가 호출되지 않아 netId가 잔존
  → 남은 1명이 진입하면 클리어 판정
- `OnServerDisconnect` 오버라이드가 없어 서버측 정리 훅이 부재
- `Door.Start` 의 `keyCount` (`:44`) 가 `KeyCounter.maxCount` 와 중복 소스

---

### 🟡 J. `RpcApplyPush` 에 `isLocalPlayer` 필터 누락

`InputPlayer.cs:552` — 플레이어를 대상으로 할 때 전 클라이언트가 프록시 몸에 힘을 가한다.
`BoomBox` 는 `ClientApplyExplosionForce` (`:227`) 에서 정확히 필터하는데 `InputPlayer` 만 누락되었다.
**문제 B와 결합해 증상이 확대된다.**

---

### 🟡 K. Rigidbody2D를 쓰면서 이동은 `transform.Translate`

`PlayerMovement.cs:55` (좌우 이동), `:171` (무빙 플랫폼 캐리) 모두 트랜스폼 직접 조작이다.
콜라이더 관통/끼임이 발생하며, 향후 서버 권한 전환 시 전면 재작성 대상이 된다.

---

## 4. 잘 되어 있는 부분 (유지할 것)

- **기믹 상태 동기화 패턴이 일관되고 교과서적이다.**
  `DisappearButton`, `FanButton`, `HoldTileButton`, `BreakTile`, `TurnColorObject`, `BoomBox` 전부
  `SyncVar + hook` 으로 상태를 두고 `OnStartClient` 에서 현재 상태를 재적용 → 늦게 접속한 클라도 상태가 일치한다.
- **`HoldTileController` 의 설계 판단이 특히 좋다.**
  경로가 결정적이므로 "밟힘 여부"만 동기화하고 위치는 각 클라가 계산 → NT 없이 지터 없는 발판 캐리.
  주석에 근거까지 기록되어 있다.
- **호스트 중복 실행 문제를 이해하고 있다.**
  "SyncVar만 변경하면 호스트 포함 모든 클라에서 hook이 호출된다, 수동 호출은 중복을 유발한다"는 주석은 정확하다.
- **`PauseController` 가 네트워크 세션에서 `timeScale` 을 건드리지 않는 것** (`:100`) 은 정확한 판단이다.
- **`PlayerVisualInterpolator`** — Rigidbody 보간 대신 프록시 렌더러로 시각만 보간하는 접근은 유효하다.
- **로비/세션 계층 전체** — 실패 경로까지 촘촘하다.

---

## 5. 확장성 / 유지보수성 평가

| 축 | 판정 | 근거 |
|---|---|---|
| 새 기믹 추가 | 🟢 양호 | 패턴이 일관되어 복사·수정으로 가능. 단 공유 베이스 부재로 복붙 비용 발생 |
| 코어(플레이어) 변경 | 🔴 나쁨 | `InputPlayer` 768줄 God object, 리팩토링 미완으로 경로 이중화 |
| 인원 수 확장 (PVP) | 🔴 불가 | 2인 하드코딩이 NetworkManager 레벨에 존재 |
| 스테이지 추가 | 🟡 가능하나 실수 유발 | `buildIndex + 1`, 문자열 의존, 수동 인스펙터 연결 |

### 5-1. 기믹 계층 — 복붙 비용

`FanButton`, `HoldTileButton`, `DisappearButton` 세 파일이 거의 동일하다.

```csharp
readonly HashSet<Rigidbody2D> _pressers = new HashSet<Rigidbody2D>();  // 3벌
bool IsPressable(Collider2D collision) { ... }                          // 3벌 (완전 동일)
[SyncVar(hook = ...)] bool isPressed;                                   // 3벌
[ServerCallback] void OnTriggerEnter2D / OnTriggerExit2D                // 3벌
```

### 5-2. 코어 계층 — 리팩토링 미완

`InputPlayer.cs:77`:

```csharp
// 기존 외부 코드가 InputPlayer를 통해 상태를 읽고 쓰는 API는 그대로 유지한다.
public bool jumpAble { get => Movement.JumpAble; set => Movement.JumpAble = value; }
public float PushCharge { get => PushController.PushCharge; ... }
public bool Push { get => PushController.IsPushing; ... }
```

컴포넌트 분리는 했으나 `InputPlayer` 가 전부 프록시 프로퍼티로 재노출한다.
**경로가 두 개 살아 있어 "어디를 고쳐야 하는가"가 매번 판단 대상이 된다.**

밀기 기능 하나가 3파일에 분산되어 있다:

```
PushGlove.OnTriggerStay2D  (판정)
  → InputPlayer.SyncApplyPush  (릴레이 — 여기 있는 유일한 이유는 NetworkIdentity가 여기라서)
    → CmdApplyPush → 권한 분기 → ApplyPushForceTo / RpcApplyPush
```

### 5-3. PVP 4인 확장 불가

`supportedMaxConnections = 4` 로 열어두고 주석에 *"향후 PVP를 위해 4명까지 허용한다"* (`PushPullNetworkManager.cs:41`) 고 기록되어 있으나,
그 위 계층은 전부 2인 전제다.

```csharp
// GetPlayerPrefabForConnection (:95)
if (existingPlayerCount == 0) return playerPrefab;   // 1번째 = 흰색
return blackPlayerPrefab;                            // 2·3·4번째 전부 검은색

// MovePlayersToStageSpawnPoints (:209)
Transform spawn = i == 0 ? whiteSpawn : blackSpawn;  // 3·4번째는 검은 스폰에 겹쳐 쌓임
```

팀/역할 개념이 없으며 `connectionId` 순서가 곧 캐릭터다. 재접속 시 캐릭터가 바뀐다.

### 5-4. 컴파일러가 검증하지 못하는 규칙

| 종류 | 실제 값 | 실패 방식 |
|---|---|---|
| 권한 모델 | 프리팹 인스펙터의 `syncDirection` | 조용히 물리 규칙이 바뀜 |
| 태그 | `"Player"`, `"interactive"`, `"Ground"` | 조용히 상호작용 안 됨 |
| 애니 스테이트 | `"Jump"`, `"Max"`, `"Fall"`, `"Push"`, `"Pull"` | 조용히 애니 안 나옴 |
| 씬 오브젝트 | `"WhiteStartPoint"` | 런타임 LogError |
| 씬 경로 | `"Assets/.../MainUI.unity"` — **2곳에 중복** | 한쪽만 바꾸면 어긋남 |

마지막 항목은 코드가 자백하고 있다 — `PauseController.cs:10`:
*"PushPullNetworkManager.offlineScene과 동일한 경로를 사용한다"* → **동기화가 수동이라는 의미다.**

### 5-5. 중복이 이미 버그를 생산했다

| 중복된 것 | 벌 수 |
|---|---|
| `IsServerAuthoritativePhysics` | 2 (InputPlayer, BoomBox) |
| 네트워크 종료 순서 | 2 (SteamLobby, PauseController) |
| `IsPressable` + `_pressers` | 3 |
| 키 개수 소스 | 2 (KeyCounter.maxCount, Door.keyCount) |

**문제 J(`isLocalPlayer` 필터가 BoomBox에만 존재)가 정확히 이 중복의 산물이다. 한쪽만 수정된 것이다.**

### 5-6. `FindObjectOfType` 의존이 참조 그래프를 은닉

`KeyCounter`, `InvertEffect`, `LevelLoader`, `SteamLobby`, `BGMScript` 모두 이 방식으로 탐색한다.
영향 범위 파악에 전수 검색이 필요하다. `Obstacle` · `Monster` 는 매 프레임 폴링한다.

```csharp
while (!TryCacheActiveLevelLoader()) yield return null;  // 내부에서 FindObjectsOfType
```

### 5-7. 전환 잔재

- `Monster`: 판정 로직 전부 주석 처리 → 현재 무동작
- `Obstacle` / `Monster`: `NewPlayer1` / `NewPlayer2` 시절 주석 잔존
- `KeyCounter._maxCount`: *"기존 코드 호환용. 당장 안 터지게 둠"*
- `Grab`: `public float moveSpeed` 를 인스펙터에 노출하고 `Start` 에서 `5.5f` 로 덮어씀

싱글 → 멀티 전환이 **진행 중이며 완료되지 않았다**는 신호다.

---

## 6. 목표 아키텍처

### 6-1. 권한 모델 선택

| | 모델 | 조작감 | 작업량 | 적합성 |
|---|---|---|---|---|
| **A** | **명시적 하이브리드** — 플레이어는 클라 권한, 나머지 전부 서버 권한 | 현재와 동일 (좋음) | 중 | ✅ **채택** |
| B | 전면 서버 권한 + 클라 예측/보정 | 예측 품질에 좌우 | 매우 큼 | ❌ |
| C | 결정론적 락스텝 | — | — | ❌ (Physics2D는 크로스 머신 결정론 없음) |

**B를 채택하지 않는 근거**:
- 친구와 하는 2인 협동 P2P이므로 서버가 곧 플레이어 중 한 명이다. **치팅 방지가 목적이 될 수 없다.**
- 전면 서버 권한 시 게스트가 자기 캐릭터 조작에 RTT만큼 지연을 겪는다. 정밀 플랫포머에서 명백한 후퇴다.
- 이를 막으려면 클라이언트 예측 + 서버 재조정이 필요하며, `transform.Translate` 기반 이동을 결정론적 시뮬레이션으로 전면 재작성해야 한다.
  **수 주 단위 작업이며 현재 구조 문제를 해결하지도 않는다.**
- PVP 4인도 Steam 친구 초대 기반이므로 A로 충분하다. 공개 매치메이킹 도입 시 재검토한다.

**즉 현재 구조의 방향 자체는 옳다. 문제는 그 규칙이 코드 어디에도 기록되어 있지 않다는 점이다.**

### 6-2. 규칙

> **플레이어 본체는 소유 클라이언트가, 그 외 모든 것은 서버가 소유한다. 예외 없음.**

이를 **주석이 아니라 타입과 에디터 검증으로 강제한다.**

### 6-3. 계층 구조

```
┌─ 세션 계층 ──── SteamLobby / RoomServerService        [현행 유지]
├─ 진행 계층 ──── StageFlowController (서버 전용)
│                 사망·클리어·씬 전환의 단일 진입점
├─ 월드 계층 ──── ServerAuthoritativeBehaviour 파생
│                 기믹·물리 오브젝트. SyncVar 상태 + 로컬 연출
├─ 플레이어 계층 ─ 소유 클라 권한. 프록시는 모든 피어에서 Kinematic
└─ 계약 계층 ──── Constants + 에디터 검증 (신설)
                  태그·애니·씬·권한을 컴파일/임포트 시점에 검증
```

### 6-4. 핵심 변화 3가지

1. **`syncDirection` 을 런타임에 읽는 코드를 전부 제거한다.**
   권한은 인스펙터 값이 아니라 **어떤 베이스 클래스를 상속했는가**로 결정된다. `IsServerAuthoritativePhysics` 2벌이 소멸한다.
2. **프리팹 설정과 코드가 어긋나면 에디터에서 잡는다.**
   `OnValidate` 또는 에셋 검증으로 "`ServerAuthoritativeBehaviour` 인데 NT가 `ClientToServer`" 조합을 임포트 시점에 에러 처리.
   **Ball 프리팹 사고가 재발 불가능해진다.**
3. **진행(사망·클리어·씬 전환)이 서버 한 곳으로 수렴한다.**
   현재는 `Obstacle`, `Monster`, `Door`, `LevelLoader` 에 분산되어 각자 다르게 동작한다.

---

## 7. 이행 계획

빅뱅 재작성을 하지 않는다. **각 Phase 종료 시 항상 플레이 가능해야 한다.**

### Phase 0 — 안전망 (선행 필수, 생략 금지)

리팩토링의 최대 리스크는 "무엇이 깨졌는지 모른다"이다. 현재 회귀 탐지 수단이 0이므로 여기서 시작한다.

- [ ] **멀티플레이 Play Mode 도입** — ParrelSync 또는 Multiplayer Play Mode.
      현재는 매번 빌드 후 2개를 실행해야 하며, 이 마찰이 곧 "테스트를 안 하게 되는" 원인이다. **투입 대비 효과 최대.**
- [ ] **네트워크 디버그 오버레이** — 오브젝트별 권한 주체, RTT, 프록시 위치 vs 로컬 위치 편차 표시.
      본 문서의 문제들이 눈에 보이게 된다.
- [ ] **플레이 테스트 체크리스트 문서화** — 부록 A 참조.

> 규모: 1~2일. **생략 시 이후 전 단계가 도박이 된다.**

### Phase 1 — 권한 규칙 명시화

문제 **A · B · J** 가 한꺼번에 해결된다.

- [ ] `Constants` static class 신설 — 태그, 애니 스테이트, 씬 경로. **씬 경로 2중 하드코딩 통합**
- [ ] `ServerAuthoritativeBehaviour` 베이스 도입 + 에디터 검증
- [ ] `IsServerAuthoritativePhysics` 2벌 제거 → `Ball` · `MoveTile` 프리팹을 `ServerToClient` 로 교정
- [ ] 원격 플레이어 프록시 Rigidbody2D → Kinematic (`OnStartClient` 의 `!isLocalPlayer` 분기)
- [ ] `RpcApplyPush` 에 `isLocalPlayer` 필터 추가

> 규모: 2~3일. **체감이 가장 큰 단계.** 공 튐, 유령 밀림, 지터가 대부분 해소된다.

### Phase 2 — 월드/진행 계층 정리

문제 **C · I** 해결.

- [ ] `PressablePlateBase` 추출 → `FanButton` / `HoldTileButton` / `DisappearButton` 3벌 → 1벌
- [ ] **`StageFlowController` 신설 (서버 전용)** — 사망·리스폰·클리어·씬 전환의 단일 진입점
  - `Obstacle` / `Monster` 는 "서버에서 사망 신고"만 하고 처리는 여기서
  - 게스트가 검은 화면에 갇히는 버그가 구조적으로 불가능해짐
  - `LevelLoader` 는 순수 연출 재생기로 축소
- [ ] `Door.keyCount` 중복 제거 → `KeyCounter.MaxCount` 단일 소스
- [ ] `OnServerDisconnect` 처리 → `Door.enteredPlayers` 정리

> 규모: 3~4일

### Phase 3 — 플레이어 계층 분해

문제 **E · F** 해결.

`InputPlayer` 768줄을 책임별로 분리한다.

| 신설 | 책임 |
|---|---|
| `PlayerNetworkRelay` | Cmd/Rpc 릴레이 전부 (밀기·당기기·플립) |
| `PlayerAnimationSync` | SyncVar 6개 + 애니 상태 머신 |
| `PlayerClearPresenter` | 클리어 페이드아웃 코루틴 (80줄) |
| `InputPlayer` (잔존) | 입력 라우팅만 |

- [ ] 위 4개로 분해
- [ ] **프록시 프로퍼티 제거** (`jumpAble`, `PushCharge`, `Push`) → 호출부가 실제 컴포넌트를 직접 참조
- [ ] **SyncVar/Rpc 이중 전송 제거** — 둘 중 하나만
- [ ] **잉여 NetworkTransform 제거** — `ID`, `PushGlove`. 4개 → 2개

> 규모: 4~5일. **회귀 위험 최대 구간.** Phase 0의 체크리스트가 여기서 값을 한다.

### Phase 4 — Grab 재설계 (Phase 1 이후 독립 진행 가능)

문제 **G** 해결. 현재 구조에서 가장 어색한 부분이라 분리한다.

```
현재:  매 프레임 Cmd(Vector3) 60Hz  →  Rpc 브로드캐스트 60Hz
        + 상대 플레이어를 당길 땐 소유자의 위치를 강제로 덮어씀

개선:  Cmd "잡았다"(netId) / Cmd "놓았다"  ← 이벤트 2회
        서버가 FixedJoint2D 또는 서버측 추적으로 대상을 견인
        결과는 NetworkTransform이 배포
```

전송량 **초당 60회 → 상호작용당 2회**. 소유자 위치를 덮어쓰는 문제 소멸. 물리적으로도 자연스러워진다.

> 규모: 2~3일

### Phase 5 — 확장 지점 개방 (PVP 착수 전)

- [ ] **역할 기반 플레이어 할당** — `PlayerRole` + 서버측 `PlayerRoster`.
      `connectionId` 순서 의존 제거, 재접속 시 캐릭터 유지
- [ ] **`StageSpawnPoints` 컴포넌트** — `GameObject.Find("WhiteStartPoint")` 제거, 역할 → 스폰 매핑
- [ ] **`StageSequenceSO`** — `buildIndex + 1` 의존 제거. 스테이지 순서를 에셋으로

> 규모: 2~3일. 완료 시 PVP 4인 착수 가능.

---

## 8. 하지 말아야 할 것

- ❌ **네트워크 라이브러리 교체 (FishNet, Netcode 등)**
  문제는 Mirror가 아니라 권한 모델이 코드에 없다는 점이다. 교체하면 같은 문제를 새 API로 다시 겪는다.
- ❌ **전면 서버 권한 + 클라 예측**
  6-1 참조. 이 게임의 위협 모델에 맞지 않으며 조작감을 해친다.
- ❌ **한 번에 전부 진행**
  각 Phase는 독립적으로 배포 가능해야 한다.
- ❌ **Phase 0 생략**
  가장 유혹적이고 가장 비싸다.

---

## 9. 일정 요약

**총 약 3주 (1인 기준).**
Phase 1까지만으로도 체감 버그 대부분이 해소되므로 **Phase 0 + 1을 먼저 끊어서 배포**할 것을 권한다.

| Phase | 규모 | 해결 항목 | 선행 |
|---|---|---|---|
| 0 안전망 | 1~2일 | (회귀 탐지 수단 확보) | — |
| 1 권한 명시화 | 2~3일 | 🔴A 🔴B 🟡J — 공 튐·유령 밀림·지터 | 0 |
| 2 월드/진행 | 3~4일 | 🔴C 🟡I — 사망 갇힘·문 판정 | 1 |
| 3 플레이어 분해 | 4~5일 | 🟠E 🟠F — 이중 전송·God object | 0, 1 |
| 4 Grab 재설계 | 2~3일 | 🟠G — 매 프레임 Cmd | 1 |
| 5 확장 개방 | 2~3일 | PVP·스테이지 확장 | 2, 3 |

Phase 4는 1 이후 언제든 가능하며 Phase 3과 병행 가능하다.

미해결로 남는 항목: **🟠D** (서버 트리거 지연 — 별도 보정 설계 필요), **🟡H** (씬 전환 경쟁 — Phase 5의 스폰 개편과 함께 처리), **🟡K** (`transform.Translate` — 이동 시스템 재작성 시점에 처리).

---

## 부록 A. 플레이 테스트 체크리스트 (Phase 0 산출물 초안)

각 Phase 종료 시 호스트/게스트 양쪽 관점에서 확인한다.
**"게스트 시점"을 반드시 포함할 것** — 본 문서의 버그 대부분이 게스트에서만 발현한다.

### A-1. 세션
- [ ] 방 생성 → 코드 표시 → 게스트가 코드로 입장
- [ ] Steam 친구 초대로 입장
- [ ] 잘못된 방 코드 입력 시 안내 표시
- [ ] 호스트가 나가면 게스트가 MainUI로 복귀
- [ ] 게스트가 나가면 호스트가 정상 유지
- [ ] 일시정지 → 나가기 → 방 정리 확인

### A-2. 이동/물리 (양쪽 시점 각각)
- [ ] 상대 캐릭터가 부드럽게 보이는가 (지터 없음)
- [ ] 상대 캐릭터가 박스/공을 유령처럼 밀지 않는가 ← **문제 B**
- [ ] 무빙 발판 위에서 양쪽 다 정상 캐리되는가
- [ ] 트램펄린 효과음이 1회만 재생되는가 ← **문제 B**

### A-3. 밀기/당기기
- [ ] 공을 밀었을 때 양쪽 화면에서 같은 위치로 가는가 ← **문제 A**
- [ ] 공을 당겼을 때 튀지 않는가 ← **문제 A**
- [ ] 상대 플레이어를 밀 수 있는가 (밀린 쪽 시점에서 자연스러운가) ← **문제 J**
- [ ] 상대 플레이어를 당길 때 당겨지는 쪽 조작이 뺏기지 않는가 ← **문제 G**
- [ ] 폭발 상자가 양쪽에서 동일하게 터지는가

### A-4. 기믹
- [ ] 버튼: 게스트가 밟아도 즉시 반응하는가 ← **문제 D**
- [ ] 버튼: 둘이 밟았다가 한 명만 내려와도 유지되는가
- [ ] 색반전: 양쪽 화면 상태가 일치하는가
- [ ] 부서지는 바닥: 양쪽에서 동일하게 부서지는가
- [ ] 사라지는 벽: 양쪽에서 동일하게 사라지는가

### A-5. 진행
- [ ] 키 획득 카운트가 양쪽 UI에 반영되는가
- [ ] **게스트가 가시에 닿아 죽었을 때 정상 재시작되는가** ← **문제 C (최우선)**
- [ ] 호스트가 죽었을 때 정상 재시작되는가
- [ ] 둘 다 문에 들어가야 클리어되는가
- [ ] 한 명이 문 안에서 접속을 끊어도 클리어되지 않는가 ← **문제 I**
- [ ] 스테이지 전환 후 양쪽이 올바른 스폰 지점에 서는가 ← **문제 H**
- [ ] 스테이지 전환 후 캐릭터 색이 유지되는가

---

## 부록 B. 참조 위치 색인

| 문제 | 파일 | 위치 |
|---|---|---|
| A | `Assets/Prefabs/InGameObject/Ball.prefab` | NT `syncDirection: 1` |
| A | `Assets/Prefabs/InGameObject/MoveTileHorizontal.prefab` | NT `syncDirection: 1` |
| A | `Assets/Scripts/Player/InputPlayer.cs` | `:536-579` |
| A | `Assets/Scripts/Gimmick/BoomBox.cs` | `:156-177` |
| B | `Assets/Scripts/Player/InputPlayer.cs` | `:130-162` |
| B | `Assets/Prefabs/Player/Player_White.prefab` | Rigidbody2D `bodyType: 0` |
| C | `Assets/Scripts/Mechanics/Obstacle.cs` | `:44-65` |
| C | `Assets/Scripts/Mechanics/Monster.cs` | `:121-153` |
| C | `Assets/Scripts/LevelLoader.cs` | `:65-82` |
| D | `Assets/Scripts/Mechanics/*.cs` | `[ServerCallback] OnTriggerEnter2D` 전반 |
| E | `Assets/Scripts/Player/InputPlayer.cs` | `:333-338`, `:653-687` |
| F | `Assets/Prefabs/Player/Player_*.prefab` | NT 4개 |
| G | `Assets/Scripts/Player/Grab.cs` | `:49-56` |
| G | `Assets/Scripts/PlayerEyes.cs` | `:50`, `:107-111` |
| H | `Assets/Scripts/P2P/PushPullNetworkManager.cs` | `:124-146` |
| I | `Assets/Scripts/Mechanics/Door.cs` | `:15`, `:44`, `:76-83` |
| J | `Assets/Scripts/Player/InputPlayer.cs` | `:552-557` |
| K | `Assets/Scripts/Player/PlayerMovement.cs` | `:55`, `:171` |
| 중복 | `PauseController.cs` `:10`, `:161` ↔ `SteamLobby.cs` `:380` | 씬 경로·종료 순서 |
