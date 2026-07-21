# HUGE_REFACTOR_PLAN.md — 万象术第一性原理与 Universal TOML 重构决策主文档

---

## 第一章：第一性原理与架构哲学 (First Principles & Architecture Philosophy)

### 1.1 软件设计与复杂度压缩公理

从 Kolmogorov 宝典的核心原则出发，构建软件设计有两种方法：一种是使其足够简单，以至于明显没有缺陷；另一种是使其足够复杂，以至于没有明显的缺陷。在万象术 (Wanxiangshu) 运行时架构中，软件设计的本质任务是**把不可消除的复杂度压成不可再短的充分描述**。

过去在多代理协作、审查循环、工具调用与上下文注入中，系统充斥着大量 Markdown 标题、伪 YAML 围栏（`---`）、连字符拼接的字符串标签，以及跨模块手写提取文本切片的胶水代码。这种“勉强工作”的代码透支的是未来的长程可控性。

必须建立极洁癖的工程防线：让人和 LLM 机器只付本质复杂度之账。小问题免框架税，大问题不手工搬砖。合适的类型与强规范让问题露本相，不在隐式约定、文本解析与调试黑箱里绕路。

---

### 1.2 零解析公理 (Zero-Parsing Axiom)

**消息与 Prompt 文本是系统的单向输出视图 (Sink / Output Only)，绝对不能反向充当数据源 (SSOT)。**

#### 公理 1.1：视图与真相绝对解耦
- 渲染给 LLM 读取的提示词文本（Prompt Text）或对话框历史（`session.messages`）仅仅是终端单向展现产物（Sink）。
- 系统内部的唯一真相（SSOT）永远是 **F# 代数数据类型 (DU / Record)** 以及磁盘物理追加日志 **`.wanxiangshu.ndjson`**。
- 全系统**彻底禁止**运行期对 Prompt 或 Response 文本进行正则扫描、YAML / Frontmatter 反序列化、`Substring` 或 `.Split` 手写切片解析。系统内不存在任何自动解析，也不存在任何手写切片解析——从根源上就不应该解析。

---

### 1.3 强类型海关拦截与零文本偷渡

#### 公理 1.2：强类型海关拦截 (Type Gatekeeper)
- LLM 的所有意图（`CoderIntent` / `InspectorIntent`）、待办提交（`TodowriteIntent`）、审查结论（`SubmitReviewIntent`），必须在宿主海关入口（Zod / JSON Schema）处直接原子化校验并打入 F# 强类型 Record / DU。
- 绝不允许在边界处将强类型撕裂为裸字符串 `prompt: string`，然后在下层函数中通过“再次手写解析”来搜寻字段。
- 类型系统是最便宜的边防。字符、数字、布尔最会偷渡错误。账户号、订单号、用户标识、目标文件路径若同属基本字符串类型则编译器分不清。概念独立命名在运行时零成本，维护时直击知识边界。状态不靠可空字段和布尔开关拼凑，那会凭空造出不存在的非法组合。

---

### 1.4 事件溯源 SSOT (Event Sourcing SSOT)

#### 公理 1.3：事件溯源真相 (NDJSON SSOT)
- 历史不是唯一真理，当下只是事件流积分。
- 跨轮次记忆、With-Review 模式激活状态、待办快照（`openTodosJson`）、方法论选择（`select_methodology`）以及万象阵 DAG 协调状态，100% 依赖物理磁盘追加日志 **`.wanxiangshu.ndjson`** 的纯函数重放（`Fold`）。
- 绝不依赖宿主 `session.messages` 或 Context Compaction 后的文本切片作为状态依据。Compaction 剪裁历史是宿主逻辑，万象术的 durable 状态绝对独立于上下文压缩。

---

### 1.5 Markdown 自由文本与 Prompt Drift 根源

传统 LLM 提示词采用 Markdown 格式（如 `# Task`、`## Background`、`### Instructions`），这种设计存在致命的工程缺陷：

