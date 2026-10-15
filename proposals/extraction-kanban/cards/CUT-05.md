# CUT-05：双语资源与包内定位

返回 [看板](../BOARD.md) / [职责卡](../CARDS.md#cut-05) / [范围对账](../COVERAGE.md) / [模板](../TEMPLATES.md#contract)。

## 1. 当前状态与前置核对

- **当前列状态**：`READY`（由 `CUT-00` 完成准备核对后转入，待认领）
- **所属泳道 / 波次**：表达 / W1 独立卡
- **切换前置**：`CUT-00`（已完成，见 [CUT-00](CUT-00.md)）
- **关联护栏**：遵守 `distribution-001/002/006/008` 与 `provider-language-001..013`、`cognitive-environment-001..016`；包内 `files` 与 tarball 发布闭包由 `CUT-33` 串行守门。
- **零阻塞结论**：核心语言类型（`ProviderLanguage`）、成对资源定位器（`ProviderResources`）与占位符模板替换（`ProviderProse`）不依赖会话编排、持久化或宿主运行时，可零阻塞独立抽出至 `wanxiangli`（对应 `src/resources` 草稿）。

## 2. 草稿概念名/路径与主仓真实模块/`.fsproj` 映射

委托说明及早期草稿中曾提及 `src/Wanxiangshu/Context/Language/` 与 `requirements/session-language/`。在主仓当前基线（`b322c180b`）中，语言已演进为无会话持久绑定的全局 `ProviderLanguage`（`provider-language-002`），其真实源码与 `.fsproj` 映射如下：

| 草稿概念 / 早期路径 | 主仓真实源码路径（含 `.fsi`） | 所属编译单元（`.fsproj`） | 去向判定 |
|---|---|---|---|
| `Context/Language/`（语言类型与偏好解析） | `src/Wanxiangshu/Participant/Provider/Language.fs(.fsi)` | `Wanxiangshu.Owner.runtime-platform.language.fsproj` | **抽出核心（A）**：`ProviderLanguage`、偏好阶梯解析与 `GlobalProviderLanguage` |
| 包内成对资源定位与读取 | `src/Wanxiangshu/Participant/Provider/ProviderResources.fs(.fsi)` | `Wanxiangshu.Owner.runtime-platform.language.fsproj` | **抽出核心（A）**：基于 `import.meta.url` 上溯包根的 `resources/provider/` 定位、成对校验与读取 |
| 会话语言只读查询与 `{{name}}` 模板渲染 | `src/Wanxiangshu/Participant/Provider/SessionLanguage.fs(.fsi)`<br>`src/Wanxiangshu/Participant/Provider/Prose.fs(.fsi)` | `Wanxiangshu.Owner.provider-language.participant-provider-language.fsproj` | **抽出核心（A）/ 薄包装（C）**：`ProviderProse.substitute / render / instructionLines / document` |
| 宿主语言偏好桥接 | `src/Wanxiangshu/OpenCode/Host/ProviderLanguageBinding.fs(.fsi)` | `Wanxiangshu.Owner.provider-language.participant-provider-language.fsproj` | **留在主仓组合层（C）**：读取环境变量、OpenCode 配置与系统 locale 并刷新全局语言 |
| 五层认知材料目录与组装 | `src/Wanxiangshu/Resources/PromptCatalog.fs(.fsi)`<br>`src/Wanxiangshu/Resources/PromptResources.fs(.fsi)`<br>`src/Wanxiangshu/Resources/RuntimeResources.fs(.fsi)` | `Wanxiangshu.Owner.cognitive-environment.resources-promptcatalog.fsproj`<br>`Wanxiangshu.Owner.cognitive-environment.resources-promptresources.fsproj`<br>`Wanxiangshu.Owner.cognitive-environment.resources-runtimeresources.fsproj` | **留在主仓业务资源层（B）**：消费抽出的资源定位与模板渲染，维护 Common Law → Role Law → Office Library 的产品组合 |
| 验证与测试公开 Surface | `src/Wanxiangshu/Participant/Provider/LanguageSurface.fs(.fsi)`<br>`src/Wanxiangshu/Resources/PromptSurface.fs(.fsi)` | `Wanxiangshu.Owner.provider-language.participant-provider-languagesurface.fsproj`<br>`Wanxiangshu.Owner.cognitive-environment.resources-promptsurface.fsproj` | **主仓适配表面**：桥接新库公开 API，保持现有测试契约不变 |
| 物理双语资源资产 | `resources/provider/**/{en.md,zh-CN.md}` | 随主仓/包分发（`distribution-001/004/008`） | **各业务语义 owner 持有正文**，统一经本卡定位器读取 |

## 3. 目标

- **用户/维护者当前损失**：`wanxiangli/src/resources` 仍为草稿，主仓与基础库尚未共享同一套成对双语资源定位、语言阶梯解析与严格 `{{name}}` 占位符渲染原语；若后续各卡各自读取 Markdown 资源，极易出现依赖 `process.cwd()`、静默单语回退或漏填占位符。
- **这次完成后可少维护什么**：在 `wanxiangli/resources` 冻结唯一的 `ProviderLanguage`、包根锚定成对资源加载器与严格占位符模板引擎；主仓 `Participant/Provider/{Language,ProviderResources,Prose}.fs` 的底层定位与替换实现收敛到单一实现。
- **明确不做**：
  - 不恢复已废止的“按会话持久化绑定语言”（严格遵守 `provider-language-002/003`：无会话语言绑定，所有入口统一读取当前全局设置）；
  - 不把主仓各角色的 Role Law、Office Library、Plan 阶段提示或 Enforcer 规则正文硬编码复制进 `wanxiangli` 源码常量；
  - 不翻译工具名、参数名、协议字段、枚举值或机器状态码（`provider-language-005/011`）；
  - 不在本卡内直接改动两仓共享锁或发布其他卡的 tarball（打包与锁更新归 `CUT-33` 串行收敛）。

## 4. 依据

- **正式 WHAT / 条款**：
  - `requirements/provider-language/WHAT.md`：`[001]` `ProviderLanguage` 强类型、`[002]` 无会话语言绑定、`[003]` 派生会话同读全局、`[004]` 全局偏好阶梯解析、`[005]` Class A / B / C 分类、`[006]` 资源成对与缺失拒绝、`[007]` 模板参数一致与漏填报错、`[008]` 工具文本遵循全局语言、`[009]` 内容/语言/布局分权、`[010]` Role Law 跨语言锚点、`[011]` 协议标识符不翻译、`[012]` 双语职责语义一致、`[013]` Class A 的交付语言。
  - `requirements/cognitive-environment/WHAT.md`：`[001]`—`[016]` 五层认知材料（`Common Law → Role Law → Office Library`）、语义所有权与 Pair Hint 语言锚定。
  - `requirements/distribution/WHAT.md`：`[001]`、`[002]`、`[006]`、`[008]` 基于 `import.meta.url` 的包根资源定位与缺失立即失败。
- **新库草稿差异及裁决**：
  - `wanxiangli/src/resources` 草稿尚未兑现 `en.md` / `zh-CN.md` 成对存在性强制检查、跨语言 `{{name}}` 占位符集合一致性校验以及 `import.meta.url` 包根定位。施工时以主仓 `Language.fs`、`ProviderResources.fs`、`Prose.fs` 已验证的严格契约为准，不采用任何“缺失中文时静默回退英文”或“从 `process.cwd()` 探测”的简化做法。

## 5. 自治个体（A / B / C）

- **A（`ProviderLanguage` & `ResourceBundle` / `ProseTemplate`，抽入新库）**：
  - 负责：封闭语言代数（`English | SimplifiedChinese`）、偏好观察阶梯纯解析（`explicit → hostConfig → vscodeNls → posixLocale → intlLocale → English`）、以包根为基准的语义路径解析（`<semanticPath>/en.md` 与 `<semanticPath>/zh-CN.md`）、成对存在性与占位符集合一致性校验、`{{name}}` 严格替换。
  - 拥有状态：无业务会话状态；`GlobalProviderLanguage` 仅持有进程级当前全局偏好快照（默认 `English`，由 C 调用 `refresh` 更新）。
  - 永远成立的不变量：任一语言文件缺失即抛致命异常；模板双语版本的 `{{name}}` 集合不等、调用方少传参数或替换后仍残留 `{{...}}` 立即报错；替换值本身作为字面量注入，绝不二次展开或翻译。
- **B（各领域认知材料与工具描述 Owner，留在主仓 `Resources/`、`Enforcer/`、`Repository/Programming/Js/` 等）**：
  - 负责：定义各自拥有的语义路径（如 `role/engineer`、`library/kolmogorov`、`tools/js-engineer`）与模板参数字典，管理 `Common Law → Role Law → Office Library` 组装顺序。
  - 与 A 不共享可变值：B 不直接拼接文件系统路径或缓存旧语言渲染结果。
- **C（`ProviderLanguageBinding` 与宿主请求转换，留在主仓 `OpenCode/Host/` 与 `OpenCode/Plugin/`）**：
  - 负责：采集宿主环境变量、OpenCode 配置及系统 locale，调用 A 的阶梯解析刷新全局语言；在每次 System Prompt、Tool Description、Pair Hint、报错恢复文案渲染时向 A 传入当前 `ProviderLanguage`。
  - 不复制领域判断：C 不内嵌双语散文分支，也不维护 per-session 语言绑定表。

## 6. 公开消息与最小完整契约

- **构造与语言解析**：
  - `ProviderLanguage = English | SimplifiedChinese`
  - `tryParse : raw: string -> ProviderLanguage option`（识别 `en`/`en-*`/`english` 与 `zh`/`zh-cn`/`zh-hans`/`chinese` 等规范标签，其余返回 `None`）
  - `fromObservationLadder : explicit: string option -> hostConfig: string option -> vscodeNls: string option -> posixLocale: string option -> intlLocale: string option -> Result<ProviderLanguage, string>`（显式配置非法时返回 `Error`，不静默吞掉；合法显式配置优先于下层环境探测）
- **输入字段 / 字节 / 范围 / 身份**：
  - `semanticPath : string`（相对 `resources/provider/` 的规范语义路径，禁止绝对路径、`..` 逃逸或带后缀名）
  - `substitutions : Map<string, string>`（精确对应模板中的 `{{name}}` 占位符）
- **成功结果及其精确保证**：
  - `relativePath : ProviderLanguage -> string -> string` 返回 `resources/provider/<semanticPath>/{en.md|zh-CN.md}`；
  - `readText : ProviderLanguage -> string -> string` 先校验双语文件成对存在（`requireLanguagePair`），再返回 UTF-8 文本（去 BOM、统一 LF）；
  - `render : ProviderLanguage -> string -> Map<string, string> -> string` 校验 `en.md` 与 `zh-CN.md` 的 `{{name}}` 占位符集合完全一致，校验调用方提供的键覆盖全部占位符，完成一次性精确替换。
- **拒绝结果及零副作用保证**：
  - 非法显式语言配置、非法语义路径、缺失任一语种文件、双语模板占位符集合不一致、缺少必填替换键：全部立即抛出明确异常或返回 `Error`，零静默回退、零半成品文本输出。
- **Unknown 的来源与恢复方式**：
  - 未设置环境变量或宿主配置属于“未指定偏好”，按阶梯向下探测直至默认 `English`；显式传入未知语言字符串属于“非法输入”，直接拒绝。
- **重复、冲突、晚到和重入**：
  - 全局语言切换（`GlobalProviderLanguage.refresh`）是幂等原子替换；下一次 Class A 渲染立即读取新语言（`provider-language-002/008/013`），已安装的 `RuntimeResources` 双语目录不因切换而重新读盘。
- **公开 `.d.ts` 与 Fable 交付形状**：
  - 跨越 JS 边界时（如 `ProviderLanguageSurface` / `PromptSurface`），语言以稳定字符串 `"en"` / `"zh-CN"` 传递，替换表以普通 JS object 传递，不暴露 F# 内部 DU 布局。

## 7. 主仓调用方清单与对应测试套件

### 7.1 主仓调用方清单

1. **资源与认知环境装配**：
   - `src/Wanxiangshu/Resources/PromptResources.fs`（组装各角色 System Prompt 与 Bookkeeper 提示）
   - `src/Wanxiangshu/Resources/RuntimeResources.fs`（启动期加载双语 `PromptCatalog` 与 `EnforcerRule` 列表）
   - `src/Wanxiangshu/Resources/PromptSurface.fs`（向验证测试暴露加载与角色渲染接口）
   - `src/Wanxiangshu/Resources/EnforcerCatalogResource.fs`（加载双语 Rulebook 目录）
2. **Enforcer、指导与恢复文案**：
   - `src/Wanxiangshu/Enforcer/Catalog.fs`、`Continuation.fs`、`Guidance/Tip.fs`
   - `src/Wanxiangshu/Interaction/Repair/InteractionRepair.fs`、`CompletedTurn.fs`
   - `src/Wanxiangshu/OpenCode/Host/PairProgrammingGuidelineProjection.fs`
   - `src/Wanxiangshu/OpenCode/Host/ProviderFailurePresentation.fs`
3. **工具描述、JS 编程面与诊断本地化**：
   - `src/Wanxiangshu/OpenCode/Tools/ManagedAgent.fs`、`StaticTools.fs`、`FileMutationTools.fs`、`FetchTool.fs`、`BookkeeperTool.fs`、`PtyTool.fs`、`ExecutorToolSurface.fs`
   - `src/Wanxiangshu/Repository/Programming/Js/Description.fs`、`Capability.fs`、`ToolsBindings.fs`、`OpenCode/ToolWorkflow.fs`、`OpenCode/ToolHost.fs`
   - `src/Wanxiangshu/Execution/Delegation/Fork/OpenCode/JoinTool.fs`、`Execution/Session/OpenCode/HorizonTool.fs`、`Mission/Deliver/DeliverTool.fs`、`Mission/Relay/ReviewTool.fs`
4. **宿主启动、会话与系统提示转换**：
   - `src/Wanxiangshu/OpenCode/Host/ProviderLanguageBinding.fs`、`ProviderSystemTransform.fs`、`Sessions.fs`、`ManagedAgentConfig.fs`
   - `src/Wanxiangshu/OpenCode/Plugin/PluginLoad.fs`、`PluginTransforms.fs`、`PluginSessionWiring.fs`
   - `src/Wanxiangshu/Mission/Planning/Core.fs`、`Mission/Relay/NarrativeTransform.fs`、`Strength/OpenCode/Delegate.fs`、`Context/Companion/Blogger/Prompt.fs`

### 7.2 对应测试套件

- `requirements/provider-language/tests/001.test.mjs` 至 `013.test.mjs`（完整覆盖 `WHAT[PROVIDER-LANGUAGE-001..013]`）
- `requirements/cognitive-environment/tests/001.test.mjs` 至 `013.test.mjs`、`015.test.mjs`、`016.test.mjs`（覆盖 `WHAT[COGNITIVE-ENVIRONMENT-001..016]`）
- `requirements/distribution/tests/001.test.mjs`、`002.test.mjs`、`006.test.mjs`、`008.test.mjs`（覆盖包内资源定位与缺失拒绝）

## 8. 必须能红的最小反例与拆除测试设计

1. **反例 1（单语缺失静默回退）**：在临时资源夹具中删除某语义路径下的 `zh-CN.md`（保留 `en.md`），请求 `English` 或 `SimplifiedChinese` 的 `readText` / `requireLanguagePair`，必须立即因配对缺失报错，严禁在请求 `SimplifiedChinese` 时回退返回 `en.md`，也严禁在请求 `English` 时放过配对残缺（对应 `provider-language-006`）。
2. **反例 2（跨语言占位符不一致或漏填参数）**：构造 `en.md` 含 `{{role}}` 而 `zh-CN.md` 含 `{{role}} {{extra}}` 的成对模板，或者调用 `render` 时少传 `role`，必须立即报错，不得输出含未展开 `{{...}}` 的残缺散文（对应 `provider-language-007`）。
3. **反例 3（依赖 `process.cwd()` 或回退源码树）**：将测试子进程工作目录切换到与仓库无关的临时空目录 `/tmp/...`，加载打包产物中的资源定位器，必须依然从包内 `resources/` 准确读取；若删除包内某资源而源码树中仍存在，必须报错而非偷读源码目录（对应 `distribution-002/006`）。
4. **反例 4（翻译协议字面量或固定会话语言）**：切换全局语言设置后，同一進行中会话的下一次系统提示与工具描述立即切换为新语言，但工具名（如 `js-engineer`、`todowrite`）、参数字段名（`find`、`put`、`all`）与错误码（`EDIT_NOT_FOUND`）保持逐字符不变（对应 `provider-language-002/005/008/011/013`）。
5. **独立拆除测试**：在 `wanxiangli` 中仅编译 `resources` 子包（不含 `work`、`trace`、`ownership`、`eventstore`），直接运行上述 4 个反例与正控测试全绿。

## 9. 精确写集合与暂留/相邻卡边界

- **库仓（`wanxiangli`）写集合**：
  - `src/resources/*` 及对应工程/导出声明（实现 `ProviderLanguage`、阶梯解析、包根资源定位、成对校验与 `{{name}}` 模板替换）。
- **主仓（`wanxiangshu`）切换期写集合**：
  - `src/Wanxiangshu/Participant/Provider/Language.fs(.fsi)`
  - `src/Wanxiangshu/Participant/Provider/ProviderResources.fs(.fsi)`
  - `src/Wanxiangshu/Participant/Provider/SessionLanguage.fs(.fsi)`
  - `src/Wanxiangshu/Participant/Provider/Prose.fs(.fsi)`
  - `src/Wanxiangshu/Participant/Provider/LanguageSurface.fs(.fsi)`
  - `src/Wanxiangshu/Wanxiangshu.Owner.runtime-platform.language.fsproj`
  - `src/Wanxiangshu/Wanxiangshu.Owner.provider-language.participant-provider-language.fsproj`
- **暂留主仓与相邻卡边界**：
  - `src/Wanxiangshu/OpenCode/Host/ProviderLanguageBinding.fs(.fsi)`（宿主环境与配置采集）留在主仓；
  - `src/Wanxiangshu/Resources/{PromptCatalog,PromptResources,RuntimeResources,PromptSurface,EnforcerCatalogResource}.fs(.fsi)` 及 `resources/provider/**` 下的业务 Markdown 正文留在主仓各自的语义 owner（与 `CUT-08`、`CUT-17`、`CUT-30` 协同）；
  - `package.json`、`package-lock.json` 与 `vendor/wanxiangli-*.tgz` 的更新由 `CUT-33` 统一串行负责。
