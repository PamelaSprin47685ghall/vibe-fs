# 03. 迁移路线与重构实施规范 (Migration Roadmap & Refactoring Implementation Spec)

## 1. 现状、目标、决策与未决风险

### 1.1 现状 (Current Status)
当前系统在 Prompt 与工具/事件展示中依赖 YAML front matter 生成、Markdown 结构标题与字符串拼接，并存在通过文本回读判断机器状态的函数（如 `hasDoubleCheckAnchor`、`isNudgePrompt`、`parseFrontMatter`）。这种模式将机器状态与模型展示视图混淆，违反了视图单向投影原则。

### 1.2 目标 (Goal)
将 `../docs/20.md` 重构计划压缩为短、闭合、可执行的迁移路线：
1. 建立基于 `smol-toml` 唯一序列化基座的强类型展示层。
2. 先建立 Typed Transport 消灭生产环境对自生成文本的反向解析（No Self-Parsing Invariant），再切换序列化格式。
3. 提供按 PR 切片执行的阶段路线图，定义明确的进入/退出条件、回滚边界与验证门禁。

### 1.3 关键决策 (Key Decisions)

| ID | 决策 | 说明 |
| --- | --- | --- |
| D1 | **唯一序列化出口** | 锁定 `smol-toml` `1.7.0`，生产环境仅在 `Toml.fs` 导出 `stringify`。 |
| D2 | **No Self-Parsing** | 生产环境零 `import parse`；机器逻辑与状态只读 typed event/metadata。 |
| D3 | **先消除回读，再换格式** | 优先建立 Typed Transport 数据链，解除业务逻辑对 Prompt/Tool 文本的依赖。 |
| D4 | **禁止 Shim 与双写** | 不保留 shim、feature flag、双写或旧格式 fallback；以 PR 切片为单位原子切换与提交回退。 |
| D5 | **配置 YAML 豁免** | AGENTS.md YAML 配置属于外部输入协议，保持既有 Codec，不纳入 Prompt 重构。 |

### 1.4 未决风险与应对 (Undecided Risks)
- **Fable ESM Named Import 兼容性**：`smol-toml` 在 Fable 构建环境下的 named import 导出可能存在打包异常。
  - *应对规则*：Phase 0 / Phase 2 必须优先通过 `tests/TomlSerializationTests.fs` 验证 ESM named import；若无法解析，触发停止条件，重新评估库构建配置，物理禁止退回手写格式化。
- **跨宿主 Metadata 同步差异**：非 OpenCode 宿主可能缺乏原生 `metadata` 字典字段。
  - *应对规则*：宿主不支持元数据时，统一退回为通过 WanEvent/NDJSON 事件流承载，严禁在 Prompt prose 中拼接文本标记作为 fallback。

---

## 2. 明确不改范围与上游约束 (Non-Goals & Upstream Constraints)

在实施迁移过程中，以下模块与上游协议属于**明确不改范围**，禁止越界修改：

1. **Mux Binding 契约**：
   - `resolveAgentFrontmatter` 是上游 agent descriptor API，属于 Host 层代理描述符声明，不属于模型 Prompt front matter。
   - 不改名、不改协议、不更改其 YAML 处理方式。
2. **OMP 上游约束**：
   - 严禁修改 `../oh-my-pi` 或上游 OMP 引擎核心 API 契约。
   - OMP 驱动与工具交互接口保持原样接入。
3. **OpenCode 上游约束**：
   - 不修改 OpenCode 上游 core plugin 接口与生命周期契约。
   - 系统状态仅在既有 `metadata.wanxiangshu` 扩展点原地读写。
4. **外部配置 YAML Codec**：
   - `FallbackConfigCodec.fs` 与 `ConfigReader.fs` 负责读取外部 `AGENTS.md` 配置，属于合法外部输入协议。
   - 保持 npm `yaml` 依赖，不改动配置格式。

---

## 3. 旧模块到新 Owner 的映射表 (Legacy Module Mapping)

