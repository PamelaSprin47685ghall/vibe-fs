# 问题 2：控制字段与执行网关

## 一、Flow 管线位置

本问题映射到管线上的 **effect 执行边界**。在 `flatMapMerge(maxConcurrency)` 之前、真实 execute 调用之前，设置统一执行网关进行三层防线：schema 软提示 → before hook 原地提取+删除 → execute wrapper 终极净化+clean assertion。`warn`、`warn_tdd`、`warn_reuse` 以及 todo 报告长度是合规提示，不得成为 Host 的执行前门禁。

控制字段提取为 `ControlEnvelope`（参见 PRD-01），与净化后业务参数彻底分离。

## 二、当前根因

仓库已经有共享的解析和删除函数：`requireWarnTddOnArgs`、`requireWarnOnArgs`、`requireWarnReuseOnArgs`。`filterAmendFromArgs` 已随 `amend` 功能一起移除。这些函数应改为记录 present/missing/blank/non-canonical 合规状态并删除字段；合规缺失不阻止工具执行。

但整个系统仍存在结构性漏洞：

### 漏洞 A：schema 注入分散

Opencode、OMP、Mux 分别有自己的 schema 改写路径。有些 host 内置工具经过增强，有些自定义工具经过增强，有些 alias 没经过，动态注册工具可能晚于增强阶段，同类修改工具在不同 host 上要求不同。

### 漏洞 B：工具分类和 schema 分类没有完全共用

`WarnTdd` 中对 modification tool、subagent tool、warn-required tool 有一套集合，但部分 host 又自行硬编码 coder、executor、write 等名单。名单一旦漂移就会出现：执行阶段按能力识别字段但 schema 没给出软提示，或 schema 给出提示但执行阶段没记录合规状态，或某个 alias 可以绕过。

### 漏洞 C：before hook 不一定真正注册

从 Mux 的组装结构看，before 和 after 逻辑存在，但插件暴露路径可能只稳定注册了 after。这样"执行前剥离"就不是强保证。

### 漏洞 D：原参数对象可能继续流向下游

即使某个 hook 删除字段，如果 wrapper 保存了原引用、host 在 hook 之前复制了参数、after hook 又读取旧 input、执行路径不经过该 hook，控制字段仍可能泄漏到实际工具。

### 漏洞 E：把合规提示当作执行前拒绝

缺少 `warn_tdd`、`warn` 或 `warn_reuse` 不属于业务参数错误。工具应先执行，随后在结果规范化末尾追加一次严厉批评；只有净化后的业务参数仍泄漏控制字段时才 fail closed。

## 三、OpenCode v1.17.13 源码定案

### before Hook 原地修改有效，替换无效（定案六）

`tool.execute.before` 收到的 `output.args` 与随后传给真实工具的局部 `args` 通常是同一个对象引用。因此：
* `delete output.args.warn_tdd` 有效；
* `output.args = sanitizedCopy` 无法改变真实 execute 使用的旧局部引用。

### before Hook 不能覆盖全部内部执行路径（定案七）

经过 before/after 的有：registry built-in 工具、custom plugin 工具、MCP server 工具、MCP resource 工具、Task/subagent 工具。

不经过或不完全经过的有：StructuredOutput、title agent、compaction agent、部分内部直接调用的 read。after hook 在工具失败或 Abort 时不会执行。

所以 after hook 不能作为安全校验边界。

### MCP 工具不经过 tool.definition

MCP 工具虽然经过 `tool.execute.before`，但其 schema 不经过通用 `tool.definition`。

### 多插件顺序产生新隐患

trigger hook 按插件加载顺序串行执行，并共享同一个 output 对象。存在危险情况：
1. 其他插件先执行，把 `output.args` 替换成新对象；
2. 万象术随后看到的是新对象；
3. 万象术在新对象中删除 warn 字段；
4. OpenCode 最终仍把旧局部 args 传给 execute；
5. 旧对象中的 warn 字段未被删除。

万象术若依赖 before hook 原地净化，应满足至少一项：万象术排在会替换 args 的外部插件之前；启动时声明插件顺序要求；对自有工具包装真实 execute；推动上游将 `plugin.trigger` 返回的 `output.args` 传入 execute；对生产部署做多插件兼容测试。

### OpenCode 没有正式 block/deny 协议

目标版本中 before hook 抛错会：阻止真实工具执行、产生 AI SDK tool-error、通常不会直接形成 session-level error、不会自动进入万象术 fallback、但 LLM 可能重新尝试同一工具。

### Opencode JSON Schema 的硬 required 注入必须移除

