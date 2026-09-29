> **⚠️ 规范修订说明（DELEGATE_REVISE.md）**
> 本文档记录了此前只读委托的初始演进方案。最新的规范与执行合同请一律以 **[DELEGATE_REVISE.md](DELEGATE_REVISE.md)** 为准。
> 核心修订包括：
> 1. 字段从意愿性的 `delegate_readonly_rounds` 改为事实性的 `estimated_readonly_rounds`（当前批次完成后后续连续只读查证轮数）；
> 2. 短记 `self_note` 改为严格的条件参数（估计大于 0 时必填非空未来展望，估计为 0 时必须完全省略，禁止出现）；
> 3. 从全量工具装饰改为**逐工具判定（仅 12 个参与工具装饰，其余无增量）**；
> 4. 彻底剔除面向模型的“同伴协作、建立信任、保留控制权”等执行分工动机叙事；
> 5. 副本在共享 schema 下按相同事实含义填写，防递归完全靠真实身份门禁，不再设立“副本必须填 0”的假约定。

# 显式只读委托：clean-break 实现指南

本文给出待实施的方案，不表示代码已经实现。核对基线为 `3674bee82`，日期为 2026-09-27。完整路径从仓库根目录起；正文中的 `Strength/...`、`OpenCode/...` 和 `Wanxiangshu.Owner.*.fsproj` 是 `src/Wanxiangshu/` 下的简称。代码示意中的新增类型和入口是目标接口，不是现有 API。

本次只替换 Strength 的决策方式，不重造会话、权限、模型租约、EventStore、XTrace 和压缩系统。产品语义最终归入 `requirements/<package>/`，本文负责实施顺序和交付检查。

## 1. 已定的方案，不再折回去

用户配置了 Predictor 模型，显式只读委托就默认启用；没有配置，就不启用。不再增加任何独立开关、环境变量或人工确认门槛。

启用后，主模型的每个可见工具 schema 增加必选参数 `delegate_readonly_rounds` 和可选参数 `self_note`。主模型知道：这个整数会让自己的**同伴**接手接下来的若干轮只读调查。同伴使用用户配置的 Predictor 模型，收到当前对话的映射，只能调用只读工具；执行完成的真实工具调用及结果再映射回主模型。面向模型的叙事一律称“同伴 / companion”，不按价格或模型等级介绍他。

以下规则是本次实现的前提：

1. **同一主模型响应包含多个并行工具调用时，预算取所有整数的 `max`。** 不取 `min`，不求和，不取最后返回的值，也不要求它们一致。必须把取 `max` 的行为告诉主模型。
2. **没有 Strength/Delegate 专属的结果字节上限。** 不设置 `maxFrameBytes`、token 配额、按长度丢弃、保留小前缀或“过大退回零步”的替代规则。工具原有截断、provider 上下文限制和主模型原有压缩照常工作。
3. **不再限制两轮。** 使用经过校验的非负整数，不再保留 `K0/K1/K2` 档位。不暗中再加一个 Delegate 业务轮数上限。
4. **这是委托上限，不是必须做满的次数。** 同伴可以提前结束；预算到达后，主模型也不必立刻修改代码。
5. **不保留旧预测路径。** 删除 `P1/P2`、成本公式、收益门槛、学习样本、control holdout 和旧 rollout 分支。不能以“先兼容一下”为由让它们继续参与是否启动、启动几轮的判断。
6. **配置 Predictor 就启用，不再二次 opt-in。** 配置存在性与当下容量分开；不通过独立 enabled、环境变量、消融选项或 canary 指纹开关否决这项配置。真实权限、取消和持久化不变量仍然约束每次执行。
7. **把协作写成逐渐了解、建立信任的过程。** 同伴在某类调查中表现好，就尽可能多信任他、多让他工作；不善于某些状况，下次就少用或不用。拿不准时，可把“距离第一次 edit 还有几步”当作抛砖引玉的基线，但它不是最优解，也不能替代实际判断。
8. **`self_note` 是可省略的第一人称自省短记。** 它随原始工具调用保留在对话里，同伴通过既有映射自然看见。不新增 hint 搬运、独立消息、必填计划或信任评分系统。

保留 `Strength` 命名空间和 `speculative-investigation` requirement 包名。它们还承载只读执行、材料映射和历史事实；没有必要为了改决策方式全仓改名。删除失去含义的 `Prediction`、`Speculate`、`Rollout` 名字，而不是保留旧实现再套一层新 facade。

## 2. 先读这些代码，别只改 CostModel

以下是已核对的现状，也是改动入口。

| 位置 | 当前行为 | 本次处理 |
| --- | --- | --- |
| `src/Wanxiangshu/Strength/Prediction/CostModel.fs` | 计算 `V0/V1/V2` | 删除文件及 `.fsi`、引用、测试入口 |
| `src/Wanxiangshu/Strength/Prediction/Predictor.fs` | 从角色、最近请求、字节桶估计 `P1/P2` | 整体删除 |
| `src/Wanxiangshu/Strength/Policy.fs` | eligibility、holdout、K1/K2 收益选择混在一起 | 只保留并改写基于真实权限和生命周期的准入 |
| `src/Wanxiangshu/Strength/Budget.fs` | `K0/K1/K2` 联合类型 | 改为非负整型预算 |
| `src/Wanxiangshu/Strength/OpenCode/PluginScope.fs` | Replica 资源、fuse、预测缓存、反事实收集器 | 保留资源和 fuse；删除预测与样本缓存 |
| `src/Wanxiangshu/Strength/OpenCode/Speculate.fs` | 在 owner transform 中冻结映射、按策略启动、发布 Prepared | 改为消费明确授权；文件改名 `Delegate.fs` |
| `src/Wanxiangshu/Strength/Prediction/BatchCollector.fs` | 收集完整工具批次，保留调用顺序 | 保留，移入 `Strength/Replica/BatchCollector.fs` |
| `src/Wanxiangshu/Strength/Replica/Runtime.fs` | 子会话运行、计数、终结与清理 | 改计数、授权绑定、Predictor 路由；删除字节配置 |
| `src/Wanxiangshu/Strength/Replica/Transform.fs` | owner 镜像加本次已完成 batches，到 K 停止 | 保留映射；预算不能再从当前可见历史长度推算 |
| `src/Wanxiangshu/Strength/Frame.fs` | 完整交换、digest、ID 重定位、字节上限 | 保留结构和完整性校验；删除大小准入 |
| `src/Wanxiangshu/Strength/Events.fs`、`Persistence/*`、`Projection/Model.fs` | Prepared/Promoted/Traced/Abandoned | 保留消费证明，补授权的一次性生命周期 |
| `src/Wanxiangshu/OpenCode/Plugin/PluginHooks.fs` | tool definition、before、after 接线 | 加字段、剥离调度元数据、保留原调用证据 |
| `src/Wanxiangshu/OpenCode/Host/ManagerReviewContract.fs` | 给 schema 加 `contract`，执行前隐藏、执行后恢复 | 可参考手法，不把新协议塞进这个评审模块 |
| `src/Wanxiangshu/OpenCode/Plugin/PluginTransforms.fs` | 回放、XTrace、Companion、XWire、冻结、Strength 顺序 | 分离授权证据捕获和实际启动，守住压缩边界 |
| `src/Wanxiangshu/OpenCode/Host/ModelRouting.fs` | scheduler 只有角色、占用、previous | 增加明确的只读委托用途，不篡改角色 |
| `resources/wanxiangshu.mjs` | 已有 `PREDICTOR_POOL` 和 `pools.get('predictor')`，容量查询还混合了 provider 健康 | 配置存在性直接从同一池派生；委托用途明确选 Predictor 池，不新增第二份模型配置 |

有两个容易漏掉的具体问题：

- `Replica/Runtime.fs` 的 `requestsFor` 中存在 `min 2 (List.length state.Batches + 1)`；构造器还有默认 `65536` 的 `frameByteLimit`。两者必须真正删除。
- `Persistence/Store.fs` 解码 frame 时，把保存的 `byteLength` 传给 `StrengthFrame.tryBuild` 当上限。删除大小门槛以后，仍须比较重新计算的长度和 digest；不能连完整性校验一起删掉。

开始实施时先重新查看 `git status` 和相关 diff。不要覆盖从本文基线之后产生的用户改动。

## 3. 参数契约

### 3.1 名字和单位

字段固定为 `delegate_readonly_rounds`，表示：**当前主模型这整批工具执行结束后，最多授权同伴继续多少次 provider 生成请求。**

一轮可以产生一个工具调用，也可以产生多个并行工具调用。一轮不是一次文件读取，不是一个 tool result，不是一个 transform callback，更不是一次重放。

