# Universal TOML 重构计划

状态：实施前决策冻结。本文是执行规格，不是愿景说明。附录 A–G 与正文同属规范；它们保存旧版完整论证、样例、审计与类型设计，禁止再次摘要删除。

## 1. 目标与完成定义

目标：所有模型可见的结构化提示与工具展示，由强类型数据单向投影并交给标准 TOML 库序列化；运行时不再从自己生成的文本中取回状态。

完成必须同时满足：

1. Prompt 生产者不再生成 YAML front matter、Markdown 结构标题或 TOML 字符串片段。
2. TOML 文本只由 `smol-toml.stringify` 产生；无手写转义、键值拼接、围栏包装或尾部修整。
3. Prompt、工具结果、事件展示在渲染前均为 F# Record/DU；内部组合不经过 `string -> parse -> data`。
4. `PromptHeader.fs`、`PromptFrontMatter.fs`、`Workspace/Yaml.fs`、`ToolOutputInfoParse.fs` 物理删除。
5. AGENTS.md 配置仍为 YAML；配置协议与模型展示协议彻底分域。
6. OpenCode、Mux、OMP、Mimocode、Wanxiangzhen 的既有行为契约与正式测试全部通过。

## 2. 冻结决策

| ID | 决策 | 理由 |
| --- | --- | --- |
| D1 | 唯一 TOML 库为 `smol-toml`，首个实现锁定精确版本 `1.7.0` | 当前项目为 ESM；该库原生 ESM、维护活跃、支持 TOML 1.1，并提供命名导出 `stringify` |
| D2 | `package.json`、`build-package.json` 同时声明精确版本；`package-lock.json` 锁完整依赖图 | 本地构建与 build 目录发布包使用不同 manifest |
| D3 | 生产适配器只导入 `stringify`，不导入 `parse` | 类型构造保证合法形状；生产期回读自己生成的文本违反单向视图边界 |
| D4 | “统一 TOML”只统一序列化技术，不合并领域 Schema | Prompt、工具输出、事件展示生命周期不同；强塞进一个万能 Record 会制造非法状态 |
| D5 | 7 个根原语只属于指令型 Prompt | 工具元数据与事件不是 agent 指令，不伪装成 `objective` / `agent_role` |
| D6 | AGENTS.md YAML 配置不迁移 | `FallbackConfigCodec.fs`、`ConfigReader.fs` 读取外部配置，属于合法输入协议，不是 Prompt 展示 |
| D7 | 不保留 shim、feature flag、双写或旧格式 fallback | 分阶段开发，最终原子切换；失败靠未合并提交回退，不在生产留下双轨复杂度 |

库选型依据：

- https://www.npmjs.com/package/smol-toml
- https://github.com/squirrelchat/smol-toml
- https://www.npmjs.com/package/@iarna/toml

若 `1.7.0` 在实际 registry 不可解析，停止实施并更新本 ADR；禁止静默换库，禁止退回徒手格式化。

## 3. 精确公理

### 3.1 禁止自解析，不禁止协议解析

禁止：系统先拥有强类型事实 A，渲染成文本 B，随后扫描 B 重建 A。

允许：

- 外部 AGENTS.md YAML → 配置类型。
- 外部 NDJSON →事件类型。
- 宿主原生 JSON/tool-call →边界 DTO →领域类型。
- 不可信模型文本的专用恢复 codec；它不得成为正常状态来源。
- 测试使用 `smol-toml.parse` 验证渲染语义。

因此原“Zero-Parsing Axiom”改名为 No Self-Parsing Invariant。禁令对象是自生成视图，不是所有字符串 API。

### 3.2 零徒手 TOML

禁止在生产代码中：

- 拼接 `key = value`、`[[table]]`、引号、换行或 TOML 围栏。
- 自定义 escape、multiline、array-of-table 排版。
- `StringBuilder`、`sprintf`、插值字符串生成 TOML 语法。
- 在 `stringify` 输出前后追加 Markdown/YAML；输出必须原样返回。

允许：构造普通 JS object。对象构造是数据投影，不是文本格式化；在 TOML 序列化链内，动态 `obj` 只能存在于唯一 Fable FFI 适配器。宿主 DTO codec 的既有动态边界不受此句误伤。

### 3.3 Pure TOML 的含义

- 文档首字节即 TOML；无 `---`、无 Markdown fence。
- 结构由 TOML key/table 表达；自然语言、源码、网页正文可作为不透明字符串值存在。
- 不承诺库选择双引号还是多行字符串；只承诺解析后的语义与稳定字段顺序。

## 4. 目标架构

### 4.1 模块边界与编译顺序

按下列顺序加入 `wanxiangshu.fsproj`：

1. `src/Kernel/Prompt/Document.fs`：Prompt 领域 DU/Record、智能构造器；零 Fable。
2. `src/Runtime/Serialization/TomlValue.fs`：受限 TOML 值代数；零 npm FFI。
3. `src/Runtime/Serialization/Toml.fs`：`TomlValue -> JS object -> smol-toml.stringify`；唯一动态出口。
4. `src/Runtime/Prompt/PromptToml.fs`：`PromptDocument -> TomlValue -> string`。
5. `src/Runtime/Tooling/ToolOutputToml.fs`：`ToolOutputMessage -> TomlValue -> string`。
6. 各 bounded context 的投影模块；它们只能返回 `TomlValue` 或领域 Document，不能接触 npm 库。

唯一 FFI 形状：

```fsharp
[<Import("stringify", "smol-toml")>]
let private stringifyNative (value: obj) : string = jsNative
```

`TomlValue` 仅含 String、Integer、Boolean、StringArray、TableArray、Table。`None` 表示省略键；类型层不提供 null、undefined、浮点特殊值与异构数组。DU → wire string 必须穷尽匹配，禁止 `ToString().ToLowerInvariant()`。

### 4.2 指令型 Prompt Schema

固定根键：

1. `objective`：必填非空字符串。
2. `background`：可选非空字符串。
3. `agent_role`：由封闭 `AgentRole` DU 产生。
4. `targets`：目标 DU 列表。
5. `boundaries`：禁止行为 DU 列表。
6. `rules`：规则 DU 列表。
7. `outcomes`：必填非空结果要求列表。

根键顺序固定为上述顺序；空可选集合省略。`PromptDocument` 构造器为 private，`create` 返回强类型错误，拦截空 objective、空 outcomes、空文本与重复 outcome label。

目标不是“通用可空字段袋”。目标使用和类型表达合法形状：

- `FileTarget(path, guide, draft)`
- `FileReference(path)`
- `EntryTarget(pathOrSymbol)`
- `QueryTarget(query)`
- `CommandTarget(language, program, dependencies, timeout)`
- `EvidenceTarget(label, content)`
- `TodoTarget(content)`

边界使用 `DoNotRead`、`DoNotModify`、`DoNotExecute`、`DoNotTouch`；读/改/触碰边界携带 `BoundaryTarget.File` / `Directory` / `PathOrSymbol`，因此不会把目录降级成普通 path。规则使用 `Policy`、`Constraint`、`Criterion`、`Question`、`Contract`。每个 case 有显式 wire 映射。

### 4.3 其他展示 Schema

格式复用不等于知识合并：