1. **缺少 Schema 约束的自由表达**：Markdown 标题对于 LLM 而言仅仅是建议性文本，LLM 极易产生格式漂移（Prompt Drift），遗漏必需的结构化字段，或者在回复中反向模仿生成乱序的 Markdown 标题。
2. **多层嵌套标题的层级模糊**：`#`、`##`、`###` 嵌套层级在长上下文传输中会发生认知流失，LLM 难以确定某个指令究竟属于全局约束还是局部子任务。
3. **大段长文本的语义混淆**：将角色定义、行为准则、交付规范、契约门禁全部绞在一大段长文本正文中，破坏了深层语义的正交性。

---

### 1.6 Pure TOML 消息即配置 (Message as Config Schema)

Pure TOML 将 Prompt 的表达彻底重构为**结构化配置对象 (Config Schema)**：
- **键名 (Key)**：结构化 `snake_case`，扁平直铺在根节点，零 `[section]` 嵌套表。
- **键值 (Value)**：地道 Plain Human English，拒绝连字符/下划线机械拼接（如 `"Read source code, think using logic"`）。
- **集合表达**：充分发挥 TOML 原生 `[[...]]` 垂直 Table 数组美学，消除内联数组换行过载与语法歧义。

---

## 第二章：不可约 7-Primitive Universal TOML Schema 公理化

---

### 2.1 从 35+ 个非正交复合字段到 7 原语的范畴归约推导

在旧设计中，代码库散落着 35+ 个非正交复合字段（`verification_policy`、`handover_report`、`forbidden_action`、`contract_rule`、`citation_rule`、`review_method`、`verdict_option_*`、`affected_files`、`do_not_touch`、`entries`、`questions` 等）。

通过第一性原理公理化与维度归约（Dimensional Reduction），这些字段被完整消解映射至 **7 个不可约的正交原语向量空间 (3 根标量 + 4 根表数组)**：

```
                              ┌──────────────────────────────────────────────┐
                              │  旧系统 35+ 个非正交复合字段                   │
                              └──────────────────────┬───────────────────────┘
                                                     │
                                                     │ 范畴归约投影 (Dimensional Reduction)
                                                     ▼
┌────────────────────────────────────────────────────────────────────────────────────────────────────────┐
│                              不可约 7-Primitive Universal TOML Schema                                   │
├────────────────────────────────────────────────────┬───────────────────────────────────────────────────┤
│                3 根标量原语 (Root Scalars)          │            4 根表数组原语 (Root Table-Arrays)       │
├────────────────────────────────────────────────────┼───────────────────────────────────────────────────┤
│  1. objective   (意图与终态目标)                   │  4. [[targets]]    (正向靶/输入/数据)               │
│  2. background  (语境与先验情境)                   │  5. [[boundaries]] (负向界/排除红线)               │
│  3. agent_role  (代理角色与操作权能)               │  6. [[rules]]      (约束律/契约/准则/疑问)         │
│                                                    │  7. [[outcomes]]   (终态产物与离散结局)             │
└────────────────────────────────────────────────────┴───────────────────────────────────────────────────┘
```

---

### 2.2 3 根标量原语的代数定义

1. **`objective`** (string) — 意图、终态目标、待解答的问题（What to achieve）。
   - **消解归并**：一律统一为 `objective`，彻底消灭 `task`、`what_to_summarize`、`question` (单数) 等重叠别名。
2. **`background`** (string) — 语境、前置条件、阻塞与先验知识（Why / Context）。
   - **消解归并**：纯粹的情境描述，与 `objective` 动静分离，杜绝在 `background` 中重复宣告目标。
3. **`agent_role`** (string) — 代理的角色与操作权能（Who & Capability）。
   - **消解归并**：模式（`agent_mode`）自然嵌入角色定义中，如 `"Implementation Agent (mutating)"` 或 `"Code Reviewer (read-only)"`，消除单独 `agent_mode` 标量的冗余。

---

### 2.3 4 根表数组原语的代数定义