当前主模型已经生成的工具调用不计入预算，正常执行一次。它的选择工具和参数的推理成本已经发生，不让 Replica 再选一遍。

示例：

```text
主模型同一响应：
  read(A, delegate_readonly_rounds=2)
  grep(B, delegate_readonly_rounds=5)
  read(C, delegate_readonly_rounds=0)

宿主执行完这一整批，得到 N=max(2,5,0)=5。

Replica 请求 1：glob(D) + read(E)       已用 1 轮
Replica 请求 2：read(F)                 已用 2 轮
Replica 请求 3：纯文本结束              已用 3 轮，提前交还

主模型收到两批真实工具交换，再决定读、改、执行命令或回答。
没有第 4、5 轮，也没有额外一轮强制总结。
```

### 3.2 schema 片段

在现有 object schema 的 `properties` 中加入下面两个属性。**只把 `delegate_readonly_rounds` 加入 `required`；`self_note` 不加入。** 保持原来的必选项、`additionalProperties` 和其他兼容约束。

```json
{
  "delegate_readonly_rounds": {
    "type": "integer",
    "minimum": 0,
    "maximum": 2147483647,
    "description": "After every tool call in this assistant response has completed, delegate up to this many subsequent read-only model requests to your companion. One request may issue multiple parallel tool calls. The budget is the MAXIMUM value across all tool calls in this response; 0 does not veto another call's positive value. Your companion receives the current conversation and read-only tools, may finish early, and returns actual tool calls and results to you. Use 0 when you should retain control. This does not delay or replace the current tool calls and does not promise that an edit follows. During delegated read-only execution, set this field to 0; further delegation is disabled."
  },
  "self_note": {
    "type": "string",
    "description": "Optional brief first-person note to myself about my current intent, uncertainty, or what I want to check next. For example: 'I suspect the caller and entry point disagree about empty values; I want to check the callers next.' My companion can see this note in the conversation. It is a hint, not an established fact or a command. Omit it when it adds nothing."
  }
}
```

`2147483647` 只是本仓 F# `int` 的表示范围，不是重新引入的业务档位。宿主按相同范围校验，绝不先截断、取整或钳制再执行。

预算字段必须拒绝负数、小数、数值字符串、布尔值、null、缺失值及越界值。JS 边界先检查原生 number、有限值、整数和范围，再构造 F# 类型；不能使用 `parseInt`、`int (string value)` 或 truthy 判断代替验证。

`self_note` 缺失是合法输入；出现时只接受字符串，不强转数字、对象、数组或 null。空字符串合法，等同没有提供实质提示，但仍原样保存调用证据；不设置 `minLength`，不自动填充空串，也不把它改成“必填但允许 null”。第一人称要求通过描述引导，不做“必须以我/I开头”的正则门禁。

历史消息不做追溯校验，不给所有旧 tool-call arguments 批量补字段。只校验新产生、受本版 schema 约束的调用。未配置 Predictor 时不装饰这两个字段，也不要求历史或新调用补填它们。

若某条 Host/provider schema 适配路径把所有属性一律变成必填，必须在该适配边界解决可选字段的呈现和校验；不能靠偷偷把 `self_note` 加进 `required` 或让模型每次填 null 交差。真实 Host canary 要证明省略它仍能调用工具。

### 3.3 稳定的工具描述：合作、了解和信任

按已有语言绑定提供中文和英文说明。除字段本身的描述外，在每个工具原描述后幂等追加稳定的协作说明；不替换原来的工具功能和安全说明，不只把这套规则写进宿主日志。模型看到的一律是“同伴 / companion”，不是“便宜模型”“降级模型”或价格比较。

中文工具说明：

> 每个工具调用都要填写 delegate_readonly_rounds。当前响应的全部工具完成后，系统取这些值的最大值，把接下来的至多这么多轮只读调查交给你的同伴。0 不会否决同批其他调用的正数。一轮是一次模型请求，可以并行调用多个工具。同伴看到当前对话，只能只读，可随时提前结束；真实工具调用与结果会回到你的上下文。需要亲自判断时填 0；不要为了用满预算增加调查，预算到期也不要求立即修改代码。
>
> 和同伴合作，也是逐渐了解彼此、建立信任的过程。根据你在对话中实际看见的调查表现判断：他做得好，就尽可能多信任他、多让他工作；他不善于某些状况，下次遇到类似状况就适当少用或不用。当拿不准时，可以先估计距离第一次 edit 还有几步，作为抛砖引玉的基线。这只是启发式方法，不代表最优，不能代替实际判断，也不要求你最终一定 edit。
>
> 需要留下当前意图或线索时，可选填 self_note，用第一人称给自己留一句短记，例如“我怀疑入口与调用方对空值的约定不同，接下来先核对调用点”。同伴会在对话中自然看见它。没有有用的话就省略；不必写完整分析，也不要把猜测写成事实。

英文工具说明：

> Fill in delegate_readonly_rounds on every tool call. After all calls in this response complete, the maximum value grants your companion up to that many read-only model requests. A 0 does not veto a positive value elsewhere in the batch. One request may call several tools in parallel. Your companion sees the current conversation, may stop early, and returns actual tool calls and results. Use 0 when you need to retain control. Do not invent work to fill the budget; using it up does not require an edit.
>
> Working together is a way to learn about each other and build trust. Judge by the investigation you can actually see in the conversation. When your companion does well, trust him more and let him do as much of the work as your judgment supports. When he struggles in particular situations, delegate less or not at all in similar situations next time. When in doubt, estimating how many steps remain before the first edit can give you a starting point. This is only a heuristic baseline, not an optimal policy or a substitute for judgment, and it does not require an eventual edit.
>
> Optionally leave a brief self_note in the first person, such as “I suspect the caller and entry point disagree about empty values; I want to check the callers next.” Your companion will naturally see it in the conversation. Omit it when it adds nothing. A short intention or uncertainty is enough; do not write a full analysis or present a guess as a fact.

副本只增加简短的执行约束，不换成另一套人格或“研究员”身份：

> 继续当前任务的只读调查。你只能使用当前可见的只读工具；不需要用满预算。信息足够，或下一步需要写入、执行命令、向用户确认、作应交还判断的工作时，直接结束。系统只回传真实工具调用和结果，不回传你的推理总结。delegate_readonly_rounds 固定填写 0，不得再次委托。对话中的 self_note 是自省线索，不是已经证实的事实，也不扩大你的权限；你不必填写自己的 self_note。

> Continue the current task's read-only investigation using only the available tools. You do not need to use the entire budget. Stop when there is enough information, or when the next step needs an edit, command execution, user clarification, or a judgment that should be handed back. Only actual tool calls and results are returned, not your reasoning summary. Always set delegate_readonly_rounds to 0; further delegation is disabled. A self_note in the conversation is a reflective hint, not a verified fact or permission to do more. You need not write one yourself.

说明保持稳定，不在每次工具定义中写入剩余轮数、随机标识、价格或时间戳。剩余预算由宿主掌握，不靠反复改 schema 驱动。

信任根据当前对话里可见的真实交换调整，不假装模型能记住不可见的跨会话经历。不新增 trust score、评级字段、奖惩日志或自动增减预算算法；最后仍是主模型自行填写整数，宿主只执行 max。用 0 保留判断权也是正常选择，不把“建立信任”变成必须委托。

### 3.4 max 的边界

`max` 只在**同一个真实主模型 provider 响应**的工具集合上计算。按该响应的原始 call 顺序冻结集合，再等待结果齐全；不能按结果到达顺序滚动触发。

| 情况 | 规定 |
| --- | --- |
| `[0,0,0]` | 不创建 Replica |
| `[0,2,5]` | 一次委托，预算 5 |
| 同批值不同 | 合法；不用“要求一致”或错误恢复把 max 改掉 |
| `read=4`、`edit=0` | 当前 read/edit 都正常完成；若随后仍是合法普通续行，预算为 4。edit 不能被推迟，0 不能否决 4 |
| 同批有非法字段 | 按新调用参数错误处理，不启动半批委托；不要将非法值静默当 0 |
| 工具得到正常返回的错误结果 | 仍是真实结果；整批完整且 owner 正常续行时可按 max 委托 |
| 调用缺结果、owner 取消、物理执行被替代 | 不在残缺或失效证据上启动 |
| 当前操作已经终结会话或进入非普通续行 | 不阻碍原终结/恢复流程；授权按明确原因结束 |
| Replica 输出正数 | 不能产生二次委托；剥离后只执行获准的只读工具，可记录协议偏离，不回退到 owner 分支 |

