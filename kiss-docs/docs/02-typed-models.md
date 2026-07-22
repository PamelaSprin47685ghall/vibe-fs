# 02. 强类型模型与展示协议规范 (Typed Models & Presentation Protocol Spec)

## 1. 现状、目标、决策与未决风险

### 1.1 现状 (Current Status)
当前系统在提示词（Prompt）与工具结果展示中存在 YAML front matter 生成、Markdown 标题/文本拼接、以及从已生成文本中回读状态的 pattern（例如 `hasDoubleCheckAnchor`、`isNudgePrompt`、`parseFrontMatter`）。这种模式导致机器逻辑依赖模型展示文本，违反了单向视图原则。

### 1.2 目标 (Goal)
建立强类型、纯函数的提示词与展示协议：
1. 模型可见的结构化 Prompt 与工具/事件展示，全部由 F# 强类型数据单向投影并交给标准库序列化。
2. 彻底消灭生产环境对自生成文本的反向解析（No Self-Parsing Invariant）。
3. 保证合法状态由编译期类型系统表达，非法状态在构造期被物理拦截。

### 1.3 关键决策 (Key Decisions)

| ID | 决策 | 说明 |
| --- | --- | --- |
| D1 | **三层解耦** | 严界 Domain Fact、Presentation View 与 External Codec。 |
| D2 | **No Self-Parsing** | 生产环境禁止 `import parse`；机器逻辑只读 typed event/metadata。 |
| D3 | **展示 Schema 分域** | 7 根原语仅用于指令 Prompt；Tool Output、Search、Caps、Squad Event 各用独立 Schema。 |
| D4 | **唯一 FFI 出口** | 锁定 `smol-toml` `1.7.0`，生产环境仅导出 `stringify` 函数。 |
| D5 | **受限 TomlValue** | 屏蔽 Null/Undefined/Float/异构数组，使非法 TOML 结构在类型层不可构造。 |
| D6 | **配置 YAML 豁免** | AGENTS.md 等外部输入配置保持既有 YAML Codec，不纳入 Prompt 重构。 |

### 1.4 未决风险与应对 (Undecided Risks)
- **第三方库边界行为未决**：`smol-toml` 1.7.0 在多行字符串换行符处理、Unicode 字符串转义、Array-of-Tables (AoT) 字段排序以及空集合渲染上的准确表现，不得在生产代码中凭空假设。
- **应对规则**：所有未决库行为**必须由 Phase 0 正式测试 (`tests/TomlSerializationTests.fs`) 提前证明**。生产代码严禁编写防御性 text post-processing 或 fallback。

---

## 2. 架构海关：三层解耦与 No Self-Parsing Invariant

### 2.1 三层契约划分

```text
+-----------------------------------------------------------------------+
| 1. Domain Fact (领域事实)                                              |
|    - F# 纯 Record / DU                                                |
|    - 表达业务意图、待办状态、审查决策、能力声明                          |
|    - 零 Fable 依赖、零字符串格式化                                     |
+-----------------------------------------------------------------------+
                                   |
                                   | Pure Projection
                                   v
+-----------------------------------------------------------------------+
| 2. Presentation View (展示视图)                                       |
|    - 受限 TomlValue 代数                                              |
|    - 单向转换为 smol-toml 对象输入                                     |
|    - 仅用于渲染供 LLM / 人类阅读的文本视图                             |
+-----------------------------------------------------------------------+

+-----------------------------------------------------------------------+
| 3. External Codec (外部编解码器)                                       |
|    - 仅处理来自系统外部的未信数据                                       |
|    - 白名单：AGENTS.md YAML、NDJSON WanEvent、模型不可信 recovery     |
|    - 严禁解析系统自身生成的 Presentation View                         |
+-----------------------------------------------------------------------+
```

### 2.2 No Self-Parsing Invariant
- **禁令内容**：系统先拥有强类型事实 A，渲染为文本 B，随后在后续流程中扫描 B 以提取/恢复 A'。
- **红线规则**：
  1. 生产代码（`src/`）**物理禁止** `import { parse } from "smol-toml"`。
  2. 生产代码中不得出现 `parseFrontMatter`、`hasDoubleCheckAnchor` 或任何通过正则/字符串包含判断机器状态的函数。
  3. **测试例外**：单元测试与契约测试可导入 `smol-toml.parse`，用于断言生成的 TOML 文本解析后符合预期结构与语义。

---

## 3. 指令型 PromptDocument 规范

### 3.1 7 根原语 (7-Primitives)
指令型 PromptDocument 根层级固定且仅包含以下 7 个原语（顺序不可变更）：