- Tool output 保留 `ToolOutputMessage` / `InfoItem`。TOML 根为 `body` 与 `[[info]]`；`Hint`、`Syntax`、`Iterator`、`Status`、`ExitCode` 由 DU 穷尽投影。
- Search、Fetch、Caps 各自保留领域 Record，分别投影 `[[results]]`、fetch 字段、`[[capabilities]]`。
- PTY 复用 Tool output Schema，不再把任意键塞进 Prompt Schema。
- Squad event 由 `SquadEvent` 单向投影为 TOML view；持久化仍走 WanEvent/NDJSON。
- 配置继续走 YAML codec；不得调用 TOML 展示层。

## 5. 真实迁移面

旧“9 处”清单作为历史审计完整保留于附录 D，但它不是完整执行清单。实际迁移按知识边界分组：

| 组 | 当前文件 | 目标 |
| --- | --- | --- |
| 基础输出 | `Runtime/PromptHeader.fs`、残留 `Runtime/PromptFrontMatter.fs`、`Runtime/Workspace/Yaml.fs` | 新序列化层落地后删除 |
| 子代理 | `Runtime/SubagentPrompts.fs`、`Runtime/Subsession/Subagent.fs`、`Kernel/Methodology/Schema.fs` | Coder、Inspector、Browser、Meditator、Executor/Web summarizer 全部构造 `PromptDocument`；Continue 的用户自由文本保持不透明输入 |
| 审查 | `Runtime/ReviewPrompts/*.fs`、`Runtime/Execution/LoopMessages.fs` | task/report/verdict/double-check 进入强类型 projection |
| Nudge | `Runtime/PromptFragments.fs`、`Runtime/Nudge/NudgeDerivation.fs` | typed origin + PromptDocument；不靠 prose marker |
| Squad worker | `Kernel/Wanxiangzhen/SquadPrompts.fs` | Kernel 只产 `SlavePromptSpec`；Runtime 负责 TOML view |
| 工具信封 | `Runtime/Tooling/ToolOutputInfo.fs`、`ToolOutputInfoParse.fs` | 全程传 `ToolOutputMessage`，最终边界渲染一次；删除 parser |
| Batch 报告 | `Runtime/Subsession/SubagentBatchSpawnCore.fs` | `formatBatchReports` 接收 typed report，不解析 iterator 文本 |
| Search/Caps | `Runtime/Search/SearchPrompts.fs`、`Runtime/Tooling/CapsFormat.fs` | 专属 typed TOML projection |
| PTY | `Hosts/OpenCode/PtyReadOutput.fs`、`PtySpawn.fs`、`PtySpawnCommon.fs`、`PtyWriteTool.fs` | 构造 ToolOutputMessage 后统一渲染 |
| Squad 展示 | `Runtime/Wanxiangzhen/SquadEventDisplayCodec.fs`、`Hosts/OpenCode/PluginWanxiangzhenHooks.fs` | 改为单向 `SquadEventTomlView`；删除生产 decode |
| 自解析状态 | `LoopMessages.hasDoubleCheckAnchor`、`Kernel/Nudge/TodoStatus.isNudgePrompt`、`Runtime/Nudge/NudgeMessageClassifier.fs`、`Hosts/OpenCode/Fallback/MessageInspection.fs` | 改读 typed event/metadata，不扫描文本 |
| 对外文案 | `Kernel/ToolCatalog/Search.fs`、`Kernel/ToolCatalog/Subagent.fs`、`Kernel/FuzzyQuery.fs`、`Hosts/Omp/OmpToolSchema.fs`、README/docs | “YAML front matter” 改为 “TOML metadata” |
| 配置豁免 | `Runtime/Fallback/FallbackConfigCodec.fs`、`Runtime/Wanxiangzhen/ConfigReader.fs` | 保持 YAML 输入；不进入 Prompt 重构 |

Mux 的 `resolveAgentFrontmatter` 是上游 agent descriptor API，不属于模型 Prompt front matter，不改名、不改协议。

## 6. 先消灭文本回读

仅把 YAML 换成 TOML 而保留 parser，等于换皮。序列化迁移前先完成三条数据链：

1. Batch：spawn 已同时拥有 child ID 与 report；内部返回 typed `{ iterator; body }`，聚合器直接组合，最后渲染一次。
2. Review double-check：新增 ReviewSession 命令/事件/状态表示 challenge 已发出；`ReviewTools.fs` 查询状态，不读 session text。
3. Synthetic message：定义 `MessageOrigin = Human | TodoNudge | ReviewNudge | RunnerNudge | CompactionNudge | ForceStop | FallbackContinuation`。发送前记录 typed origin；宿主支持 metadata 时写 `metadata.wanxiangshu`，消费者读 metadata/事件状态。禁止 prose fallback。

`scanToolCallAsText` 处理不可信模型输出，属于显式恢复 codec，暂不在本迁移删除；它不得处理本系统渲染的 TOML。

## 7. TDD 迁移顺序

### Phase 0：RED 基线

1. 为库 escaping、Unicode、multiline、array-of-tables、空可选值、字段顺序写正式失败测试。
2. 为 Batch iterator、Review double-check、Synthetic origin 写不依赖文本标记的失败测试。
3. 为各旧 producer 写语义 characterization：保留事实，不锁旧 YAML 字节。
4. 先把现有 300+ 行 `ArchitectureGatesTests.fs` 按 size/layer/project 三个知识边界拆成小文件，保留单一聚合入口；新 Prompt gate 不得建立在既有红线文件继续腐烂之上。

退出条件：测试因新类型/行为尚不存在而失败；测试均注册进 `wanxiangshu.fsproj` 与现有 test entry。

### Phase 1：GREEN typed transport

1. Tool output 与 Batch 聚合改为 typed composition。
2. Review challenge 进入状态机/事件流。
3. Synthetic origin 进入 host metadata/运行时状态。
4. 删除生产回读调用，再删 parser/classifier 的废弃函数。

退出条件：相关单元、契约、重放测试通过；机器决策不再读取 Prompt prose/YAML。

### Phase 2：GREEN 序列化基座

1. 添加 `package.json`、`build-package.json`、`package-lock.json` 三份依赖落点与新模块。
2. 实现 `TomlValue` 唯一 JS 投影及 named-import stringify。
3. 实现 PromptDocument 智能构造器与 PromptToml。
4. 测试侧直接 import `smol-toml.parse`，断言解析后的结构；生产侧零 parse。

退出条件：Phase 0 库/Schema 测试通过；canonical snapshot 固定库字节输出；无手写 TOML。

### Phase 3：GREEN 指令 Prompt

依次迁移 Subagent（Coder / Inspector / Browser / Executor summarizer / Web summarizer / Meditator）→ Review/Loop → Nudge → Squad worker。每个切片先改测试，再改 producer；共享 producer 一次迁移覆盖所有宿主，不复制 host 版本。

退出条件：所有 Prompt 可被测试 parser 解析；根键只含 7 原语；无 TOML 外层正文。

### Phase 4：GREEN 工具与事件展示

依次迁移 ToolOutput → Search/Fetch/Caps → PTY → Squad event view。Squad display decoder 生产调用为零后物理删除。

退出条件：展示文本均由库产生；typed metadata 在渲染前完整可用；输出体积预算未回归。