这里的生命周期准入和 `max` 是不同的事。前者决定当前是否存在可委托的续行，后者只决定一份合法授权的预算。

### 3.5 self_note：留在对话里，不另造提示传输

`self_note` 是给自己留的简短意图、疑点或下一步线索。推荐“我目前想确认……”“我还不能确定……，接下来留意……”这样的口气，不写“你去做……”“便宜模型负责……”等面向下属的命令，不要求输出完整思维过程。

它与预算独立：预算为 0 时可以有短记，预算为正时可以没有。短记不提供额外权限、不决定轮数、不触发委托；缺失时不自动生成、追问或重试。

同批多个调用各自可以有或没有短记，全部保留在各自原始 arguments 中，按原始调用顺序出现在对话映射里。**只对整数取 max；不对短记取 max、不挑最大预算对应的那一条、不合并成提示段，也不丢掉其他调用的短记。**

传递只走一条既有路径：原始工具调用记录 → 冻结 owner 对话 → `StrengthFrame.tryLocalizeMirror` 的 ID 重定位 → 同伴可见对话。不得再复制到 system prompt、bootstrap、额外 user 消息、子会话启动参数或新的 hint 事件。第 4.3 节的业务参数剥离不能删掉这条路径里的原始短记。

不为短记保留特殊上下文窗口、补发消息或建立独立缓存。常规截断/压缩照常适用；这是一项便利，不是运行正确性所必需的隐式指令。

## 4. 接入真实 Host 接口，不发明 hook

### 4.1 已核实的接口限制

本基线 `package.json` 的开发依赖固定为 `@opencode-ai/plugin@1.18.29`、`opencode-ai@1.18.29`。已读本机 `node_modules/@opencode-ai/plugin/dist/index.d.ts`：

```ts
"tool.definition"?: (
  input: { toolID: string },
  output: { description: string; parameters: any }
) => Promise<void>

"experimental.chat.messages.transform"?: (
  input: {},
  output: { messages: { info: Message; parts: Part[] }[] }
) => Promise<void>
```

所以不能在 messages transform 中假装有 `tools` 可以改，也不能在 tool.definition 中假装有 `sessionID/model` 可用于选择性装饰。

**本指南选择与现有接口相符的实现：从同一模型配置确定 Predictor 已配置后，固定装饰经过该 hook 的工具 schema，实际是否接纳授权由执行时的真实身份决定。** 主模型的全部可见工具必须带必填预算、可选短记及协作说明；同一装饰也可能出现在 Replica 或其他内部请求的工具上，它们预算填写 0，短记仍可省略，且没有发出授权的权能。这是静态 schema 的明确代价，不是假装实现了只有主模型能看见字段。

本次不为了隐藏 Replica 上的这两个字段去 fork Host，不加全局 `currentSession/currentModel` 开关，也不靠异步回调顺序猜身份。模型配置必须在注册/呈现工具定义之前完成加载和验证，不能让首轮 schema 漏掉协议。将来若要严格做每请求 schema 分化，应另立 Host 能力改动；它不是本次实施的隐含前提。

真实 Host canary 必须枚举最终 provider-visible tools，覆盖内建、插件、MCP/动态工具以及工具发现后的新定义。不能只证明自定义的 read 工具被装饰。发现某条路径绕过 hook，就先补真实的工具定义边界；未覆盖所有主模型可见工具时不得宣称交付完成。

### 4.2 新增薄模块

新增 `src/Wanxiangshu/OpenCode/Host/ReadonlyDelegationContract.fs/.fsi`，只负责：

- 稳定且幂等地装饰 schema；
- 从新调用参数中验证并拆出预算；短记存在时校验字符串形状，不把它抽成独立业务消息；
- 保留 provider 原始 arguments 证据，给工具业务代码提供不带 `delegate_readonly_rounds` 和 `self_note` 的参数；
- 提供中英文协议与协作说明，按契约版本幂等追加到原工具描述。

不要让这个模块创建子会话、发 provider 请求、算 max 或写业务事件。

遇到任一同名属性时，只接受与本协议相同的重复装饰；不覆盖工具自己定义的冲突属性。`required` 不是数组、根 schema 无法合法扩展时明确报错，不能悄悄发布不完整协议。若 Host 同时给出 `parameters`、`jsonSchema`，两份实际用于发送/校验的视图都要一致，包括短记的可省略性。

保留原 schema 的组合结构。不能为了加字段把 `$ref`、`oneOf`、nullable 或 strict 约束展平为另一种意思。先用当前真实工具集合证明支持范围；不支持的定义是接入问题，不能把预算改成可选，或把短记改成必填。

### 4.3 原始参数和工具参数分开

例如 provider 产生：

```json
{"filePath":"src/A.fs","delegate_readonly_rounds":5,"self_note":"我怀疑入口与调用方对空值的约定不同，接下来先核对调用点。"}
```

工具业务实现只应收到：

```json
{"filePath":"src/A.fs"}
```

原始调用记录仍须原样保留整数 5 和 `self_note`。预算供本次授权使用，短记通过现有对话映射自然可见；两者都不能因为清洗业务参数而从历史证据消失。不要让评审 contract 的隐藏/恢复覆盖掉新字段，也不要反过来破坏评审 contract。

`StrengthFrame.tryLocalizeMirror` 只重定位调用 ID，保留原始 arguments，包括其中的 `self_note`。**不增加 `carryHint`、`injectSelfNote`、`ReplicaHint` 等第二条搬运路径。** 普通对话已经带着它，不必再发一次。

优先在调用边界分出独立业务参数对象，避免修改持久化调用证据。若沿用本仓 `ManagerReviewContract` 的隐藏/恢复手法，必须用真实 Host 测试证明参数对象的所有权、持久化时机和异常路径成立：不能假设 `after` 一定运行，也不能假设它拿到的就是 `before` 的同一个对象。

不得在单个 `tool.execute.after` 中启动 Replica。该回调不知道整个并行批次是否完成，只能完成该调用的边界工作。

## 5. 预算与批次：纯逻辑先落地

### 5.1 用类型表达非负数

`Strength/Budget.fs/.fsi` 改成一个小型非负整数值类型，例如：

```fsharp
[<Struct>]
type ReadonlyRoundBudget = private ReadonlyRoundBudget of int

module ReadonlyRoundBudget =
    let tryCreate value =
        if value < 0 then Error "negative-readonly-round-budget"
        else Ok(ReadonlyRoundBudget value)

    let value (ReadonlyRoundBudget value) = value
```

JS/JSON 边界已做严格类型和范围校验后才能调用 `tryCreate`。内部消费者只接收该类型；启动 Replica 要求预算大于 0。0 是不启动，不是一种物理 Replica 模式。

添加纯函数：对一批**已校验**的预算取最大值，空工具集合得到“不存在授权机会”。空集合与一个显式填 0 的工具批次可以在证据层区分，但都不启动。

### 5.2 完成批次的定义

沿用并收紧现有 collector：同一 assistant request 的所有 call 都有且只有一个 result，才形成一个完整 batch。结果乱序到达时按原始 call 顺序归一。孤儿结果、重复结果、重复 call id、跨下一条 user/provider 边界拼接结果一律不能变成有效 batch。

区分两类数据：

1. 主模型的来源批次，带真实来源身份和每个调用的预算，用来形成一次委托请求；可选短记留在原始 arguments，不参与聚合；
2. Replica 的完成批次，只是本次回传的真实只读交换，不再产生授权。

不要因为字段相似，就让一个函数扫描全部映射历史后同时推导这两者。

### 5.3 预算按获准的 provider 请求记账

计数必须来自运行时接纳请求的事实，不能来自 `List.length batches`。纯文本结束占一轮；已外发后失败的一轮也占一轮；同一请求的重复 transform/terminal 通知不再占轮。

目标逻辑：

```text
admit(exactRequestKey):
  已处于语义终态                  -> 拒绝
  这个 exactRequestKey 已获准      -> 幂等返回原 admission
  requestsAdmitted >= budget      -> 结束委托；禁止外发
  否则                           -> 记一次 admission，再允许本轮外发

observeCompletedBatch(exactRequestKey, batch):
  必须对应已经获准的真实 Replica 请求
  相同内容的重复观察              -> 无操作
  同一身份出现冲突内容            -> 不变量错误
  新的完整批次                    -> 追加材料；不再增加请求计数
```

`exactRequestKey` 必须由 Host 的实际物理请求/attempt 边界提供。不能用当前时间、消息总数、一次 callback 一个 UUID 或仅 session id 替代。若现有 transform 入口尚无足够身份，就补真实 admission 适配边界，并以 canary 证明它与外发次数一致；不要根据尚未出现的 assistant message 猜未来 ProviderRun。

