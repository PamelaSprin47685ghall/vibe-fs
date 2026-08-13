# 架构 — 证明

行为见 `what/architecture.md`，边界见 `shape/architecture.md`，实现要点见 `how/architecture.md`。

## Gates A–F 证明义务（ARCH-016；§17.2 / §19.21–24）

静态/契约门禁；可失败；各域不得以局部方便绕过。算法见 `how/architecture.md` ARCH-016。

| Gate | 不变量 | 证明义务 | 域指针 |
|------|--------|----------|--------|
| A Tool Referential Integrity | 同名工具 → 唯一 schema owner + 唯一 semantic contract；异硬语义不得同名 | 静态扫描 / capability isomorphism；同名双合同红 | ARCH-007；工具 rename 面见 execution/agent proof |
| B Provider Leak | provider 输出禁 SessionId / AgentId / ManagerJobId / PtyId / FissionGroupId / lane / worktree / fallback offset / `fast-`·`deep-` / spool | 扫描 schema / fixed prose / join·horizon 后果；泄漏必红 | EXEC-030；projection / join proof |
| C Language Parity | 每个 provider semantic resource：EN + zh-CN 皆存在；叶对 + `{{placeholder}}` 集合一致；Role Law 与高风险 tool description semantic-anchor 同 ID 双语命中（PROMPT-019/020） | 资源成对存在；缺语言 fail；invariant 标识不译；占位符集合不一致红；Role Law 或缺 tool 描述锚点红 | HOST-026、PROMPT-017/019/020；`proof/host.md`、`proof/prompt.md` |
| D Prompt Stability | 同 session：Fallback / T1 / review / reanchor / Strength → system prompt 字节相同；只允许改 EffectiveAgent | before/after 字节相等；Persona / SessionProviderLanguage 不重绑 | PROMPT-014、FALLBACK-014、AGENT-029；`proof/prompt.md` |
| E Provider Prose Ownership | 已知 provider-surface owner 不得新增 NL literal；baseline ratchet 只减不增 | `scripts/checks/provider-prose-ownership.mjs` + `tests/unit/verify/provider-prose-ownership.test.mjs`；per-file 计数 > baseline → 红 | PROMPT-019；`proof/prompt.md`、`proof/verify.md` |
| F Office Capability Integrity | 五 Office entitled consequence 在 Manager Role Law 与 `fork` description 等同 ID 命中 | `semantic-anchors.mjs` OFFICE_CAPABILITY_ANCHORS；缺投影或把 Office 写成可互换 agent → 红 | ARCH-017、PROMPT-021 |

§17.1 语义不变量中与本门相关：`EN/ZH covers all provider prose`；`Technical identifiers stay same in both languages`；`system prompt before T1 == after T1`；`provider-surface NL literals do not grow`。

## Student / Teacher absence

| 证明 | 条款 |
|------|------|
| `scripts/checks/student-teacher-absence.mjs` 证明生产源码无 Student/Teacher Agent、Role、request kind、tool、Satellite kind 与 QA runtime | ARCH-013、HOST-014、AGENT-020、PROMPT-012 |
| unified-store gate 的 `student-qa-revival` fixture 必须能红，生产扫描保持绿；禁止隐藏 QA storage/feature ref 复活 | ARCH-013、PERSIST-007 |
| SyncInspector/SyncCoder 只走 Work+Attached 与 EXEC Returned→Completion，不存在 legacy Student/Teacher fallthrough | ARCH-013、HOST-008、EXEC-026/028 |
| Host 仍只用公开 hook/SDK；Student/Teacher absence 不以 Host patch 或 alias 实现 | ARCH-003、ARCH-013 |

## G9 ratchets（Playbook §24）

Playbook §24 四条 ratchet 由已接线 gate 分别钉死，不是新 ARCH Clause。`scripts/check.mjs` 已纳入下表静态门。**G9 Product Exit DONE**（2026-08-12 Amendment：问卷八 kind 穷尽 + 无 special pleading + 兄弟 ratchet 绿 = ownership Exit；不另造 mega-gate）。

