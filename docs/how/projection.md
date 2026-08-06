# Projection — 目标实现

行为见 `what/projection.md`；边界与意图见 `shape/projection.md`。  
接管 `COMPANION-007` 与 `VERIFY-007`：Semantic 投影是 canonical digest 唯一来源，Wire 带 ID 字节相等。

## 装配：三层

一次 provider request 的投影按 shape/projection.md 三层实现：

```text
Effectful Coordinator
    Host 读取 → 完整 snapshot（PROJ-002 的 ProjectionSnapshot）→ 不可变，交 Planner

Pure Projection Planner
    汇总各功能 ProjectionIntent（PROJ-005）→ 排序 → 冲突检查（PROJ-006）→ 产出有序 intent 序列

Canonical Renderer
    逐 intent 落 ProjectionSnapshot 的语义树 → 渲染 provider wire bytes → 生成 digest/seal
```

禁令（PROJ-001/007）落点：功能模块只声明 intent，不得直接接收/改写 `Message list`；渲染器不驱动生命周期。

## Intent 排序与冲突（PROJ-005/006）

intent 排序锚点固定为「当前被投影前缀锚」。合并律先于实现固定，禁止依赖注册顺序：

```text
keepPhysicalPrefix        // 无 X 恢复时兜底：物理前缀原样
activatePrefixEpoch       // X probe 已提交 或 reanchor 后 Snapshot 生效
insertBlogFrames          // Y 有效帧（Entry/Squash）插入历史槽
insertRepair              // Interaction Repair 回合
suppressTransportOnly     // transport-only 消息剔除（COMPANION-012）
appendReviewChallenge     // skeptical challenge
reanchorAfterCompaction   // ContextReanchored → Snapshot=None
```

同锚冲突无明确定义合并律 → 返回 `ProjectionConflict`，fail closed；不静默选边。

## 语义树与两投影

快照落到 `SemanticEventTree`，再两选一投影（VERIFY-007，不同型，禁止隐式互转）：

```text
ProviderSemanticProjection    // 去 ID：语义等价、跨会话可比较、canonical digest 唯一来源（COMPANION-007）
ProviderWireProjection        // 含 ID、字节相等、本地时间线、seal 与前缀缓存用
```

实现边界：

- Semantic：过滤 transport-only 字段（timestamp/cost/usage/runtimeId…，COMPANION-012），只留进模型的字节相关部分；TEXT 序列化顺序固定，供 digest。
- Wire：在 Semantic 之上补合成 identity（COMPANION-013：SealRoot/frameEpoch/ordinal 确定性派生，禁 GUID/random/时间）与本地时间线序号。

禁止：把 Wire 反解析回 Semantic 当 digest（COMPANION-007）；用 wireshape 判语义等价。

## Canonical digest

```text
CanonicalDigest = SHA-256( 规范序列化(ProviderSemanticProjection(tree)) )
```

- 规范序列化：固定字段序、UTF-8、无空白歧义；同语义对话跨 ID 产出同一 digest。
- 使用点：CoveredPrefixDigest（COMPANION-011 重算失配 fail closed）、canary 的 `LegacyDigest = DslDigest` 切换判据、Review 双 PERFECT。

## Seal 生成点

Wire 渲染收敛后、首字节外发前收敛 `ProviderInputSeal`：

```text
耗时最长、边界最窄处生成；
ProviderRunIdentity 由排查到唯一未完成 assistant 顶（HOST-010）；
seal 一经发出不可变（COMPANION-009 字节级 sealed 屏障）。
```

## 接线

| 来源 | 落点 |
|------|------|
| X prefix probe 候选 | `AttemptExecutionProfile.ProjectionChoice` → `activatePrefixEpoch`（how/context.md CTX-010） |
| Y frames | insert during `BloggerMain`/`BloggerSquash`（how/companion.md） |
| LWR / delta | Semantic 源同 XTrace、不同投影（COMPANION-007；delta 见 how/context.md CTX-013） |
| reanchor | transform 观察到 compaction → `reanchorAfterCompaction`（how/host.md HOST-006） |
| challenge / seal | how/review.md REVIEW-003/004/006 |

## PROJ-008：迁移顺序

旧投影必须按以下顺序迁移到 DSL：

1. 普通 X + ActivePrefixEpoch projection
2. attempt-local PrefixProbe projection
3. Companion BloggerMain / BloggerSquash / BloggerDelta projection
4. InteractionRepair projection
5. ReviewConfirmation + skeptical challenge Seal projection
6. Host compaction reanchor 后 projection
7. Strength Primary/Replica projection（含 transport-only suppression）

迁移期间测试环境可同时运行 LegacyProjection 和 DslProjection 并比较 canonical digest；生产环境不得按请求随机混用两套实现。

切换条件：所有历史 canary 轨迹 `LegacyDigest = DslDigest`；允许有意变化的差异须有明确的新 SSOT 条款。

切换后删除 `LegacyProjection`，不长期维护双实现。

---

## 历史说明

`docs/proposal/strength.md` 第 19 节把 Projection DSL 称为「spec/13 — Projection Algebra（条款前缀 `PROJ-`）」。本文件生效后，该引用 superseded 为 `docs/what/projection.md`（条款前缀 `PROJ-`）。