达到 N 时，已经获准的第 N 轮全部工具结果仍要收完。禁止的是第 N+1 次请求，不是中途砍掉第 N 轮的工具，也不额外请求一次总结。Replica 不新增自动 provider 重试流程；重复通知与真实重新外发必须分清，不能把重发当免费轮次。

### 5.4 截断、压缩不能重置状态

当前映射中只剩两批，不代表只用过两轮。Host 截断旧工具结果、剪掉旧消息或发生普通压缩后，`requestsAdmitted` 和已观察请求集合仍由本次运行资源拥有，不能从变短的对话重建为更小值。

同理，收集新结果时必须区分 Replica 真实物理输出和注入的 owner mirror/local replay。不能把镜像里十轮旧调用算成这次完成十轮，也不能在每次 transform 中把相同结果重新追加。

## 6. 授权必须只消费一次

### 6.1 不使用 session 上的 lastBudget

新增纯模型 `Strength/Delegation.fs/.fsi`，表达来源授权及生命周期。进程内资源可以保存活跃子会话、等待器和已接纳请求，但不能独自决定一次授权是否已经消费。

一份请求至少绑定：

```text
DecisionId                 本次授权的稳定 ID，可继续用现有强类型
OwnerSessionId
OwnerLogicalRunIdentity    沿用现有 authority/identity 证据，不另造字符串身份体系
AuthorityRootUserMessageId
SourcePhysicalUserMessageId
SourceProviderRun          发出这一整批工具调用的真实主模型请求
SourceToolCallIds           固定顺序的完整调用集合
RequestedRounds            同批 max
ContractRevision           本版字段说明/schema 契约的稳定版本
```

这份授权不增加 `SelfNote`、`Hint` 或 `TrustScore` 字段。短记已经在来源调用的原始对话证据里；冻结镜像和 anchor digest 按既有规则覆盖它，不需要再持久化一份提示副本。

`DecisionId` 从协议版本、owner logical run 和 `SourceProviderRun` 确定性派生。不以工具完成顺序派生，也不以未来的目标请求派生：同一个来源不能因为重试换了 target 就得到第二份预算。

明确区分：`SourceProviderRun` 是授权来源，`TargetProviderRun` 是稍后第一次消费回传材料的主模型请求；二者不相同。

### 6.2 最小持久化状态

在现有 Strength EventStore 领域里增加三类事实，不创建第二套日志：

| 事实 | 何时写入 | 必须证明的事 |
| --- | --- | --- |
| `DelegationRequested` | 来源整批完成，获得真实 owner 新输出证据后 | 来源、max、协议版本和 authority 已固定 |
| `DelegationBound` | 为合法主模型续行冻结 target 与 mirror 后，副本首次外发之前 | 这份请求只被一个执行消费；固定 target、ReplicaSessionId 和 anchor digest |
| `DelegationClosed` | 无材料结束、不可继续、取消、被替代或恢复时放弃未完成执行 | 同一授权不能在下一次 transform 又启动 |

有有效结果时继续使用 `Prepared -> Promoted -> Traced`；材料废弃使用现有 `Abandoned`。成功路径不再额外写一个含义相同的 `Closed`。投影用明确联合类型表达 Requested、Bound、Prepared、Promoted、Traced、Closed/Abandoned，拒绝非法状态跳转，不堆布尔值猜组合。

`Requested` 是一次调度请求事实，不是“模型准确预测了未来”。`Bound` 是实际消费授权的事实，不是 provider 已经看过回传材料的证明。

所有追加都沿用现有 canonical Integrator、payload_refs 和持久化回执。`Unknown` 必须解析现有存储事实；无法证明时不能外发。不得把存储错误降成内存里的 `consumed=true`。

为了让 Bound 带上真实 `ReplicaSessionId`，把当前“创建并立即运行 child”的入口拆出薄的准备阶段：先创建尚未发送 prompt 的 child 并建立内部身份，再持久化 Bound，最后才允许发送 prompt 和进入模型准入。创建空 child 不得预占模型容量。Bound 写失败时清理空 child；写入未知时先解析事实，不能先跑起来再补日志。

恢复时用 Bound 中的 child 身份隔离和清理残留执行。即使本进程的活跃表已经丢失，晚到的旧 child 回调也不能被当成普通 Work；先恢复/确认身份边界，再接纳后续回调。没有 Bound 的空 child 从未获准发 provider 请求，只作为空资源清理，不能补发 prompt。

### 6.3 因果父边和冲突规则

以一次 DecisionId 为单位沿用现有事件 stream，父边为：

```text
Requested -> Bound -> Prepared -> Promoted -> Traced
Requested -> Closed
Bound     -> Closed
Prepared  -> Abandoned
```

图中的 Closed 表示从 Requested 或 Bound 的合法关闭，只写一个终结事实；不是先 Bound 后再回到 Requested。`parentsFor` 必须引用实际合法前驱，不能给所有关闭事件填同一个父边。

同一来源重复提交相同 Requested 是幂等；同一来源改 max、call 集合或 authority 是冲突。重复 Bound 相同 target/child/anchor 是幂等；改其中任何一个都不能偷偷开启第二个副本。

在新版结构中，`RequestedRounds` 由 Requested 唯一持有，Bound 和 Prepared 引用同一 DecisionId。删掉旧 Prepared 里的档位预算，不在每层复制一个可各自修改的预算字段。Replica binding 可以从不可变投影取得本次执行所需的预算快照，必须和来源请求一致。

Prepared 关联已 Bound 的授权；Promoted 仍须证明**该 exact target** 产生真实非空输出。禁止以“tool.after 跑过”“child 完成了”“owner session 还活着”代替消费证据。

### 6.4 恢复决策表

| 恢复后事实 | 处理 |
| --- | --- |
| 没有 Requested，仍有本版当前真实来源批次 | 可以重新记录同一请求；不能扫描任意旧历史找正数 |
| Requested，没有 Bound | 在同一未失效 logical continuation 上重新准入；新用户输入/authority 替代则 Closed |
| Bound，没有 Prepared，进程内原执行已不存在 | 写 Closed，主模型继续；不重新运行一遍相同预算 |
| Prepared，target 尚可合法消费 | 加载原 payload，渲染相同材料，不重跑只读工具 |
| Promoted，尚未 Traced | 按现有规则在正确因果位置回放，再进入 XTrace |
| Closed/Abandoned | 不启动、不复活 |
| 追加状态未知 | 查询并解析确切事实；未证实前不猜结果、不外发 |

Bound 后崩溃但尚未得到 Prepared 时，允许损失本次同伴调查机会。它不是任务正确性的必要条件；不能为省一点调查损失引入自动重复消费。

## 7. owner transform 的接线顺序

### 7.1 把“取授权”和“启动副本”拆开

当前 `PluginTransforms.normalTransform` 大致为：绑定物理 attempt、relay、Strength replay、XTrace capture、Companion、XWire、冻结 attempt、Enforcer、StrengthSpeculate、其他提示和 sanitize。不能在末尾从压缩后的 messages 里重新猜来源授权。

调整为两段：

**第一段：在普通 owner 分支、取得真实来源批次后，且在任何压缩/消息替换丢失该批元数据之前，捕获并记录 Requested。** 输入必须来自 Host/已协调的真实物理批次，带 exact 来源身份；不是从 provider-visible 合成消息倒推来源。若只能在 tool.after 拿到单个值，先归属于 exact 来源批次，等批次封口后再记录请求，不能拿一个 session 级 mutable int 拼起来。

**第二段：原先启动 Strength 的位置，读取 canonical Current 中待处理的 Requested，在 target、authority、mirror 已冻结后，准入、写 Bound、运行 Replica、Prepared、渲染。** 改名后的 `StrengthDelegate.tryApply` 不再调用任何预测或成本逻辑。

两段之间通过持久化身份关联，不通过模块全局的“最近一次预算”关联。一次 transform 内已有的纯证据可以直接传参，不应为传一次参数额外造跨回调缓存。

### 7.2 准入范围

沿用现有合法普通 Work/root authority 边界，以及实际活跃角色和工具能力规则。不得恢复已经废弃的 coder/inspector/inquiry 角色来满足过时的文字列表；同步改规范，使之和本仓当前合法角色一致。

必须满足：Predictor 模型已配置；来源是新鲜的真实 owner 输出；owner logical run 未被替代；Requested 尚未关闭或绑定；待消费的是合法 WorkMain 续行；不是 Replica、其他 InternalLeaf、interaction repair、显式恢复特殊分支或 prefix probe；EventStore、Host 边界与进程 fuse 健康。这些是实际执行的不变量，不是另一个功能开关；不再要求人工配置 canary 指纹。

