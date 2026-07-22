# 04. 验证与架构门禁规范 (Verification & Architectural Gatekeeping Spec)

## 1. 现状、目标、决策与未决风险

### 1.1 现状 (Current Status)
在现有系统的重构与演进过程中，架构门禁与验证机制存在以下不足：
1. **门禁依赖脆弱的全局字符串扫描**：旧门禁常采用全局文本 `grep` 或正则扫描（如查找 `---`、`frontmatter` 或 `smol-toml`），极易误伤 `docs/` 文档、`README.md`、`package.json` 或代码注释。
2. **临时探针脚本替代正式测试**：开发中存在使用一次性 `node -e` 脚本或未注册探针脚本验证能力的现象，导致验证逻辑无法落入 CI 轮回。
3. **测试分层不清晰**：单元测试、宿主契约测试与集成重放测试边界混淆，缺少对强类型 Prompt/TOML 语义、typed transport、metadata 原地写、宿主契约、事件重放、输出预算、SSRF/权限硬约束的体系化断言。

### 1.2 目标 (Goal)
建立覆盖全面、分层严密、防误伤的验证与架构门禁体系：
1. **四层测试分层**：明确划分 L0 单元测试、L1 契约测试、L2 集成测试与 L3 架构门禁。
2. **精准 Allowlist 门禁**：淘汰全仓全局字符串扫描，改为基于文件路径白名单、AST 与 import 符号定位的门禁机制。
3. **硬约束 100% 测试落点**：确保 Prompt 7 原语语义、Typed Transport、Metadata 原地修改、宿主契约、事件重放、输出预算、SSRF 与权限控制等各项硬约束均有明确的测试用例承载。
4. **证据区分与零临时探针**：严格区分当前可运行的单模块证据与未来 Phase 5 终极验收证据；物理禁止任何临时 Probe 脚本。

### 1.3 关键决策 (Key Decisions)

| ID | 决策 | 说明 |
| --- | --- | --- |
| D1 | **Allowlist 门禁机制** | 门禁仅扫描 `src/` 下源码路径，基于显式模块白名单校验依赖与符号，严禁全仓盲扫。 |
| D2 | **零临时 Probe 原则** | 物理禁止 `node -e` 或临时 `probe.js` / `probe.fsx`；所有边界用例必须落入 `tests/` 正式文件并注册至 `.fsproj`。 |
| D3 | **双向证据分离** | 明确区分“当前可运行证据”（特定 Slice 单元/契约测试输出）与“未来验收证据”（Phase 5 终极全量门禁命令输出）。 |
| D4 | **No Self-Parsing & FFI 强校验** | 门禁强制断言 `src/` 下 `smol-toml.parse` 导入数为 0，`smol-toml` FFI 导出仅 1 处，旧 FrontMatter 文件 0 存在。 |
| D5 | **KISS 架构分层门禁** | 强行断言 `Kernel` 纯净度（零 Fable、零 FFI、零 npm 导入），确保 Runtime 与 Host 薄隔离。 |

### 1.4 未决风险与应对 (Undecided Risks & Mitigations)
- **门禁白名单漏检风险**：若白名单过于狭窄，可能漏掉新增模块中的违规导入。
  - **应对规则**：门禁采用“默认拒绝 + 显式白名单”原则（Deny-by-default），非白名单模块导入敏感库（如 `yaml` 或 `smol-toml`）直接触发编译/门禁报错。
- **不同宿主 Metadata 读写行为差异**：OpenCode 原地修改 `metadata.wanxiangshu`，而某些宿主可能不原生支持 Object metadata。
  - **应对规则**：在 L1 宿主契约测试中通过 Mock 宿主环境，断言 Metadata 在各种 Host 映射下的序列化与恢复不变性。

---

## 2. 测试分层与矩阵规范 (Test Layering & Matrix Specification)

### 2.1 四层测试体系 (Four-Layer Test Architecture)