4. **`[[targets]]`** — 正向靶 / 输入 / 探查目标 / 注入数据。
   - **属性**：`kind` (`"file" | "path" | "query" | "command" | "evidence"`), `value`, `hint` (可选), `draft` (可选), `content` (可选)。
   - **消解归并**：`entries`、`affected_files`、`program`、`caps`、`raw_results` 统一归并入 `[[targets]]`。例如外部观察数据归并为 `kind = "evidence"` 的 target 项。
5. **`[[boundaries]]`** — 负向界 / 排除项 / 越界红线。
   - **属性**：`kind` (`"file" | "dir" | "path" | "action"`), `value`, `action` (可选: `"read" | "modify" | "execute"`).
   - **消解归并**：`do_not_touch`、`forbidden_action` 统一归并入 `[[boundaries]]`。
6. **`[[rules]]`** — 约束律 / 规范 / 契约 / 准则 / 疑问。
   - **属性**：`kind` (`"policy" | "constraint" | "criterion" | "question" | "contract"`), `text`.
   - **消解归并**：`verification_policy`、`citation_rule`、`contract_rule`、`review_method`、`criteria`、`questions` (复数) 统一归并入 `[[rules]]`。
7. **`[[outcomes]]`** — 终态产物 / 结论 / 响应。
   - **属性**：`label` (如 `"report"` | `"PERFECT"` | `"REVISE"` | `"running"` | `"killed"`), `text`.
   - **消解归并**：`report` 归并为 `label = "report"` 的 outcome；`verdicts` 归并为 `label = "PERFECT"` / `"REVISE"` 等离散结局表项。

---

### 2.4 类比法与正交矩阵对偶检验 (A:a, B:b => A:b, B:a)

通过对 Coder ($A$)、Inspector ($B$)、Reviewer ($C$)、Executor/PTY ($D$) 进行全矩阵交叉对偶补全，消除了 5 个隐蔽的领域能力盲区：

| 领域 / 概念 | Coder ($A$) | Inspector ($B$) | Reviewer ($C$) | Executor / PTY ($D$) | 对偶补全结论 (Cross-Transfer) |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **`[[boundaries]]`**<br/>(禁区) | 原有 `do_not_touch` ($a$) | **补全 $B:a$**<br/>(读禁区 `kind="dir"`) | **补全 $C:a$**<br/>(写/改禁区 `action="modify"`) | **补全 $D:a$**<br/>(破坏命令禁区 `action="execute"`) | 所有 Agent 均补全边界约束。通过 `action = "read/modify/execute"` 统一区分正负领土。 |
| **`[[rules]]`**<br/>(疑点与疑问) | **补全 $A:c$**<br/>(`kind="question"`) | 原有 `questions` ($c$) | **补全 $C:c$**<br/>(待核验假设 `kind="question"`) | **补全 $D:c$**<br/>(环境不确定性 `kind="question"`) | Coder 求解前可暴露静态推导疑问；Reviewer 可显式列举需针对性核验的疑点。 |
| **`[[rules]]`**<br/>(评审准则/规则) | **补全 $A:d$**<br/>(`kind="criterion"`) | **补全 $B:d$**<br/>(搜索完备性 `kind="policy"`) | 原有 `criteria` ($d$) | **补全 $D:d$**<br/>(超时规则 `kind="constraint"`) | Coder 预声明“必须满足的验收准则”，Reviewer 拿同一表做对照，实现双向契约对齐。 |
| **`[[targets]]`**<br/>(目标工件) | 原有 `targets` | 原有 `entries` | 原有 `affected_files` | 原有 `command` | 统一归并为 `[[targets]]`。通过 `kind = "file/path/query/command/evidence"` 消歧。 |
| **`[[outcomes]]`**<br/>(离散结局) | **补全 $A:e$**<br/>(预声明离散结局) | **补全 $B:e$**<br/>(预声明离散结局) | 原有 `verdicts` ($e$) | **补全 $D:e$**<br/>(进程退出结局) | 全员预声明结局分支，带 `label` 与 `text`，使调度方拿到结论即可自动推演下一步。 |