非只读的**来源批次**不自动否决委托。当前操作已经照常做完，委托指的是接下来；不能把“副本只读”误写成“发出授权的当前批次也只能只读”。最终是否存在普通续行由现有 lifecycle 决定。

未配置 Predictor、配置错误与暂时没有容量须分开：没有配置就是没有新委托；配置结构非法明确报告；有效配置下 scheduler 的 `null` 仍按既有 pending/cancellation 规则等待，不偷换成 Predictor 缺失，也不回退到 owner 的模型池。

### 7.3 压缩和普通恢复不被 Delegate 接管

保留 `PrefixPresentationHorizon.TentativeCold` 的隔离，不在 prefix probe 中启动副本或反射旧材料。因为普通压缩暂时经过 probe，并不自动把同一 logical continuation 的未 Bound 请求判成失效；Requested 的事实不会因 messages 变短而消失。

Delegate 不新增压缩阈值，不主动“压到可以委托为止”，不禁用 Host/工具既有截断。主模型确实换成了另一个 physical target 时，使用既有 exact-target 恢复/废弃规则；不把原 Prepared 擅自冒名渲染给新 target。

回传注入后形成的**实际完整请求**必须仍进入正常的上下文大小/失败/压缩处理路径。若当前顺序导致“常规检查看不到回传尾部，溢出后又反复重送同一超大输入”，修的是通用请求呈现与恢复衔接；禁止通过恢复 `MaxFrameBytes` 掩盖问题。

为此必须有真实长结果 canary：出现常规工具截断或主模型压缩时，执行可以按通用规则向前推进，不重复委托、不绕过压缩、不形成无限重送。不能为了让压缩提前看见 Candidate 就把尚未消费的 Prepared 假装写进 XTrace 历史。

## 8. Replica：保留执行骨架，替换控制来源

继续使用当前 `InternalLeaf × Attached(owner, StrengthReplica)`，每个 owner 至多一个活跃副本，不跨决策复用 transcript。

身份不改：继承 owner 的 participant、Role、Persona、provenance/version 和语言。变化的是执行用途、模型目标、可见工具集合和短期控制权，不是“扮演另一个人”。

继续只允许本仓已有 `read/glob/grep`。工具 schema 和执行门禁必须同源。即便 shell 命令看起来只读，也不把 `exec/bash/pty` 加进去；也不允许通过 fork、MCP 写操作或通用 JS 工具绕过只读能力。

映射继续使用冻结 owner 对话和本次已完成批次；保留完整 call/result、原始 arguments 中的可选 `self_note`、调用顺序、确定性 ID 重定位。不要复制原 owner wire-local call id，不把当前新 Candidate 反射回生成它的 Replica。对短记不做提取、改写、去重合并或额外注入；原调用出现一次，短记就随之出现一次。

回传材料只包括真实只读工具交换。Replica 的纯文本、reasoning、未执行工具计划、最终总结不写入主模型上下文。纯文本输出是提前结束信号，不是待主模型采信的研究报告。实际调用 arguments 中若本来含有可选短记，它仍属于调用记录，不被额外抄成“已证实结论”；主模型依据真实工具结果判断同伴表现。

当前 `TextCompleted` 可以继续作为提前交还出口，不新增 `yield` 工具。预算用完与主动结束都必须保留语义终态/物理尾部的分离：先停止接纳新请求，再按真实 Host terminal 清理 child 和租约。晚到 callback 不能落回普通 owner 分支。

普通 Replica provider/tool 失败按当前决策结束；已经完成且通过结构/权限校验的完整前缀可以回传，残缺批次不补造。出现权限突破、材料冲突等不变量失败时沿用明确的失败关闭规则；不要把普通文件不存在当成进程级熔断。

不增加 deadline、sleep、按毫秒竞争的提前结束或新的 failure budget。取消来自 owner/operator 和现有确切生命周期事实。

## 9. 配置 Predictor 即启用，委托实际走 Predictor

### 9.1 配置存在性与运行容量分开

已核对 `resources/wanxiangshu.mjs`：模板已有 `PREDICTOR_POOL` 和 `pools` 中的 predictor 条目。用户继续在这份模型配置中设置 Predictor，不新增模型配置文件或启用选项。模板本身若含非空有效 Predictor 池，使用该模板就默认启用。

启用状态从实际模型配置派生：Predictor 槽位不存在或候选为空，表示未配置；目标合法且非空，表示已配置；目标结构非法则是配置错误。模型配置所有者提供只读的配置存在性查询，工具装饰和委托准入共用同一结果，不维护第二份 enabled 真相。

当前 `hasTheoreticalCapacity` 还检查 provider 健康，不能用来代替配置存在性，也不能用一次 route 返回 null 判断用户没配置。容量暂满或 provider 不可用时，已配置状态不变；具体请求按现有等待、失败和取消规则处理。

`Replica/Runtime.StartReplica` 目前沿用 owner 的 canonical role，调度 ABI 为 `route(role, running, previous)`，尚无委托用途。配置存在并不保证 Replica 已经选到 Predictor：下节还须明确修改目标选择。

participant 角色保持不变，`SendPrompt` 保持原有准入约束，模型目标仍归唯一 MJS 配置管理。保留的是 Predictor 模型槽位，不是待删除的 `StrengthPredictor` 统计预测器。

### 9.2 本次明确扩展调度 ABI

保留前三个输入含义，新增一个独立于身份的用途参数：

```js
export const routingProtocol = 2

export default function route(role, running, previous, purpose) {
  // purpose 只有 "normal" 或 "readonly-delegate"。
  // readonly-delegate 从现有 predictor 模型池选择；normal 沿用角色池。
  // role/participant 身份不变，provider 容量策略仍归用户的 MJS 配置。
}
```

F# 内部使用封闭类型 `ModelExecutionPurpose.Normal | ReadonlyDelegate`。它由 Host 真实请求类型/授权 binding 推导，沿 physical admission 传到 scheduler；不从工具参数、用户文本、模型自行声明或角色名推导。

修改 `ModelRouting.fs/.fsi`、调用它的资源/Host admission 边界和推荐模板，使 fresh Replica 请求明确传 `readonly-delegate` 并选择实际 Predictor 池。其他请求传 `normal`。当前 role/participant 仍不变。

同一次配置加载提供第 9.1 节的只读存在性查询，并在工具定义注册前完成解析。查询直接基于实际模型槽位，不探测当前空闲容量，不让用户填写布尔值。`routingProtocol` 是模板的协议格式版本，不是另一个启用开关；本次不另造配置热更新机制。

连同 pending demand、目标可用性复查、`hasTheoreticalCapacity`、previous 偏好和同 physical 重试一起传递/保存用途。不能只在首次 route 调用加参数，稍后复查又退回默认 normal。

既有 exact target、capacity fence、lender、取消和 provider failure settlement 规则照常。Replica 是新的物理执行，不继承 owner 的模型 target 作为自己的 previous；同一 Replica physical 内不能换用途或目标。DevOps 固定目标等角色规则也必须明确区分原 owner execution 和新的只读委托 execution，不能借同一 role 把 owner target 覆盖到 Replica。

旧三参数 JS 函数会忽略多余参数，所以不能用 `function.length` 或“调用没抛错”判断升级成功。加载器明确验证 `routingProtocol === 2`；旧配置给出可操作错误，不自动覆盖用户已有 `wanxiangshu.mjs`。升级模板和示例，但本次写指南不触碰用户配置。

真实 provider-wire 测试必须观察 owner 与 Replica 的实际 `provider/model` 和请求用途，证明后者使用配置的 Predictor 目标。Predictor 与 owner 配成相同模型也是合法状态，不因模型名相同、价格未知或没有经济测量而关闭。成本不再参与准入，面向模型始终称同伴。

### 9.3 容量等待不能制造父子死锁

本仓在 provider transform 获取 capacity token，且工具执行边界会结束 owner 的 provider step。检查插入委托时 owner 是否已经重新持有一个尚未外发的 step token：owner 一边占着唯一容量一边同步等 Replica，会死锁。

只能通过现有 capacity owner 的明确 step/lender 协议协调。宿主顺序要保证等待 Replica 时没有被误标为真实 InFlight 的 owner 请求；需要借用时显式传 lender，按真实 step 边界转移，不能靠 session 亲缘关系自动猜借用。

必须覆盖“同 provider 容量为 1”的真实集成测试。不得以扩容、超时后抢跑、假释放 token 或允许双重 InFlight 让测试通过。已有 `execution-model-routing/WHAT.md [010]` 的 owner transform 抢占召回规则也要在这个父等子场景里验证，不可只测不同 provider 的宽松配置。

