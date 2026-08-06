# 架构 — 目标实现

## 需求意图与范围（A2 需求意图）

### 1. 问题陈述
万象术（Wanxiangshu）插件需要处理高并发、多 Agent 协作、上下文溢出与硬件/网络故障。传统基于状态机表、隐式字符串协议或全局可变状态的架构极易陷入竞态条件、非法状态拼接与过度复杂的解释器开销。架构子模块旨在使用直接执行的结构化程序（F# CE）、信号/数据分离、零修改 Host 本体（ARCH-003）以及绝对强类型的 Fail-Closed 边界，构建高效且明显正确的系统基础设施。

### 2. 输入输出与规则边界
- **输入**：OpenCode 原始事件、SDK 消息快照、Journal 持久化事实。
- **输出**：强类型领域信号、结构化 Workflows 执行结局、受控的并发执行结果。
- **核心边界与不变量**：
  1. 结构化程序替代状态机（ARCH-001）：业务流程只用 `let!/do!/use!/match/尾递归`，绝对禁止定义程序计数器、Stage、Phase、Lease、Owner 或 Generation 字段；禁止第二运行时或 Interpreter。
  2. 事件是信号不是数据（ARCH-002）：流式碎片在最早边界丢弃；只有 `session.status=idle/retry`、`session.deleted` 能驱动业务层。
  3. 零修改 OpenCode 本体（ARCH-003）：只使用 OpenCode 现有 Hook 与 SDK API。
  4. 受限并发扇出：仅使用 `mapBounded` 且 `maxConcurrency` 为正有限整数，失败路径必须释放许可，结果严格按下标排列。

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