旧实现为 `properties` 增加 `warn_tdd` 并追加到 `required`，对 `warn`/`warn_reuse` 也执行类似操作；该实现与“漏填也执行”冲突，必须移除。保留字段、description、examples 和 `x-wanxiangshu-soft-required` / `x-wanxiangshu-soft-min-length` 元数据即可。`enum` 只能放在不参与 Host 校验的说明性元数据中；不得放入会使 Host 拒绝调用的 schema。

需重点检查的是：Zod/Effect Schema 转换后的最终 JSON Schema 是否保留约束、Host 是否实际使用改写后的 schema、tool definition hook 是否覆盖所有工具、alias 和动态工具是否在 hook 之后才注册、某些 wrapper 是否替换了 parameters/jsonSchema、Mux/OMP 是否共享同一工具能力分类。

## 四、修复方案

### 4.1 建立统一 Control Field Policy

建立唯一策略表，以"工具能力"而不是工具名为主键。

每个字段定义：适用能力、软合规提示文本、推荐值、错误消息、是否向 LLM 可见、是否允许到达下游、是否进入审计事件、retry 时是否需要重新提供。缺失和短报告必须可执行，并由结果批评标记。

能力分类：
* FileMutation：coder、edit、write、apply_patch、patch
* ProcessExecution：executor、pty_*
* SubagentDelegation：investigator、meditator、browser、coder delegation
* SearchOnly
* ReadOnly
* ToolCorrection

新增 alias 时只需要注册能力，不需要复制四处名单。

### 4.2 Schema 软提示（第一层）

#### schema 必须在最终导出边界统一增强

正确顺序：
1. host 收集内置工具；
2. 插件加入自定义工具；
3. alias/包装器全部建立；
4. 最后统一执行 schema decorator；
5. 启动时检查所有适用工具均暴露软字段说明和元数据。

不能在各工具定义过程中零散增强。

#### 启动检查不得把软字段变成 fail closed

* FileMutation、ProcessExecution、SubagentDelegation 工具必须在 `properties` 中展示相应字段及软合规元数据；
* 不得把 `warn_tdd`、`warn`、`warn_reuse` 放入 Host 会强制执行的 `required`，不得设置会被 Host 执行的 `minLength`；
* 缺少软字段说明只产生诊断/警告，不得阻止插件启动或工具调用；
* 只有 malformed business args、permission/security denial、parse failure，或净化后控制字段泄漏，才允许启动/执行 fail closed。

#### MCP 工具

如果受保护能力全部来自 registry 或 Task，可以正常注入 schema。如果未来某个敏感 MCP 工具也需要这些字段，必须自己包装该 MCP tool、或修改 OpenCode adapter、或推动上游为 MCP 暴露 definition hook。不能假设现有 `tool.definition` 自动覆盖 MCP。

### 4.3 before Hook 原地验证+删除（第二层）

在 v1.17.13 中，正确做法是原地验证 → 原地提取 → 原地 delete。

禁止 `output.args = sanitizedCopy`，因为真实 execute 仍会使用旧局部引用。

before hook 提取控制字段，保存为 `ControlEnvelope` 到 transient `ToolComplianceStore`（键：session ID + tool call ID）。before hook 写入，after hook 读取并删除。

合规检查发现缺失、空白或非规范值时：before hook 必须保存 violation 并继续真实 execute；after/final result normalization 之后追加一次 `WANXIANGSHU_COMPLIANCE_REPRIMAND`，不改变 success、不抹掉原始输出、且明确不要重复已成功的调用。只有硬业务错误、权限/安全拒绝、解析失败或净化后仍泄漏控制字段时，才返回结构化 tool rejection。

### 4.4 execute wrapper 终极净化（第三层）

目标：抵御 Host 克隆和重建。

在最靠近真实副作用的统一 wrapper 中：
1. 从本次实际收到的 execute args 创建业务参数副本（浅拷贝或深拷贝，因 Host 可能有 Object.defineProperty 或 frozen object）；
2. 再次删除全部控制字段；
3. 检查删除后的对象；
4. 若净化后的业务参数仍发现保留字段，立即拒绝执行（fail closed）；这只适用于安全泄漏，不适用于控制字段缺失；
5. 把控制信息放入独立的 `ControlEnvelope`；
6. 真实工具只能看到净化后的业务参数。

"最靠近 execute"不等于指定某一个现有 wrapper。实施前必须绘制各 Host 的实际调用图：原生工具、插件工具、Mux 代理工具、Subagent 转发工具、PTY、文件修改、executor、MCP、动态注册工具。找到每条路径真正不可绕过的最后公共边界。如果不存在单一公共边界，就需要在少数几个 adapter 上放置同一个共享 sanitizer。

### 4.5 净化后断言