## 10. 删除专属字节上限，但保留完整性

这部分按调用链清理，不只是删一个配置名。

1. `StrengthFrame.tryBuild` 去掉 `maxBytes` 参数和大小比较；保留非空、ordinal 连续、完整交换、工具白名单、canonical text、digest 与实际字节长度。
2. 删除 `StrengthFrameError.ByteLimitExceeded`，同步 `.fsi`、Surface 错误映射和全部匹配分支。
3. 删除 `StrengthReplicaBinding.MaxFrameBytes`、Replica runtime 构造器的 `?maxFrameBytes`、`frameByteLimit` 及 `MaxFrameBytes` 成员。
4. 清理 `Replica/Transform.fs`、改名后的 `OpenCode/Delegate.fs`、`PluginSessionWiring.fs` 和测试 fixture 的传参。
5. `Persistence/Store.decodeFrameBundlePayload` 无尺寸预算地重建 frame，然后逐项核对存储的 digest、byte length。长度不一致仍是损坏，不是“自然截断”。
6. 删除规范和测试中“太大整体丢弃为 K0”的要求。替换为超过旧 64 KiB 门槛仍可构建、持久化、映射、恢复的行为证明。

保留 `ByteLength`、UTF-8 字节计量、payload_refs 和 digest。它们服务完整性、存储和观测，不再服务预算选择。

“自然截断”的意思是：如果原工具按通用规则返回了截断结果，Frame 保存并回放这个**实际返回给模型的结果**，包括原有截断标记。不能先对原始全文算 digest，再在回放时另切一刀；那会破坏 Prepared 与实际消费内容的一致性。

不设置等价替身，例如 `maxDelegateTokens`、`maxBatchesBytes`、`Int32.MaxValue` 伪上限、只保留最后几次工具结果或超过阈值不启动。通用物理错误仍按通用错误处理，不承诺无限内存。

## 11. clean-break 的具体删除清单

### 11.1 代码和接线

删除 `Prediction/CostModel.fs/.fsi`、`Prediction/Predictor.fs/.fsi`、`Rollout.fs/.fsi`。将 BatchCollector 移走后删除空 `Prediction` 目录。

从 Policy、Surface、PluginScope、PluginStrengthPorts、TurnEvidence 及其签名里删除：

```text
StrengthValueInputs / StrengthValueEstimate / StrengthCostModel
StrengthPrediction / StrengthPredictorState / StrengthFeatureKey
StrengthPredictorBucket / StrengthPrimarySymbol
CounterfactualPair / CounterfactualCollector / CounterfactualEpisode
StrengthFeature / StrengthPrediction / StrengthBucket
ArmStrengthCounterfactual / ObserveStrengthPrimary
strengthRecentPrimary / strengthPredictorState
controlBucket / isControlHoldout / ControlHoldout
K1Margin / K2Margin / K2MinimumEvidence
P1 / P2 / V0 / V1 / V2 对应的旧经济决策接线
```

保留 `TurnEvidence.classifyParts`、promotion decision 等真实消费证明；只删除用于训练预测器的 `primarySymbol` 及其调用。不要整文件误删。

`OpenCode/Speculate.fs/.fsi` 改名 `Delegate.fs/.fsi`，模块改为 `StrengthDelegate`。`PluginTransforms` 中的 capability、接线和测试同步改名 `ApplyReadonlyDelegation`，旧入口不留转发函数。

### 11.2 配置

删除旧 Shadow/DryRun/Treatment 生产分支。生产启用状态只由有效的 Predictor 模型配置派生：未配置就没有新委托，已配置就默认启用。预算为 0 只表示本次不委托，不是关闭功能。

不新增任何 Delegate 开关或环境变量。此前指南中的 `WANXIANGSHU_DELEGATE_ENABLED`、`WANXIANGSHU_DELEGATE_HOST_CANARY` 方案作废。原有 Strength 模式、成本、阈值、holdout 和人工 canary 配置读取也不再保留。旧环境变量不影响新启用判断，不因为残留旧值阻止运行；升级文档说明它们已失效即可。

Delegate 不再具有独立的生产消融选项。同步调整 `speculative-investigation` 在 feature-ablation 注册表、profile、依赖和测试中的开关语义，避免它成为 Predictor 配置之外的第二个启用条件。包名继续用于 requirement/owner 归属；其他特性的消融行为不变。测试对照通过测试模型配置中的有/无 Predictor 表达。

真实 Host canary 仍是交付要求，权限、持久化、接口兼容性和资源不变量也全部保留。接口和版本检查从真实运行证据取得，不再要求用户手工填指纹来激活功能。自动启用不意味着免除安全检查。

未配置 Predictor 时不装饰两个字段、不追加协作说明、不产生新授权、不启动 Replica。移除配置只影响新委托：未 Bound 的请求在新配置加载后明确关闭；已 Bound 的物理执行遵守原有 target 和结束/取消规则；Prepared/Promoted 恢复继续独立于模型配置。不开辟专属热更新机制，不删除合法历史。

协议版本是代码中的稳定契约版本，不是由环境变量随意改变的“策略版本”。旧对照实验若仍有研究价值，放在测试 harness，用分开的完整运行比较；不能留在生产准入里，隐瞒主模型已经作出的授权。

### 11.3 编译与构建所有权

本仓有 compile shard，不只改一个 `.fsproj`：

- 更新 `src/Wanxiangshu/compile-order.txt`，仍保证 `.fsi` 在 `.fs` 前、contract 在 consumer 前。
- 检查并调整 `Wanxiangshu.Owner.speculative-investigation.strength-budget.fsproj`、`strength-prediction-predictor.fsproj`、`strength-opencode-settings.fsproj`、`strength-replica-runtime.fsproj` 等实际 owner shard。
- 旧 prediction shard 内目前还包含 Frame、Events、Commit、Promotion、TraceRecovery，不能整 shard 盲删。按保留的语义职责移交 Compile/ProjectReference，去掉失去含义的 shard 名。
- 查完下游 ProjectReference、owner metadata、requirement APPLIES-TO 和测试 import。不要手改 `dist` 冒充源代码已编译，也不留“路径还在但实现空了”的旧文件。

## 12. 历史事件与配置升级：不能假装无状态

旧 Prepared 的 payload 把 budget 保存成 `"K1"/"K2"`。本次改变授权父边、DecisionId 来源和事件结构，所以是协议升级，不是随手把 `Decode.string` 换成 `Decode.int`。

新运行时只使用新版协议；不保留旧预测器、新旧预算双解析或多套生命周期。**clean-break 不授权删除用户的旧 EventStore、XTrace 或会话。**

实施时提供单独的离线迁移入口，通过 EventStore 所有者的读取、payload 和 append 边界，在备份或副本上工作，不原地改 append-only 历史、Git raw object 或 refs。先核对仓库是否已有通用迁移工具，有则复用；本文未验证存在现成的 export/import API，不以臆造的接口作为实施前提。迁移不是新运行时启动时的隐式副作用。

迁移需要明确分开两种历史：

1. 已 Promoted/Traced 的旧只读材料是已经发生的事实，应导入新版的“已接纳历史材料”表示，保留原 owner 因果位置、实际交换、digest 和 trace coverage。**不能伪造一条主模型从未发出的 DelegationRequested 来给它补授权。** 历史导入是离线事实类型，不参与新委托准入，也不能产生副本。
2. 旧未消费的 Prepared 与进程内运行不带新版显式授权。升级前能正常收尾的先收尾；不能收尾的在迁移中明确记录放弃，不升级成可运行的新请求。K1/K2 可保留为离线迁移报告中的旧预算证据，不重新进入生产预算选择。

如果已有通用历史导入/迁移事实，复用它；没有则先在 durable-events 规范里定义一个仅承载历史材料的导入事实。不要借本特性搭建独立迁移存储。导入必须保留或正确重建所有 envelope 父边、payload refs、decision 引用和 XTrace 因果锚点，并以 cold replay 对照证明业务 Current 一致。

新旧协议必须能被入口检查区分。旧存储未迁移就拒绝以新运行时继续消费，给出明确说明；不能边读边忽略不认识的旧事件。迁移工具、输入/输出版本和一次性操作步骤要有自动化测试。

如选择新建独立的测试会话/存储做验证，应保留旧存储且明确它尚未迁移，不能以空库成功代替升级证明。没有授权，不执行清库或覆盖用户的 `wanxiangshu.mjs`。

## 13. 先改规范，再按依赖顺序实施

### 第一步：更新产品合同

阅读并修改 `requirements/speculative-investigation/WHY.md`、`WHAT.md`、`APPLIES-TO`。原编号按下表改写，使测试仍有明确条款归属。