系统测试严格分为四个层级，各层级职责独立，禁止跨层混用：

```text
+-----------------------------------------------------------------------+
| L3: Architectural Gates (架构门禁)                                     |
|     - Allowlist 依赖扫描, 物理删除断言, 代码禁令校验, Kernel 纯净度       |
|     - 命令: npm run test:gates                                        |
+-----------------------------------------------------------------------+
                                   |
+-----------------------------------------------------------------------+
| L2: Integration & Replay (集成与重放测试)                               |
|     - Squad Event 单向视图与 NDJSON 重放, Batch 报告聚合, 输出预算与 Cap    |
|     - SSRF / 权限禁区拦截, KISS Per-Runtime Journal k-way merge 兼容性  |
|     - 命令: npm run test:integration                                  |
+-----------------------------------------------------------------------+
                                   |
+-----------------------------------------------------------------------+
| L1: Contract & Transport (契约与传输测试)                              |
|     - Host DTO / Metadata 原地写契约, Typed Transport 传输状态机       |
|     - Review Challenge State (Requested/Answered), MessageOrigin 匹配  |
|     - 命令: npm run test:contract                                     |
+-----------------------------------------------------------------------+
                                   |
+-----------------------------------------------------------------------+
| L0: Unit & Serialization (单元与序列化测试)                            |
|     - smol-toml FFI 库行为, TomlValue 代数, PromptDocument 7 原语投影  |
|     - ToolOutput / Search / Fetch / Caps 独立 Schema 正向序列化          |
|     - 命令: npm run build-and-test                                    |
+-----------------------------------------------------------------------+
```

### 2.2 完整测试矩阵 (Comprehensive Test Matrix)

| 验证能力 | 测试层级 | 测试落点文件 | 必须覆盖的硬约束与测试逻辑 |
| --- | --- | --- | --- |
| **库 FFI 序列化** | L0 单元 | `tests/TomlSerializationTests.fs` | `smol-toml` named import `stringify`、UTF-8 Unicode 转义、多行字符串 `"""`、AoT (Array-of-Tables) 顺序、末尾换行、非法根类型抛错。 |
| **Prompt 7 原语语义** | L0 单元 | `tests/PromptTomlTests.fs` | 7 根键顺序 (objective → background → agent_role → targets → boundaries → rules → outcomes)、DU 全分支投影、`None` 省略、`PromptDocument.create` 非法空值/重复 Label 拦截、Canonical Snapshot 比对。 |
| **ToolOutput Schema** | L0 单元 | `tests/ToolOutputInfoTests.fs` | 通用输出、Executor 专属输出 (`stdout`, `exit_status`, `exit_code`)、Fuzzy Search 匹配项、WriteResult 结果；删除旧 Parse Round-trip 测试。 |
| **Batch 报告聚合** | L1 契约 | `tests/SubagentBatchReportTests.fs` | 子代理强绑定 `iterator` 与 `summary/body` 表结构；全过程强类型组合，禁止 Markdown `---` 拼接与文本 Parse。 |
| **Review Challenge 状态** | L1 契约 | `tests/ReviewSessionTests.fs` | Review Challenge 由 `Requested(round)` / `Answered(round)` 强类型状态机驱动；禁止 `SessionIo` 扫描包含 `"double-check"` 文本。 |
| **Typed Origin & Nudge** | L1 契约 | `tests/NudgeMessageClassifierTests.fs` | `MessageOrigin` 枚举标记（`Human`, `TodoNudge`, `ReviewNudge` 等）；断言 Prompt 文本改动不影响机器分类结果。 |
| **Metadata 原地写** | L1 契约 | `tests/HostMetadataContractTests.fs` | OpenCode 等宿主的 `metadata.wanxiangshu` 原地修改；断言消息生命周期内透传 Metadata 不丢失字段。 |
| **宿主模型路由契约** | L1 契约 | `tests/HostModelRoutingTests.fs` | 宿主 User (`model: { providerID, modelID, variant }`) 与 Assistant (`providerID`, `modelID`, `variant`) 契约断言；Nudge Prompt 继承 Session 当前模型。 |
| **Squad Event 单向 View** | L2 集定 | `tests/WanxiangzhenSquadTests.fs` | Squad Worker 单向投影为 TOML View (`event_kind`, `task_id`)；持久化重放仍使用 NDJSON `WanEvent`，零 TOML Parse。 |
| **KISS Journal 重放兼容** | L2 集成 | `tests/JournalReplayTests.fs` | KISS-04 Per-Runtime Journal 模式断言：启动按 `ObservedAt → RuntimeId` k-way merge 归并，单源截断不破坏全局重放。 |
| **输出预算 (Output Budget)** | L2 集成 | `tests/ExecutorOutputBudgetTests.fs` | Executor 与 Web Search 结果超出 Budget 自动截断并标注 `truncated = true`；确保堆栈与 exit_code 不丢，LLM 上下文不爆栈。 |
| **SSRF 与权限禁区** | L2 集成 | `tests/SecurityBoundaryTests.fs` | Fetch/Search 工具拦截非法 URL 协议 (`file://`, `gopher://`, `127.0.0.1` / 内网 IP)；`boundaries` (`DoNotRead`, `DoNotModify`, `DoNotExecute`, `DoNotTouch`) 物理拦截。 |
| **架构门禁 (Gates)** | L3 门禁 | `tests/PromptArchitectureGatesTests.fs` | Allowlist 扫描 `src/`：No Self-Parsing (0 parse)、唯一 FFI (1 stringify)、2 处 config YAML、旧文件 0 存在、旧 Helper 0 引用、Kernel 纯洁性。 |