在开发和测试构建中，真实 execute 之前应断言业务参数中不存在 warn、warn_tdd、warn_reuse、已移除的 `amend`（如仍出现则视为残留）、`_ui`（若它只属于 UI）、未来新增的控制字段。

这条断言比"我们调用过 deleteKey"更有证明力。

```text
IF checkControlFieldsExist(args) THEN
    LogErrorAndFailClosed()
```

### 4.6 after hook 职责

after hook 只做：成功结果审计、使用统计、不变量告警。不能承担安全校验，因为失败和 Abort 时 after 根本不会执行。

### 4.7 三层防线正式定案

| 层 | 目标 | 机制 |
| :--- | :--- | :--- |
| 第一层 Schema | 让 LLM 正确生成 | properties + description + examples + `x-wanxiangshu-*` 软元数据；不得使用 Host 强制 required/minLength |
| 第二层 before Hook | 运行时记录 + 原地删除 | 原地 delete（不替换 output.args）+ 保存缺失/短报告 violation，不阻断 |
| 第三层 execute wrapper | 再次验证 + 再次净化 + clean assertion | ControlEnvelope 分离 + fail closed |

### 4.8 各 Host 具体改造

#### Opencode

* 保留 ToolDefinitionHooks 的 schema 扩展职责，但最终改为调用统一 decorator。
* HookExecute 不再自己定义工具名单。
* before 阶段调用统一执行网关。
* after 阶段不得再次要求已经被剥离的字段。
* 动态工具注册完成后重新执行完整性检查。

#### OMP

* 删除或废弃本地重复的 `requireWarn*Omp` 判断。
* coder、executor 之外的 host built-in edit/write/patch/pty 也要走统一能力分类。
* schema 和运行时都调用同一策略源。
* ToolResult hook 不再作为"二次安全兜底"；它只能检查执行网关留下的审计标记。

#### Mux

* 明确注册真正的 `tool.execute.before`。
* wrapper 和插件不能各自决定是否剥离。
* PluginCatalog 中只为少数工具注入字段的做法要改成全目录遍历。
* 对由 Mux 转发到其他 host 的工具，也要保证转发参数是净化后的参数。

### 4.9 仅硬错误作为 tool rejection

malformed business args、权限/安全拒绝、解析失败，或净化后仍存在控制字段泄漏时，返回强类型 `DomainError`，编码为"工具调用被拒绝"的正常 tool result：
* `executionStarted=false`；
* 不产生 session-level error；
* 不进入 fallback 错误分类器；
* 不调用真实工具。

`warn` 类缺失、空白、非规范值以及 todo 报告短不得走该路径；它们必须执行成功并在最终规范化结果中追加批评。不得抛出普通 JavaScript Error，因为可能被上层解释为 session.error、未知执行错误、retryable error、fallback 触发条件。

## 五、验收矩阵

对每个 host、每个 tool alias 都验证：
* schema 中字段存在；
* 软字段说明、examples 和 `x-wanxiangshu-*` 元数据存在，且没有 Host 强制 required/minLength；
* LLM 缺字段时工具仍执行，结果只有一次批评；
* warn 值缺失/空白/非规范时工具仍执行，结果保留原输出并追加批评；
* 合法字段可以执行且不产生批评；
* 实际下游序列化参数不含 warn/warn_tdd/warn_reuse；
* after hook 看不到普通参数中的控制字段；
* 重试不会因为字段已被删除而错误失败；
* null、空对象、额外字段行为一致；
* plugin 注册顺序改变后仍然有效；
* 动态加入的新工具要么被增强，要么产生软合规诊断但仍可执行；
* before hook 删除失效的模拟 Host 下，execute wrapper 仍能净化；
* 真实工具参数中控制字段数量为零；
* 软合规 violation 不产生 session.error 或 fallback；硬错误仍按安全策略拒绝；
* wrapper 直接调用 execute 仍无法绕过；
* after hook 检测到 before 被绕过时只告警，不假装已阻止；
* 替换 `output.args` 的回归测试明确失败并被检测；
* 其他插件替换 args 时产生兼容性警告；
| 硬错误或净化后泄漏导致 before/ wrapper 拒绝时真实 execute 次数为零；
| after 缺失不影响安全；
| 自有 wrapper 存在 clean assertion。

## 六、深拷贝问题专项测试

建立一个模拟 Host：
1. before hook 收到对象 A；
2. Host 在 hook 前保存 A 的深拷贝 B；
3. before hook 删除 A 中的控制字段；
4. Host 把 B 传给 execute；
5. 最终 wrapper 必须再次净化 B；
6. 真实工具收到的对象不得包含控制字段。

同时测试：浅拷贝、深拷贝、JSON round-trip、args wrapper 重建、alias 重新解码、nested args、frozen object、null prototype object。
