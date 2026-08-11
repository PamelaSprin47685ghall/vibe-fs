# 架构 — 证明

行为见 `what/architecture.md`，边界见 `shape/architecture.md`，实现要点见 `how/architecture.md`。

## Student / Teacher absence

| 证明 | 条款 |
|------|------|
| `scripts/checks/student-teacher-absence.mjs` 证明生产源码无 Student/Teacher Agent、Role、request kind、tool、Satellite kind 与 QA runtime | ARCH-013、HOST-014、AGENT-020、PROMPT-012 |
| unified-store gate 的 `student-qa-revival` fixture 必须能红，生产扫描保持绿；禁止隐藏 QA storage/feature ref 复活 | ARCH-013、PERSIST-007 |
| SyncInspector/SyncCoder 只走 Work+Attached 与 EXEC Returned→Completion，不存在 legacy Student/Teacher fallthrough | ARCH-013、HOST-008、EXEC-026/028 |
| Host 仍只用公开 hook/SDK；Student/Teacher absence 不以 Host patch 或 alias 实现 | ARCH-003、ARCH-013 |

## 层 0（无产物即可跑）

| 检查 | 命令 / 位置 | 守住的条款 |
|------|-------------|------------|
| 条款唯一与引用 | `scripts/checks/spec.mjs`（经 `npm run lint`） | GOV-005；全文 `## ID` 定义 |
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
