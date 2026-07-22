# 01-architecture.md: 系统前瞻架构规范

> **状态声明**：本文定义 Universal TOML 重构与 KISS 理念融合后的**目标架构（Target Architecture）**。源码现存的 Nudge 文本分类、Fallback 扫描与 Actor 复杂层属于过渡现状（Current Status），非永久架构。

---

## 1. 分层架构与依赖方向

系统划分为三层，遵循严格的单向依赖规则：`Host -> Runtime -> Kernel`。

```mermaid
+-----------------------------------------------------------------------+
| Host (OpenCode / OMP / CLI / Mux)                                     |
|  - 宿主特定 DTO, Protobuf, PTY, Subprocess, CLI 交互                  |
+-----------------------------------------------------------------------+
                                   | (调用 & 桥接)
                                   v
+-----------------------------------------------------------------------+
| Runtime (Side-Effect Shell)                                           |
|  - IO / Persistence (Per-Runtime Journal), Driver, MessageTransform   |
|  - Prompt Renderer (PromptDocument -> smol-toml -> string)            |
|  - Typed Metadata & Event Sourcing                                    |
+-----------------------------------------------------------------------+
                                   | (使用领域类型 & 纯逻辑)
                                   v
+-----------------------------------------------------------------------+
| Kernel (Pure Kernel)                                                  |
|  - Pure Domain Model (PromptDocument, ToolOutput, Event, State)       |
|  - Flow 控流与业务算子，零 IO，零 FFI，零文本 Parse/Format            |
+-----------------------------------------------------------------------+
```

### 1.1 三层职责与可验证规则

| 层级 | 职责范围 | 输入/输出类型 | 禁区与验证规则 `[NORMATIVE]` |
| :--- | :--- | :--- | :--- |
| **Host** | 宿主 Protocol 对接、终端 PTY、CLI 参数解析、进程生命周期 | 宿主 DTO / Raw JSON / CLI Args | **禁止**包含领域业务逻辑；只做协议转换，转为 Typed Transport 传给 Runtime。 |
| **Runtime** | 副作用外壳 (Shell)、Journal 持久化、Driver 调度、MessageTransform、Prompt 渲染 | DTO <-> Domain Value <-> TOML/Text | **禁止** Kernel 业务状态逃逸；唯一允许 `smol-toml` FFI 导入层；负责写操作与外部 IO。 |
| **Kernel** | 纯业务内核，Domain DU/Record 模型定义，Flow 执行逻辑 | Pure F# Types (`PromptDocument`, `Event`, `State`) | **纯逻辑**：零 Fable FFI，零 npm 依赖，零 IO/Disk/Network，零文本格式化/解析。 |

---

## 2. 核心设计原则

### 2.1 Pure Kernel 与 Side-Effect Shell
- **Kernel** 永远是纯函数与不可变数据代数。Kernel 不持有线程、句柄、文件描述符或定时器。
- 所有与操作系统、网络、宿主 API 的交互一律在 **Runtime/Host** 壳层完成。

### 2.2 Typed Transport (强类型传输)
- 系统内部模块间通信全过程使用 F# DU/Record（如 `PromptDocument`、`ToolOutputMessage`、`WanEvent`）。
- **禁止**以弱类型 `string` 或任意 `JSON/obj` 在内部充当隐式契约或 SSOT。

### 2.3 MessageTransform 与 Protocol 隔离
- 模型与宿主特定格式（OpenCode JSON、OMP Message、Protobuf）只在 **Runtime/Adapter** 层进行双向 MessageTransform。
- **Kernel** 完全隔离于宿主协议细节，不包含任何 OpenCode/OMP/Protobuf 结构体或字段依赖。

### 2.4 Prompt Renderer (单向视图渲染)
- Prompt 构造在 **Kernel** 中表达为强类型 `PromptDocument` 领域模型（包含不可约 7 原语）。
- Prompt 渲染由 **Runtime** 模块调用 `smol-toml.stringify` 统一生成 TOML 字符串。
- **No Self-Parsing Invariant**：生产代码禁止解析自己生成的 TOML 或 Prompt 字符串来恢复状态。