### Phase 5：物理清扫

1. 删除四个旧文件及对应 fsproj 节点。
2. 删除 `yamlField`、`yamlSeqField`、`yamlStringSeqField`、`frontMatter*`、`promptHeader*`、旧 parse API。
3. 更新工具描述、README、相关 docs 与旧 YAML 断言。
4. 保留 npm `yaml`，仅供两个配置 codec。

退出条件：架构门禁绿；源码无旧 Prompt API 引用；无 stub、alias、obsolete wrapper。

## 8. 正式测试矩阵

| 能力 | 测试落点 | 必须覆盖 |
| --- | --- | --- |
| 库 FFI | `tests/TomlSerializationTests.fs` | named import、引号、反斜杠、换行、中文、AoT、顺序、unsupported value 不可构造 |
| Prompt Schema | `tests/PromptTomlTests.fs` | 7 根键、DU 全分支、None 省略、非法空值、canonical snapshot |
| Tool output | `tests/ToolOutputInfoTests.fs` | typed compose、重复 info、iterator、body、exit code；删除 parse round-trip 用例 |
| Subagent | `tests/SubagentPromptsTests.fs`、`SubagentPromptBuildTests.fs` | 六类结构化 prompt 的语义结构与只读/可写规则；Continue 自由文本不被伪结构化 |
| Review | `tests/ReviewTestsPrompts.fs`、`ReviewPromptsFormatTests.fs`、ReviewSession tests | verdict、report、affected files、double-check typed state |
| Nudge | `tests/AgentNudgeSpecs*.fs`、`NudgeTodoStatusTests.fs` | typed origin；正文变化不改变分类 |
| Search/Caps/PTY | 现有 Executor/Caps/Fuzzy/PTY suites | 专属 Schema、特殊字符、大输出预算 |
| Squad | `tests/Wanxiangzhen/SquadPromptsTests.fs`、`EventCodecTests.fs` | worker prompt、单向 event view；持久重放仍只读 NDJSON |
| Hosts | 现有 OpenCode/Mux/OMP integration suites | 实际发送文本可解析、metadata 原地修改、review/nudge 生命周期 |

禁止临时探针。所有发现的边界用例必须落进上述正式测试并进入标准 runner。

## 9. 架构门禁

新增 `tests/PromptArchitectureGatesTests.fs`，不要继续膨胀已超 300 行的 `ArchitectureGatesTests.fs`。门禁检查：

1. `smol-toml` 只可在 `Runtime/Serialization/Toml.fs` 和测试 assertion helper 中 import。
2. 生产源码禁止 import `smol-toml.parse`。
3. npm `yaml` 生产 import 白名单仅 `FallbackConfigCodec.fs`、`ConfigReader.fs`。
4. `PromptHeader.fs`、`PromptFrontMatter.fs`、`Workspace/Yaml.fs`、`ToolOutputInfoParse.fs` 必须不存在。
5. 旧 helper 符号、`CapsYamlItem`、Prompt builder 中的 `---` / `# Task` 必须不存在。
6. Prompt projection 禁止 `StringBuilder`、`escapeToml*`、`ToString().ToLowerInvariant()` 与 TOML 语法字面量。
7. `hasDoubleCheckAnchor`、`isNudgePromptText`、`isNudgePrompt`、依赖固定 prose 的 synthetic classifier 必须不存在。
8. 新文件全部出现在 fsproj 正确层次；每个测试出现在 runner entry。

门禁扫描明确路径与 allowlist；不使用“全仓禁字符串”误伤配置、文档、合法外部 codec。

触发时机：Phase 1–4 每完成一个 slice 即运行该 slice 的新增 gate；Phase 5 删除完成后运行完整 `test:gates`。门禁不是等全部改完才第一次执行。

## 10. 最终验收与停止条件

最终命令只在全部切片完成后运行一次。`build-and-test` 已执行 Fable build、postbuild 与 L0，因此不再单独重复 `test:unit`：

1. `npm run build-and-test`
2. `npm run test:contract`
3. `npm run test:integration`
4. `npm run test:gates`

验收断言：

- Prompt renderer 数量 = 1；TOML npm FFI 生产入口数量 = 1。
- 自生成文本的生产 parser 数量 = 0。
- Prompt YAML serializer 与 fence 数量 = 0。
- 配置 YAML parser 数量 = 2，且仅在白名单。
- 7 根原语对指令 Prompt 覆盖完整；不存在伪造 agent role 的工具/事件文档。
- 所有 DU 投影穷尽；新增 case 会造成编译失败或 gate 失败。
- 所有宿主测试通过；OpenCode hook metadata 继续原地修改字段。

任一情况立即停止当前切片，不以 fallback 掩盖：

- Fable 无法解析 `smol-toml` named ESM import。
- 库未把对象数组输出为合法 array-of-tables。
- typed transport 尚未建立却需要解析 TOML 才能继续业务流程。
- Prompt 迁移要求改 `../oh-my-pi` 或 OpenCode 上游。
- Mux 需要超出 binding 的核心改动。
- 输出预算、review 终止、nudge 分类、iterator continuation 任一正式回归失败。

发布前只允许一个实现路径。回退单位是提交，不是运行时兼容层。

## 附录 A：第一性原理完整推导

### A.1 软件设计与复杂度压缩

软件设计的任务不是制造框架，而是把不可消除复杂度压成不可再短的充分描述。旧 Prompt 路径把角色、任务、背景、输入、禁区、规则、交付物混进 Markdown 标题、YAML 围栏、自由 prose 与手写字符串标签；同一事实随后又被历史扫描器、正则或 substring 找回。读者与机器同时承担偶然复杂度。

目标架构遵循五条因果链：

1. 强类型数据先于文本；文本只是最终视图。
2. 领域边界先于格式复用；相同 TOML 库不代表相同 Schema。
3. 纯函数投影先于字符串拼接；格式细节交给标准库。
4. 事件与 metadata 保存机器事实；自然语言只服务模型理解。
5. 架构门禁替代口头纪律；旧 helper 物理删除而非留壳。

### A.2 视图与真相绝对解耦

原 Zero-Parsing Axiom 的不可丢核心：Prompt text、工具展示、对话历史是单向输出视图，不得反向充当 SSOT。当前正文把它精确收窄为 No Self-Parsing Invariant，因为“禁止所有解析”会错误禁止合法外部协议 codec。

设系统已有强类型事实 A，渲染函数为 R，文本视图 B = R(A)。非法链为：

```text
A -> R -> B -> regex/YAML/Substring/Split -> A'
```

即使 A' 当前等于 A，这条链仍复制知识：字段名、围栏、顺序、转义、空值语义同时存在于 renderer 与 parser。任一端漂移即产生静默分叉。合法链只有：

```text
A -> pure projection -> TOML library -> B
A -> event/metadata -> typed consumer
```

配置文件、NDJSON、宿主 JSON、模型不可信恢复文本从系统外进入，解析是边界职责；系统自己生成 B 后再解析 B，不是边界，是遗忘 A 后付费找回。

### A.3 强类型海关拦截

LLM 意图、待办、审查提交与 verdict 必须在宿主 Tool Call 边界经 Zod/JSON Schema 校验，直接收敛为 F# Record/DU。禁止把已经结构化的参数降级成 `prompt: string`，再由下层搜索 `objective`、`report`、`iterator` 或 `verdict`。