---

## 3. 架构门禁与 Allowlist 机制 (Architectural Gates & Allowlist Mechanism)

### 3.1 淘汰全仓脆弱字符串扫描 (Deprecating Fragile Global String Scanning)
旧门禁使用全仓正则/文本 `grep`，导致以下致命问题：
- 在 `docs/02-typed-models.md` 中引用 `"smol-toml.parse"` 用于编写规范时，触发 CI 报错。
- 在 `package.json` 或 `README.md` 中包含配置文件说明时，触发假阳性误报。
- 无法区分生产代码（`src/`）与测试代码（`tests/`）对 `parse` 的合法与非法使用。

**新规则**：架构门禁**必须指定明确的源码路径**（如 `src/**/*.fs`），并使用 **Allowlist（白名单）机制** 校验依赖导入与符号引用。

### 3.2 路径与 Import 白名单规则 (Path & Import Allowlist Rules)

新增门禁文件 `tests/PromptArchitectureGatesTests.fs`，执行以下 Allowlist 校验：

1. **`smol-toml` 导入白名单**：
   - 生产代码（`src/`）中，仅允许 `src/Runtime/Serialization/Toml.fs` 包含对 `smol-toml` 的导入。
   - 导入形式**必须**为 named import `stringify`：`[<Import("stringify", "smol-toml")>]`。
   - 生产代码中 `smol-toml.parse` 导入总数必须**精确等于 0**。
   - *测试代码（`tests/`）白名单豁免*：测试用例允许导入 `smol-toml.parse` 用于校验生成的 TOML 语义。

2. **`yaml` 导入白名单**：
   - 生产代码（`src/`）中，`yaml` 库的导入总数必须**精确等于 2**，且仅限于：
     - `src/Runtime/Fallback/FallbackConfigCodec.fs`
     - `src/Runtime/Wanxiangzhen/ConfigReader.fs`
   - 上述两处仅用于读取 `AGENTS.md` 外部配置文件（符合 D6 决策），禁止任何 Prompt 或展示层代码导入 `yaml`。