| 条款 | 新内容 |
| --- | --- |
| `[001]` | 未配置 Predictor 保持无功能基线；配置后默认提供协议，预算 0 不等于未配置 |
| `[002]` | 新鲜真实授权、max、固定身份、普通续行和真实安全准入；无经济门槛 |
| `[003]` | 非负整数请求预算、提前结束、N+1 外发门禁、重复通知幂等 |
| `[004]` | 同一 owner 身份的只读内部执行；委托用途选择 Predictor 模型池，不换角色 |
| `[005]` | 真实完整交换与 digest；无专属字节限制 |
| `[006]` | 授权的 Requested/Bound 与 Prepared 持久化前置 |
| `[007]` | exact target 的消费证明与 Promotion，关闭路径幂等 |
| `[008]` | Promoted replay、XTrace 和普通压缩闭包 |
| `[009]` | 镜像、ID 重定位、短记随原始 arguments 自然可见；不反射、不另搬运提示 |
| `[010]` | 删除统计预测器与 Control，保留 Predictor 模型槽位；授权一次性、恢复与 supersession |
| `[011]` | 无时钟终结、普通失败、取消、fuse、物理尾部清理 |
| `[012]` | 同伴叙事、基于可见表现建立信任、首次 edit 步数启发式、可选第一人称 self_note；只回传工具事实 |
| `[013]` | 删除生产 DryRun；改为本版真实 Host 集成证明要求 |
| `[014]` | Predictor 配置是唯一启用依据；不再有独立开关、环境变量或生产消融选项，合法历史不丢失 |

涉及新增迁移合同可增加 `[015]`，测试文件用 `015.test.mjs`，不要复用与新语义无关的旧测试标题充数。

同步更新 `host-boundary`、`execution-model-routing`、`participant-identity`、`session-ontology`、`durable-events`、`semantic-trace`、`prefix-stability`、`context-compression`、`feature-ablation` 和受影响的 `behavior-diagnosis` 合同。后者的 system prompt/tool definitions 记录要覆盖必选预算、可选短记及稳定协作说明，不继续证明旧“模型不可见”。feature-ablation 同步取消该特性的独立开关，不能让其他文档仍要求 ablated 否决 Predictor 配置。

### 第二步：先补纯行为的失败测试

从 Predictor 配置存在性、预算严格校验、短记可省略、同批 max、complete batch、授权状态跳转、零字节门槛开始。测试通过对外 Surface 的普通 JSON 值与 opaque handle，不能依赖 Fable union/record 的内部数组布局。

### 第三步：替换领域类型和事件

实现整型预算与 Delegation 纯模型，修改 Events、EventVocabulary、Store、Durability、Projection 和 Integrator 注册；先证明写入失败/未知、重复、冲突及冷启动折叠。然后改下游调用者。领域不读取 env、时钟、文件或 Host 对象。

### 第四步：修好真实 schema/调用边界

实现 `ReadonlyDelegationContract`，接入 PluginHooks，补评审 contract 共存和参数异常路径测试。验证预算必填、短记可省略，两个字段不传业务工具但保留在原始调用里；原工具描述只追加一次中英文协作说明。先通过真实 Host canary，再接生产授权；只在 mock 对象上增加 required 不算完成。

### 第五步：改模型路由和 capacity

扩展用途类型、scheduler ABI、模板、admission、pending 和复查路径。从同一模型配置提供 Predictor 存在性查询，不从容量推导启用。完成仅配置 Predictor 即启用、owner/Replica 实际目标、容量为 1 的父子等待与取消释放测试。再接运行中的 Replica，确保同伴实际使用配置的 Predictor 池。

### 第六步：接入授权捕获和消费

在普通 owner 路径捕获真实来源，记录 Requested；在冻结后的正常续行写 Bound，调用 `StrengthDelegate.tryApply`。不改变当前批次执行，不在每个 tool.after 启动，不从历史合成行取授权。

### 第七步：重做 Replica 计数并移除字节限制

以 actual request admission 记账，收集完整结果，保留提前结束和两类终态。把 Frame、Binding、Runtime、Store、Surface 中字节上限参数删干净，验证超过旧门槛仍能完整走通。

### 第八步：接通回放、压缩和重启

保持 Prepared 不提前成为历史、exact target 才能 Promote、Promoted 在正确位置进入 XTrace。验证压缩导致的投影变化不会丢失或重复授权，Replica 截断不会减少已用轮数；一般溢出恢复不会无限重送候选。

### 第九步：删除旧实现和旧测试接线

删除统计预测器、成本模型、旧 rollout、counterfactual collector、旧功能开关与环境变量读取、旧 Surface 和失去作用的诊断字段；保留 Predictor 模型槽位。更新 compile shards、imports、测试 fixtures、E2E 场景与 GAP 记录。新实现已经引用不到旧路径，不等于旧路径可以留下。

### 第十步：升级证明与交付

完成离线历史迁移测试及 MJS 配置升级说明。先跑相关包测试，再跑仓库完整门禁。最终提交是可构建、可冷启动、无旧语义分支的一次完整切换；开发过程可以拆小提交，但不要发布中间态。

## 14. 必须有的回归测试

下面是行为验收，不是源码字符串验收。源码搜索只用于最后清理。

### 14.1 参数与 max

在 `speculative-investigation/tests/002.test.mjs`、`003.test.mjs` 和 Host 对应条款覆盖：

- `[0,0] -> 0`、`[0,2,5] -> 5`、`[7,1] -> 7`；所有调用顺序和结果返回顺序保持相同结论。
- 一个批次只启动一个 child；结果尚未齐全时一个也不启动。
- 预算字段缺失、null、字符串、负数、小数和越界数都不能授权或被偷偷规范化；不把这一必填规则施加给 self_note。
- schema 重复装饰无重复 required 或协作描述；保留原必选项，新增 required 只有预算；两个字段的同名冲突均明确失败。
- 已配置 Predictor 时，所有实际 provider-visible 工具都有必填预算、可选短记；真实业务工具看不到这两个字段。
- self_note 省略、空字符串均合法；预算为 0 可以写短记，预算为正也可以省略。非字符串值明确拒绝，不自动补值或改成必填 nullable。
- 中英文及含引号、换行的短记随原始 arguments 保真进入镜像；没有额外 system/user/bootstrap 提示副本。
- 同批多个短记按原调用顺序各自保留；不选择 max 所在调用的短记，不拼接成新消息；有无短记不改变 max。
- 和 `js-manager` 评审 `contract` 共存；工具抛错、取消、不同参数对象、并发两会话都不串值或丢原始证据。
- 混合 `read/edit` 当前批次正常执行一次，之后按 max 委托；不能因同批一个 0 偷换成否决。

### 14.2 轮数和提前结束

在 `003.test.mjs`、`004.test.mjs`、`011.test.mjs` 覆盖：

- N=0 不创建会话、不占模型资源；N=1、2、3、7 都能正确门禁。
- 一次 provider 请求并行发十个只读调用，只耗一轮。
- N=3 时，收齐第三轮所有结果，但真实 provider 观察不到第四次请求。
- 第二轮纯文本结束时计数为 2；文本/reasoning 不进入 owner；没有额外总结请求。
- 第三轮发生 provider 失败也不能把它当没消耗，然后偷发第四轮。
- 重复 transform、同一 terminal 重复通知、tool result 乱序不会增加预算或重复材料。
- Host 截断/移除旧可见批次后，已用预算不降低；owner mirror 中旧调用不计入本次。
- 语义结束后的晚到 transform 仍识别为 Replica，不能走普通 Work；资源只清理一次。
- Replica 填正数、尝试写、调用非白名单工具，都不能嵌套委托或实际写入。

### 14.3 持久化与恢复

在 `006.test.mjs`、`007.test.mjs`、`008.test.mjs`、重写后的 `010.test.mjs` 覆盖：

- 相同 Requested/Bound 重复写入幂等；同来源不同 N/target/anchor 是冲突。
- Requested、Bound、Prepared 各写入点前后崩溃，分别符合第 6.4 节决策表。
- Bound 以后、Prepared 以前重启不再运行副本。
- Prepared payload/父边完整，Unknown 不外发；Prepared 不出现在 XTrace/Companion 的事实历史中。
- wrong target、空输出、传输错误不能 Promote；真实消费以后冷启动能回放同一材料。
- 新用户输入、logical run 替代、取消、删除正确关闭旧授权；新请求不会拾取旧正数。
- 移除 Predictor 配置后不启动新委托，合法 Prepared/Promoted 仍按既有生命周期恢复；不依赖独立 Off/ablation 状态。
- cold replay 与 live fold 的业务 Current 一致，不能只是事件数量一致。