| 旧模块 / 旧符号 | 迁移后新 Owner / 目标 | 变动类型 | 迁移说明 |
| --- | --- | --- | --- |
| `PromptHeader.fs`, `PromptFrontMatter.fs`, `Workspace/Yaml.fs` | `Kernel/Prompt/Document.fs`, `Runtime/Serialization/TomlValue.fs`, `Toml.fs`, `PromptToml.fs` | 物理删除 / 替代 | 彻底废弃 YAML FrontMatter 渲染，由强类型 Document 与 smol-toml 序列化基座替代 |
| `SubagentPrompts.fs`, `Subagent.fs`, `Methodology/Schema.fs` | `Kernel/Prompt/Document.fs` (Coder, Inspector, Browser, Meditator, Executor/Web Summarizer 构造器) | 重构 | 子代理提示词改由 `PromptDocument.create` 构造 7 原语结构 |
| `ReviewPrompts/*.fs`, `LoopMessages.fs` | `PromptDocument` + `ReviewSession` 状态机 | 重构 | 审查指令转为 PromptDocument；Review challenge 改由 typed event/state 记录 |
| `PromptFragments.fs`, `NudgeDerivation.fs` | `MessageOrigin` DU + `PromptDocument` | 重构 | 拆离机器来源与文本展示；Nudge 指令统一转为 PromptDocument |
| `SquadPrompts.fs` | `Kernel/Wanxiangzhen/SlavePromptSpec.fs` + `PromptToml` | 重构 | Kernel 仅产出强类型 Spec，Runtime 统一完成 TOML view 渲染 |
| `ToolOutputInfo.fs`, `ToolOutputInfoParse.fs` | `ToolOutputMessage` + `ToolOutputToml.fs` | 删除 Parser / 替代 | 生产环境传输强类型 `ToolOutputMessage`；物理删除 `ToolOutputInfoParse.fs` |
| `SubagentBatchSpawnCore.fs` | `BatchReport` typed composition + `[[reports]]` TOML view | 重构 | Iterator 与 body 在 `SubagentReport` 表中强绑定，消灭 Markdown `---` 拼接 |
| `SearchPrompts.fs`, `CapsFormat.fs` | `SearchPrompts` / `CapsFormat` 专属 TOML view | 重构 | 改用 `[[results]]` 与 `[[capabilities]]` 独立 TOML Schema |
| `Hosts/OpenCode/Pty*.fs` | `ToolOutputMessage` + `ToolOutputToml.fs` | 重构 | PTY 输出统一接入 ToolOutput 展示 Schema，不再伪造 Prompt 7 原语 |
| `SquadEventDisplayCodec.fs`, `PluginWanxiangzhenHooks.fs` | `SquadEventTomlView` | 重构 / 删 Decode | 改为单向 TOML 视图渲染；物理删除生产环境 decode 逻辑 |
| `hasDoubleCheckAnchor`, `isNudgePrompt`, `NudgeMessageClassifier.fs`, `MessageInspection.fs` | `MessageOrigin` / `ReviewChallengeState` (Typed Metadata / Event) | 物理删除 / 替代 | 消费方直接读取 Typed Metadata 或事件流，禁止正则与文本扫描 |

---

## 4. 六阶段 PR 切片迁移路线图 (Six-Phase PR Slice Roadmap)

### Phase 0: RED 基线 (RED Baseline)
- **目标**：建立完整的失败测试基线，锁定旧逻辑语义与新增类型的失败断言。
- **主要任务**：
  1. 编写 `tests/TomlSerializationTests.fs`：断言 escaping、Unicode、multiline `"""`、AoT 顺序、空值省略。
  2. 编写 `tests/PromptTomlTests.fs`：断言 7 根原语、DU 穷尽分支、非法构造拦截。
  3. 编写 Typed Transport 失败测试：断言 Batch iterator 关联、Review double-check 状态机、MessageOrigin 分类不依赖文本。
  4. 拆分已超 300 行的 `ArchitectureGatesTests.fs`，保持单一入口。
- **进入条件**：架构重构决策已冻结；所有开发人员对 7 原语 Schema 达成一致。
- **退出条件**：Phase 0 新增测试因目标功能未实现而集中失败；测试均已注册至 `wanxiangshu.fsproj`。
- **回滚边界**：未合并的 Phase 0 PR 直接闭合；无代码库污染。