3. **禁止徒手 TOML 拼接**：
   - 扫描 `src/` 源码，禁止使用 `StringBuilder`、`sprintf`、插值字符串或手写 `key = value` / `[[table]]` 拼接 TOML 文本。
   - 所有模型可见 TOML 必须由 `Toml.stringify` 统一输出。

### 3.3 物理删除与废弃符号黑名单 (Physical Deletion & Blacklist Assertions)

门禁校验以下文件与符号在系统中**彻底消失**：

1. **物理文件不存在黑名单**：
   - `src/Runtime/PromptHeader.fs`
   - `src/Runtime/PromptFrontMatter.fs`
   - `src/Runtime/Workspace/Yaml.fs`
   - `src/Runtime/Tooling/ToolOutputInfoParse.fs`
   *断言要求：不仅磁盘文件被删，`wanxiangshu.fsproj` 项目文件中亦不得包含上述编译节点。*

2. **废弃符号与逻辑黑名单**：
   - 生产代码中禁止出现以下依赖固定 Prose / Substring 扫描的旧函数：
     - `hasDoubleCheckAnchor`
     - `isNudgePromptText`
     - `isNudgePrompt`
     - `CapsYamlItem`
     - `parseFrontMatter`
     - `escapeToml*` / `ToString().ToLowerInvariant()`

### 3.4 Kernel 纯净度与层级边界门禁 (Kernel Purity & Layer Boundary Gate)

为符合 KISS 架构设计（纯 Kernel、薄 Runtime/Host）：
1. **Kernel 零 Fable / 零 FFI 门禁**：
   - 扫描 `src/Kernel/` 下的所有 F# 文件。
   - 断言：不得包含 `open Fable.Core`、`open Fable.Core.JsInterop`、`smol-toml` 或 `yaml` 导入。
   - Kernel 仅包含强类型 DU/Record、领域模型与纯逻辑智能构造器（如 `PromptDocument.create`）。
2. **Runtime 薄隔离门禁**：
   - 投影模块仅将 Domain Document 转为受限 `TomlValue`，动态 `obj` 转换为 JS 原生对象的逻辑被隔离在 `Toml.fs` 唯一出口。

---

## 4. 核心硬约束验证规范 (Hard Constraints Verification Specifications)

### 4.1 Prompt / TOML 语义测试 (Prompt & TOML Semantic Testing)
- **落点文件**：`tests/PromptTomlTests.fs`
- **核心断言**：
  1. 根键顺序严格遵循 `objective → background → agent_role → targets → boundaries → rules → outcomes`。
  2. `PromptDocument.create` 对非法输入拦截：空 `objective`、空 `outcomes`、包含纯空白字符的文本、重复 `outcome.label`，必须返回 `Error (PromptDocumentError list)`。
  3. `None` 字段在输出 TOML 中自动省略键，不得渲染为 `key = ""` 或空声明。
  4. 绝不依赖手写字符串比较，测试端通过 `smol-toml.parse` 将输出还原为 AST/Map 进行深层语义比对。

### 4.2 Typed Transport & 状态重构测试 (Typed Transport & State Refactoring)
- **落点文件**：`tests/SubagentBatchReportTests.fs` 与 `tests/ReviewSessionTests.fs`
- **核心断言**：
  1. **Batch Spawn 报告**：子代理产生的强类型 `{ iterator; body }` 直接组装为 `SubagentReport` 表数组，在边界统一调用 `renderBatchReport`，不经过任何 `string -> parse -> data` 过程。
  2. **Review Challenge 状态机**：二次确认（Double-check）流程由 `Requested(round)` 与 `Answered(round)` 状态跃迁驱动，不依赖检索历史 Prompt 消息文本。

### 4.3 Metadata 原地修改与宿主契约 (In-place Metadata & Host Contract)
- **落点文件**：`tests/HostMetadataContractTests.fs`
- **核心断言**：
  1. 宿主（如 OpenCode）在发送消息时，机器 Provenance、`MessageOrigin`、Session/Run ID 物理写入 `metadata.wanxiangshu`。
  2. 宿主在 Context 压缩（Compaction）或多轮对话中透传 `metadata`，确保结构化元数据原地更新且不丢失。

