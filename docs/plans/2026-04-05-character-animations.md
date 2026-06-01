# Character Animation System Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Replace capsule player with animated character model — Idle, Walk, and tool-specific action animations driven by game state.

**Architecture:** Animator Controller with two layers: Base (locomotion: Idle/Walk blend by Speed) and Actions (tool-use animations triggered by ToolAction int parameter). PlayerController drives Speed, FarmInteractionController triggers actions on tool use. All animation FBX files share avatar from Char_rig.fbx (Generic rig).

**Tech Stack:** Unity Animator (Generic rig), Mixamo skeleton, R3 ReactiveProperty for tool state observation.

---

## Animation Mapping

| FBX File | Animator State | Trigger | Loop |
|----------|---------------|---------|------|
| Idle.fbx | Idle | Speed < 0.1 | Yes |
| holding walk.fbx | Walk | Speed > 0.1 | Yes |
| watering.fbx | Water | ToolAction = 1 | No |
| dig and plant seeds.fbx | Plant | ToolAction = 2 | No |
| pick fruit.fbx | Harvest | ToolAction = 3 | No |
| pull plant.fbx | Uproot | ToolAction = 4 | No |
| plant tree.fbx | PlantTree | ToolAction = 5 | No |
| kneeling idle.fbx | KneelingWork | ToolAction = 6 | No |

ToolAction mapping to ToolId:
- WateringCan → 1 (Water)
- Planter (crops) → 2 (Plant)
- Sickle → 3 (Harvest)
- Uproot → 4 (Uproot)
- Planter (trees) → 5 (PlantTree)
- Fertilizer, Dye → 6 (KneelingWork)
- Bulldoze, Placement (buildings) → 2 (Plant — generic ground-work)

---

### Task 1: Configure FBX Import Settings

**Files:**
- Modify: `Assets/Project/Models/ai/anim/*.fbx.meta` (all animation FBX files)

Configure all animation FBX files to:
1. animationType: 3 (Generic)
2. avatarSetup: 2 (Copy From Other Avatar)
3. sourceAvatarPath: Assets/Project/Models/ai/Char_rig.fbx
4. Set clipAnimations with loopTime=1 for Idle and holding walk, loopTime=0 for actions
5. Set proper clip names matching state names

For each FBX meta file, replace `clipAnimations: []` with proper clip definition and set animationType/avatarSetup.

**Loop animations:** Idle.fbx, holding walk.fbx, kneeling idle.fbx
**One-shot animations:** watering.fbx, dig and plant seeds.fbx, pick fruit.fbx, pull plant.fbx, plant tree.fbx

---

### Task 2: Expand Animator Controller

**Files:**
- Modify: `Assets/Project/Animations/FarmerAnimator.controller` (via Unity MCP manage_animation)

**Parameters:**
- Speed (float) — already exists
- ToolAction (int) — 0=none, 1-6=action types
- IsActing (bool) — true during action playback

**Base Layer States:**
- Idle (default) — Idle.fbx, loop
- Walk — holding walk.fbx, loop

**Base Layer Transitions:**
- Idle → Walk: Speed > 0.1, no exit time, duration 0.15
- Walk → Idle: Speed < 0.1, no exit time, duration 0.15

**Action Layer (override, weight 1.0):**
- Empty (default) — no animation (base layer plays through)
- Water — watering.fbx, exit time → Empty
- Plant — dig and plant seeds.fbx, exit time → Empty
- Harvest — pick fruit.fbx, exit time → Empty
- Uproot — pull plant.fbx, exit time → Empty
- PlantTree — plant tree.fbx, exit time → Empty
- KneelingWork — kneeling idle.fbx, exit time → Empty

**Action Layer Transitions:**
- Empty → [each action]: ToolAction == N, no exit time, duration 0.1
- [each action] → Empty: has exit time (end of clip), set IsActing=false via StateMachineBehaviour or transition condition

---

### Task 3: Create PlayerAnimationController script

**Files:**
- Create: `Assets/Scripts/Player/PlayerAnimationController.cs`

This MonoBehaviour sits on the FarmerModel child object (next to Animator). It:
1. Caches Animator reference
2. Exposes `SetSpeed(float)` for PlayerController
3. Exposes `PlayAction(int actionId)` for FarmInteractionController
4. Listens for action clip end to reset ToolAction to 0
5. Exposes `IsActing` property to block tool re-use during animation

```csharp
using UnityEngine;

namespace WheatFarm.Player
{
    public class PlayerAnimationController : MonoBehaviour
    {
        private Animator _animator;

        private static readonly int SpeedHash = Animator.StringToHash("Speed");
        private static readonly int ToolActionHash = Animator.StringToHash("ToolAction");

        public bool IsActing { get; private set; }

        private void Awake()
        {
            _animator = GetComponent<Animator>();
        }

        public void SetSpeed(float speed)
        {
            if (_animator != null)
                _animator.SetFloat(SpeedHash, speed);
        }

        public void PlayAction(int actionId)
        {
            if (_animator == null || IsActing) return;
            IsActing = true;
            _animator.SetInteger(ToolActionHash, actionId);
        }

        // Called by animation event or checked in Update
        public void OnActionComplete()
        {
            IsActing = false;
            if (_animator != null)
                _animator.SetInteger(ToolActionHash, 0);
        }

        private void Update()
        {
            // Auto-detect action completion: if action layer is in Empty state, reset
            if (IsActing && _animator != null)
            {
                var stateInfo = _animator.GetCurrentAnimatorStateInfo(1); // Action layer
                if (stateInfo.IsName("Empty") || stateInfo.normalizedTime >= 0.95f)
                {
                    OnActionComplete();
                }
            }
        }
    }
}
```

---

### Task 4: Update PlayerController

**Files:**
- Modify: `Assets/Scripts/Player/PlayerController.cs`

Remove direct Animator reference. Use PlayerAnimationController instead.

Changes:
1. Replace `_animator` field with `PlayerAnimationController _animController`
2. Find via `GetComponentInChildren<PlayerAnimationController>()`
3. Call `_animController.SetSpeed()` instead of `_animator.SetFloat()`
4. Remove SpeedHash static field (moved to PlayerAnimationController)

---

### Task 5: Update FarmInteractionController to trigger animations

**Files:**
- Modify: `Assets/Scripts/Player/FarmInteractionController.cs`

Changes:
1. Add `PlayerAnimationController _animController` field
2. Find via `GetComponentInChildren<PlayerAnimationController>()` in Start
3. In HandleToolUse, before calling UseCurrentTool, call `_animController.PlayAction(GetActionId(toolId))`
4. Add mapping method `GetActionId(ToolId) → int`
5. Optionally: block tool use while IsActing

ToolId → ActionId mapping:
```
WateringCan → 1
Planter → 2 (or 5 for trees, need to check PlacementTool state)
Sickle → 3
Uproot → 4
Fertilizer → 6
Dye → 6
Bulldoze → 2
Build → 2
Placement → 2
```

---

### Task 6: Verify in Play Mode

1. Enter Play Mode
2. Character shows Idle animation
3. WASD movement → Walk animation blends in
4. Select WateringCan → Click ground → Watering animation plays
5. Select Planter → Click ground → Planting animation plays
6. Select Sickle → Click crop → Harvest animation plays
7. Tool use blocked during animation playback