---

### 2.5 100% 正交性、平坦性与无损性形式化证明

- **正交性 (Orthogonality)**：
  - $\text{target} \cap \text{boundary} = \emptyset$：`target` 为应做/输入，`boundary` 为禁做/禁止，互补不相交。
  - $\text{rule} \cap \text{boundary} = \emptyset$：`rule` 为逻辑连续的准则/契约，`boundary` 为离散集合的越界红线。
  - $\text{outcomes} \cap \text{targets} = \emptyset$：流向相反，`outcomes` 为代理产出，`targets` 为外部输入。
  - $\text{objective} \cap \text{background} = \emptyset$：目标（将要达成）与情境（已经存在），时态相反。
- **平坦性 (Flatness)**：
  - 零 `[section]` 表头，深度 $D = 0$。所有表数组均直接挂载于根节点。
- **无损性 (Losslessness)**：
  - 存在左逆映射 $G: \text{UniversalTOML} \rightarrow \text{LegacyDomain}$，原有的所有上下文信息无损双向映射。

---

## 第三章：全场景派生与 TOML 极简规范 (Derivation & Style Specifications)

---

### 3.1 Coder 子代理派生 TOML Schema & 样例

```toml
objective = "Refactor subagent prompt generators to pure TOML schema"
background = "Eliminate YAML frontmatter text scraping across all subagent boundaries"
agent_role = "Implementation Agent (mutating)"

[[targets]]
kind = "file"
value = "src/Runtime/SubagentPrompts.fs"
hint = "Refactor Coder prompt generator using universal TOML schema"
draft = """
let coderPrompt (intent: CoderIntent) : string =
    UniversalSchema.render (coderProjection intent)
"""

[[boundaries]]
kind = "path"
value = "src/Hosts/OpenCode/Plugin.fs"
action = "modify"

[[rules]]
kind = "policy"
text = "Static verification only by reading and thinking with pure logic."

[[rules]]
kind = "constraint"
text = "Do NOT run unit tests or execute commands."

[[rules]]
kind = "criterion"
text = "Must eliminate all parseFrontMatter call sites from prompt generation."

[[rules]]
kind = "question"
text = "Are there any legacy hosts expecting raw prose without TOML headers?"

[[outcomes]]
label = "report"
text = "Return a detailed summary of changes and any difficulties encountered."
```

---

### 3.2 Inspector 搜索代理派生 TOML Schema & 样例

```toml
objective = "Find all modules responsible for tool argument validation"
background = "Investigate unhandled exception during session teardown"
agent_role = "Codebase Search Agent (read-only)"

[[targets]]
kind = "path"
value = "src/Hosts/OpenCode/"

[[targets]]
kind = "path"
value = "src/Runtime/Tooling/"

[[boundaries]]
kind = "dir"
value = "node_modules/"
action = "read"

[[rules]]
kind = "policy"
text = "Report concrete file paths and line-number references."

[[rules]]
kind = "question"
text = "Which modules validate tool parameters at the host boundary?"

[[rules]]
kind = "question"
text = "How are validation failures surfaced to the LLM?"

[[outcomes]]
label = "report"
text = "Return your report with related file paths and line ranges."
```

---

### 3.3 Browser 浏览器代理派生 TOML Schema & 样例

```toml
objective = "Navigate to official documentation and extract API signature"
background = "Verifying external MCP tool payload contract"
agent_role = "Browser Automation Agent (read-only)"

[[targets]]
kind = "query"
value = "https://modelcontextprotocol.io/docs/concepts/tools"

[[rules]]
kind = "policy"
text = "Use stealth browser mcp tools to interact with web pages."

[[outcomes]]
label = "report"
text = "Return detailed extraction report with source citations."
```

---

### 3.4 Reviewer 审查任务派生 TOML Schema & 样例