### 14.4 无专属字节门槛与常规压缩

在 `005.test.mjs`、`008.test.mjs`、`009.test.mjs` 和 context/prefix 包覆盖：

- 构造 canonical 交换超过旧 65536 bytes，仍能 build、persist、load、render，不能出现 ByteLimitExceeded 或预算归零。
- 原工具自然截断后的实际结果和标记能原样进入 Frame；不伪造被截掉的全文。
- 长输入触发正常压缩或其原有恢复路径，主模型能继续，不出现 Delegate 专属长度决定。
- 压缩前捕获的授权在同一合法续行仍有效；prefix probe 不消费预算。
- checksum/byte length 被篡改仍被拒绝；删大小上限不能导致坏 payload 被接纳。
- 单纯长度增长不触发 fuse；不变量冲突仍会触发原有失败关闭。

### 14.5 路由、并发和真实 Host

在 `execution-model-routing`、`host-boundary` 和本包 `[013]` 集成测试覆盖：

- owner/Replica 保持相同 participant/Role/Persona，但用途不同；后者实际路由至用户配置的 Predictor 池。Predictor 与 owner 同模型时仍属有效配置。
- 旧 routingProtocol 明确拒绝，不因 JS 忽略第四参数而假通过。
- pending demand、previous 复查和 retry 不丢用途；配置错误与暂时无容量有不同结果。
- 同 provider、容量 1 时父等子不会死锁，也不出现双占；取消时 fence/child 正确释放。
- 两个 owner 并发，不串 schema 状态、call id、授权、预算、结果或模型资源。
- 真实 provider-wire 观察到预算 required、self_note 可省略、短记自然出现在镜像、max 效果、N>2、N+1 阻止、结果映射以及提前结束。
- E2E 同时覆盖正常完成、提前结束、自然截断/压缩和恢复；只写 canary 日志没有断言不算证明。

原 `host-boundary/tests/032.test.mjs` 可作为 contract 共存测试基础；`019.test.mjs` 的 transform 分支和顺序测试也要同步维护。调整 `verification-system/tests/e2e/scenarios/long-stroke.toml` 中旧 DryRun/K1/K2 场景，不能靠跳过旧场景隐藏缺口。

### 14.6 默认启用与协作说明

在本包 `[001]`、`[012]`、`[014]` 及路由、Host 对应条款覆盖：

- 没有 Predictor 槽位或候选为空，不装饰协议、不创建 Replica；只配置一个合法 Predictor 就默认启用，无须其他设置。
- 使用模板自带的非空 Predictor 池也默认启用；非法模型配置明确报告错误，不静默变成“未配置”。
- 旧启用/关闭环境变量对新功能均无影响；旧消融选项不再作为该特性的生产配置入口。
- 临时容量不足不改变配置存在性，不来回改 schema；请求等待和取消仍正确，其他特性的消融行为保持原样。
- 中英文工具描述都称“同伴 / companion”，都解释 max、0 的边界、提前结束、首次 edit 步数只是启发式及不能替代判断。
- 工具描述明确要求根据实际可见表现多信任、多委托，或在不擅长的状况少用/不用；不要求正数，不生成宿主信任分。
- self_note 的第一人称要求是提示风格，不是字符串前缀门禁；省略时不追问、不重试、不合成提示。
- 镜像中短记只随原调用出现；常规压缩使其不再可见时，不通过专属通道重新注入。

描述文字测试只证明契约已送达，不能据此宣称模型一定形成了理想信任或取得更好任务结果。实际效果仍用完整任务的可见工具轨迹与结果评估，不把评估再接回生产启用门槛。

## 15. 验证命令与交付顺序

下面的命令用于将来实施源码改动后的验证。本次仅写指南，不把这些命令列在这里当作“已通过”。在仓库根目录运行，先确认当前测试 runner 和环境要求仍与基线一致。既有测试层级环境变量只控制测试执行，不是 Delegate 的生产启用选项。

```bash
# 先查看工作区，保留用户改动。
git status --short
git diff --check

# 唯一构建路径是 Fable 脚本；不用 dotnet build。
node scripts/build.mjs

# 先运行本领域的行为测试。
node --test requirements/speculative-investigation/tests/*.test.mjs

# 然后运行直接受影响的边界与路由测试。
node --test requirements/host-boundary/tests/019.test.mjs \
  requirements/host-boundary/tests/032.test.mjs
node --test requirements/execution-model-routing/tests/*.test.mjs

# 回放、身份、压缩、消融等按实际改动范围分包运行。
node --test requirements/participant-identity/tests/*.test.mjs \
  requirements/durable-convergence/tests/*.test.mjs \
  requirements/prefix-stability/tests/*.test.mjs \
  requirements/context-compression/tests/*.test.mjs \
  requirements/feature-ablation/tests/*.test.mjs

# tier-gate.mjs 使用这些开关；不设置时 integrationTest 会 skip。
WXS_TIER_INTEGRATION=1 node --test \
  requirements/speculative-investigation/tests/*.test.mjs \
  requirements/host-boundary/tests/032.test.mjs

# 整体交付门禁，含 format/check/build/unit/integration。
npm run format-build-test

# 发布才运行 release；需要满足真实 Host/E2E 的环境要求。
npm run verify:release
```

路由及 capacity 改动触及的其他 requirement 包也要运行，不以这份列举代替构建影响分析。测试显示 skipped 不等于 Host canary 已通过；记录真实执行的套件、失败和未执行的环境依赖。

清理搜索使用明确范围，避免把本文和离线迁移测试中的旧名字误当成运行时残留：

```bash
rg -n 'StrengthCostModel|StrengthPredictor|Counterfactual|ControlHoldout|K1Margin|K2Margin|K2MinimumEvidence' src
rg -n 'MaxFrameBytes|maxFrameBytes|frameByteLimit|ByteLimitExceeded' src/Wanxiangshu/Strength
rg -n 'StrengthBudget\.(K0|K1|K2)|min 2' src/Wanxiangshu/Strength
rg -n 'WANXIANGSHU_STRENGTH_' src scripts requirements
rg -n 'WANXIANGSHU_DELEGATE_' src scripts requirements
rg -n 'Strength/Prediction|Strength/Rollout|Strength/OpenCode/Speculate' src scripts
```

运行时实现中的旧路径和功能环境变量读取应清零；离线迁移、升级文档或反例测试可以保留旧字符串，但须说明用途。不要为了检测旧环境变量而新增一条拒绝运行的门槛，也不要误删 Predictor 模型池。不要写批量脚本自动删改命中的代码。

## 16. 完成的标准

完成后，一条授权应该可以从头到尾讲清楚：哪一次真实主模型响应产生了哪些整数，为什么 max 是 N，当前工具何时全部完成，哪份持久化事实绑定了副本，副本实际发了几次请求，为什么停下，哪些真实结果被哪个 exact 主请求消费，何时进入历史，以及重启后为什么不会再做一次。

实现交付必须同时满足：

- 主模型确实看见并知道 max 协议；当前工具正常执行；Replica 只读、可提前结束、不能递归委托。
- Predictor 模型配置是唯一启用依据，无独立开关、环境变量或二次确认；未配置与暂时无容量分清。
- 工具描述采用同伴、了解和信任的叙事；首次 edit 步数只是判断起点，不被宿主计算成新策略或硬性承诺。
- self_note 真正可省略，使用第一人称短记，随原始调用自然映射；无显式提示搬运、独立 hint 事件或信任评分系统。
- N 来自主模型同批 max，不来自旧预测器或任何隐藏的收益/长度/两轮门槛。
- 真实请求记账能跨重复回调、截断和压缩保持正确；N+1 在外发前被阻止。
- 没有 Delegate 专属字节/token 上限；普通工具截断与主模型压缩没有被关闭或绕过。
- 只回传真实完整工具交换，保留现有持久化、exact-target 消费、回放和权限不变量。
- Predictor 目标由明确用途进入唯一 MJS 调度 authority；不换身份，不绕过租约，不引入父子容量死锁。
- 新旧协议、配置和历史升级有明确边界；不保留双决策引擎，不丢用户历史，不假造旧授权。
- 相关行为测试、真实 Host canary、冷启动恢复和构建门禁确实通过；文档、规范、代码和测试没有互相矛盾。

最重要的不是把 `CostModel.fs` 改短，而是让系统只做一件说清楚的事：**配置 Predictor 后，主模型通过 N 和可选的自省短记与同伴合作；宿主管理这份只读授权，同伴做完或做够就交还，主模型根据可见表现调整下次委托；其他系统各守原来的职责。**