类型边界分三层：

1. Host DTO：只处理宿主字段名、null/undefined、动态对象。
2. Domain DU/Record：表达合法任务、规则、事件、状态与结果。
3. Presentation projection：把 Domain 值映射为 `TomlValue`，再由库输出字符串。

动态 `obj` 只允许出现在 Host DTO codec 与 TOML FFI 的窄边界，不得渗入 Prompt 领域模型。新增 DU case 必须迫使所有投影穷尽匹配；禁止万能 `_` 分支吞掉未来状态。

### A.4 事件溯源 SSOT

跨轮次记忆、With-Review 激活状态、待办快照、方法论选择与 Wanxiangzhen DAG 状态依赖 `.wanxiangshu.ndjson` 重放，不依赖 `session.messages` 文本切片。Compaction 可以裁剪上下文，不能裁剪 durable truth。

本重构新增的 Review challenge 与 Synthetic message origin 必须进入现有 typed state/event 或宿主 metadata。禁止为了“少改状态机”继续扫描 `double-check`、`command: with-review`、todo nudge prose。文本变化不应改变机器状态。

### A.5 Markdown Prompt Drift 根源

自由 Markdown 的问题不是 Markdown 本身不可读，而是结构职责不清：

1. 标题只有建议性，无 schema 约束；同一概念可写成 Task、Objective、Goal。
2. 多层 `#` 容易与嵌入源码、网页正文、worker report 的 Markdown 混淆。
3. 角色、规则与交付要求散落在 prose，调用方无法穷尽检查。
4. Host-specific tail 在 stringify 后追加，制造“半结构化头 + 自由尾”双协议。
5. 模型可能模仿标题返回格式，但机器又不应依赖这种模仿。

Pure TOML 只替代结构，不禁止字符串值内部携带源码、Markdown 网页或报告。结构与内容必须分层：结构由库输出的 keys/tables 承载；内容作为 opaque string。

### A.6 消息即配置的适用边界

“Message as Config”仅适用于系统构造的结构化指令视图。用户在 Continue 中输入的自由问题不是配置，不应强塞 7 根键；外部模型报告也不是可信配置。统一格式不能抹掉数据来源与生命周期。

## 附录 B：不可约 7-Primitive Schema 完整论证

### B.1 归约图

```text
Legacy prompt vocabulary
task, question, what_to_summarize, entries, affected_files,
do_not_touch, verification_policy, citation_rule, contract_rule,
criteria, report, verdicts, raw_results, program, caps, todos ...
                              |
                              v
+-------------------------------------------------------------+
| Instruction PromptDocument                                  |
|                                                             |
| Root scalars             Root arrays-of-tables               |
| objective                [[targets]]                         |
| background               [[boundaries]]                      |
| agent_role               [[rules]]                           |
|                          [[outcomes]]                        |
+-------------------------------------------------------------+
```

7 原语不是“全系统万能对象”，而是指令 Prompt 的根级语义基。Tool output、Search、Caps、Event 各有独立 Schema，只复用 `TomlValue` 与 stringify。

### B.2 三个根标量

1. `objective`：将要达成的终态。归并旧 `task`、单数 `question`、`what_to_summarize`。必填、非空。
2. `background`：已存在语境、原因、阻塞、先验。可选；存在时非空，不重复 objective。
3. `agent_role`：由封闭 `AgentRole` DU 产生的角色与权能。`agent_mode` 不再作为独立布尔或字符串。

### B.3 四个根表数组

1. `[[targets]]`：正向输入与目标。旧 `entries`、`affected_files`、program、raw output、todos 按具体 Target DU 映射。
2. `[[boundaries]]`：负向领土。旧 `do_not_touch`、forbidden action 映射为 DoNotRead/Modify/Execute/Touch。
3. `[[rules]]`：policy、constraint、criterion、question、contract。规则有名字、有 kind，不与边界混合。
4. `[[outcomes]]`：要求代理产出的报告、tool call、verdict option 或 next action；至少一项。

### B.4 Legacy key → 7 原语无损迁移表

| Legacy fact | 新位置 | 规则 |
| --- | --- | --- |
| `task` | `objective` | Browser/loop/squad 任务直接成为 objective |
| `question` / `what_to_summarize` | `objective` | 单一求解目标 |
| `objective` | `objective` | 原值保留 |
| `background` | `background` | 原值保留 |
| role prose / mutating flag | `agent_role` | 由 AgentRole DU 统一产生 |
| coder `targets` | `FileTarget` | 保留 path、guide、draft |
| `entries` | `EntryTarget` | 每项独立 table |
| `affected_files` | `FileReference` | 只表达 reviewer 应读取的文件，不伪装待修改文件 |
| executor language/program/dependencies/timeout | `CommandTarget` | 同一 case 携带相关字段，禁止拆散 |
| raw executor/web output | `EvidenceTarget` | label + content |
| `do_not_touch` | `DoNotTouch` | 每项独立 boundary |
| inspector `questions` | `Rule.Question` | 顺序保留 |
| read-only/tool policies | `Rule.Policy` / `Rule.Constraint` | 不再作为 TOML 外 prose |
| report requirement | `Outcome(report, ...)` | 指令要求，不是已发生事实 |
| PERFECT/REVISE options | `Outcome` | 仅 reviewer instruction 使用 |
| `review_loop_id` / `prompt_origin` | 不进入 Prompt | typed metadata/event；机器 provenance 不交文本承担 |
| `review_round` / latest feedback | `background` / `EvidenceTarget` | 仅模型确需时展示 |
| result `verdict` | Tool/Event output Schema | 已发生结果，不是 instruction root |

### B.5 正交性检验

候选不变量：

- target ∩ boundary = ∅：一个描述应处理/观察的对象，一个描述禁止越过的领土。
- rule ∩ boundary = ∅：规则决定如何判断，边界决定哪些操作不可发生。
- outcome ∩ target = ∅：target 是输入流，outcome 是要求的输出流。
- objective ∩ background = ∅：objective 指未来，background 指当前/过去。

旧版曾声称“存在左逆 G，因此 100% 无损”。该结论只有在 B.4 映射表及每个 Target DU 保留全部字段时才成立；本计划不把它当已证明定理。Phase 0 characterization tests 必须逐项证明语义保留。无法映射的字段必须新增正确领域 case，禁止塞入 `text` 逃避建模。

### B.6 交叉对偶补全矩阵

| 概念 | Coder | Inspector | Reviewer | Executor/PTY | 结论 |
| --- | --- | --- | --- | --- | --- |
| boundaries | do_not_touch | read exclusions | modify exclusions | execute exclusions | 每类 agent 都能显式表达禁区 |
| questions | implementation uncertainty | required questions | review doubts | environment uncertainty | Question 是 rule，不是 objective 别名 |
| criteria | target acceptance | search completeness | review criteria | timeout/output policy | Criterion/Policy 按语义区分 |
| targets | files | entries | affected files/report evidence | command/output | 相同根类别，具体 DU 不同 |
| outcomes | change report | investigation report | PERFECT/REVISE tool verdict | summarized result/status | outcome 是要求，不是运行时状态 |