1. `objective` *(必填非空 string)*：目标终态。
2. `background` *(可选非空 string)*：先验语境与原因。
3. `agent_role` *(必填 DU)*：代理角色与权能。
4. `targets` *(表数组 `[[targets]]`)*：输入与目标 DU 列表。
5. `boundaries` *(表数组 `[[boundaries]]`)*：负向禁区 DU 列表。
6. `rules` *(表数组 `[[rules]]`)*：规则与契约 DU 列表。
7. `outcomes` *(表数组 `[[outcomes]]`，至少 1 项)*：要求的产出与交付物列表。

### 3.2 最小 F# 形状与“非法状态不可构造”规则

```fsharp
namespace Wanxiangshu.Kernel.Prompt

module Document =

    [<RequireQualifiedAccess>]
    type AgentRole =
        | Implementation
        | CodebaseSearch
        | BrowserAutomation
        | CodeReview
        | ExecutorSummarization
        | WebSearchSummarization
        | MethodologyReasoning
        | NudgeSupervisor
        | SquadWorker

    type TimeoutKind = Short | Long

    type PromptTarget =
        | FileTarget of path: string * guide: string * draft: string option
        | FileReference of path: string
        | EntryTarget of pathOrSymbol: string
        | QueryTarget of query: string
        | CommandTarget of language: string * program: string * dependencies: string list * timeoutKind: TimeoutKind
        | EvidenceTarget of label: string * content: string
        | TodoTarget of content: string

    [<RequireQualifiedAccess>]
    type BoundaryTarget =
        | File of path: string
        | Directory of path: string
        | PathOrSymbol of value: string

    type PromptBoundary =
        | DoNotRead of BoundaryTarget
        | DoNotModify of BoundaryTarget
        | DoNotExecute of action: string
        | DoNotTouch of BoundaryTarget

    type PromptRule =
        | Policy of text: string
        | Constraint of text: string
        | Criterion of text: string
        | Question of text: string
        | Contract of text: string

    type PromptOutcome = { label: string; text: string }

    type PromptDocumentView =
        { objective: string
          background: string option
          agentRole: AgentRole
          targets: PromptTarget list
          boundaries: PromptBoundary list
          rules: PromptRule list
          outcomes: PromptOutcome list }

    // 核心：构造函数私有化，确保非法状态在创建期即被拦截
    type PromptDocument = private PromptDocument of PromptDocumentView

    type PromptDocumentError =
        | EmptyObjective
        | EmptyText of fieldName: string
        | EmptyOutcomes
        | DuplicateOutcomeLabel of label: string

    // 智能构造器：非法状态不可构造规则
    // 1. objective / outcomes 不能为空
    // 2. 文本字段不能包含纯空白字符
    // 3. outcome.label 必须唯一
    val create : PromptDocumentView -> Result<PromptDocument, PromptDocumentError list>
    val view : PromptDocument -> PromptDocumentView
```

---

## 4. 展示 Schema 独立分域

格式复用（TOML）不等于 Schema 合并。工具输出、搜索结果、能力声明与事件展示不伪装成指令 Prompt，不混用 7 原语。

### 4.1 Tool Output Schema
`ToolOutputMessage` 保持独立，针对不同工具结果输出专属 TOML 结构：
- **通用输出**：`output` (opaque string), `iterator` (optional string), `syntax` (optional string)。
- **Executor 输出**：`stdout`, `exit_status`, `exit_code`, `truncated`。
- **Fuzzy Search 输出**：`total_matched`, `[[matches]]` (`path`, `line`, `content`)。
- **WriteResult 输出**：`path`, `success`, `syntax_errors`。

### 4.2 Search / Fetch / Caps Schema
- **Search 视图**：根节点为 `[[results]]`，包含 `title`, `url`, `content`。
- **Fetch 视图**：根节点为 `title`, `byline`, `length`, `content`。
- **Caps 视图**：根节点为 `[[capabilities]]`，包含 `label`, `content`。

### 4.3 Squad Event Schema
Squad Event 视图仅用于向 UI/模型展现事件（单向投影）：
- 根键为 `event_kind`, `session_id`, `task_id`, `commit_sha`, `message`。
- **注意**：系统持久化与重放继续使用 NDJSON `WanEvent`，不解析此 TOML 视图。

### 4.4 Batch Subagent Report Schema
多子代理批量报告投影为独立的 `[[reports]]` 表数组：
- 每个 element 为 `SubagentReport` 表：`iterator` (optional string), `summary`, `error` (optional string), `findings` (string array), `related_files` (string array)。
- **规则**：`iterator` 与 `body/summary` 必须在同一 table 内强绑定，严禁使用 Markdown `---` 分割符，严禁拆分为平行数组。

---

## 5. Machine Provenance 与 Typed State

机器状态（Provenance、轮次、来源、审查 Challenge）严禁依赖 Prompt 文本标记，必须由 Typed State / Event 或 Host Metadata 承载。

