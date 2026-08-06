# 架构 — 证明

行为见 `what/architecture.md`，边界见 `shape/architecture.md`，实现要点见 `how/architecture.md`。

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
