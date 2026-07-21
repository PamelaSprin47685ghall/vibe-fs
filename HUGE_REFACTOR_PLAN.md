# HUGE_REFACTOR_PLAN.md — 万象术第一性原理与 Universal TOML 重构决策

## 一、 第一性原理：零解析公理 (Zero-Parsing Axiom)

1. **视图与真相绝对解耦**：
   - Prompt / 消息文本是系统的**单向输出视图 (Sink / Output Only)**，绝不能反向充当数据源 (SSOT)。
   - 全系统**彻底禁止**运行期对 Prompt 或 Response 文本进行正则扫描、YAML / Frontmatter 反序列化、`Substring` 或 `.Split` 手写解析。
2. **强类型海关拦截**：
   - LLM 的所有意图、待办提交、审查结论，100% 由宿主强类型 Tool Call 签名（`todowrite` / `submit_review` / `return_reviewer`）在入口收敛为 F# 强类型 Record / DU。
3. **事件溯源 SSOT**：
   - 跨轮次记忆与重构状态 100% 消费物理磁盘追加日志 **`.wanxiangshu.ndjson`**，绝不扫描 `session.messages` 或对话历史文本。

---

## 二、 核心架构：不可约 7-Primitive Universal TOML Schema

彻底消灭 35+ 个非正交复合字段（`verification_policy` / `handover_report` / `forbidden_action` / `contract_rule` 等），全系统 Prompt 统一投影为 **7 个绝对正交的原语 (3 根标量 + 4 根表数组)**：

### 1. 3 个根标量原语 (Root Scalars)
- **`objective`** (string) — 意图与终态目标（统一消灭 `task` / `what_to_summarize` / `question` 单数）。
- **`background`** (string) — 语境、前置条件与先验知识。
- **`agent_role`** (string) — 代理角色与操作权能（模式自然嵌入，如 `"Implementation Agent (mutating)"` / `"Code Reviewer (read-only)"`）。

### 2. 4 个根表数组原语 (Root Table-Arrays)
- **`[[targets]]`** — 正向靶 / 输入 / 探查目标 / 注入数据（`kind = "file" | "path" | "query" | "command" | "evidence"`）。
- **`[[boundaries]]`** — 负向界 / 排除项 / 越界红线（`kind = "file" | "dir" | "path" | "action"`）。
- **`[[rules]]`** — 约束律 / 规范 / 契约 / 准则 / 疑问（`kind = "policy" | "constraint" | "criterion" | "question" | "contract"`）。
- **`[[outcomes]]`** — 终态产物 / 结论 / 响应（包含 `label` 与 `text`）。

---

## 三、 规范与工程纪律

1. **消息即配置 (Message as Config Schema)**：
   - 抛弃 Markdown 自由发挥，全量采用 Pure TOML。
2. **键名与键值规范**：
   - **Key**：结构化 `snake_case`，扁平直铺根节点，零 `[section]` 嵌套表。
   - **Value**：地道 Plain Human English，拒绝连字符/下划线机械拼接。
3. **集合表达**：
   - 充分发挥 TOML 原生 `[[...]]` 垂直 Table 数组表达美学。

---

## 四、 全场景派生对照表

| 子代理 / 场景 | `objective` | `background` | `agent_role` | `[[targets]]` | `[[boundaries]]` | `[[rules]]` | `[[outcomes]]` |
| :--- | :---: | :---: | :---: | :---: | :---: | :---: | :---: |
| **Coder** | ✓ | ✓ | ✓ | `kind = "file"` | `kind = "path"` | `policy` / `criterion` | `label = "report"` |
| **Inspector** | ✓ | ✓ | ✓ | `kind = "path"` | `kind = "dir"` | `policy` / `question` | `label = "report"` |
| **Reviewer** | ✓ | – | ✓ | `kind = "file"` | `kind = "file"` | `contract` / `criterion` | `label = "PERFECT/REVISE"` |
| **Search / Fetch** | ✓ | – | ✓ | `kind = "evidence"` | – | – | `label = "report"` |
| **PTY / Executor** | – | – | ✓ | `kind = "command"` | `kind = "action"` | `constraint` | `label = "running/killed"` |

---

## 五、 全面清查与 Pseudo-YAML 彻底收拢计划

经过对代码库的深度扫荡，共查出 9 处 Pseudo-YAML / 伪 YAML Header 围栏遗留点。必须全量改造收拢为 Universal TOML Schema：

| # | 涉及模块文件 | 现存 Pseudo-YAML 模式 / 留存点 | 统一收拢与重构方案 |
| :---: | :--- | :--- | :--- |
| 1 | `src/Runtime/PromptHeader.fs`<br/>`src/Runtime/Workspace/Yaml.fs` | `Yaml.stringify` / `yamlField` / `yamlSeqField` / `yamlStringSeqField` / `---` 围栏 | 销毁所有 YAML 生成函数与 `---` 围栏构建器。全量替换为纯 F# AST 构建的 `PromptToml.fs` 生成器。 |
| 2 | `src/Kernel/Wanxiangzhen/SquadPrompts.fs` | 硬编码 `"---\ntask: %s\n---\n\n"` 伪 YAML 模板 | 替换为 Universal TOML：`objective = "%s"` / `agent_role = "Wanxiangzhen Slave Agent"`。 |
| 3 | `src/Runtime/Wanxiangzhen/SquadEventDisplayCodec.fs` | `Yaml.stringify` 构造 `"---\n" + yamlText + "---\n\n"` 及其 `IndexOf` | 彻底移除 `---` 围栏与 `Yaml.stringify`，事件展示统一收敛为 Plain Text / JSON 结构输出。 |
| 4 | `src/Runtime/Tooling/CapsFormat.fs` | `CapsYamlItem` 类型与 `yamlSeqField "caps"` 伪 YAML 块 | 收敛为 Universal Schema `[[targets]]` (`kind = "evidence"`, `value = label`, `content = content`)，Pure TOML 输出。 |
| 5 | `src/Runtime/Search/SearchPrompts.fs` | `yamlSeqField "results"` 与 `yamlField` 生成 `---` 伪 YAML 标头 | 收敛为 Universal Schema `[[targets]]` (`kind = "evidence"`, `value = title/url`, `content = content`)，Pure TOML 输出。 |
| 6 | `src/Runtime/Subsession/SubagentBatchSpawnCore.fs` | `yamlStringSeqField "iterators"` 构造 `---` 伪 YAML 标头 | 收敛为 Universal Schema `[[targets]]` (`kind = "command"`, `value = iterator`)，Pure TOML 输出。 |
| 7 | `src/Runtime/Tooling/ToolOutputInfo.fs` | `flatFields` 转伪 YAML 并用 `---` 围栏包裹渲染 | 收敛为 Pure TOML `[[outcomes]]` / `[[rules]]` 格式，彻底移除 `---` 围栏。 |
| 8 | `src/Runtime/Fallback/FallbackConfigCodec.fs` | `extractAgentsMdHeaderConfig` / `yamlParse` 解析 `AGENTS.md` 伪 YAML | 配置解析统一收敛为标准 TOML 配置构建器或原生 Parser，消除伪 YAML。 |
| 9 | `src/Runtime/Wanxiangzhen/ConfigReader.fs` | `extractAgentsMdHeaderConfig` / `yamlParse` 解析 `AGENTS.md` 伪 YAML | 统一为标准 TOML 配置文件读入，彻底拔除 `yaml` npm 包依赖。 |