### Phase 1: GREEN 强类型传输 (GREEN Typed Transport)
- **目标**：消灭生产环境文本回读，建立强类型数据传输链。
- **主要任务**：
  1. Tool output 与 Batch 聚合全面切换为 Typed Composition（`SubagentReport` / `ToolOutputMessage`）。
  2. Review challenge 进入 `ReviewSession` 状态机与事件流。
  3. Synthetic message 接入 `MessageOrigin` 与 host `metadata.wanxiangshu`。
  4. 物理删除生产环境 `hasDoubleCheckAnchor`、`isNudgePrompt` 等文本回读 API。
- **进入条件**：Phase 0 Typed Transport 失败测试已就位。
- **退出条件**：机器决策与状态流转零 Prompt/Tool 文本扫描；重放与契约测试通过。
- **回滚边界**：回退 Phase 1 提交；系统维持旧版文本传输与回读逻辑。

### Phase 2: GREEN 序列化基座 (GREEN Serialization Base)
- **目标**：引入 `smol-toml` 依赖，落地强类型 Prompt 与序列化出口。
- **主要任务**：
  1. 在 `package.json` 与 `build-package.json` 引入精确版本 `"smol-toml": "1.7.0"` 并锁 `package-lock.json`。
  2. 新建 `src/Kernel/Prompt/Document.fs`（PromptDocument 智能构造器）。
  3. 新建 `src/Runtime/Serialization/TomlValue.fs` 与 `Toml.fs`（唯一 `stringify` FFI）。
  4. 新建 `src/Runtime/Prompt/PromptToml.fs`。
- **进入条件**：Phase 1 完成；`smol-toml` 1.7.0 库依赖准备就绪。
- **退出条件**：Phase 0 库序列化与 Document 单元测试通过；生产环境零 `import parse`。
- **回滚边界**：卸载 `smol-toml` 依赖，移除新模块；回退至 Phase 1 终态。

### Phase 3: GREEN 指令 Prompt 迁移 (GREEN Instruction Prompts)
- **目标**：按切片将所有指令 Prompt 生成器重构为 `PromptDocument` + `PromptToml`。
- **主要任务**：
  1. 切片 3a：Subagents（Coder / Inspector / Browser / Executor Summarizer / Web Summarizer / Meditator）。
  2. 切片 3b：Review / Loop 消息重构。
  3. 切片 3c：Nudge 消息重构。
  4. 切片 3d：Squad worker (`SlavePromptSpec`) 重构。
- **进入条件**：Phase 2 序列化基座测试全绿。
- **退出条件**：所有 Prompt 均由 `smol-toml` 生成；根节点仅含 7 原语；无 Markdown fence / YAML front matter。
- **回滚边界**：按切片 PR 独立回退；未迁移的 Prompt 保持旧 Header 渲染。

### Phase 4: GREEN 工具与事件展示迁移 (GREEN Tool & Event Presentation)
- **目标**：将工具结果、搜索能力与 Squad 事件视图切换为专属 TOML 投影。
- **主要任务**：
  1. 切换 ToolOutput / BatchReport 为 `ToolOutputToml` 渲染。
  2. 切换 Search / Fetch / Caps 为专属 `[[results]]` / `[[capabilities]]` TOML 渲染。
  3. 切换 PTY 读写输出为 ToolOutput TOML。
  4. 切换 Squad event 为单向 `SquadEventTomlView`，物理删除生产 `SquadEventDisplayCodec` 解码器。
- **进入条件**：Phase 3 指令 Prompt 迁移完成。
- **退出条件**：所有展示文本均由 `smol-toml` 产生；Squad decode 生产调用为零；输出体积预算无回归。
- **回滚边界**：回退 Phase 4 提交；展示层恢复旧格式。

### Phase 5: 物理清扫与门禁重置 (Physical Cleanup & Gates Enforcement)
- **目标**：清理旧文件与废弃 API，开启全新架构门禁。
- **主要任务**：
  1. 物理删除 `PromptHeader.fs`、`PromptFrontMatter.fs`、`Workspace/Yaml.fs`、`ToolOutputInfoParse.fs` 及其 fsproj 节点。
  2. 删除 `yamlField`、`frontMatter*`、`promptHeader*` 等旧 Helper 符号。
  3. 更新工具描述、README 与文档中的“YAML front matter”表述为“TOML metadata”。
  4. 运行 `tests/PromptArchitectureGatesTests.fs` 门禁检查。
