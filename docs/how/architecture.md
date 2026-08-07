# 架构 — 目标实现

## Implements

行为合同见 `what/architecture.md`；本文件只描述事件收敛、结构化执行和包面装配。

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
4. 资源：`resources/prompts/*`、`resources/enforcer/catalog.json`；加载仅 `Infrastructure/Resources/`，fail fast。

---

## 合成文本

统一 string owner + renderer；inventory 与 golden 守 ARCH-010（`how/synthetic-toml.md`）。

---

## Student/Teacher 边界

`StudentQaStore` 不解析正文，只做 bytes 校验、换行拼接、尾部去重和原子 replace。`StudentRun` 的 typed
request kind 决定 Learn/Compile；idle reconcile 只读 typed profile 和 Satellite return waiter，不读
assistant 正文推测状态。Prompt 文本是模型指令，绝不反向解析成控制流（ARCH-011/013）。

Student provider view 仍属于 Work Session，但 `CompanionTransform` 对 Student 视图直接裁成无 material：
不执行 Companion decode、不写 XTrace、不创建 Blogger child。Learn → Compile 保持同一 message prefix，
只以 typed AttemptExecutionProfile 原子替换 tools；canary 将该位置声明为 request-kind 冷边界。

SatelliteRuntime 以 `(owner SessionId, SatelliteKind)` 为唯一键，Companion 与 Teacher 共享 create/query/
link/abort/retire 程序；kind-specific 代码只提供 Agent、首个 Prompt 与 terminal handler。