### B.7 Key 与 Value 规范的精确边界

- Schema key 使用 `snake_case`；根键固定，nested key 由 DU projection 固定。
- 自然语言 prose 使用地道 English，不用 `read_think_edit` 之类机器拼词。
- 路径、命令、标识符、代码、URL 可合法包含 `_` 与 `-`；禁止连字符的规则不得误伤 opaque data。
- 不使用 `[section]` 表组织 Prompt 根语义；集合使用对象数组，由库自然输出 array-of-tables。
- 不手工要求 triple-quoted string。多行选择由 `smol-toml` 决定，测试只锁语义与必要字节快照。

## 附录 C：全场景 TOML 派生样例

以下样例保存旧版每个场景的具体信息。它们描述解析后的语义形状，不授权手写这些文本；实际字节只能来自 `smol-toml.stringify`。

### C.1 Coder

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
    PromptToml.render (coderProjection intent)
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

### C.2 Inspector

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

### C.3 Browser

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

### C.4 Reviewer

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
text = "Full use of language features, correct algorithms, and correct data structures."

[[rules]]
kind = "criterion"
text = "Minimal complexity, no dead code, garbage code, or legacy wrappers."

[[rules]]
kind = "question"
text = "Did the implementation completely avoid manual string parsing at call sites?"

[[outcomes]]
label = "report"
text = "Return a verdict and the evidence supporting it."

[[outcomes]]
label = "PERFECT"
text = "Accept submission without required changes."

[[outcomes]]
label = "REVISE"
text = "Reject submission and return detailed actionable feedback."
```

### C.5 Executor summarizer

```toml
objective = "Summarize the executor output while preserving stack traces and exit status"
agent_role = "Executor Output Summarizer (read-only)"

[[targets]]
kind = "command"
language = "shell"
program = "npm run test"
dependencies = []
timeout_type = "long"

[[targets]]
kind = "evidence"
value = "executor_output"
content = """
... raw capped output ...
"""

[[rules]]
kind = "constraint"
text = "Preserve errors, stack traces, key paths, values, and exit status."

[[outcomes]]
label = "report"
text = "Return a dense summary without inventing facts."
```

### C.6 Search summarizer

```toml
objective = "Answer the retrieval question from raw web results"
agent_role = "Web Search Summarizer (read-only)"

[[targets]]
kind = "evidence"
value = "websearch_results"
content = """
... raw search results ...
"""

[[rules]]
kind = "constraint"
text = "Preserve concrete facts and omit unrelated boilerplate."

[[outcomes]]
label = "report"
text = "Answer the objective using only supplied evidence."
```

### C.7 Nudge / Todo

```toml
objective = "Continue incomplete work and finish the next pending todo"
background = "The stream ended while work remained open"
agent_role = "Nudge Supervisor (synthetic)"

[[targets]]
kind = "todo"
value = "Implement PromptDocument and PromptToml"

[[targets]]
kind = "todo"
value = "Run the standard verification suite"

[[rules]]
kind = "policy"
text = "Mark work in progress before editing and complete only after verification."

[[outcomes]]
label = "continue"
text = "Resume the next pending work item instead of ending the session."
```

Machine provenance such as `prompt_origin`, loop ID, round and nonce lives in typed metadata, not in this model-facing TOML.

### C.8 Wanxiangzhen worker

```toml
objective = "Implement worktree isolation for the assigned squad task"
background = "Execute one DAG node in a dedicated worktree and submit it to the coordinator"
agent_role = "Wanxiangzhen Slave Agent (mutating)"

[[targets]]
kind = "command"
language = "shell"
program = "git worktree add ../worktree-task feature/task"
dependencies = []
timeout_type = "long"

[[rules]]
kind = "contract"
text = "Complete development, pass review, commit, and call submit_to_squad."

[[outcomes]]
label = "submitted"
text = "Return the committed task to the coordinator."
```

### C.9 Meditator

```toml
objective = "Apply root cause analysis to the current engineering decision"
background = "The parent supplied workspace facts, prior attempts, risks, and acceptance criteria"
agent_role = "Methodology Reasoning Agent (read-only)"

[[targets]]
kind = "evidence"
value = "methodology_note"
content = "The user supplied methodology-specific note."

[[rules]]
kind = "policy"
text = "Use the selected methodology definition and return dense modern Chinese unless the inputs are English-only."

[[rules]]
kind = "constraint"
text = "Do not call tools or invent workspace facts."

[[outcomes]]
label = "report"
text = "Use every methodology output section and end with concrete next actions."
```

`Kernel/Methodology/Schema.renderMeditatorIntent` 的 Markdown string renderer 必须改为 typed `MeditatorPromptSpec`；Mimocode 的 `agentReportTail` 必须在 stringify 前加入 outcomes/rules，禁止在 TOML 字符串后拼接。

### C.10 Tool output 独立 Schema

Tool output 不伪装成 instruction Prompt：

```toml
body = "Search result body"

[[info]]
kind = "iterator"
text = "iter-123"

[[info]]
kind = "syntax"
text = "fsharp"

[[info]]
kind = "exit_code"
number = 0
```

### C.11 Search / Fetch 独立 Schema

```toml
[[results]]
title = "Wanxiangshu Architecture Guide"
url = "https://example.invalid/architecture"
content = "Event sourcing uses .wanxiangshu.ndjson as durable truth."
```

```toml
title = "Fetched Page"
byline = "Author"
length = 500
content = "Fetched body"
```

### C.12 Caps 独立 Schema

```toml
[[capabilities]]
label = "repository rules"
content = "Capability content"
```

### C.13 Squad event 独立 Schema

```toml
event_kind = "task_submitted"
session_id = "squad-1"
task_id = "task-1"
commit_sha = "abc123"
message = "Task task-1 was submitted."
```

事件展示没有 decoder。Durable replay 继续读取 NDJSON `WanEvent`，不解析此 TOML。

### C.14 旧版 PTY / Executor 通用投影样例（历史保留）

旧版曾把 PTY 状态也强塞进 7 原语。该设计已被 D4/D5 supersede，但原始场景信息必须保留，供迁移对账：

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

当前裁决：PTY 命令仍以 typed command 输入执行；PTY 工具返回使用 C.10 ToolOutput Schema，不伪造 agent instruction。

### C.15 旧版 Search / Fetch 通用投影样例（历史保留）

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

当前裁决：Search summarizer instruction 使用 C.6；原始 Search/Fetch 工具展示使用 C.11 独立 Schema。

### C.16 Batch subagent report 独立 Schema

```toml
[[reports]]
iterator = "iter-coder-1"
body = "First subagent report"

[[reports]]
iterator = "iter-coder-2"
body = "Second subagent report"

