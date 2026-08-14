# Projection — 边界

## PROJ-004：三层结构

实现必须分为：

```text
Effectful Coordinator
    读取 Host、等待结果、生成不可变快照

Pure Projection Planner
    汇总各功能 ProjectionIntent、排序、冲突检查

Canonical Renderer
    渲染最终 provider wire bytes、生成 digest/seal
```

## PROJ-005：ProjectionIntent

功能模块只能声明以下形态的 intent：

```text
keepPhysicalPrefix
activatePrefixEpoch
insertBlogFrames
insertRepair
useStrengthMirror
insertStrengthFrames
suppressTransportOnly
appendReviewChallenge
reanchorAfterCompaction
```

HOST-013 pair-programming marker 不占 intent：wire 级 DSL 消息无 transcript 地址，无法做
anchored 渲染；由 `PairProgrammingThoughtTransform` 在 raw 域按 durable gap anchor
replay（见 `how/host.md` HOST-013）。

`UseStrengthMirror` 是 StrengthReplica-only base selection，与 `keepPhysicalPrefix` / `activatePrefixEpoch` 互斥；`InsertStrengthFrames` 通过显式 visibility/anchor 表达 Candidate、Promoted replay 或 Replica-local batch（STRENGTH-009/016），renderer 不猜来源。

禁止任何业务功能直接接收和修改 `Message list`。

## PROJ-006：合并与冲突

不同 intent 修改同一锚点时：

* 有明确定义的合并律；或
* 返回 `ProjectionConflict`；
* 不允许依赖注册顺序。

Strength 额外要求：同 DecisionId+同 digest 幂等；同 anchor/DecisionId 不同 payload → `ProjectionConflict`；Candidate wrong-target render → `ProjectionConflict`（STRENGTH-006/009）。
