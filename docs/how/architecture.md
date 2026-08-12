# 架构 — 目标实现

## Implements

行为合同见 `what/architecture.md`；本文件只描述事件收敛、结构化执行、Horizon 滤镜、Gates 与包面装配。

## Ownership

依赖方向和源码边界见 `shape/architecture.md`。

---

## 事件与 reconcile

1. 适配层丢弃碎片事件；仅 `idle` / `retry` / `deleted` 进入 single-flight。  
2. Reconciler 只读 SDK 完整 snapshot，产出 `TurnOutcome` 等 typed 结果。  
3. 业务策略禁止依赖 event 先后顺序或 payload 形状。

---

## 控制流与并发

1. 业务流程：F# CE（`let!` / `match` / 有界递归）直接执行。  
2. 参考入口：`Session/*Program.fs`、`Application/Reconciliation/*Workflow.fs`。  
3. 扇出：仅 `mapBounded`；`maxConcurrency` 正有限；失败归还许可；结果按下标排列。  
4. 禁止业务 Program AST + Interpreter（FLOW / dsl-ownership）。

---

## 前缀与包面

1. 平常只增 Y frames；X active prefix 字节不变。  
2. PrefixEpoch 切换只绑 probe 提升 / ContextReanchored；BlogSquash 只推进 FrameEpoch。
3. 入口 `dist/Infrastructure/OpenCode/Plugin/Plugin.js`。  
4. 资源：`resources/provider/`（Common Law / Role Law / Library）、`resources/enforcer/<TipName>/{enforcer.md,main.md}`；加载仅 `Infrastructure/Resources/`，fail fast。**无** `catalog.json` SSOT；**无** `resources/prompts/*`。

---

## 合成文本

统一 string owner + renderer；inventory 与 golden 守 ARCH-010（`how/synthetic-toml.md`）。  
Join/fork/commission payload 字段名：`commissioner_record`、`root_requirement`；禁止 Join status plane（`status`/`count`/`ordinal`/`kind`/`agent`/…）进 provider data。

---

## Provider Horizon 滤镜算法（ARCH-014）

每个即将离开墙、进入 provider surface 的字段，按固定决策滤镜（what ARCH-014）：

```text
Did the participant already know this?        → omit
Did they just supply this themselves?          → omit
Is it implied by successful completion?        → omit
Is it useful only for correlation/debug?       → keep internal
Would different values change next action?     → if no → omit
Does the participant need the value itself
  rather than merely its consequence?          → if no → render consequence
                                                → if yes → preserve minimal observation
```

实现落点：各域 tool renderer / JoinResultRenderer / horizon / commission 成功后果。  
机器可尽知 Journal/CAS/cursor/UUID；穿过 horizon 的只能是后果、measurement 与 WorkRecord。  
禁止：`status`/`code`/`error` DTO、SessionId/AgentId/JobId、fallback offset、`fast-`/`deep-` 自称、已删 spool 路径。

---

## Gates A–E 检查算法（ARCH-016）

静态/契约门禁（可失败；不是业务状态机字段）：

| Gate | how 检查 |
|------|----------|
| A Tool Referential Integrity | 同名工具 → 唯一 schema owner + 唯一 semantic contract；不同硬语义不得同名（`commission`≠`fork`；`judge`≠旧 `verdict` 工具名） |
| B Provider Leak | 扫描 provider 输出 / schema / fixed prose：禁 SessionId/AgentId/ManagerJobId/PtyId/Fission/lane/worktree/offset/`fast-`·`deep-`/spool |
| C Language Parity | 每个 provider semantic resource：EN 与 zh-CN 皆存在（HOST-026）；缺语言 fail。现行：叶对成对 + `{{placeholder}}` 集合一致 + Role Law semantic-anchor 同 ID 双语命中（PROMPT-019） |
| D Prompt Stability | 同 session 上 Fallback / T1 / review / reanchor / Strength 后 system prompt 字节相同；只允许改 EffectiveAgent |
| E Provider Prose Ownership | 见下节算法 |

### Gate C — Role Law semantic-anchor（PROMPT-019）

```text
catalog = scripts/checks/semantic-anchors.mjs
  role → [{ id, en regex, zh regex }]

every resources/provider/role/<role>/ directory with locale leaves
  must appear in catalog

for each catalog role:
  en.md must match every id's en regex
  zh-CN.md must match the same id's zh regex
```

与 `prompt-depth-ratchet` 共用 `ROLE_ANCHOR_DIRS`。Word-count 不是质量；缺锚点才红。

### Gate E — Provider Prose Ownership（PROMPT-019）

```text
scan allowlisted production modules:
  Domain/*Prompt.fs
  Domain/*Narrative.fs
  Domain/RuntimeNudge.fs   （及同命名族）
  Domain/JsDescription.fs  （及同命名族）
  Infrastructure/OpenCode/Tools/*   （provider-facing）

detect natural-language string literals

allow (do not count as prose debt):
  resource paths / technical ids（Class B）
  {{placeholders}}
  explicitly annotated diagnostics（Class C）

compare per-file NL literal counts → baseline
  count > baseline[file]  → fail（regression）
  count ≤ baseline[file]  → ok；baseline 只许收缩，不许膨胀
```

证明面见 `proof/` 与 Proposal §17；各域不得以局部方便绕过。

---

## Student/Teacher — G3 已删除（absent）

G3 clean-break：`Role.Student` / `Role.Teacher`、Learn/Compile、`StudentQaStore`、SKILL 制品门与
`StudentTeacherRuntime` **不存在于生产**（AGENT-020…022 空缺；`scripts/checks/student-teacher-absence.mjs`）。
不得写成 pending / 过渡 / 双写。后继同步委派：ordinary completion → bounded WorkRecord（EXEC-028/031）；**无**独立 `return` 工具 / 双 await。

`SatelliteKind` 现仅 `Companion`。`AttachedSessionRuntime` 拥有 Attached 创建/恢复/retire；
kind-specific 只提供 Agent、首个 Prompt 与 terminal handler。Prompt 文本是模型指令，绝不反向解析成
控制流（ARCH-011）。