[[reports]]
body = "Synchronous report without a continuation iterator"
```

iterator 与对应 body 同处一张 table。禁止恢复旧 `iterators = [...]` + Markdown report blocks 的平行数组设计；平行数组无法由类型保证索引对齐。

## 附录 D：旧 9 处审计与扩展追溯

### D.1 原始 9 处 Pseudo-YAML 审计

| # | 涉及文件 | 原模式 | 原迁移意图 | 当前裁决 |
| --- | --- | --- | --- | --- |
| 1 | `src/Runtime/PromptHeader.fs`、`Runtime/Workspace/Yaml.fs` | `Yaml.stringify`、yaml helpers、`---` | 替换 Prompt renderer | 保留意图；两个文件最终删除 |
| 2 | `src/Kernel/Wanxiangzhen/SquadPrompts.fs` | 硬编码 `task:` front matter | worker prompt TOML 化 | 保留；Kernel 改产 `SlavePromptSpec` |
| 3 | `src/Runtime/Wanxiangzhen/SquadEventDisplayCodec.fs` | YAML encode + `IndexOf` decode | 去围栏 | 强化：单向 Event TOML view，删除 decode |
| 4 | `src/Runtime/Tooling/CapsFormat.fs` | `CapsYamlItem`、`caps` YAML | evidence target | 修正：独立 Caps Schema，不塞 Prompt target |
| 5 | `src/Runtime/Search/SearchPrompts.fs` | `results` YAML | evidence target | 修正：独立 Search/Fetch Schema |
| 6 | `src/Runtime/Subsession/SubagentBatchSpawnCore.fs` | `iterators` YAML | iterator TOML | 强化：先 typed composition，最终 ToolOutput TOML |
| 7 | `src/Runtime/Tooling/ToolOutputInfo.fs` | flat YAML fields | rules/outcomes | 修正：独立 ToolOutput Schema |
| 8 | `src/Runtime/Fallback/FallbackConfigCodec.fs` | AGENTS.md YAML parse | 曾计划 TOML 化 | superseded by D6：外部配置继续 YAML |
| 9 | `src/Runtime/Wanxiangzhen/ConfigReader.fs` | AGENTS.md YAML parse | 曾计划 TOML 化 | superseded by D6：外部配置继续 YAML |

### D.2 为什么必须保留旧表

旧表回答“哪些文件存在伪 YAML”；正文 §5 回答“哪些知识边界必须迁移”。两者不是重复维度。D.1 保存发现历史与 superseded 决策，正文表指导实施。

### D.3 旧表漏项扩展

| 漏项 | 具体文件/符号 | 迁移责任 |
| --- | --- | --- |
| Review family | `ReviewPrompts/Commands.fs`、`Format.fs`、`Submission.fs`、`Instructions.fs` | 结构 prose 全部进入 PromptDocument；verdict output 进入独立 output view |
| Loop | `Execution/LoopMessages.fs` | 删除 YAML anchors 与 `hasDoubleCheckAnchor` |
| Nudge | `PromptFragments.fs`、`NudgeDerivation.fs` | typed origin；结构化规则进入 PromptDocument |
| Meditator | `Kernel/Methodology/Schema.renderMeditatorIntent` | string renderer 改 typed spec |
| Host report tail | `Runtime/Subsession/Subagent.withReportTail` | host rule 在 stringify 前投影 |
| Report joining | `Subagent.joinReports`、`SubagentSpawn.runParallelSpawns` | typed batch output；删除 `\n---\n` divider |
| PTY | 四个 `Hosts/OpenCode/Pty*.fs` | ToolOutputMessage → ToolOutputToml |
| Text classifiers | Nudge/Review/Fallback scanners | typed metadata/event |
| Tool descriptions | Search/Subagent/Fuzzy/Omp docs | YAML front matter 文案改 TOML metadata |

## 附录 E：F# 类型与序列化详细设计

### E.1 旧 flat 模型为何不足

旧版完整 flat 设计如下，信息必须保留为反例：

```fsharp
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

它可构造 `File + content + no value`、`Action + draft` 等无意义组合。当前设计用 DU 把字段绑定到合法 case。

### E.2 Prompt Domain

```fsharp
module Wanxiangshu.Kernel.Prompt.Document

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

type TimeoutKind =
    | Short
    | Long

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

type PromptOutcome =
    { label: string
      text: string }

type PromptDocumentView =
    { objective: string
      background: string option
      agentRole: AgentRole
      targets: PromptTarget list
      boundaries: PromptBoundary list
      rules: PromptRule list
      outcomes: PromptOutcome list }

type PromptDocument = private PromptDocument of PromptDocumentView

type PromptDocumentError =
    | EmptyObjective
    | EmptyText of field: string
    | EmptyOutcomes
    | DuplicateOutcomeLabel of label: string
```

`PromptDocument.create` 收集所有独立错误并返回 `Result<PromptDocument, PromptDocumentError list>`。`PromptDocument.view : PromptDocument -> PromptDocumentView` 只暴露只读投影视图；外部可构造 View，但不能绕过 create 把它升级为合法 Document。Runtime `PromptToml` 只调用 view，不依赖 private representation。Host schema 已保证非空的字段仍经过领域构造器，避免跨宿主输入差异制造非法态。

### E.3 显式 wire 映射

```fsharp
let agentRoleText = function
    | AgentRole.Implementation -> "Implementation Agent (mutating)"
    | AgentRole.CodebaseSearch -> "Codebase Search Agent (read-only)"
    | AgentRole.BrowserAutomation -> "Browser Automation Agent (read-only)"
    | AgentRole.CodeReview -> "Code Reviewer (read-only)"
    | AgentRole.ExecutorSummarization -> "Executor Output Summarizer (read-only)"
    | AgentRole.WebSearchSummarization -> "Web Search Summarizer (read-only)"
    | AgentRole.MethodologyReasoning -> "Methodology Reasoning Agent (read-only)"
    | AgentRole.NudgeSupervisor -> "Nudge Supervisor (synthetic)"
    | AgentRole.SquadWorker -> "Wanxiangzhen Slave Agent (mutating)"

let ruleKind = function
    | Policy _ -> "policy"
    | Constraint _ -> "constraint"
    | Criterion _ -> "criterion"
    | Question _ -> "question"
    | Contract _ -> "contract"

let timeoutKindText = function
    | Short -> "short"
    | Long -> "long"

let boundaryTargetWire = function
    | BoundaryTarget.File path -> "file", path
    | BoundaryTarget.Directory path -> "dir", path
    | BoundaryTarget.PathOrSymbol value -> "path", value
```

Target wire 映射固定如下；“字段相似”不允许调用方自行换 key：

| Target case | TOML table fields |
| --- | --- |
| `FileTarget(path, guide, draft)` | `kind = "file"`、`value = path`、`hint = guide`、可选 `draft` |
| `FileReference(path)` | `kind = "file"`、`value = path` |
| `EntryTarget(entry)` | `kind = "path"`、`value = entry` |
| `QueryTarget(query)` | `kind = "query"`、`value = query` |
| `CommandTarget(language, program, dependencies, timeoutKind)` | `kind = "command"`、`language`、`program`、`dependencies`、`timeout_type` |
| `EvidenceTarget(label, content)` | `kind = "evidence"`、`value = label`、`content` |
| `TodoTarget(content)` | `kind = "todo"`、`value = content` |

Boundary wire 映射：

| Boundary case | TOML table fields |
| --- | --- |
| `DoNotRead(target)` | `kind` / `value` 来自 `boundaryTargetWire target`、`action = "read"` |
| `DoNotModify(target)` | `kind` / `value` 来自 `boundaryTargetWire target`、`action = "modify"` |
| `DoNotExecute(action)` | `kind = "action"`、`value = action`、`action = "execute"` |
| `DoNotTouch(target)` | `kind` / `value` 来自 `boundaryTargetWire target`、`action = "all"` |