- **进入条件**：Phase 1–4 所有功能切片测试全绿。
- **退出条件**：架构门禁全绿；源码零旧 Prompt API 引用；零 Stub / Alias / Obsolete Wrapper。
- **回滚边界**：重新恢复被删文件；架构门禁退回松散模式。

---

## 5. 切片管理与阶段门禁原则 (Slice Management & Stage Gate Principles)

### 5.1 禁止 Fallback / Shim / 双写原则
1. **原子切换**：每个切片必须实现 clean cutover；禁止在生产代码中保留新旧格式双写、Feature Flag 或运行时兼容 Shim。
2. **异常拦截**：序列化或构造失败必须直接抛出错误或返回 `Error`，禁止静默 fallback 至旧格式文本。
3. **回退单位**：阶段回退的唯一手段是 Git Commit Revert，不得靠运行时降级分支承载回退。

### 5.2 立即停止条件 (Immediate Stop Conditions)
若在迁移过程中发生以下任一情况，必须**立即停止当前切片**，禁止强行掩盖：
- Fable 打包环境无法正确解析 `smol-toml` 命名导出 (`stringify`)。
- `smol-toml` 库无法将 Table 列表正确渲染为 TOML `[[array-of-tables]]` 语法。
- 业务流程要求在 Typed Transport 未建立前通过解析 TOML 文本来驱动逻辑。
- 迁移要求修改 `../oh-my-pi`、OMP 核心或 OpenCode 上游插件契约。
- 出现输出体积超预算、Review 终止失效或 Nudge 分类错乱等功能回归。

---

## 6. 实施出口与验证证据 (Implementation Exits & Verification Evidence)

### 6.1 实施出口
1. **源码出口**：
   - 新增：`src/Kernel/Prompt/Document.fs`
   - 新增：`src/Runtime/Serialization/TomlValue.fs`
   - 新增：`src/Runtime/Serialization/Toml.fs`
   - 新增：`src/Runtime/Prompt/PromptToml.fs`
   - 新增：`src/Runtime/Tooling/ToolOutputToml.fs`
   - 删除：`src/Runtime/PromptHeader.fs`
   - 删除：`src/Runtime/PromptFrontMatter.fs`
   - 删除：`src/Runtime/Workspace/Yaml.fs`
   - 删除：`src/Runtime/Tooling/ToolOutputInfoParse.fs`
2. **命令出口**：
   - 执行 `npm run test:gates` 验证新架构门禁。
   - 执行 `npm run build-and-test` 验证 Fable 编译与单元测试。
   - 执行 `npm run test:contract` 与 `npm run test:integration` 验证宿主契约与集成流程。

### 6.2 验证证据

| 验证项 | 验证手段 | 预期断言结果 |
| --- | --- | --- |
| **No Self-Parsing 门禁** | `PromptArchitectureGatesTests` | 生产代码（`src/`）中 `smol-toml.parse` 引用次数为 `0`；`parseFrontMatter` 引用次数为 `0` |
| **唯一 FFI 出口** | `PromptArchitectureGatesTests` | `smol-toml` 库 import 仅存在于 `Runtime/Serialization/Toml.fs` 与测试 assertion helper 中 |
| **YAML 配置白名单** | `PromptArchitectureGatesTests` | npm `yaml` 库生产 import 仅限 `FallbackConfigCodec.fs` 与 `ConfigReader.fs` |
| **旧文件物理清扫** | 文件系统与 `wanxiangshu.fsproj` 校验 | 4 个旧文件在磁盘与 fsproj 中不存在 |
| **7 原语覆盖** | `PromptTomlTests` | 任意指令型 Prompt 结构只包含 7 个根键，顺序固定 |
| **Typed Transport 独立性** | `ReviewTests` / `NudgeTests` | 修改 Prompt 中的 prose 不影响状态机跳转或 Nudge 分类逻辑 |
| **全套宿主集成** | `test:integration` | OpenCode / Mux / OMP 所有集成用例全部绿色通过 |