### 4.4 事件重放与 KISS Journal 兼容性 (Event Replay & KISS Journal Compatibility)
- **落点文件**：`tests/WanxiangzhenSquadTests.fs` 与 `tests/JournalReplayTests.fs`
- **核心断言**：
  1. **Squad Event 视图**：TOML Event 仅用于向展示层单向输出；持久化存储与重放引擎**100% 依赖 NDJSON `WanEvent`**。
  2. **KISS-04 Journal 兼容**：在 Per-Runtime Journal 模式下，系统启动时按 `ObservedAt → RuntimeId` 进行确定性 k-way merge 归并；即使日志在 Frontier 处存在半行截断，Reader 仅止步于合法前缀，不进行写截断，也不破坏重放结果。

### 4.5 输出预算与截断机制 (Output Budget & Truncation)
- **落点文件**：`tests/ExecutorOutputBudgetTests.fs`
- **核心断言**：
  1. Executor 命令输出、Web Search 抓取结果在序列化为 TOML 前进行硬预算截断（Budget Capping）。
  2. 超出预算时，必须设置 `truncated = true`，并保留关键堆栈信息、异常上下文与 `exit_code`。
  3. 截断后的 ToolOutput TOML 字符数严禁超过配置门槛，杜绝 LLM 上下文溢出。

### 4.6 SSRF 与 权限边界拦截 (SSRF & Permission Boundary Enforcement)
- **落点文件**：`tests/SecurityBoundaryTests.fs`
- **核心断言**：
  1. **SSRF 拦截**：Web Search / Fetch 工具试图请求 `file://`、`gopher://`、`127.0.0.1`、`localhost` 或私有网段 IP（如 `10.0.0.0/8`, `192.168.0.0/16`）时，拦截器直接抛出安全异常。
  2. **Boundary 拦截**：Prompt 中的 `boundaries` 声明（如 `DoNotRead`, `DoNotModify`, `DoNotExecute`, `DoNotTouch`）在 Tool Dispatcher 处执行前校验，拒绝执行越权操作。

---

## 5. 门禁命令顺序与停止条件 (Gate Execution Sequence & Stop Conditions)

### 5.1 增量与终极命令执行顺序 (Incremental & Final Execution Order)

#### 开发阶段（Slice Incremental Execution）
在 Phase 0–Phase 4 分阶段重构期间，开发者/Subagent 每完成一个模块切片，**必须**顺序运行：
1. 运行该 Slice 已注册的专属单元/契约测试；若项目 runner 尚无切片入口，先新增正式入口，不用一次性命令代替。
2. 运行架构门禁：`npm run test:gates`。

#### 终极验收阶段（Phase 5 Final Acceptance Sequence）
仅在 Phase 5 物理清扫完成、所有模块就位后，按严格顺序执行以下四大验收命令：

```bash
# 1. 编译与 L0 单元测试 (含 Fable 构建与 Toml/Prompt 基础单元断言)
npm run build-and-test

# 2. L1 契约与 Transport 测试 (含 Host Metadata, Review State, MessageOrigin)
npm run test:contract

# 3. L2 集成与重放测试 (含 Squad/Journal Replay, Budget Cap, SSRF 校验)
npm run test:integration

# 4. L3 架构门禁校验 (全仓 Allowlist, 依赖白名单, 物理删除校验)
npm run test:gates
```

### 5.2 熔断与停止条件 (Halt / Stop Conditions)

遇到以下任意一种情况，**必须立即停止当前切片开发并 Halt 报告**，严禁编写 Fallback 掩盖或绕过：