Target 与 Boundary projector 必须按上表穷尽匹配。禁止 `ToString()`、reflection、case-name lowercasing；wire 协议变更必须显式修改函数并触发 snapshot。

### E.4 受限 TOML 值代数

```fsharp
module Wanxiangshu.Runtime.Serialization.TomlValue

type TomlValue =
    | String of string
    | Integer of int
    | Boolean of bool
    | StringArray of string list
    | TableArray of (string * TomlValue) list list
    | Table of (string * TomlValue) list
```

不提供 Null、Undefined、Float、Date、Function、Symbol，也不提供任意 `TomlValue list`，因此异构数组在源码层不可构造。字符串数组用 `StringArray`；对象数组用 `TableArray`，由 smol-toml 输出 `[[...]]`。空 optional key 在 projection 阶段省略；空 table-array 整键省略，避免库无法从空 JS array 推断 array-of-tables。

### E.5 唯一 npm FFI

```fsharp
module Wanxiangshu.Runtime.Serialization.Toml

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Runtime.Serialization.TomlValue

[<Import("stringify", "smol-toml")>]
let private stringifyNative (value: obj) : string = jsNative

let rec private toJs = function
    | String value -> box value
    | Integer value -> box value
    | Boolean value -> box value
    | StringArray values -> values |> List.toArray |> box
    | TableArray tables ->
        tables
        |> List.map (Table >> toJs)
        |> List.toArray
        |> box
    | Table fields ->
        fields
        |> List.map (fun (key, value) -> key, toJs value)
        |> createObj

let stringify = function
    | Table _ as document -> document |> toJs |> stringifyNative
    | _ -> invalidArg "document" "TOML document root must be a table"
```

实际实现不得 catch library error 后 fallback。`TomlValue` 使 unsupported values 不可构造；非 Table root 是程序缺陷，测试必须覆盖。`smol-toml` 保证输出末尾换行，renderer 原样返回，不 TrimEnd。

### E.6 Prompt projection

投影职责拆为小函数：

```fsharp
val target : PromptTarget -> TomlValue
val boundary : PromptBoundary -> TomlValue
val rule : PromptRule -> TomlValue
val outcome : PromptOutcome -> TomlValue
val document : PromptDocument -> TomlValue
val render : PromptDocument -> string
```

`document` 只建立七个根键，按 objective → background → agent_role → targets → boundaries → rules → outcomes 顺序插入。空 optional/list 不插键；outcomes 由构造器保证非空。`render = document >> Toml.stringify`，零前后处理。

### E.7 Tool output domain 保留

现有 `InfoItem` 是正确 DU：Hint、Syntax、Iterator、Status、ExitCode。保留它，删除 `flatFields : InfoItem list -> FrontMatterField list` 与所有 parse-back API。单个工具输出与批量子代理报告是两个领域，不用有损 `combine` 混在一起：

```fsharp
type SubagentReport =
    { iterator: string option
      body: string }

type BatchReport = private BatchReport of SubagentReport list

val toolOutputDocument : ToolOutputMessage -> TomlValue
val renderToolOutput : ToolOutputMessage -> string
val batchReportDocument : BatchReport -> TomlValue
val renderBatchReport : BatchReport -> string
```

Batch spawn 在拥有 child ID 时直接构造 `SubagentReport`，iterator 与 body 永远同表绑定。BatchReport 保留输入顺序，拒绝空 report list；它投影为 `[[reports]]`，不读取已渲染字符串、不用 Markdown divider、不把多个 iterator 压成失去对应关系的 flat info。

### E.8 Synthetic origin 与 Review challenge

```fsharp
type MessageOrigin =
    | Human
    | TodoNudge
    | ReviewNudge
    | RunnerNudge
    | CompactionNudge
    | ForceStop
    | FallbackContinuation

type ReviewChallengeState =
    | NotRequested
    | Requested of round: int
    | Answered of round: int
```

OpenCode 原地写入 `metadata.wanxiangshu`；不支持同形 metadata 的宿主在发送前写现有 event/state store。机器消费者匹配 DU，不匹配 prose。metadata 至少携 `version = 1`、origin、session/run identity；具体 codec 必须单独契约测试。

`FallbackContinuation` 指现有产品域的模型重试/续接事实，不是 D7 禁止的“格式迁移 fallback”。D7 禁止 TOML 失败时退回 YAML、双写与兼容 shim；它不删除项目既有 Fallback 子系统。

### E.9 测试期语义校验替代生产 axiomCheck

旧版 `axiomCheck(tomlText)` 试图在生产或共享 Runtime 中解析文本，和 No Self-Parsing 冲突。保留其七类检查意图，迁入 tests：

1. root key whitelist。
2. 禁止 nested section 作为 Prompt 根组织。
3. key 为 snake_case。
4. prose style 检查只针对自然语言 fixture，不扫描 path/code。
5. targets/boundaries/rules/outcomes 解析为 arrays-of-tables。
6. nested key 与 DU case 对齐。
7. objective、agent_role、outcomes 必填。

旧版 `AxiomError` 名称同样保留并精化：

| 旧错误 | 当前去向 |
| --- | --- |
| `NonOrthogonalKey` | `UnknownRootKey` / `UnexpectedNestedKey` |
| `NestedSectionForbidden` | Prompt root shape assertion |
| `HyphenatedValueForbidden` | 不再全值扫描；仅自然语言 fixture 做 style assertion，路径/代码豁免 |
| `MissingMandatoryField` | `MissingRootKey` 与 `PromptDocumentError` |

```fsharp
type PromptTomlAssertionError =
    | ParseFailed of string
    | UnknownRootKey of string
    | MissingRootKey of string
    | InvalidArrayItem of root: string * index: int
    | UnexpectedNestedKey of root: string * key: string
    | RootOrderChanged of expected: string list * actual: string list
```

测试 helper 可 `[<Import("parse", "smol-toml")>]`；生产 `src/` 禁止 parse import。

## 附录 F：文件级迁移与验证细目

### F.1 Phase 0 RED 文件

| 新/改测试 | RED 行为 |
| --- | --- |
| `tests/TomlSerializationTests.fs` | named import、escaping、Unicode、multiline、AoT、末尾换行、顺序 |
| `tests/PromptTomlTests.fs` | 7 root、每个 DU case、非法构造、legacy field mapping |
| `tests/ToolOutputInfoTests.fs` | 单个 ToolOutput 的重复 info 顺序；BatchReport 保持 iterator/body 关联且不需要 parse |
| ReviewSession tests | challenge state/event 取代 history scan |
| Nudge tests | 改 prose 后 origin 分类保持不变 |
| Host contract tests | metadata.wanxiangshu 原地更新且跨 host 行为一致 |

### F.2 Phase 1 typed transport 调用链

```text
HostAdapter.SpawnSubagent
  -> SubagentResponse(childId option, report)
  -> SubagentReport(iterator = childId, body = report)
  -> BatchReport preserving report/iterator association
  -> BatchReportToml.render at tool boundary
```

Review：`return_reviewer` 第一次 PERFECT →发出 challenge event →第二次提交查询 ReviewSession state。禁止 `SessionIo.readSessionTexts` + `Contains("double-check")`。