```toml
objective = "Review code changes for WIP acknowledgment formatting"
agent_role = "Code Reviewer (read-only)"

[[targets]]
kind = "file"
value = "src/Runtime/PromptHeader.fs"

[[targets]]
kind = "file"
value = "src/Runtime/SubagentPrompts.fs"

[[boundaries]]
kind = "file"
value = "src/Runtime/PromptHeader.fs"
action = "modify"

[[rules]]
kind = "contract"
text = "You MUST call return_reviewer before finishing. Do not end the conversation without submitting your verdict."

[[rules]]
kind = "criterion"
text = "1. Full use of language features, correct algorithms and data structures"

[[rules]]
kind = "criterion"
text = "2. Minimal complexity, no dead code, garbage code, or legacy wrappers"

[[rules]]
kind = "question"
text = "Did the implementation completely avoid manual string parsing at call sites?"

[[outcomes]]
label = "report"
text = "Replaced prompt stringifiers with strict TOML builder. Verified zero Markdown text parsing."

[[outcomes]]
label = "PERFECT"
text = "Accept submission without feedback (or optional minor suggestions)."

[[outcomes]]
label = "REVISE"
text = "Reject submission and return detailed actionable feedback to worker."
```

---

### 3.5 Executor / PTY 终端响应派生 TOML Schema & 样例

```toml
objective = "Run interactive test suite under background runner"
agent_role = "Terminal Execution Monitor (read-only)"

[[targets]]
kind = "command"
value = "npm run test"
hint = "Interactive Test Suite"

[[boundaries]]
kind = "action"
value = "kill"
action = "execute"

[[rules]]
kind = "constraint"
text = "Timeout set to 300 seconds. Process will be terminated automatically if exceeded."

[[outcomes]]
label = "running"
text = "Continue background execution and pipe log lines to channel."

[[outcomes]]
label = "killed"
text = "Clean up PTY session buffer and notify parent session."
```

---

### 3.6 Search / Fetch 上下文派生 TOML Schema & 样例

```toml
objective = "Provide web fetch context for LLM question answering"
agent_role = "Information Retrieval Agent (read-only)"

[[targets]]
kind = "evidence"
value = "webfetch: Wanxiangshu Architecture Guide"
content = """
Wanxiangshu event sourcing uses .wanxiangshu.ndjson as the single source of truth.
All state transitions are folded pure functions.
"""

[[targets]]
kind = "evidence"
value = "websearch: Fable F# Compiler Documentation"
content = """
Fable compiles F# code to clean JavaScript.
"""

[[outcomes]]
label = "report"
text = "Preserve raw facts and eliminate marketing boilerplate."
```

---

### 3.7 Nudge / Todo 催促派生 TOML Schema & 样例

```toml
objective = "Nudge user and agent to complete remaining open todos"
background = "Stream ended with pending work items"
agent_role = "Nudge Supervisor (trigger)"

[[targets]]
kind = "todo"
value = "Implement UniversalSchema.fs AST compiler"

[[targets]]
kind = "todo"
value = "Run full integration test suite"

[[rules]]
kind = "policy"
text = "You must mark todos in_progress before working, and completed after verification."

[[outcomes]]
label = "report"
text = "Please focus on completing the next pending todo item."
```

---

### 3.8 Wanxiangzhen 协调器与 Slave 派生 TOML Schema & 样例

```toml
objective = "Implement worktree isolation for subagent task"
background = "Squad DAG node execution in dedicated worktree"
agent_role = "Wanxiangzhen Slave Agent (mutating)"

[[targets]]
kind = "command"
value = "git worktree add .wanxiangzhen/task_001 feature/task_001"

[[rules]]
kind = "contract"
text = "Perform work in worktree. Submit commit SHA to coordinator upon completion."

[[outcomes]]
label = "submitted"
text = "Task completed and submitted to coordinator."
```

---

### 3.9 Key 命名规范与 Value 英文地道规范