1. **Fable ESM 编译失败**：Fable 无法正确编译 `smol-toml` 的 named ESM import (`stringify`)。
2. **库序列化行为异常**：`smol-toml` 未能将对象数组输出为标准 `[[table]]` 结构，或多行字符串换行符损坏。
3. **No Self-Parsing 破坏**：Typed Transport 尚未建立，业务流程尝试通过 `parse` TOML 文本来获取机器状态。
4. **越界上游修改**：重构要求修改上游仓库（如 `../oh-my-pi` 或 OpenCode 上游源码）。
5. **Mux 绑定破坏**：Mux 宿主适配器要求超出其现有 Binding 范围的破坏性改动。
6. **核心功能回归**：输出预算截断失败、Review 终止条件失效、Nudge 分类不准确或 Iterator 强绑定破坏。

---

## 6. 证据分类与零临时探针原则 (Evidence Categorization & No-Probe Invariant)

### 6.1 证据双向分类 (Two-Tier Evidence Categorization)

为保证交付报告真实可信，严格区分两类证据：

1. **当前可运行证据 (Current Runnable Evidence)**：
   - 指针对当前磁盘上**已完成编写并可通过测试命令真实运行**的模块所产生的测试输出。
   - 报告中引用此类证据时，必须贴出项目标准 runner 命令的真实运行结果日志。

2. **未来验收证据 (Future Acceptance Evidence)**：
   - 指整套 Universal TOML / KISS 重构计划在 Phase 5 全部完成后，预期通过终极验收四步命令所产生的合格证明。
   - 报告中声明此类证据时，必须显式标注 `[未来验收证据 / 待 Phase 5 运行]`，**严禁虚构**未运行命令的输出日志。

### 6.2 零临时探针原则 (No Temporary Probe Invariant)
- **禁令内容**：严禁使用 `node -e "..."`、一次性命令行脚本或未注册至项目 `.fsproj` 的临时 `probe.js` / `probe.fsx` 文件来进行能力验证或 bug 探针。
- **原因**：临时探针无法留存在 CI 门禁中，容易制造“本地临时 OK 但代码库腐烂”的幻觉。
- **强制落点**：任何探针逻辑或边界用例，**必须直接写成 `tests/` 下的正式 F# 测试用例**，注册进 `wanxiangshu.fsproj` 并由 `test runner` 统一驱动。

---

## 7. 实施出口与验证证据 (Implementation Exits & Verification Evidence)

### 7.1 实施出口 (Implementation Exits)
1. **测试文件落地**：
   - 新建 `tests/TomlSerializationTests.fs` （L0 库 FFI 单元测试）
   - 新建 `tests/PromptTomlTests.fs` （L0 Prompt 7 原语单元测试）
   - 新建 `tests/SubagentBatchReportTests.fs` （L1 Typed Transport 契约测试）
   - 新建 `tests/HostMetadataContractTests.fs` （L1 Metadata 原地写测试）
   - 新建 `tests/PromptArchitectureGatesTests.fs` （L3 精准 Allowlist 架构门禁）
2. **`package.json` 脚本落位**：
   - `npm run test:gates` 绑定已注册的 `PromptArchitectureGatesTests` runner 条目，不绑定临时文件路径或不存在的测试目标。
   - `npm run test:contract` 绑定 L1 契约测试套件。
   - `npm run test:integration` 绑定 L2 集成与重放测试套件。

### 7.2 验证证据 (Verification Evidence)
1. **当前可运行验证证据**：
   - 本次仅重写文档，未运行源码构建、测试或门禁；因此不存在可声称的运行时通过证据。
   - 本文只固定未来门禁的路径范围与断言，不把规范文本的存在误报为代码验证。
2. **未来验收证据 (待 Phase 5 终极运行)**：
   - 运行 `npm run test:gates`，断言 `src/` 中 `smol-toml.parse` 引用次数为 0，`smol-toml` 导入处为 1，旧文件为 0，`yaml` 导入数为 2。
   - 运行四步验收命令并保存真实日志；在命令实际通过前，禁止写“全量绿灯通过”。