Nudge：derive typed action →记录 MessageOrigin →host codec 发消息。ProgressObserver、NudgeModelResolver、Fallback MessageInspection 读 typed marker；删除固定英文 Contains 链。

### F.3 Phase 2 序列化依赖与编译落点

1. `package.json` dependencies 加精确 `"smol-toml": "1.7.0"`。
2. `build-package.json` dependencies 同步。
3. 正常 package install 更新 `package-lock.json` integrity；禁止手改 lock。
4. fsproj 在 `src/Kernel/SubagentIntents.fs` 后加入 `src/Kernel/Prompt/Document.fs`；在 Runtime 首段用 `Serialization/TomlValue.fs` → `Serialization/Toml.fs` → `Prompt/PromptToml.fs` → `Tooling/ToolOutputToml.fs` 的顺序取代旧 `Workspace/Yaml.fs` / `PromptHeader.fs` 渲染基座。F# 编译顺序即依赖顺序，禁止把新文件随手追加到项目末尾。
5. root `package.json` 供本地 Fable/Node 解析依赖；`postbuild.mjs` 把 `build-package.json` 复制为 `build/package.json`，所以从 build 目录安装/发布时由该 manifest 声明 `smol-toml`。postbuild 不复制 node_modules 是正确行为，禁止复制依赖目录制造双份包树。

### F.4 Phase 3 Prompt producer 映射

| Producer | Projection | 额外约束 |
| --- | --- | --- |
| `coderPrompt` | CoderIntent → PromptDocument | targets 非空；doNotTouch → boundaries |
| `inspectorPrompt` | InspectorIntent → PromptDocument | questions → rules；entries → targets |
| `browserPrompt` | intent → PromptDocument | intent → objective；browser policy → rules |
| executor summarizer | args/output → PromptDocument | cap raw output before projection；final rendered byte budget测试 |
| websearch summarizer | question/raw → PromptDocument | raw → EvidenceTarget |
| meditator | Methodology args/entry → PromptDocument | 删除 `renderMeditatorIntent` Markdown renderer |
| review submission | task/report/files → PromptDocument | report → evidence；files 保留 review 语义 |
| loop/nudge | typed state → PromptDocument | provenance 不进 text |
| squad worker | SlavePromptSpec → PromptDocument | Kernel 无 Fable/TOML import |

Mimocode `agentReportTail` 变成 host-specific Rule/Outcome，在 `PromptDocument` 渲染前添加。Continue prompt 是用户 opaque text，直接发送；不得把用户内容解析成 7 根键，也不得追加结构 tail。

### F.5 Phase 4 展示 producer 映射

| Producer | Target view | 测试 |
| --- | --- | --- |
| ToolOutputInfo | ToolOutput TOML | `ToolOutputInfoTests` |
| SearchPrompts | Search/Fetch TOML | `ExecutorFormatCoverageTests` |
| CapsFormat | Capabilities TOML | `CapsFormatTests` |
| PTY read/spawn/write/list | ToolOutput TOML | PTY suites |
| SquadEventDisplayCodec | `SquadEventTomlView` | Event view tests；删 decode roundtrip |
| Subagent joinReports | `[[reports]]` BatchReport TOML | parallel spawn/dispatcher tests |

### F.6 Phase 5 物理删除与文案更新

删除：

- `src/Runtime/PromptHeader.fs` 及 fsproj entry。
- `src/Runtime/PromptFrontMatter.fs` 残留文件；它当前不在 fsproj，不伪造“移除 entry”。
- `src/Runtime/Workspace/Yaml.fs` 及 fsproj entry。
- `src/Runtime/Tooling/ToolOutputInfoParse.fs` 及 fsproj entry。
- `Kernel/Wanxiangzhen/SquadPrompts.fs` 若已由 `SlavePromptSpec.fs` 取代。

更新：

- Tool catalog 中 “YAML front matter” → “TOML metadata”。
- OMP iterator 描述、FuzzyQuery、README Wanxiangzhen worker 文案。
- Review encouragement 中 “task front matter” → “task document”。
- docs 中配置 frontmatter 保持 YAML，不做全仓无脑替换。

### F.7 最小正式验证顺序

每个 slice 只跑对应测试与新增 gate；全部完成后按正文 §10 依次运行 build-and-test、contract、integration、gates 一次。不得用一次性 JS/F# probe 代替正式库 FFI 测试。

## 附录 G：旧版知识覆盖矩阵

| 旧版知识 | 保留位置 | 修订 |
| --- | --- | --- |
| 软件设计与复杂度压缩 | A.1 | 恢复完整因果链 |
| Zero-Parsing Axiom | A.2、正文 §3.1 | 精确改名为 No Self-Parsing；合法外部 codec 豁免 |
| 强类型海关 | A.3 | 补 Host DTO / Domain / Presentation 三层 |
| NDJSON SSOT | A.4 | 补 Review/Synthetic typed state |
| Markdown drift | A.5 | 补 opaque content 边界 |
| Message as Config | A.6 | 限定结构化 instruction prompt |
| 35+ → 7 原语图 | B.1 | 原理保留，作用域纠正 |
| 3 scalar + 4 table arrays | B.2–B.3 | 全量保留 |
| 旧字段归并 | B.4 | 逐字段明确映射 |
| 正交/无损论证 | B.5 | 保留并把“证明”改为待测试命题 |
| 对偶矩阵 | B.6 | 全量恢复 |
| key/value 规范 | B.7 | 修复路径/命令误伤 |
| Coder 样例 | C.1 | 保留 |
| Inspector 样例 | C.2 | 保留 |
| Browser 样例 | C.3 | 保留 |
| Reviewer 样例 | C.4 | 保留 |
| Executor/PTY 样例 | C.5、C.10、C.14 | 原始通用投影保留；最终分离 instruction 与 output |
| Search/Fetch 样例 | C.6、C.11、C.15 | 原始通用投影保留；最终分离 summarizer instruction 与 search view |
| Batch report 样例 | C.16 | iterator/body 同表绑定，替代平行数组与 Markdown divider |
| Nudge/Todo 样例 | C.7 | provenance 移出 text |
| Wanxiangzhen 样例 | C.8、C.13 | 分离 worker instruction 与 event view |
| 旧 9 处审计 | D.1 | 原表逐项保留并标 superseded 行 |
| 物理清扫 | 正文 §5/§7、F.6 | 补真实 fsproj 状态 |
| Yaml config-only | D.1、F.6 | 删除 output Yaml.fs；保留两个 direct parse codec |
| No Stub | 正文 D7、Phase 5 | 保留 |
| UniversalSchema 类型 | E.1–E.3 | flat Record 作为反例保留，目标改 DU |
| PromptToml serializer | E.4–E.6 | @iarna/手工设计 superseded；smol-toml named import |
| axiomCheck 七步 | E.9 | 迁入 tests，生产零 parse |
| 迁移路线 | 正文 §7、F.1–F.7 | 旧四阶段细化为 RED + 五阶段 |
| CI 断言 | 正文 §9–§10 | 增加 allowlist、typed transport、停止条件 |

覆盖规则：修改本文时，G 表任一行若失去对应章节，修改即不完整。禁止以缩短文件为理由删样例、审计历史、映射表或 superseded 决策链。