| §24 | 义务 | 现有载体 | 钉死程度 |
|------|------|----------|----------|
| 24.1 Symbol | 生产无 Role.Student/Teacher、fast/deep-student/teacher、StudentLearn/Compile/QaStore/StudentTeacherRuntime/Tools/StudentSkill、SatelliteKind.Teacher/Replica | `scripts/checks/student-teacher-absence.mjs` + `tests/unit/verify/student-teacher-absence.test.mjs` | 已钉（token 集 + scanEntries） |
| 24.1 / 24.2 Storage | 无 feature-owned `refs/wanxiang/*`、无 Casebook custom ref、无 legacy Journal/Blob reader、无 dual-write | `scripts/checks/unified-store-gate.mjs` + `tests/unit/verify/unified-store-gate.test.mjs` | 已钉 |
| 24.3 Capability | Agent×RequestKind×AttemptExecutionProfile → capability/SDK/description/example/alias/runtime 五层同构 | `scripts/checks/capability-isomorphism-gate.mjs` + `tests/unit/verify/capability-isomorphism-gate.test.mjs` | 静态同构已钉（G9 Exit 载体） |
| 24.4 Session ownership | Companion / SyncInspector / SyncCoder / Bookkeeper / hidden Reviewer / StrengthReplica / fork agent / Executor child 问卷 | `scripts/checks/session-ownership-ratchet.mjs` + `session-ownership-matrix.json` + `tests/unit/verify/session-ownership-ratchet.test.mjs` | 八 kind 穷尽 + 无 special pleading = G9 ownership Exit |
| JS surface / G3 rebase | 无 js-student/js-teacher、无手写 per-role js-* | `scripts/checks/js-surface-gate.mjs` | 已钉 |

G5 Amendment C-3：builtin `read`/`edit`/`write`/`glob`/`grep`/`patch` 保留，与 js-ROLE 共存。§24.1「legacy five tool implementation absent」已被 C-3 取代，本证明不要求删除 builtin。

本表各门绿 = G9 Product Exit（2026-08-12 Amendment）。

## 层 0（无产物即可跑）

| 检查 | 命令 / 位置 | 守住的条款 |
|------|-------------|------------|
| 条款唯一与引用 | `scripts/checks/spec.mjs`（经 `npm run format-build-test`） | GOV-005；全文 `## ID` 定义 |
| 源码根 / fsproj / 分层 | `scripts/checks/architecture.mjs` | ARCH-001 分层；资源读取位置；无旧路径 |
| DSL 所有权 | `scripts/checks/dsl-ownership.mjs`（threshold=0） | ARCH-001、FLOW-001/006 |

## 层 1–3（`dist` + unit/integration）

| 性质 | 测试落点（代表） | 条款 |
|------|------------------|------|
| 有界并发 | `tests/unit/kernel/parallel.test.mjs` | ARCH-009 |
| 事件/信号边界 | plugin host-hooks、reconcile 相关 unit | ARCH-002、HOST-001/002 |
| 前缀 / seal | context / review unit | ARCH-004、COMPANION-009 |
| 合成 TOML / 状态先于表示 | synthetic-toml unit、arch010 harness | ARCH-010、ARCH-011 |
| Tool 文本结果边界 | `tests/unit/context/tool-result-bound.test.mjs` | ARCH-012 |
| Host 不改本体 | 仅挂现有 hook；无 Host patch 路径 | ARCH-003 |

## 失败形态（门禁必须能红）

- 业务层出现第二运行时 / 程序计数器 → dsl-ownership 红  
- Domain 引用上层 OpenCode 命名空间 → architecture 红  
- 资源读取散落在 `Infrastructure/Resources/` 外 → architecture 红  
- 条款重复定义或悬空引用 → spec-check 红  

## 与 VERIFY 的关系

晋级与 canary 纪律见 `proof/verify.md`（VERIFY-001…008）。本文件只列**架构 DNA** 的证明面，不重复 canary 剧本规则。