1. **键名 (Key) 规范**：
   - 全量采用结构化 `snake_case`（如 `agent_role`、`[[targets]]`、`[[boundaries]]`）。
   - 扁平直铺根节点，绝对禁止 `[section]` 或多级 `[a.b]` 嵌套表。
2. **键值 (Value) 规范**：
   - 必须使用地道 Plain Human English（如 `"Read source code, think using logic, then edit or create files"`）。
   - 严禁在 Value 中使用下划线/连字符进行机械拼接（如禁止 `"read_think_edit"`），确保自然语言注意力对齐。

---

## 第四章：全库 9 处 Pseudo-YAML / 伪 YAML 遗留点清查与收拢

经过对代码库的全量扫荡，共排查出 9 处 Pseudo-YAML / 伪 YAML Header 围栏遗留点。必须全量改造收拢为 Universal TOML Schema：

| # | 涉及模块文件 | 现存 Pseudo-YAML 模式 / 留存点 | 统一收拢与重构方案 |
| :---: | :--- | :--- | :--- |
| **1** | `src/Runtime/PromptHeader.fs`<br/>`src/Runtime/Workspace/Yaml.fs` | `Yaml.stringify` / `yamlField` / `yamlSeqField` / `yamlStringSeqField` / `---` 围栏 | **全面销毁**所有 YAML 生成函数与 `---` 围栏构建器。替换为基于纯 F# AST 构建的 `PromptToml.fs` 生成器。 |
| **2** | `src/Kernel/Wanxiangzhen/SquadPrompts.fs` | 硬编码 `"---\ntask: %s\n---\n\n"` 伪 YAML 模板 | **替换为 Universal TOML**：`objective = "%s"` / `agent_role = "Wanxiangzhen Slave Agent (mutating)"`。 |
| **3** | `src/Runtime/Wanxiangzhen/SquadEventDisplayCodec.fs` | `Yaml.stringify` 构造 `"---\n" + yamlText + "---\n\n"` 及其 `IndexOf` | **彻底移除** `---` 围栏与 `Yaml.stringify`，事件展示统一收敛为 Plain Text / JSON 结构输出。 |
| **4** | `src/Runtime/Tooling/CapsFormat.fs` | `CapsYamlItem` 类型与 `yamlSeqField "caps"` 伪 YAML 块 | **收敛为 Universal Schema** `[[targets]]` (`kind = "evidence"`, `value = label`, `content = content`)，Pure TOML 输出。 |
| **5** | `src/Runtime/Search/SearchPrompts.fs` | `yamlSeqField "results"` 与 `yamlField` 生成 `---` 伪 YAML 标头 | **收敛为 Universal Schema** `[[targets]]` (`kind = "evidence"`, `value = title/url`, `content = content`)，Pure TOML 输出。 |
| **6** | `src/Runtime/Subsession/SubagentBatchSpawnCore.fs` | `yamlStringSeqField "iterators"` 构造 `---` 伪 YAML 标头 | **收敛为 Universal Schema** `[[targets]]` (`kind = "command"`, `value = iterator`)，Pure TOML 输出。 |
| **7** | `src/Runtime/Tooling/ToolOutputInfo.fs` | `flatFields` 转伪 YAML 并用 `---` 围栏包裹渲染 | **收敛为 Pure TOML** `[[outcomes]]` / `[[rules]]` 格式，彻底移除 `---` 围栏。 |
| **8** | `src/Runtime/Fallback/FallbackConfigCodec.fs` | `extractAgentsMdHeaderConfig` / `yamlParse` 解析 `AGENTS.md` 伪 YAML | 配置解析统一收敛为标准 TOML 配置构建器，消除伪 YAML 依赖。 |
| **9** | `src/Runtime/Wanxiangzhen/ConfigReader.fs` | `extractAgentsMdHeaderConfig` / `yamlParse` 解析 `AGENTS.md` 伪 YAML | 统一为标准 TOML 配置文件读入，彻底拔除 `yaml` npm 包依赖。 |

---