### 5.1 MessageOrigin 代数
```fsharp
type MessageOrigin =
    | Human
    | TodoNudge
    | ReviewNudge
    | RunnerNudge
    | CompactionNudge
    | ForceStop
    | FallbackContinuation
```

### 5.2 Review Challenge Typed State
审查 Challenge 不靠匹配历史消息中的 `"double-check"` 文本，由状态机与 typed event 控制：
```fsharp
type ReviewChallengeState =
    | NotRequested
    | Requested of round: int
    | Answered of round: int
```

### 5.3 Host Metadata 契约
在支持元数据的宿主（如 OpenCode）中，系统状态写在 `metadata.wanxiangshu` 字段内。消费者直接进行 DU / Enum 匹配，不读消息 text。

---

## 6. TOML Projection 与 受限 TomlValue FFI

### 6.1 受限 TomlValue 代数
为防止在序列化边界制造非法 TOML 类型（如 Null、Undefined、NaN、异构数组），定义受限代数：

```fsharp
namespace Wanxiangshu.Runtime.Serialization

module TomlValue =

    type TomlValue =
        | String of string
        | Integer of int
        | Boolean of bool
        | StringArray of string list
        | TableArray of (string * TomlValue) list list
        | Table of (string * TomlValue) list
```

### 6.2 唯一 FFI 出口 (`Toml.fs`)
`smol-toml` 是项目中唯一的 TOML 序列化库，只在 `Toml.fs` 中建立唯一 FFI：

```fsharp
namespace Wanxiangshu.Runtime.Serialization

open Fable.Core

module Toml =

    [<Import("stringify", "smol-toml")>]
    let private stringifyNative (value: obj) : string = jsNative

    // 将 TomlValue 递归转换为 JS 原生 object/array，并调用 stringifyNative
    val stringify : TomlValue -> string
```
*注：`stringify` 函数若接收到的根节点不是 `Table`，必须抛出明确异常。生产代码不得捕获异常进行 fallback。*

### 6.3 Projection 规则与 Wire 标准
1. **Snake Case 标准**：TOML key 必须严格使用 `snake_case`（如 `agent_role`, `timeout_type`）。
2. **DU 穷尽匹配**：DU 投影到 wire string 必须手写穷尽 match，物理禁止 `ToString().ToLowerInvariant()` 或反射匹配。
3. **空值处理**：`None` 或空集合在投影阶段直接省略该 key，不得渲染为 `key = ""` 或空 table 声明。

---

## 7. Opaque Content 边界与配置 YAML 豁免

### 7.1 Opaque Content 边界
- 用户在 Continue 中输入的自由文本、模型生成的代码、网页 HTML/Markdown，作为 **Opaque String（不透明字符串）** 存放在 `content` 或 `draft` 字段中。
- 系统**不得**尝试将用户输入的任意文本解析或强行包装为 7 根原语结构。

### 7.2 配置 YAML 豁免
- `AGENTS.md`、`FallbackConfigCodec.fs` 与 `ConfigReader.fs` 属于系统读取外部环境配置的合法 Input Codec（D6 决策）。
- 配置 Codec 继续使用 npm `yaml` 库，不属于 Prompt / Presentation 视图重构范畴，不受 TOML 迁移约束。

---

## 8. 实施出口与验证证据 (Implementation Exits & Verification Evidence)

### 8.1 实施出口
1. **源码新增**：
   - `src/Kernel/Prompt/Document.fs` （Prompt 领域类型与智能构造器）
   - `src/Runtime/Serialization/TomlValue.fs` （受限 TOML 代数）
   - `src/Runtime/Serialization/Toml.fs` （唯一 FFI 入口）
   - `src/Runtime/Prompt/PromptToml.fs` （PromptDocument 投影器）
2. **物理删除**：
   - `src/Runtime/PromptHeader.fs`
   - `src/Runtime/PromptFrontMatter.fs`
   - `src/Runtime/Workspace/Yaml.fs`
   - `src/Runtime/Tooling/ToolOutputInfoParse.fs`

### 8.2 验证证据
1. **库行为验证 (`tests/TomlSerializationTests.fs`)**：
   - 证明 `smol-toml` 1.7.0 对 UTF-8 中文、多行文本 `"""`、AoT 顺序、引号转义的处理符合预期。
2. **No Self-Parsing 架构门禁 (`tests/PromptArchitectureGatesTests.fs`)**：
   - 扫描 `src/` 下所有文件，断言 `smol-toml.parse` 的引用次数为 `0`。
   - 扫描 `src/` 下所有文件，断言 `smol-toml` 模块 import 仅存在于 `Runtime/Serialization/Toml.fs`。
   - 验证四个旧文件已在磁盘与 `wanxiangshu.fsproj` 中物理消失。