### 2.5 事件与 Metadata SSOT
- **SSOT**：持久化 `.wanxiangshu.ndjson` 事实日志与 typed metadata（如 `metadata.wanxiangshu`）。
- **Derived View**：渲染给 LLM 的 TOML、Markdown、Prose 均属于只读派生视图。
- 机器 provenance（如 `prompt_origin`、`loop_id`、`round`）必须存在于 typed metadata/event 中，绝对禁止塞入模型 Prompt 文本中反向扫描。

---

## 3. 目标设计：KISS 架构演进 `[TARGET]`

> **注意**：本节为目标架构（Target Architecture），当前源码仍处于分阶段重构中。

### 3.1 Per-Runtime 生命周期隔离
- 每个 Runtime 实例拥有独占的进程上下文与专属 Journal 日志文件（`Per-Runtime Journal`）。
- 多 Runtime 并行运行时，彼此生命周期与内存状态隔离，不依赖跨进程共享内存锁，通过按 ObservedAt 时间序确定性归并重放（Chronological Replay）收敛状态。

### 3.2 单 Driver 简化流控
- 消除旧系统为了补救复杂控制流而引入的 Nudge Prose 扫描、Fallback 轮询及 Actor 复杂死循环。
- 业务逻辑收敛为结构化 Driver Flow：一个 Session 由单一 Driver FIFO 处理事件与状态推进。

---

## 4. 禁止项 `[FORBIDDEN]`

1. `[FORBIDDEN]` **Kernel 依赖宿主/FFI**：Kernel 模块中出现 `Fable.Core`、`smol-toml`、`System.IO` 或任何 npm 库。
2. `[FORBIDDEN]` **自解析 (Self-Parsing)**：生产代码中通过正则、`IndexOf`、`Substring`、`parse` 扫描自己生成的 Prompt/TOML/Text 文本提取业务状态。
3. `[FORBIDDEN]` **反向依赖**：`Kernel` 引用 `Runtime` 或 `Host`，或 `Runtime` 引用具体 `Host` 实现。
4. `[FORBIDDEN]` **徒手 TOML 拼接**：使用 `sprintf`、`StringBuilder`、字符串插值拼接 `key = value`、`[[table]]` 或 TOML 围栏。
5. `[FORBIDDEN]` **机器 Provenance 混入 Prose**：把 `loop_id`、`round`、`nudge_origin` 写入 Prompt 正文并解析取回。

---

## 5. 数据流与转换模型

```mermaid
+-----------------+      Host DTO      +------------------+     Typed Event/Doc    +----------------+
| Host Boundary   | -----------------> | Runtime Adapter  | ---------------------> | Kernel Flow    |
| (OpenCode/OMP)  | <----------------- | MessageTransform | <--------------------- | Pure Function  |
+-----------------+   Rendered TOML    +------------------+     PromptDocument     +----------------+
                                                |                                          ^
                                                v                                          |
                                       +------------------+                                |
                                       | smol-toml        |                                |
                                       | (stringify only) |                                |
                                       +------------------+                                |
                                                |                                          |
                                                v                                          |
                                       +---------------------------------------------------+
                                       | Per-Runtime Journal (.ndjson Fact Event Sourcing) |
                                       +---------------------------------------------------+
```

---

## 6. 实施出口与验证门禁

| 门禁 ID | 验证项 | 自动化检验方式 |
| :--- | :--- | :--- |
| **GATE-01** | `smol-toml` 导入唯一性 | `tests/PromptArchitectureGatesTests.fs` 检查 `smol-toml` 仅在 `Runtime/Serialization/Toml.fs` 与测试中 import。 |
| **GATE-02** | 生产代码零 Parse 自解析 | 扫描生产代码禁止出现 `smol-toml.parse` 或自生成文本的 parse 逻辑。 |
| **GATE-03** | 依赖单向性 | F# 项目文件 `wanxiangshu.fsproj` 编译顺序必须严格为 `Kernel -> Runtime -> Host`。 |
| **GATE-04** | Metadata Provenance | 验证 `MessageOrigin` 与 `loop_id` 只通过 `metadata.wanxiangshu` 或 `WanEvent` 传输。 |