## 第五章：物理清扫与 Yaml.fs 权能红线 (Config-Only Constraint)

---

### 5.1 `PromptFrontMatter.fs` 与 `PromptHeader.fs` 物理销毁纪律

1. **彻底物理删除别名残留**：
   - 彻底删除 `src/Runtime/PromptFrontMatter.fs` 别名残留文件，并在 `wanxiangshu.fsproj` 中全量移除该编译节点，**零 Stub / Dummy 文件留存**。
2. **重构 PromptHeader 为 PromptToml**：
   - 彻底删除 `src/Runtime/PromptHeader.fs` 中包含 `yamlField` / `---` 围栏的逻辑，重构替换为纯 F# 实现的 `PromptToml.fs`。

---

### 5.2 `Yaml.fs` 权能红线 (Config-Only Constraint)

1. **权能硬隔离**：
   - 重构完成后，`Yaml.fs` 及其底层 `yaml` 库**只准用于 Config 静态文件处理**（如启动时解析/读取 `AGENTS.md` 的 `squad:` / `models:` 静态配置）。
2. **红线硬约束**：
   - `Yaml.fs` **严禁**被任何 Prompt 渲染、消息合成、Tool Output 格式化模块引用或调用。

---

### 5.3 零 Stub 纪律 (No-Stub Principle)

1. 废弃模块或函数一律进行全量物理拔除，严禁留存空 module 声明、`// Deprecated` 注释占位文件或 Dummy 函数。
2. 在 F# / .NET 项目中，一旦决定废弃某个文件名，必须同步从 `*.fsproj` 中销毁 `<Compile Include="..." />` 节点并物理删除文件。

---

## 第六章：F# 模块架构与 AST 编解码器设计

---

### 6.1 `UniversalSchema.fs` 的 F# 强类型 Record 与 DU 定义

```fsharp
module Wanxiangshu.Runtime.UniversalSchema

type TargetKind =
    | File
    | Path
    | Query
    | Command
    | Evidence

type TargetItem =
    { kind: TargetKind
      value: string
      hint: string option
      draft: string option
      content: string option }

type BoundaryKind =
    | File
    | Dir
    | Path
    | Action

type BoundaryItem =
    { kind: BoundaryKind
      value: string
      action: string option }

type RuleKind =
    | Policy
    | Constraint
    | Criterion
    | Question
    | Contract

type RuleItem =
    { kind: RuleKind
      text: string }

type OutcomeItem =
    { label: string
      text: string }

type UniversalPrompt =
    { objective: string option
      background: string option
      agentRole: string
      targets: TargetItem list
      boundaries: BoundaryItem list
      rules: RuleItem list
      outcomes: OutcomeItem list }
```

---

### 6.2 `PromptToml.fs` 单向生成器实现规范

```fsharp
module Wanxiangshu.Runtime.PromptToml

open Fable.Core
open Wanxiangshu.Runtime.UniversalSchema

let private escapeTomlString (s: string) : string =
    if s.Contains("\n") then
        "\"\"\"\n" + s.Replace("\"\"\"", "\\\"\\\"\\\"") + "\n\"\"\""
    else
        "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\""

let render (prompt: UniversalPrompt) : string =
    let sb = System.Text.StringBuilder()

    match prompt.objective with
    | Some objVal when objVal <> "" ->
        sb.AppendLine($"objective = {escapeTomlString objVal}") |> ignore
    | _ -> ()

    match prompt.background with
    | Some bgVal when bgVal <> "" ->
        sb.AppendLine($"background = {escapeTomlString bgVal}") |> ignore
    | _ -> ()

    sb.AppendLine($"agent_role = {escapeTomlString prompt.agentRole}") |> ignore

    for t in prompt.targets do
        sb.AppendLine() |> ignore
        sb.AppendLine("[[targets]]") |> ignore
        sb.AppendLine($"kind = {escapeTomlString (t.kind.ToString().ToLowerInvariant())}") |> ignore
        sb.AppendLine($"value = {escapeTomlString t.value}") |> ignore
        match t.hint with Some h -> sb.AppendLine($"hint = {escapeTomlString h}") |> ignore | None -> ()
        match t.draft with Some d -> sb.AppendLine($"draft = {escapeTomlString d}") |> ignore | None -> ()
        match t.content with Some c -> sb.AppendLine($"content = {escapeTomlString c}") |> ignore | None -> ()

    for b in prompt.boundaries do
        sb.AppendLine() |> ignore
        sb.AppendLine("[[boundaries]]") |> ignore
        sb.AppendLine($"kind = {escapeTomlString (b.kind.ToString().ToLowerInvariant())}") |> ignore
        sb.AppendLine($"value = {escapeTomlString b.value}") |> ignore
        match b.action with Some a -> sb.AppendLine($"action = {escapeTomlString a}") |> ignore | None -> ()

    for r in prompt.rules do
        sb.AppendLine() |> ignore
        sb.AppendLine("[[rules]]") |> ignore
        sb.AppendLine($"kind = {escapeTomlString (r.kind.ToString().ToLowerInvariant())}") |> ignore
        sb.AppendLine($"text = {escapeTomlString r.text}") |> ignore

    for o in prompt.outcomes do
        sb.AppendLine() |> ignore
        sb.AppendLine("[[outcomes]]") |> ignore
        sb.AppendLine($"label = {escapeTomlString o.label}") |> ignore
        sb.AppendLine($"text = {escapeTomlString o.text}") |> ignore

    sb.ToString().TrimEnd()
```

---

### 6.3 七步静态校验流水线 (axiomCheck)

```fsharp
type AxiomError =
    | NonOrthogonalKey of string
    | NestedSectionForbidden of string
    | HyphenatedValueForbidden of string
    | MissingMandatoryField of string

let axiomCheck (tomlText: string) : Result<unit, AxiomError> =
    // Pipeline checking:
    // 1. Root-level key whitelist check
    // 2. Zero [section] table check
    // 3. Snake_case key check
    // 4. Plain Human English value check (no hyphenated values)
    // 5. Array-of-tables [[table]] check
    // 6. Non-orthogonal key blacklist check
    // 7. Mandatory field presence check
    Ok ()
```

---

## 第七章：迁移路线图、验证断言与工程纪律 (Migration Roadmap & CI Assertion)

---

### 7.1 阶段 1：核心 AST 模块与静态门禁建立
1. 新建 `src/Runtime/UniversalSchema.fs` 与 `src/Runtime/PromptToml.fs`。
2. 在 `tests/` 中增加 `UniversalSchemaAxiomTests.fs`，验证全原语编解码正交性。

---

### 7.2 阶段 2：宿主子代理与审查模块逐个替换
1. 替换 `SubagentPrompts.fs`（Coder / Inspector / Browser / Summarizer）为 Universal TOML 投影。
2. 替换 `ReviewPrompts/`（Instructions / Submission / Commands / Format）为 Universal TOML 投影。

---

### 7.3 阶段 3：工具输出与框架展开层替换
1. 替换 `CapsFormat.fs`、`SearchPrompts.fs`、`PtySpawn.fs`、`ToolOutputInfo.fs` 为 Universal TOML `[[targets]]` / `[[outcomes]]` 渲染。

---

### 7.4 阶段 4：配置层 `Yaml.fs` 权能硬隔离与黑名单校验
1. 限制 `Yaml.fs` 仅供 `ConfigReader.fs` 与 `FallbackConfigCodec.fs` 启动载入使用。
2. 彻底移除 `PromptHeader.fs` / `PromptFrontMatter.fs` 的依赖。

---

### 7.5 CI 阻断断言与自动化架构测试防线
在 CI 流程中配置架构断言：
1. 任何在 Prompt 模块中 `open Wanxiangshu.Runtime.Yaml` 的行为将被 CI 阻断。
2. 任何引入 7 原语之外 Key 值的行为将被 `axiomCheck` 阻断。
