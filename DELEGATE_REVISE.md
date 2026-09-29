# 只读调查展望：DELEGATE 协议修订与逐项改造指南

状态：待实施的改造方案，不是实现完成报告。

审阅基线：2026-09-29，`master`，`ab62b7d2f`。本轮只新增本文件，不修改源码、产品规范、测试、用户模型配置或历史数据。本轮没有运行构建、业务测试、真实 Host canary 或模型行为实验。

工作区审阅时已有 `AGENTS.md` 的未提交修改，以及 `ASK.md`、`PROMPT-006-implementation-plan.md` 两个未跟踪文件。它们不属于本次交付，不能混入本文件的改动或提交。实际施工前重新查看 HEAD、状态与 diff，不把这个基线当成永久现状。

本文是对 `DELEGATE.md` 的明确修订，不是把旧方案完整重做一遍。冲突处以本文所记录的新决定为准；产品语义实施时仍须落入 `requirements/<package>/`。不允许实现者只更新本文，却让 WHAT 和正式测试继续要求旧行为。

文中的源码简称，例如 `Strength/OpenCode/Delegate.fs`，均从 `src/Wanxiangshu/` 起算。以 `requirements/`、`resources/`、`scripts/` 开头的路径从仓库根目录起算。拟定类型、函数和测试名称是施工目标，不表示当前已经存在这些接口。

---

## 0. 先看这一页：这次到底改什么

### 0.1 用户已明确推翻的三件事

第一，不再给所有工具添加两个参数。Manager 的 `fork/join` 等协调和收尾工具不需要；Orchestrator 的工具大多不需要；Blogger 的工具不能加。每个工具要按实际用途判定，不能改成另一种角色级或读写级的一刀切。

第二，`self_note` 只有在本次调用的估计值大于 0 时填写；其他时候省略。它是对未来若干回合的展望，不是任意文本、当前状态、完成摘要或泛泛感想。

第三，模型可见的 `delegate_readonly_rounds` 改为事实性估计字段。不再询问愿不愿意委托，不再讲同伴、信任、让出控制权，也不把系统利用这个数进行调度的意图写进模型可见说明。

### 0.2 本稿采用的工程决定

下列细节承接前一轮草案，是本稿选定的实施起点，不冒称用户逐字指定：

| 条目 | 本稿决定 |
| --- | --- |
| 新字段 | `estimated_readonly_rounds` |
| 估计对象 | 当前整批工具完成后，到下一次实质修改、必须亲自作出的复杂判断、执行命令、用户确认或结论之前，连续只读查证还需要多少轮 |
| 单位 | 一次模型生成请求；一轮可以有多个并行工具调用，当前整批不计入 |
| 数值范围 | 原生整数，`0..2147483647`；不强转、不截断、不钳制 |
| 数值为 0 | 不填写 `self_note`，也不填空串或 `null` |
| 数值大于 0 | 填写非空白的 `self_note`；正文面向后续查证 |
| schema | 只把估计字段无条件列入 `required`；短记的条件关系由同一个调用边界校验 |
| 内部预算 | 对同一真实响应中参与工具的有效估计取 `max`，按既有机制转换为只读执行上限 |
| 防递归 | 依靠真实请求身份与生命周期，不靠让内部执行者虚报 0 |
| 未判定工具 | 不自动装饰、不截取同名字段；进入工具清点的待判定项 |
| 升级方式 | 新调用一次切换到新字段；历史事实原样保留，不双读两个新调用字段名 |

正数短记必填、空白短记拒绝，是本稿将“只有正数才需要填写”落实成可测试合同的具体选择。内容是否有用不能用正则证明；边界只检查存在性、字符串和非空白，展望质量另做行为验证。

### 0.3 本次明确不做

不重建 Predictor 模型池，不改变配置即启用的规则，不复活成本模型、统计预测器、holdout、K1/K2 档位或独立开关。

不扩大 Replica 的执行权限，不让 shell、PTY、通用 JS、MCP 或工作分配工具进入只读白名单。增加估计参数与增加工具执行权限是两件完全不同的事。

不重写容量调度、租约、身份、Companion、XTrace、压缩和 EventStore。相关边界需要回归验证，但不借本次修订顺手实施其他架构方案。

不删用户历史，不改旧调用 arguments，不为升级伪造主模型曾经填过的新字段，不因字段改名再做一遍旧 K1/K2 历史迁移。

不以“正数比例提高”作为成功的唯一指标，不强迫模型填写正数，不用错误恢复把 0 偷改成 1。

---

## 1. 已核实的现状与施工入口

以下是本次阅读支持的事实，不是运行测试结论。行号随施工会变化，后续优先按路径和符号定位。

| 已核实位置 | 现状 | 这次必须处理的原因 |
| --- | --- | --- |
| `OpenCode/Host/ReadonlyDelegationContract.fs`，`decorateToolDefinition` | 主要只跳过 Host 合成的 `invalid`，其余定义进入装饰 | 这是全量加字段的直接入口 |
| 同文件，字段描述及 `englishCollaboration/chineseCollaboration` | 明说 delegate、companion、信任、保留控制权和每个工具都要填写 | 只改字段键名会留下完整的旧行为引导 |
| 同文件，`trySelfNote` | 缺失与空串合法，仅检查字符串形状 | 不能表达新合同的正数必填、零值省略 |
| 同文件，`hide/restore` | 参数是可变对象；用私有 Symbol 保存属性描述符 | 不能假设 after 一定到达或拿到相同对象 |
| `OpenCode/Plugin/PluginHooks.fs`，`toolDefinition` | Predictor 已配置时调用装饰器 | 保留配置查询，但增加逐工具判定 |
| 同文件，`sanitizeSnapshot/toolBefore/toolAfter` | 短记和旧数值字段按全局协议截取、隐藏和恢复；评审 `contract` 有工具过滤，调查字段没有同等过滤 | 只收窄 schema 会继续误删未参与工具的业务参数 |
| `OpenCode/Host/ProtocolArgumentVault.fs` | 进程内按 session/call 保存原字段，供后续 provider 投影恢复 | 需要收窄字段所有权；不能把它说成跨进程持久化保证 |
| `Strength/OpenCode/Delegate.fs`，来源批次解析 | 来源调用沿若干路径收成 `(ToolCallId * string)`，工具名在后续消费时不可用 | 选择性解析必须保留真实工具身份 |
| 同文件，`budgetOfCall/batchBudgetOfCalls` | 对每个来源调用读旧字段，缺失即错误，最后 `List.max` | 去掉协调工具参数后，混合批次会被旧解析器判错 |
| 同文件，`tryUnboxRoundsNumber` | 用 `unbox<float>` 配合后续数值检查 | 不能拿这一写法替代 JS 原生 number 检查；新边界须有字符串、布尔等回归证明 |
| 同文件，`contractRevision` | 当前构造值为 1 | 新协议须有稳定版本，且处理跨版本的同来源去重 |
| `Strength/Budget.fs` | 已有非负 `ReadonlyRoundBudget` 和 `maxOf` | 不需要重建预算系统；要区分输入估计与内部执行上限 |
| `Strength/Runtime.fs`，`StrengthReplicaTools` | 实际只读表为 `read/glob/grep` | 本次不扩大这个集合 |
| `OpenCode/Tools/ManagerReviewTools.fs` | `js-manager` 是评审调查表面 | 不能把 Manager 全部排除，也不能借参数让其在禁止阶段可用 |
| `Foundation/OfficeCapability.fs` | Blogger 的合法工具集合为 `chronicle`；Manager 的后段能力有收窄规则 | 要验证真实生命周期，而不是只测一个伪造角色对象 |
| `Repository/Programming/Js/Surface.fs` | 没有文件系统能力时不生成 JS 表面 | 已知工具名称表不等于当前可见工具集合 |
| `requirements/speculative-investigation/WHAT.md` [001/002/012/013] | 仍要求全量装饰、同批全体旧字段、同伴叙事与独立短记 | 规范与代码要同批切换 |
| `requirements/behavior-diagnosis/WHAT.md` | 明文要求 Blogger life 冻结附加的旧协议 | 这是容易漏掉的跨包反向约束，必须同步撤回 |
| `requirements/host-boundary/WHAT.md` [032] | 原始参数保证主要落在 provider wire 投影，不承诺 Host 持久化对象形态 | 不得以私有 vault 已存在为由宣称重启后字段必然完整 |
| `speculative-investigation/tests/013.test.mjs` 的真实 Host 测试 | 遍历全部可见工具，断言都有旧字段；示例为 0 加短记 | 两类旧预期都要改，不能只替换字符串 |
| `verification-system/tests/e2e/support/long-stroke-oracles.mjs` | oracle 仍从旧字段提取预算并要求全工具装饰 | 不改 oracle 会出现测试语义与实现互相迁就 |

`package.json` 的开发依赖声明为 `@opencode-ai/plugin@1.18.29` 和 `opencode-ai@1.18.29`，但 schema 适配源码注释引用过其他 Host 版本。这里只确认仓库声明和注释确实如此，没有确认当前实际可执行二进制、锁文件解析和 canary 所用二进制完全一致。施工 P0 必须重新核对，不照抄旧文档的版本结论。

---

## 2. 哪些旧决定撤回，哪些不动

### 2.1 对 `DELEGATE.md` 的覆盖清单

| 旧位置或主题 | 新处理 |
| --- | --- |
| 第 1 节“每个可见工具 schema” | 改为经过逐项判定的工具，不再全量 |
| 第 1 节关于同伴、建立信任的前提 | 撤回模型可见的协作意图叙事 |
| 第 3.1 节固定字段名和授权含义 | 新调用改为事实性调查估计；授权是宿主内部转换结果 |
| 第 3.2 节 schema | 新字段名、新说明、条件短记；不再沿用旧 JSON 示例 |
| 第 3.3 节三段协作说明 | 全部替换为简短、稳定的调查展望说明 |
| 第 3.4 节 max | 算法保留；操作数改为参与工具子集，等待范围仍为整批 |
| 第 3.5 节“预算与短记独立” | 明确撤回；新调用满足正数有短记、零值无短记 |
| 第 4.1 节静态装饰所有工具 | 保留静态 Host 限制认识，改为按 toolID 的静态选择 |
| 第 4.2/4.3 节参数清理 | 同一参与判定约束装饰、校验、截取、剥离与捕获 |
| 第 5 节逐调用取值 | 先证明完整来源批次，再只读取参与调用的估计 |
| 第 6 节授权与一次性消费 | 主体保留；补协议版本切换和跨版本同来源防重 |
| 第 7 节真实来源、冻结与启动顺序 | 保留；不从合成历史和任意旧正数重建新请求 |
| 第 8 节副本固定填 0 | 撤回；防递归由真实身份决定，不能逼模型虚报估计 |
| 第 9 节 Predictor 和容量 | 不重做 ABI；核实并保留当前已实现的用途路由与容量边界 |
| 第 10/11 节删除旧预测和字节上限 | 不重新施工已完成部分；仅检查本次没有复活它们 |
| 第 12 节古老 K1/K2 迁移 | 不等同于本次字段升级；旧导入工具保持原有职责 |
| 第 13—16 节实施、测试和完成标准 | 按本文重写，删除全量装饰、信任、0 加短记等旧验收 |

旧文档可留作历史说明，但必须在实施时加醒目的修订入口，或局部同步更新被覆盖章节。不能让两份文件都宣称自己是当前唯一完整合同，却互相矛盾。

### 2.2 继续有效的不变量

当前整批工具正常执行一次，不推迟 edit，不让副本重新选择已经发生的工具参数。

Replica 只读、可以提前结束、没有额外总结轮；N 是上限而不是配额。N+1 在外发前阻止，已接纳的第 N 轮结果仍需收齐。

只回传真实完整的工具调用与结果；推理文本、纯文本总结和未执行计划不成为 owner 已查证事实。

Requested、Bound、Prepared、Promoted、Traced/Closed/Abandoned 的真实持久化和因果要求保留。未知追加结果不能当成没发生，也不能当成已经提交。

不新增专属字节/token 上限，不从压缩后的历史长度重算已用轮数，不把 prefix probe 变成消耗或重置预算的机会。

真实权限、owner authority、请求用途、取消、终态、租约和容量仍约束执行。估计正数不能覆盖任何一项准入限制。

---

## 3. `estimated_readonly_rounds` 的精确定义

### 3.1 它是估计，不是意愿，也不是已经发生的事实

“事实性名字”的意思是询问任务所处的位置，不是保证模型知道真实未来。模型给出的是当前证据下的估计，系统不得把它记录为“未来确实还需 N 轮”的已证实事实。

字段回答的问题是：

> 当前这整批工具完成后，接下来连续的只读查证预计还要多少轮，才会到达不能继续只靠查证推进的下一步？

“不能继续只靠查证推进”的典型下一步包括实质修改、执行命令、询问用户、作出需要亲自权衡的关键判断，或给出结论。普通的选文件、沿引用继续读、判断搜索结果是否相关，不应一概包装成“关键判断”，否则估计又会退化成每次 0。

不要求模型给任务难度打分，不设置“难度超过多少才交还”的阈值，不把“高难度”机械等同于任何一次 `edit`。一次纯分析任务可以在没有文件修改的情况下结束调查。

### 3.2 连续前缀，不是全任务剩余工作量

只计算从下一轮开始连续可做的只读查证。中间一旦需要命令、写入、用户授权或关键取舍，这一段就结束，不能把后面的调查也提前计入。

例如：读调用点 → 读测试 → 修改 → 运行测试 → 再读失败堆栈。当前估计可以覆盖前两项，不覆盖修改以后的那一段。

例如：必须先运行一次测试才能知道失败位置。下一步不是只读查证，应填 0；不能把“测试之后大概还要读三个文件”填成当前的 3。

### 3.3 计数与当前批次

当前响应的全部工具调用视为一个已经选择好的批次。每个参与调用都针对这整批完成之后估计，不只针对自己返回之后估计。

```text
当前主模型响应：read(A) + grep(B) + edit(C)
这三项都不计入 estimated_readonly_rounds。

后续第 1 次生成：glob(D) + read(E)       一轮
后续第 2 次生成：read(F)                 一轮
后续进入修改或复杂判断                  结束这一段调查

合理估计可以是 2，而不是工具个数 3，也不是把当前批次加进去的 3。
```

模型可能估错，工具结果也可能改变路线。宿主不因实际轮数与估计不等就报协议错误，更不能要求模型为了兑现估计多读文件。

执行器若在某次获准生成中直接输出文本结束，该次生成仍按既有请求记账计一轮。预测的调查长度与运行时每次实际请求的消耗不是同一件事；不为了给结束文本留位置偷偷把 N 改成 N+1。

### 3.4 0 的含义

0 表示当前批次完成后，没有可合理展望的连续只读调查轮次，或者下一步已经到达上述边界。它不表示关闭功能，也不表示拒绝某个执行者。

材料已足够、下一步准备改代码、必须先运行命令、需要用户确认、准备给出结论，都是合理的 0。仅仅“不想让别人做”不再是这个字段询问的内容。

不要求精确预测。信息不足但下一步明确仍要沿调用关系查证时，可以给当前合理估计；不能把“不是百分之百确定”解释为必须填 0。

### 3.5 同批 max 只留在宿主合同里

参与工具分别给出的估计可能不同。宿主取 `max`，不相加、不平均、不取最后返回值；0 不否决同批其他正数。

模型可见文字不再解释“取最大值后交给谁、谁接手、如何保留控制权”。宿主的聚合算法不是模型填估计所必需的动机说明。本稿将旧文档“必须把 max 调度行为告诉模型”的要求一并撤回。

多项估计描述的是同一段后续调查的不同视角，不能解释为互不重叠的工作配额。宿主取最大值也不保证完成每条短记中的所有计划。

### 3.6 输入范围与拒绝规则

新调用必须首先证明值是 JS 原生 `number`，随后检查有限、整数、非负且不超过 `2147483647`，最后才构造领域值。

拒绝缺失、`null`、字符串数值、布尔值、小数、负数、数组、对象、NaN、Infinity 和越界值。NaN/Infinity 主要由直接 JS 边界测试覆盖，不能伪造它们是合法 JSON 字面量。

不使用 `parseInt`、`Number(...)`、字符串转换、truthy 判断或 Fable `unbox` 代替这些检查。`-0` 若由真实 number 到达，数值上按 0 处理；不因此改写原始调用证据。

`2147483647` 是表示范围，不是业务档位。不为了限制成本另加 2、3、7 等隐藏上限。

---

## 4. `self_note`：条件性未来展望

### 4.1 以本次调用的值为准，不以批次 max 为准

| 调用是否参与 | 本次估计 | 短记输入 | 结果 |
| --- | --- | --- | --- |
| 不参与 | 无关 | 任意同名业务字段 | 本协议不接管；由原工具合同处理 |
| 参与 | 0 | 属性不存在 | 合法 |
| 参与 | 0 | `""`、空白、普通字符串、`null` 或其他值 | 新调用参数错误；不能先删掉短记再装作合法 |
| 参与 | 正数 | 非空白字符串 | 结构上合法；有用性另评估 |
| 参与 | 正数 | 属性不存在、空串或仅空白 | 新调用参数错误 |
| 参与 | 正数 | `null`、数字、布尔、对象、数组 | 新调用参数错误 |
| 参与 | 非法估计 | 任意 | 拒绝估计；不据短记补出数值 |

例如同批 `read=0`、`grep=3`，read 必须省略短记，grep 必须提供短记。不能因为整批 max 为 3，倒过来要求 read 也补一条。

真实 JSON 中省略属性与提供 `null` 不等价。直接 JS 单元测试还应覆盖“自有属性存在但值为 undefined”；本稿按出现了字段处理，零值时拒绝。校验存在性用 own-property 证据，不用 `value == null` 混淆。

### 4.2 内容应该包含什么

一至三句话即可。围绕接下来要核对的材料或关系、准备消除的不确定性、到什么证据出现时可以进入下一步。

不要求逐轮编号，不要求长计划，不要求解释为什么选择这个数，不要求输出完整思考过程。第一人称是合适的语气，但不是必须以“我”或“I”开头的语法门禁。

合格示例：

```json
{
  "estimated_readonly_rounds": 2,
  "self_note": "接下来先核对入口对空值的处理，再查调用方与现有测试的约定。确认差异是否发生在参数归一化之前后，就可以确定修改位置。"
}
```

```json
{
  "estimated_readonly_rounds": 1,
  "self_note": "接下来只核对评审依据中的三个路径是否对应实际改动。证据吻合后即可作出评审判断，不再扩大调查。"
}
```

```json
{
  "estimated_readonly_rounds": 0
}
```

不合格的内容示例：

| 文本 | 问题 |
| --- | --- |
| “继续看看。” | 没有说明具体查证对象或停点 |
| “我正在认真分析这个复杂问题。” | 当前状态，不是未来展望 |
| “这次修改已经完成。” | 完成摘要，不是后续调查 |
| “请子代理替我检查所有文件。” | 指挥其他执行者，而且范围失控 |
| “为了让系统多工作，我填 5。” | 围绕调度意图，而非任务所处位置 |
| “入口一定有 bug，继续找证据证明。” | 把未经证实的猜测当成既定事实 |

上述内容差异用于说明和行为审阅，不能据此写关键词黑名单来判断所有自然语言。边界能拒绝空白，不能可靠地证明一段话一定有价值。

### 4.3 不改变短记的证据地位

短记是未执行展望，不是已查证结果，不扩大权限，不允许执行原工具集合以外的动作，也不覆盖系统或用户要求。

仍只沿原始调用记录 → 既有对话投影 → ID 重定位 → 可见历史这条路径出现。不再复制到 system prompt、bootstrap、额外 user 消息、子会话启动参数或新 hint 事件。

同批多个正数调用各自保留自己的短记。只对数值取 max，不挑最大值对应的一条，不合并成“任务总指令”，不去掉其他调用的短记。

判断是否空白可以使用 `trim` 的结果，但保存时必须保留原字符串，包括空格、换行、引号和 Unicode。不能边验证边把原始证据修剪成另一段文本。

历史中的空串、0 加短记、正数无短记仍是过去真实发生的调用，不按新规则追溯拒绝或修补。

---

## 5. 工具逐项判定表

### 5.1 判定方法

问的是：这个工具调用结束后，让调用者顺手估计接下来的一段仓库只读查证，是否有具体用途？不是问这个工具自己是否只读，也不是问它属于哪个角色。

下表是本稿采用的初始清单。用户明确点名的排除项已经确定；其余具体取舍是按现有工具用途作出的工程决定。实现者发现实际职责与表中依据不符时，应拿代码和用例修改这一行，而不是顺手改成全量或整角色规则。

“加”表示在 Predictor 已配置且该工具本来可见时提供两个字段，绝不授予新的工具可见性。“不加”表示本机制不增加字段、不增加说明、不截取同名业务字段。

### 5.2 参与工具：每个入口为什么保留

| 工具 | 判定 | 依据与边界 |
| --- | --- | --- |
| `read` | 加 | 读取一个文件后经常仍需核对定义、调用方或测试 |
| `glob` | 加 | 文件定位通常只是后续查证的起点 |
| `grep` | 加 | 命中位置通常需要后续读取确认，不能把一次搜索当成全部调查 |
| `js-manager` | 加 | Manager 的真实评审调查入口；仍受评审阶段与 `contract` 约束，后段不能借新字段复活它 |
| `js-engineer` | 加 | 综合仓库操作表面；估计在当前脚本完成之后，不静态猜脚本是否写入 |
| `js-devops` | 加 | 综合仓库操作后可能继续查证源码或配置；不表示可把此 JS 工具交给 Replica |
| `edit` | 加 | 当前修改完成后可能继续调查其他调用点、测试或关联实现；当前 edit 照常执行 |
| `write` | 加 | 新文件或内容写入后仍可能需要连续查证；不是“出现写入就永远没有后续调查” |
| `mv` | 加 | 移动完成后可能核对路径引用、导入关系和测试 |
| `rm` | 加 | 删除完成后可能查证残余引用；原删除权限与确认要求不变 |
| `fetch` | 加 | 本仓是 Casebook 案例获取，不是按名称猜成任意网络 fetch；材料获取后可继续核对当前仓库 |
| `run` | 加 | DevOps 同步命令返回后可根据结果进入源码查证；若下一步仍必须运行命令，估计为 0 |

### 5.3 不参与工具：不能只写一个“其余都不加”

| 工具 | 判定 | 具体理由 |
| --- | --- | --- |
| `fork` | 不加 | 工作分配入口，不承载第二套调查调度参数；用户明确点名 |
| `resume` | 不加 | 恢复既有工作分配，保持与 fork 同一职责边界 |
| `commission` | 不加 | Orchestrator 委派工作，不是仓库调查展望入口 |
| `join` | 不加 | 消费子工作结果或等待其完成；用户明确点名，不因返回文本就当成调查工具 |
| `horizon` | 不加 | 观察协作状态，不是文件证据调查；需要调查时由实际调查工具进入 |
| `review` | 不加 | 提交评审判断，不再借此继续开启调查 |
| `suicide` | 不加 | 生命周期终结，不附加未来调查参数 |
| `fission` | 不加 | 同一参与者的并行执行控制，不承载该估计 |
| `open-terminal` | 不加 | 开启活跃 PTY，后续属于终端交互链路 |
| `send-terminal` | 不加 | 发送终端输入，可能仍需继续控制或等待进程 |
| `read-terminal` | 不加 | 虽然名称像只读，但对象是活跃 PTY；可能还需轮询/控制，不等同于稳定文件查证 |
| `signal-terminal` | 不加 | 对进程发信号，属于控制动作 |
| `skill` | 不加 | 加载方法不等于已经开始具体查证；从后续真正的调查调用填写即可 |
| `sphinx` | 不加 | 保持专门问询协议，不叠加这项仓库调查估计 |
| `assume` | 不加 | 维护认知状态，不是获取外部证据的工具 |
| `enough` | 不加 | 标记注意力或事项处置，不让“够了”再携带调查展望 |
| `abandon` | 不加 | 放弃事项的状态动作 |
| `defer` | 不加 | 延后事项的状态动作 |
| `subscribe` | 不加 | 建立关注关系，不是调查证据读取 |
| `publish` | 不加 | 发布信息，不是未来查证入口 |
| `celebrate` | 不加 | 制度学习记录，不是接下来若干轮的调查计划 |
| `regret` | 不加 | 制度学习记录，不把总结改造成调查参数载体 |
| `chronicle` | 不加 | Blogger 的唯一合法工具；不得装饰，用户明确排除 Blogger |
| `js-bookkeeper` | 不加 | 私有叶节点的专用工作，不参与普通主工作估计 |
| `bash-honeypot` | 不加 | 诱捕/纠错表面，没有正常的调查估计消费者 |
| `invalid` | 不加 | Host 合成错误占位符，不应增加任何估计协议 |

`read-terminal` 不加的明确代价是：从终端结果转入源码调查时，要到下一次真正的调查工具调用才出现估计。这是为了保持终端生命周期边界，不是遗漏。

### 5.4 名称存在不代表当前已暴露

| 名称或类别 | 处理 |
| --- | --- |
| `js-orchestrator` | 已知名称表可以有它，但当前生成器不会为无文件系统能力生成这一表面；不按 `js-` 前缀自动加入 |
| `js-blogger` | 当前非 Blogger 合法表面；不得为了使工具清单“完整”而生成或开放，更不能装饰 |
| `query-shell` 等历史角色专用定义 | 不因为源码仍有函数就认定是当前合法可见工具；若实际可见，先核实其权威和职责 |
| Host 自带 `question`、todo、web 类等实际额外工具 | 当前清单未证明全部存在；P0 通过最终 provider 枚举逐项判定，不凭印象填完整性结论 |
| MCP / 动态发现工具 | 未判定前不加；取得准确身份和职责后明确登记，不能按名称含 read/search 就自动加入 |
| 未知 `js-*` 名称 | 未判定，不使用宽泛前缀匹配，也不假定具有仓库调查语义 |

### 5.5 真实角色和阶段验收

Manager 的调查阶段必须看到本来合法的 `js-manager` 加新字段，并保留原评审合同。进入评审后段、有效证书收尾或清理受限窗口时，工具可见性继续由现有能力事实决定；剩下的 `join/suicide` 等不带本协议。

Orchestrator 不能因为全局开启 Predictor 就在 `commission/join/horizon` 等工具上被迫填两个字段。将来确有独立调查工具可见时，按该工具行决定，不增加角色总开关。

Blogger 的真实 Main、压缩/收尾相关生命周期都不应看到本机制附加字段。验证的是实际 provider-visible `chronicle` 定义，而不是只验证 Blogger 不会启动 Replica。

Engineer 和 DevOps 的参与工具只在既有权限允许时出现。不能修改 permission map 来让估计协议“有地方挂”。

### 5.6 单一判定来源

实现一个小而明确的逐工具判定入口，供 schema、调用边界和来源批次消费共同使用。不要在三个文件各写一份 `Set<string>`。

可采用以下语义形状；名称可随代码风格调整，但三种状态不能混成一个不知含义的布尔默认值：

```fsharp
// 拟定接口，不是现有 API。
type InvestigationToolPolicy =
    | EstimateAfterCall
    | NoEstimate
    | Unreviewed

val classifyTool: toolName: string -> InvestigationToolPolicy
```

固定已知名称用逐项匹配即可。判定理由以本表和测试保留，不需要为每条理由建立运行时日志、数据库或通用策略引擎。

生产遇到未判定工具保持其原有业务行为，不加字段、不错误剥离参数；清点与发布验收必须显示未判定项。新工具需要一条明确判定及测试，不能通过“未知默认不加”永远逃避审阅。

参与清单与 Replica 的 `read/glob/grep` 执行白名单必须是不同概念，不能为了复用代码合并成一张表。

---

## 6. 把数据与职责分清，再改函数签名

### 6.1 四件事不要再叫同一个 budget

| 概念 | 所有者 | 是否持久化为新的独立事实 |
| --- | --- | --- |
| 单个调用的未来调查估计 | 模型原始调用，经参数边界验证 | 随原始调用证据保留，不另建逐调用估计日志 |
| 短记 | 同一个原始调用 | 不另建 Hint/SelfNote 事件 |
| 完整来源批次的聚合结果 | 批次纯计算 | 由已存在的 Requested 记录执行所需结果 |
| 本次只读执行的请求上限与已用次数 | 既有 Delegation/Replica runtime | 沿既有事件和资源生命周期处理 |

输入估计可以使用一个小的 `EstimatedReadonlyRounds` 值类型；内部继续使用 `ReadonlyRoundBudget`。二者的转换发生一次，名字明确，例如 `toExecutionBudget`，数值不变。不要求每个函数再包装一种 record，也不为这一转换建立服务或管理器。

保留 `RequestedRounds`、`DelegationRequested`、`ModelExecutionPurpose.ReadonlyDelegate` 等内部名字是允许的：它们描述的是宿主真实执行行为。要删除的是模型可见的意图询问，不是把内部真实机制也藏成无法理解的名字。

### 6.2 一份共享输入合同，两个薄适配器

建议新增一个窄模块 `Strength/InvestigationEstimateContract.fs/.fsi`，只承载：

1. 新字段名、稳定协议版本、数值范围。
2. 逐工具参与判定。
3. 原始值的严格类型检查，以及估计/短记的条件关系。
4. 可供 Host 与来源批次共同调用的纯输入解析结果。

它不得依赖 `OpenCode/Plugin`、模型配置、日志、文件系统、EventStore、租约或 runtime。JS 原生类型检查是边界计算，不产生外部效果；需要 Fable interop 时按实际 owner shard 依赖编排，不能反向引用高层 Host 模块。

`OpenCode/Host/ReadonlyDelegationContract.fs/.fsi` 保留为 schema 和业务参数适配器，调用这个共享合同。文件名可以保留，因为它仍接入内部只读执行；其中 `Collaboration`、`retain control` 等已失去含义的私有符号和注释应改成调查估计语义。

`Strength/OpenCode/Delegate.fs/.fsi` 负责从真实来源提取调用，复用同一解析器，再把聚合值交给既有领域和持久化流程。不要让它另写一套宽松的数值检查。

这是一份输入合同的复用，不是让所有工具都共用一个含角色、状态、语言、租约、短记的万能上下文对象。

### 6.3 建议的解析结果

```fsharp
// 语义草图；实现时使用本仓已有的 Result/错误类型习惯。
type ParsedInvestigationEstimate =
    | NoFurtherReadonlyRounds
    | FurtherReadonlyRounds of EstimatedReadonlyRounds

// 短记在函数中验证，但不从原始 arguments 中搬运为业务消息。
// 原始 arguments 在调用证据所有者处保留。
val parseParticipatingArguments:
    arguments: obj -> Result<ParsedInvestigationEstimate, EstimateArgumentError>
```

如果现有代码更适合返回一个包含非负数的已验证值，也可以直接使用该形状。必须保持的行为是：参与与否先由工具身份决定；正数/短记关系经过同一验证；业务预算不能从未验证的普通数字构造。

建议错误至少能区分：缺失估计、错误数值类型、非法范围、0 时出现短记、正数时缺少/空白短记、短记非字符串、新请求混用旧字段。不要全部压成“参数无效”，也不要为每个内部辅助函数建立一种错误层。

### 6.4 来源调用要保留工具身份

目标来源结构至少包含下列信息：

```fsharp
// 本次来源解析的局部值，不是新的事件格式。
type SourceToolCall =
    { CallId: ToolCallId
      ToolName: string
      CanonicalArguments: string }
```

所属 owner、来源 provider run、physical user message、协议版本与原始顺序属于完整来源批次的证据，不必在每个调用 record 重复存一遍。

Host 原始 tool part 与 wire `WireToolCall` 两种路径都必须提供同一工具身份。不能从 arguments 字段猜工具类型，不能按下一个结果的位置推断名称，也不能只给其中一条路径补 ToolName。

不要为了改本地输入形状重写已经稳定的 Frame/event schema。SourceCall 是本次来源解析的值；回传 Replica 交换和持久化材料仍用其现有领域类型。

---

## 7. 模型实际看到什么：可直接使用的中英文文本

### 7.1 英文属性说明

下面是参与工具根 object 的增量片段，不是替换整个原 schema。只把 `estimated_readonly_rounds` 追加到已有 `required`。

```json
{
  "estimated_readonly_rounds": {
    "type": "integer",
    "minimum": 0,
    "maximum": 2147483647,
    "description": "Estimate how many consecutive read-only investigation rounds will still be needed after ALL tool calls in this response have completed, before a substantive change, a command, user clarification, a conclusion, or a consequential judgment that you must make yourself. One round is one model request and may contain several parallel tool calls; do not count the current batch. Routine choices about which reference or file to inspect are part of investigation. Use 0 when no such investigation remains or the next step already reaches one of those boundaries. Give your current best estimate; it need not be exact, and do not add work to match it."
  },
  "self_note": {
    "type": "string",
    "description": "Provide this field only when this call's estimated_readonly_rounds is greater than 0; otherwise omit the field entirely, without an empty string or null. For a positive estimate, leave a brief, non-empty outlook for the next investigation rounds: what evidence or relationships to inspect and what finding will make the next step possible. One to three sentences are enough. Do not provide a progress report, generic filler, instructions to another worker, or a full reasoning trace."
  }
}
```

### 7.2 中文属性说明

`estimated_readonly_rounds`：

> 当前响应的全部工具执行完成后，预计还需要连续进行多少轮只读查证，才会到达实质修改、执行命令、向用户确认、给出结论，或必须亲自权衡的关键判断？一轮是一次模型请求，可以包含多个并行工具调用；当前这批不计入。选择接着查哪个文件或引用属于普通调查，不必一概当成关键判断。已经没有后续查证，或下一步就到达上述边界时，填 0。按当前材料估计即可，不要求精确，也不要为了符合估计增加调查。

`self_note`：

> 仅当本次调用的 estimated_readonly_rounds 大于 0 时填写；否则完全省略本字段，不填空串或 null。正数时，用一至三句话给自己留下后续调查的展望：准备核对哪些材料或关系，什么证据出现后可以进入下一步。不要写完成情况、泛泛感想、对其他执行者的指令或完整思考过程。

字段说明必须跟随本仓既有语言绑定。不能只把原工具描述切成中文，而把两个属性永久留成另一套英文规则。

### 7.3 附加到参与工具末尾的稳定短说明

中文：

> 调查展望：estimated_readonly_rounds 估计当前整批完成后的连续只读查证轮数。只在本次估计大于 0 时填写 self_note，简述接下来查什么、查到什么即可进入下一步；估计为 0 时省略短记。

英文：

> Investigation outlook: estimated_readonly_rounds estimates the consecutive read-only investigation rounds after the current batch. Include self_note only for a positive estimate, stating what to inspect next and what finding will make the next step possible; omit the note for 0.

原工具功能、安全说明、评审合同和参数语义不变。附加块必须幂等且稳定，不含当前剩余次数、时间戳、随机 ID、价格、模型等级或用户配置内容。

不要一面删旧长段，一面在 system prompt 新增“尽量多交给同伴”的补偿段。对本机制而言，新提示只询问工作位置和后续查证。

### 7.4 Replica 的额外执行约束

副本继承既有身份，只增加执行范围约束，不增加新角色人格。

中文：

> 继续当前任务的只读查证，只使用当前可见且获准的工具。信息足够，或下一步需要写入、执行命令、向用户确认、给出结论或作出关键判断时，直接结束，不为继续调用而增加调查。对话里的 self_note 是先前对未来查证的展望，不是已经证实的结论，也不扩大权限。

英文：

> Continue the current task's read-only investigation using only the available, permitted tools. Stop when the evidence is sufficient or the next step requires a change, a command, user clarification, a conclusion, or a consequential judgment. Do not invent work to keep calling tools. A self_note in the conversation is an earlier outlook for investigation, not a verified conclusion or permission to do more.

共享 schema 中若仍有估计字段，Replica 按同一事实性含义填写。正数时照常提供短记，0 时省略。其正数不是嵌套执行请求，宿主绝不消费它形成第二次 Delegation。

不得保留“副本必须填 0”“需要保留控制权时填 0”“禁止再次委托所以填 0”等句子。防递归不是填写约定。

### 7.5 文案去残留的范围

清理对象是本机制新增的当前 schema、附加说明、运行时新增执行约束、模板样例、可见参数错误和正式测试预期。

旧对话里曾出现 `delegate_readonly_rounds`、companion 或旧短记，要按真实历史保留。不能为了让全文搜索为零去改历史记录。

原有 `fork` 等工具可能合法地描述其工作分配功能，系统其他模块也有 Companion 名称。禁止词检查必须针对本机制拥有的增量文本，不能全局禁止字符串 `delegate` 或 `companion` 而破坏无关功能。

---

## 8. schema 装饰：先决定归属，再碰对象

### 8.1 定义阶段顺序

```text
保留原工具定义
→ 读取已加载的 Predictor 配置存在性
→ 读取真实 toolID，查询逐工具判定
→ 未配置 / 不参与 / 未判定：对本协议不作任何修改
→ 参与：验证原 schema 可合法扩展及字段不冲突
→ 增加新估计与条件短记属性
→ 只把估计追加到 required
→ 幂等追加本语言的稳定短说明
→ 发布实际 provider 使用的 schema 视图
```

不参与路径应尽早返回，不做仅为本协议而进行的 Effect schema 转换、对象重建、空字符串追加或冲突检查。它自己的业务字段即使叫 `self_note` 也不归本协议管理。

`ManagerReviewContract` 是另一份合同，按原来的工具权限单独工作。本协议 no-op 不等于阻止其他合法装饰器运行；比较无变化时应比较进入本装饰器之前与之后，而不是否定所有插件效果。

### 8.2 `tool.definition` 的静态限制

当前方案依据的 hook 入口只有 toolID，不提供可靠的 session/role/request-kind 分化证据。逐工具静态选择能够排除 `chronicle`、`fork` 等不同名称，但不能让同名 `read` 在 owner 时显示字段、在 Replica 时隐藏字段。

本稿接受同名参与工具可能在其他合法请求中看到相同字段。是否形成内部执行仍在真实调用/来源准入时决定，不扩大现有 root WorkMain 边界。

不加全局 `currentSession/currentRole/isReplica`，不依赖异步回调顺序修改共享 schema，不用工具名称猜调用者身份，也不为本任务 fork Host。

将来若要按真实请求隐藏字段，先取得并证明公开 Host 能力，再另行修改；它不是本次完成的隐含前提。

### 8.3 schema 视图与组合结构

逐一覆盖当前适配器的三条路径：已有 `jsonSchema`、`parameters` 自带 object properties、Host 内建 Effect schema 转换出的 provider JSON schema。

不修改 Effect 业务 schema，让其继续验证工具原本参数；协议字段必须在进入业务解码前剥离。两个普通 JSON 视图确实都被发送或验证时要保持语义一致，不能只让测试读取的那份正确。

保留原有 required 项和顺序约束、`additionalProperties`、`$ref`、`oneOf/allOf`、nullable 及 strict 语义。只添加根 properties 并不自动证明所有组合 schema 都可扩展；发现不支持的参与工具定义，应明确阻止发布该不完整定义并补适配证明。

不能为了让装饰成功，把原 schema 展平或把 `additionalProperties:false` 改成 true。也不能把估计改成可选、把短记改成必填 nullable 来迁就 provider。

### 8.4 为什么不直接依赖条件 JSON Schema

逻辑上可以用 `if/then/else` 表达条件必填，但当前 Host/provider 路径是否原样接受和验证尚未证明。本稿基础实现不依赖这类关键字，采用简单属性 schema、明确描述和统一运行时条件校验。

省略 `self_note` 的能力必须由真实 provider-visible 请求和工具执行证明。某个 strict 适配器若强制所有属性必填，该组合在修复前就是兼容性阻塞；不能在日志里标成功，也不能偷偷改为“每次填 null”。

### 8.5 冲突、重复装饰和重载

参与工具若本来定义了同名字段，只接受可证明属于同版本本协议的幂等重入；工具本身的不同语义字段不得覆盖。`required` 非数组、根 schema 非法、字段形状冲突，要给出具体 toolID 和错误原因。

旧装饰对象不能简单再叠加新字段。默认以全新进程、重新从原始工具定义构造 schema 完成切换；不新增热迁移共享对象的全局状态。

若现有开发重载确实复用已装饰对象，必须用本协议所有权证据识别并重建自己的增量，不能删除所有同名字段、替换任意包含 companion 的段落，或把旧 required 留在原处。

同一新定义重复装饰、语言按既有流程重新构造、多个 owner 同时获取定义时，结果稳定且没有旧长段重复残留。

---

## 9. 调用边界：校验、原始证据、业务参数各归其位

### 9.1 当前代码的风险不能靠改键名解决

当前 `ReadonlyDelegationContract.hide` 只处理参数隐藏，不调用 `trySelfNote` 或数值验证；`PluginHooks` 的全局隐藏也不以逐工具参与为前提。不能因为已有两个纯校验函数就声称真实 before 链路已经执行校验。

改造后必须用实际生产 hook 的测试证明：非法新协议参数在工具业务 body 之前被拒绝；不参与工具完全保留自己的业务入参。

### 9.2 新调用处理顺序

沿用现有权限、需求约束和 Host 错误所有权，接入以下边界：

```text
取得真实工具名及调用身份
→ 判定本次新调用是否受新协议约束
→ 参与时验证 estimate/self_note 的完整关系
→ 保存本协议确实拥有的原始字段证据
→ 分离业务参数，去除仅属于本协议的两个字段
→ 执行原工具的解码和业务行为
→ 由已有路径记录真实成功或错误结果
→ provider 历史投影恢复该调用的原始协议字段
```

权限拒绝仍然拒绝，不因输入满足新协议就放行。现有前置检查的职责与副作用需保持，不为追求一条短链把所有 gate 重排。必要保证是：本协议参数错误不能在业务效果发生后才首次发现。

同一并行批次中的其他合法调用可能已经执行。一个调用协议非法时，不得宣称整批业务效果原子回滚；能保证的是该非法调用不进入业务 body，整批不产生新只读执行。不能通过自动重放整批来修复元数据，从而重复 edit 或命令。

### 9.3 未配置与未参与时真正无操作

未配置 Predictor 的新调用不要求两个字段，本机制不截取、不隐藏、不恢复任何未经自己接纳的同名参数。原业务 schema 是否接纳这些字段由原工具决定。

未参与或未判定工具同理。即使它的 arguments 恰好包含 `estimated_readonly_rounds:5` 和 `self_note`，也不能据此认定它加入了本协议，更不能从中产生预算。

不能用“字段出现了就顺手清洗”作为防御式兜底。字段所有权来自工具判定和真实呈现合同，不来自字符串相同。

### 9.4 与评审 `contract` 共存

`js-manager` 的 `contract` 保持原来的乐观提示与业务权限合同，不把新估计的严格校验移植到 `contract` 上。

新估计与短记的原始字段在本协议拥有的范围内处理；评审 `contract` 在其拥有的范围内处理。一个协议的 Symbol、快照或恢复失败不能覆盖另一个协议的数据。

`sanitizeSnapshot` 不再只过滤 Contract。应分别依据 review-tool 判定和 investigation-tool 判定收窄各自字段；配置未开启或未接纳本协议时，不保存调查字段为协议快照。

### 9.5 参数对象与异常路径

优先使用独立业务参数视图，不让业务代码直接修改原始调用证据。若公开 Host 接口只能替换 `toolOutput.args`，先用 canary 证明替换时机和原始持久化归属；不能想象存在一个隔离参数 API。

若继续沿用现有私有 Symbol 隐藏/恢复，必须保留并扩展以下证明：不可扩展对象、不可配置属性、冻结对象、before 重入、after 重复、after 不到达、after 拿到不同对象、业务抛错、取消以及两会话同名调用交错。

异常路径不得使原始短记消失或串到另一调用。after 只是可能的同源恢复时机，不是唯一证据保证。

恢复应依据该 exact call 曾经保存的快照，不仅看“当前配置是否仍开启”。配置撤销不能让已经接纳的调用在结束时丢失证据，也不能反过来给从未参与的调用补字段。

### 9.6 vault 的真实边界

保留 `ProtocolArgumentVault` 作为现有 Host 清理补偿的一部分，但只保存本机制实际拥有的字段。快照仍从真实原始 arguments 取得，不合成短记、不另存计划摘要、不计算预算。

核实当前 session/call key 在支持的 Host 中是否足以唯一关联；若两个真实 provider run 可以复用 callID，必须使用已有 exact run 证据修正关联，不能拿 session 当前值兜底。不要无证据地扩大 key，也不要因测试手填唯一 ID 就宣称生产无碰撞。

此 vault 是进程内资源，不是历史真相。重启后若原字段已进入既有 canonical/XTrace 记录，就从原记录读取；若尚未记录而丢失，不伪造。已有 durable Requested 可提供既定预算，但不能凭预算恢复出模型从未保存的短记。

短记本身不构成运行授权，普通压缩使其不可见时不补注入。若完整来源证据缺失到无法合法形成新 Requested，则放弃本次机会或按既有恢复规则报告阻塞，不从残缺投影猜测。

### 9.7 可见错误文字

参数错误只解释当前输入规则，例如“estimated_readonly_rounds 为 0 时必须省略 self_note”，或“正数估计需要非空的后续查证展望”。

不要输出“你没有授权同伴”“请增加委托轮数”“必须给子代理计划”之类旧动机。错误处置不能强迫正数；模型改为合法 0 且省略短记也是有效修正。

普通输入错误按工具参数错误处置，不因漏一条短记就触发全进程 fuse。持久化矛盾、权限突破和材料身份冲突仍按各自不变量失败策略处理。

---

## 10. 完整批次与参与子集：本次最容易改错的地方

### 10.1 两个集合，各有一个用途

```text
AllCalls：同一真实 provider 响应中的全部调用，按原始顺序。
EstimateCalls：AllCalls 中被明确判定为 EstimateAfterCall 的调用。

等待完整性看 AllCalls。
参数校验与 max 看 EstimateCalls。
Requested.SourceToolCallIds 继续记录完整 AllCalls。
```

不能先过滤掉 `join/fork`，再宣布“参与调用的结果收齐了，所以整批完成”。不参与工具可能还在工作，或者其结果已经使 owner 结束，必须等整个来源批次的真实结局。

同样，不能因为一个不参与调用没有估计，就把整批判成参数错误。缺失非法仅适用于确实参与、且确实受本版合同约束的新调用。

### 10.2 纯聚合流程

```text
collect(allCalls, allResults, sourceEvidence):
  证明 exact 来源、调用唯一性、结果一一配对、整批封口
  按原始 call 顺序恢复 AllCalls
  对每个调用查逐工具判定
  NoEstimate / Unreviewed：不读它的协议同名字段
  EstimateAfterCall：使用共享输入解析器完整校验
  任一参与调用非法：返回参数错误；不形成执行请求
  没有参与调用：返回 NoEstimateOpportunity
  全部有效且 max = 0：返回 EstimatedZero
  max > 0：返回 PositiveEstimate(N) 和完整来源证据
```

`NoEstimateOpportunity` 与 `EstimatedZero` 都不启动，但诊断含义不同。不能对空参与集合调用 `List.max`，也不能用填一个 0 的方式掩盖“没有参与调用”。

宿主的纯聚合不需要返回合并后的短记。原始调用仍由原始证据持有，镜像按已有路径携带。

### 10.3 Host 与 wire 两条解析路径

`tryExtractRawAssistantBatch` 等 Host tool-part 路径保留名称、调用 ID、arguments 与真实结果。

`tryExtractWireAssistantCalls`、`tryExtractWireCompletedBatch`、`resolveCompletedSourceBatch` 等 wire 路径同步保留相同信息。对照尾批时，不能只比较 arguments 列表；工具名、ID、顺序和真实来源身份也必须一致。

两个工具可以恰好有一样的 JSON arguments。arguments 相同不意味着同一个工具，也不能证明 provider 响应相同。

原始 schema 的工具 ID 与实际调用名若有 Host 命名转换，使用真实注册/适配映射。不能在 capture 中再维护一张猜测别名表，不能把 MCP 外部名称截断后当成本地 `read`。

### 10.4 批次场景表

表中带正数的参与调用均已提供合法短记；省略号表示原业务参数，不代表省略必需协议字段。

| 来源批次 | 聚合与执行预期 |
| --- | --- |
| `read=0, grep=0` | 明确零估计，不创建 child |
| `read=0, grep=3, glob=5` | N=5，不求和，不要求一致 |
| `fork, join` | 没有参与调用，不要求任何估计，不创建 child |
| `read=3, join` | 等二者都完成，再检查合法续行；满足时 N=3 |
| `edit=0, read=4` | 当前 edit/read 各执行一次，合法续行时 N=4 |
| `run=2, read=1` | 原命令完成并有合法续行时 N=2；后续 Replica 仍不能执行 run |
| `read=3, suicide` | 即使有正数，真实生命周期结束则不启动，不拖延终结 |
| `read=3, fork` 且 fork 缺结果 | 整批未完成，不启动 |
| `read` 漏估计，`join` 无估计 | read 是参数错误；join 的省略不是错误；整批不启动 |
| `read=0` 却有短记，`grep=5` | 不把非法 read 当 0 接纳；整批不启动 |
| `read=2` 无短记，`grep=5` 合法 | 整批不启动，不偷偷只取合法部分 |
| 不参与工具自带同名数值 99 | 忽略其协议意义，不能抬高 max，也不删除原业务字段 |
| `read=3` 返回文件不存在错误结果 | 仍是真实结果；完整性与合法续行满足时可按既有策略处理 |
| 调用 ID 重复、结果重复或孤儿结果 | 不构成有效完整批次，不靠集合去重修补 |
| 两次 provider 响应各有一个调用 | 分别处理，不能跨响应拼成一个 max |
| 只有文本，没有工具调用 | 不存在来源工具批次 |
| Replica 自己发出 `read=7` | 可以是合法事实性估计，但绝不形成嵌套执行 |

### 10.5 捕获身份必须真实

来源批次属于哪个 provider run，必须由真实物理输出及既有权威关联证明。当前 `OwnerSurface.Target` 等名字不能当成身份正确性的证明；本次应检查取来源和稍后冻结消费目标的调用链，保持 Source 与 Target 的真实含义。

发现既有链路把不同身份混同，应先补失败测试并修正直接受影响的关联，不能为了少改文件继续将“最新 assistant”当作唯一来源。也不能把无证据状态默认成普通 WorkMain，使内部或恢复请求获得执行资格。

普通 owner/root WorkMain、非内部叶节点、非 Replica、非 prefix probe、非 repair、未取消且 authority 未失效的边界照常。静态给某工具加字段不会扩大这一准入范围。

---

## 11. 从估计到执行：保留旧骨架，切断新递归机会

### 11.1 两段式接线保持

第一段在真实来源完整、元数据尚未被压缩替换时捕获估计，形成并持久化 Requested。第二段在已有合法续行位置读取 Requested，冻结 target/mirror，持久化 Bound，再运行 Replica。

本次只替换输入字段、工具参与过滤与短记校验，不把 child 启动移入某个 `tool.execute.after`。单个 after 仍不知道整个并行批次的完成情况。

不增加 session 上的 `lastEstimate`、`nextBudget`、`latestNote`。同一 transform 内可直接传值；跨回调的生命周期关联继续由已有持久化身份负责。

### 11.2 内部转换规则

所有参与调用有效且 max=N>0，才有一个待准入的执行上限。既有策略再判断配置、身份、生命周期、存储和资源是否允许。

不新增收益阈值、估计可信度、短记字数评分、模型价格比较或正数比例门槛。N 的转换是明确的数值映射，不是偷偷恢复统计预测器。

同批不参与工具不是数值 veto；但其完成导致会话终态、取消或非普通续行时，生命周期仍可拒绝启动。这两种逻辑必须在测试和诊断里分开。

### 11.3 Replica 正数不再是协议偏离

共享 schema 下 Replica 可以估计还需 2 轮，并按条件填写短记。不能一边要求事实性估计，一边在宿主把它记成“违反不得委托约定”。

禁止递归应在 capture 入口、身份 gate、晚到回调和恢复路径上成立：任何 StrengthReplica/InternalLeaf 的输出都不是新的合法 owner 来源。

这些估计和短记可以随真实 Replica 工具调用回到 owner 历史，但其来源仍是 Replica。随后 owner transform 不能因为这些历史记录位于末尾，就把它们重新消费成主模型的新估计。

### 11.4 请求记账与资源不变

仍按实际接纳的请求记账，纯文本结束和已外发后失败都占一轮；重复通知不重复计数。禁止按完成 batch 数、当前历史长度或回调次数计数。

预算耗尽不截断已经开始的一轮，物理尾部清理与语义终止继续分离。普通错误可交回已有合法完整前缀，权限或材料不变量失败按原有严肃处置。

同 provider 容量为 1 时，owner 等 Replica 仍须通过已有 step/lender/fence 协议避免死锁。不能为了让新估计更常触发而增加容量、允许双占、制造超时抢跑或跳过租约检查。

### 11.5 Predictor 配置与其他改造隔离

工具定义和新请求准入共用同一份已加载的配置存在性。临时容量不足不改变“已配置”状态，也不来回改变 schema。

内部用途仍明确走 Predictor 池，owner 身份不变。字段改名不需要修改 routing ABI 版本，更不能覆盖用户的 MJS 配置。

工作区中的 PROMPT-006 方案可能调整 exact execution 查询、发送和准入模块。本方案只要求接入届时真正的权威边界；若另一项改造先落地，重新定位调用点，不恢复已删除的绑定缓存来迁就本文旧路径。

---

## 12. 协议版本、历史和升级：新调用断开，旧事实留下

### 12.1 三种历史不能混成一次迁移

| 数据 | 本次处理 |
| --- | --- |
| 古老的 K1/K2 预测协议材料 | 继续由已有历史迁移合同和离线工具处理；不在本次字段改名里另写一套 |
| 当前显式 delegate 协议的版本 1 调用和 Requested 等事件 | 原始事实保留；新运行只接受新调用合同，旧未执行请求按本节收尾 |
| 新事实性估计协议调用 | 使用新字段和新条件关系，产生稳定的新合同版本 |

当前源码 `contractRevision` 为 1。本稿以 2 作为目标版本；真正施工前若其他改动已使用 2，应协调唯一下一版本，不重复占用。版本由代码确定，不从环境变量或模型参数读取。

这是工具输入协议修订，不意味着 `requested_rounds`、Frame 格式、EventStore envelope、digest、routingProtocol 都要同时升版。只有实际格式变化才变更对应协议，不能为了“版本整齐”制造多余迁移。

### 12.2 新调用的 clean-break

新 schema 不暴露 `delegate_readonly_rounds`，新调用解析器不把它当新字段的别名。

参与工具在新合同下只带旧字段、或同时带新旧字段，都不能产生新请求；参数边界给出明确错误。不能用 `newValue ?? oldValue`，也不能旧字段缺失时补 0。

历史投影仍保留旧字段原名。合法旧 `self_note` 为空、估计为 0 却有短记、正数无短记，都不进行追溯校验。

本稿区分“读取历史事件格式”和“接纳新的工具输入”。保留既有事件解码能力不等于保留两套新调用协议；反过来，拒绝旧新调用也不等于可以删除旧事件。

### 12.3 不要按新版本给旧来源再造一个 DecisionId

现有 DecisionId 派生包含 ContractRevision。升级后对同一来源用新版本再派生，ID 会变化；若只按 ID 查重，就可能把已经消费过的来源再执行一次。

在形成新 Requested 前，要用既有 canonical 投影检查这个真实来源是否已经有任何版本的请求事实。来源身份至少由 owner、logical run、source physical user message 和 source provider run 共同限定。

已经有 Requested/Bound/Prepared/Promoted/Traced/Closed/Abandoned 的同一来源，不因版本变化重新开预算。调用集合、预算或 authority 不一致是冲突，不能换个 DecisionId 绕过。

优先在既有投影上提供确定性的来源查询；如果需要索引，它只能是从原事件派生的查询结构，不是一份可以独立写入的“已消费表”。同来源判定和 Requested 追加应进入既有串行/幂等提交边界，不靠两个无协调的先查后写。

### 12.4 升级时状态处理表

| 升级时事实 | 新进程处理 |
| --- | --- |
| 只有旧原始调用，没有 Requested | 不用新合同重解释；不由旧正数生成新 Requested |
| 版本 1 Requested，尚未 Bound | 本稿选择明确关闭旧待执行机会，owner 正常继续；不在升级后启动新的旧协议工作 |
| 版本 1 Bound，尚无 Prepared，旧执行进程已结束 | 按既有恢复规则 Closed，不重跑 |
| 旧执行在升级前仍活跃 | 先按真实生命周期完成或取消，再重启；不支持两个不同版本写入者同时接管同一活动资源 |
| 版本 1 Prepared，target 仍合法 | 按原 exact-target 合同消费已有材料，不重跑工具，不重解释短记 |
| 版本 1 Prepared，target 已失效 | 按既有 Abandoned 路径处理，不给新 target 冒名消费 |
| 版本 1 Promoted，尚未 Traced | 继续在原因果位置回放并完成 trace |
| 任意版本 Closed/Abandoned/Traced | 保持其终态，不产生新执行 |
| 任一追加结果未知 | 先读确切持久化事实；不能因升级把 unknown 当 absent |

关闭原因应使用语义真实的既有类型；现有原因集不能表达协议替换时，补一个明确的原因并测试解码，而不是挪用“用户取消”伪造事实。仍使用同一既有关闭事件，不另建升级事件总线。

关闭旧未 Bound 的机会是本稿的保守升级选择，损失仅是一次尚未执行的调查机会，不是任务结果或用户历史。必须写进升级说明，不能静默丢弃 pending 状态。

### 12.5 如何区分新调用与旧历史

新协议约束来自实际呈现的工具定义及其所属真实请求，不来自模型自行填写的 `protocol_version`，也不来自“现在程序版本是 2，所以所有 messages 都是 2”。

复用已有 provider/attempt/trace 的真实边界证据来关联呈现版本与来源。若当前证据不足以证明某条尾部调用是在新定义下产生，不能把它判成新输入缺字段错误，也不能把它升级为新授权。

默认升级流程是停接新工作，等待活动请求真实结束或通过既有取消流程收尾，再用新进程重新构造定义。新请求使用新合同；已持久化的历史按自己的版本读取。不要增加基于秒数的“过了多久就当成新版本”规则。

缺少必要来源证明时，先阻止该来源的新执行机会，并补公开 Host 关联的 canary。不能为了恢复成功再造一个 session-current-version 缓存当权威。

### 12.6 短记与冷启动的证据边界

新合同要求在新调用时提供短记，但不承诺短记拥有独立于普通历史的永久存活窗口。

若短记已进入 canonical 调用或 XTrace，则按原调用重建，保持原名、原文和 ID 重定位语义。若只在已丢失的进程 vault 中存在，不从其他短记、预算或模型重新生成来补洞。

Requested 持久化后，预算已有事实来源，不应再从压缩后的调用 arguments 重算。尚未 Requested 且完整来源不可恢复时，不重建一个猜测的预算。

### 12.7 回退不是重写历史

升级前先在隔离测试存储或备份副本验证恢复。不得清库、覆盖用户配置或修改 append-only 原记录。

新协议产生了新事件以后，回退到旧二进制是否可读必须先在副本中证明。不能声称“工具字段改名，所以 git revert 就足够”；旧程序可能仍会按旧 schema 从尾部调用取值。

无法证明安全回退时，保留现有数据，使用经过验证的兼容读取或前向修复方案；不能以回退为由删除已发生的新材料。代码回退和存储恢复是两个操作，各自需要依据。

---

## 13. 规范文件必须怎样改

### 13.1 主包逐条修改

保留现有 WHAT 编号，让测试仍能映射到真实条款。不为了避开旧测试另建一个平行“estimate”规范包。

| `speculative-investigation` 条款 | 修改内容 | 必须保留 |
| --- | --- | --- |
| [001] 启用与呈现 | 配置后仅装饰参与工具；字段改名；未参与工具无增量 | 未配置无功能基线 |
| [002] 来源与 max | 全批封口，参与子集校验与 max；不参与字段不消费 | 真实来源、单次请求、非只读来源不自动否决 |
| [003] 请求预算 | 说明事实性估计如何转换成内部上限 | 请求计数、提前结束、N+1 门禁 |
| [004] 同身份只读执行 | 去掉副本必须填 0 的假条件；允许共享 schema 下真实估计 | 真实身份、Predictor 用途、只读权限、防递归 |
| [005] 真实交换 | 新字段随真实 arguments 保存 | 完整性、digest、无专属长度上限 |
| [006] 持久化 | 明确新版本和完整 SourceToolCallIds；无额外 Hint 事件 | Requested/Bound/Prepared 前置 |
| [007] 消费与关闭 | 补协议替换的真实关闭路径及终态防重 | exact-target Promotion |
| [008] 回放和压缩 | 老字段历史不重写；注入的 Replica 记录不是新来源 | Promoted 的因果位置与 XTrace 闭包 |
| [009] 镜像与短记 | 短记改为未来展望，单路传递不变 | 原文保真、不复制提示、不反射 |
| [010] 一次性与恢复 | 跨版本按真实来源防重，旧 pending 的处置 | Bound 后不重新消费 |
| [011] 失败与取消 | 区分普通参数错误与严重不变量错误 | 不靠时钟终结、资源清理和 fuse 边界 |
| [012] 模型可见协议 | 整节重写为事实估计、条件短记与无意图说明 | 稳定语言、只回传真工具事实 |
| [013] Host 集成 | 按完整工具判定清单断言，不再要求全量加字段 | 真实 provider wire、原参数执行、并发证明 |
| [014] Predictor | 改名及选择性呈现后的基线 | 配置唯一启用依据，容量不是配置 |
| [015] 历史迁移 | 区分古老预测材料迁移与本次 v1→v2 输入协议修订 | 不删除或伪造历史 |

`WHY.md` 说明改变的原因：把工作位置估计与执行意愿分离；减少无意义的工具入参；让短记有明确用途。不要把用户观察直接写成“所有模型都因 subagent 联想而填 0”的已证实结论。

`APPLIES-TO` 补齐实际新增/调整的 `.fs/.fsi`、参数 vault、工具判定入口和受影响组合层路径。保持现有 glob 约定，不因更名留下旧路径，也不无理由扩大到整个仓库。

### 13.2 跨包联动

| 包或文件 | 必须修改/核对的点 |
| --- | --- |
| `host-boundary` [032] | 由全局隐藏改为按字段所有权处理；条件校验、异常恢复、未参与 no-op |
| `host-boundary` [019] 及实际工具定义边界 | 捕获/变换顺序和真实来源，不从合成尾部反推 |
| `behavior-diagnosis` | 删除 Blogger life 冻结本协议的要求；保留 RulebookRevision、chronicle.tip 和生命周期字节稳定性 |
| `capability-enforcement` | 参数参与与执行权限分离；不因新字段扩大能力 |
| `office-capability` | Manager 阶段、Orchestrator、Blogger 的真实可见工具不回归 |
| `provider-language` | 两个属性和附加短说明一起遵守既有语言绑定 |
| `provider-projection` / `semantic-trace` | 新旧原始字段保真；Replica 来源不被误认作 owner 新输出 |
| `prefix-stability` / `context-compression` | schema 稳定，估计捕获不被压缩丢失，预算不重置 |
| `execution-model-routing` | 用途、Predictor 配置存在性和容量等待不变；不重复改 ABI |
| `managed-chat-execution` / `crash-reconciliation` | 真实 request-kind、exact 来源与晚到回调的隔离 |
| `durable-events` / `durable-convergence` | 若实际新增关闭原因或来源查询，补解码与冷折叠证明，不改旧 envelope |
| `verification-system` | 修改真实 canary、长程场景和 oracle，报告 skip 与真实运行范围 |
| `README.md` / `CHANGELOG.md` / `DELEGATE.md` | 实施完成时更新用户可读协议和升级说明；不保留矛盾的“每个工具都填”示例 |

这些包不意味着每个都必须改源码。先查规范和调用影响，确实受影响才修改；未改但经过回归验证的包记录测试即可。

---

## 14. 文件级施工清单

下面分“必改”“条件改”“只回归”，不是要求把所有文件重新写一遍。

### 14.1 必改的协议和接线

| 文件/符号 | 具体动作 | 不能留下的尾巴 |
| --- | --- | --- |
| 新 `Strength/InvestigationEstimateContract.fs/.fsi` | 小型共享输入合同、参与判定、新字段常量、严格成对校验 | 不读配置，不建事件，不依赖 Host 组合层 |
| `OpenCode/Host/ReadonlyDelegationContract.fs/.fsi` | 先按工具判定；新属性和双语说明；复用共享校验；调整 hide/restore 接口所需上下文 | 不再有全量装饰和独立短记合同 |
| `OpenCode/Host/PluginHooksSurface.fs/.fsi` | 导出能测试新整体合同的业务 Surface；旧单值 helper 按真实用途清理 | 不保留仅为旧测试服务的兼容入口 |
| `OpenCode/Plugin/PluginHooks.fs` | definition/before/after/vault snapshot 同步收窄；真实 before 链路执行验证 | 不能 schema 选择性、hide 仍无条件 |
| `OpenCode/Host/ProtocolArgumentVault.fs/.fsi` | 新字段接入、字段所有权、原始值快照与恢复；内部命名可改为估计语义 | 不收集未参与工具同名业务字段，不合成短记 |
| `Strength/OpenCode/Delegate.fs/.fsi` | 保留工具身份，校验参与子集，区分无机会/零值/正值，复用共享解析，使用新版本 | 不再对全体调用读取旧字段，不留下 `List.max []` |
| `Strength/Surface.fs/.fsi` | 面向 JS 的正式测试 Surface 对齐新输入、来源和错误语义 | 不让测试 helper 默认补 0 而掩盖生产严格边界 |
| 主包规范和对应测试 | 按第 13 节更新 | 不只批量换字符串 |

如果新增共享模块后出现依赖环，应调整其 compile 所有权到真实公共底层，而不是在捕获端复制一个不同解析器；也不能把整个 OpenCode Host 模块拉进纯领域 shard。

### 14.2 依据实际依赖决定是否修改

| 文件/区域 | 何时修改 |
| --- | --- |
| `Strength/Budget.fs/.fsi` | 需要明确估计→预算转换或新增小值类型时；保留预算现有行为 |
| `Strength/Delegation.fs/.fsi` | 新版本的来源防重或关闭原因确有领域接口需要时 |
| `Strength/Projection/Model.fs/.fsi` | 提供跨版本 exact-source 查询或投影验证时 |
| `Strength/Events.fs/.fsi`、`EventVocabulary.fs/.fsi`、`Persistence/*` | 关闭原因或解码合同实际发生变化时；不预设需要换事件格式 |
| `Strength/Replica/Runtime.fs/.fsi`、`Replica/Transform.fs/.fsi` | 有旧字段/固定 0 约定，或新来源形状直接影响其适配时 |
| `OpenCode/Plugin/PluginTransforms.fs` | capture 接口、原始参数恢复或新版本来源证据需要接线时 |
| `PluginStrengthPorts.fs/.fsi`、`PluginSessionWiring.fs` | 仅调整必要签名与已存在资源接线 |
| 模型可见 prose assets / Persona prompt 生成 | 实际发现旧协作意图或固定 0 文字时，局部清理 |
| `ToolRegistry.fs` / `StaticTools.fs` | 工具清点或判定共享接口确有需要时，不改原能力集合来迁就协议 |
| compile-order 与 owner shard | 新模块、删入口或签名依赖变化时同步修改 |

### 14.3 原则上只回归的机制

`Strength/Frame`、Prepared/Promotion/TraceRecovery、普通压缩、租约调度、owner 身份继承、只读执行白名单、古老预测材料迁移，不因字段更名自动重做。

发现本次触及的真实缺陷可以修，但要有对应失败测试和因果说明。不能把之前尚未完成的所有大型改造都混入本次提交。

### 14.4 编译所有权

新增 `.fsi` 与 `.fs` 时，确认编译顺序先签名后实现、公共合同先于消费端。查实际 `.fsproj` 的 Compile 与 ProjectReference，不只改 `compile-order.txt`。

本基线已有 `Wanxiangshu.Owner.speculative-investigation.strength-event-vocabulary-contract.fsproj` 编译 Delegation 等领域入口，Host 合同由 opencode plugin 组合 shard 编译。具体把共享合同放在哪个最小合适 shard，应以当时依赖图为准，不能单凭名称硬塞。

更新 requirement 所有权、test imports、Surface 导出和 build freshness 所依赖的源文件集合。严禁手改 dist、留下空实现占旧路径、复制一份“临时兼容版本”逃避依赖清理。

---

## 15. 按依赖分批施工，每批都有退出条件

### P0：冻结真实基线与工具清点

先读生效 AGENTS，查看 HEAD、状态和相关 diff，记录其他工作区修改。核实 package 声明、锁文件、已安装包及 canary 真正运行的 Host 二进制，不通过安装最新版来掩盖当前兼容问题。

列出各合法角色/阶段实际 provider-visible 工具，记录稳定工具 ID、定义来源、当前参数/required、逐工具判定和依据。覆盖内建、插件、MCP/动态发现；不把 `knownToolNames` 直接当实际清单。

先运行受影响现有套件取得基线。记录已有失败与 skip，不靠重复跑、放大超时或减少断言制造干净基线。

退出条件：工具清单已核对；旧全量行为能由现有测试重现；Host schema 和 before 参数形态有可用证据。未知工具不凭空判为已覆盖。

### P1：修订产品合同并写失败用例

先更新主包及直接冲突的 WHAT/WHY/APPLIES-TO，尤其是 Blogger 的跨包冻结条款。形成第 5 节清单的正式归属，定义新字段、正数短记和旧历史规则。

新增最小失败测试：`fork/join/chronicle` 不装饰，`js-manager/read` 装饰；0 有短记拒绝；正数缺短记拒绝；未参与工具同名字段保持；混合批次参与子集 max；Replica 正数不递归。

测试应在旧实现上因这些具体行为失败，不以找不到新函数、导入报错或编译失败充当问题证明。需要新增 Surface 时，先通过现有公开 hook/Surface 表达能证明的失败，再与实现同批补新出口。

退出条件：失败原因明确对应新合同。失败测试可以先在工作区建立，但不能单独向主分支发布一批必红用例。

### P2：公共输入合同、类型与逐工具判定

实现共享判定和严格输入解析，明确 estimate 与内部 budget 的转换。只做纯输入逻辑，不接创建 child。

复用原生 JS 类型检查，一次解析完整 estimate/self_note 关系。给旧字段与新字段并存、own-property undefined、空白文本和极值补回归。

证明参与清单与实际权限表不同，未知工具不自动加入，解析器不吞掉不参与工具字段。

退出条件：共享合同对所有边界输入有确定结果；两处消费者不需要各写一份校验；相关编译和纯行为测试通过。

### P3：schema、文案和真实 before/after 边界

在 Host 适配器接入选择性装饰，替换中英文属性和旧协作长段。保持原 schema 约束，短记不无条件加入 required。

同步修改 before 的验证、快照过滤、字段剥离和 after/transform 恢复。保证不参与工具不被该机制修改；不是“先改 schema，执行边界以后再补”。

改造现有 Host canary，真实发送 `0 + 省略短记` 和 `正数 + 非空展望`，观察业务 body 与后续 provider 历史。不可只验证 mock schema 对象。

退出条件：参与与不参与两类真实工具都证明正确；中英文无旧增量文案；原业务执行与原参数证据成立。strict 适配器若破坏省略能力，相关支持结论暂停。

### P4：完整来源与参与子集聚合

同时修改 raw Host 与 wire 的来源解析，使工具名、callID、arguments、顺序和真实来源身份保真。把无参与、全零、合法正数、非法参与值四种结果区分开。

仍等全批结果，再计算参与子集 max。记录完整 SourceToolCallIds，不过滤后提前封口，不把失败结果凭空丢掉。

与真实 before 校验共享逻辑；capture 处的再验证只用于同一合同的证据核对，不另造宽松规则或参数修复。

退出条件：第 10.4 节全部场景有正式回归；调用/结果顺序变化不改变 max；源身份不明时不能启动。

### P5：执行、Replica 与当前请求身份

把新聚合接到原 Requested→Bound→Replica 链路。去除只为禁止递归而要求填 0 的文字和“正数即偏离”诊断。

证明副本正数、回传历史正数、晚到副本 callback、内部叶节点、repair、prefix probe 都不成为新的合法 owner 来源。不能借静态工具有字段扩大 root WorkMain 范围。

回归实际请求计数、N+1、提前结束、完整前缀、容量为 1、取消和权限白名单。

退出条件：新协议触发的只读执行真实可用；不存在递归或当前工具重复执行；内部调度目的与资源约束没有改变。

### P6：版本切换与历史恢复

设置新的稳定合同版本，增加跨版本 exact-source 检查，实施旧未 Bound 请求的明确关闭策略。旧事件和调用仍按事实读取，不批量替换字段。

使用含旧调用、各持久化阶段和新调用的混合历史，在两个独立进程中验证冷启动。不要只清空某个字典冒充进程死亡。

证明 v1 同来源不能因新版本得到第二份预算；旧 Prepared/Promoted 的 digest、因果位置和回放内容不变；未知追加仍阻止无证据外发。

退出条件：本稿第 12 节处理表逐项有测试；清楚说明哪些来源被关闭、哪些材料保留；无破坏式迁移。

### P7：全链路 oracle、文档和旧路径清理

更新 `032`、`012`、`013`、长程场景及其 oracle 的行为，不只是键名。删除“每个可见工具都有字段”“0 可以有短记”“副本正数违规”等断言。

修正配置撤销、Blogger 冻结、语言稳定、评审合同共存的测试。检查公共 Surface、fsproj、APPLIES-TO、README/CHANGELOG 和 DELEGATE 旧说明。

保留历史 fixture 中必要的旧字段，并在 fixture/测试标题说明其历史用途。运行时新输入的旧别名应清零，但历史解码和负面用例允许出现旧字符串。

退出条件：源码、规范、测试、例子没有相互矛盾；没有空 facade、死分支、调试打印或临时生成物。

### P8：工程门禁与行为评估分开交付

先完成真实 Host、冷启动、构建、正式 runner 和适用的发布门禁，再执行第 18 节的模型行为比较。工程正确性与效果好坏分别报告。

真实模型评估涉及实际 provider 调用和费用时，使用已有授权与测试配置；不因本方案写了评估步骤就擅自访问生产数据、消耗未授权额度或部署。

退出条件：工程测试能证明新合同落地；行为结果能说明估计、短记与实际任务表现，而不是只给一个正数比例。尚未执行行为实验时，明确标“未测”，不阻止把已验证的工程结论写清楚。

### 15.1 哪些工作可以并行

工具清点、规范冲突检索、测试设计可并行。公共合同确定后，schema 文案与版本 fixture 的准备也可并行。

同一文件重叠编辑、公共类型修改与调用者适配、before 快照与 capture 证据链、版本追加与恢复查询，必须按依赖串行收敛。不能两个分支各自发明一套参与清单，最后用合并解决语义冲突。

每个代码交付批次应可构建、对应正式测试通过。若 P3/P4/P5 的中间状态会向真实用户发布不一致合同，就合成一次对外切换；不要用临时兼容别名或新生产开关让半成品长期存活。

---

## 16. 自动化回归矩阵：不能省掉的具体用例

本节的 S/A/B/R/H/P 编号只是方案内的检查编号，不替代 requirement 编号。正式测试文件仍按 `NNN.test.mjs` 命名，标题带真实 `WHAT[包名-NNN]` 锚点。

测试走生产 Surface、真实 hook 或正式集成入口，不复制一份生产算法到测试里再测试它自己。测试期望清单应独立写出已判定的工具；不能调用生产 `classifyTool` 生成 expected，再证明生产结果等于自己。

### 16.1 S：工具选择与 schema

主要归属：`speculative-investigation` [001/012/013/014]、`host-boundary` [032]、相关 office/capability 条款。

| 编号 | 输入/场景 | 必须断言 |
| --- | --- | --- |
| S01 | 未配置 Predictor，参与工具 read | 不加两个字段、不加说明、不额外 required |
| S02 | 未配置 Predictor，fork/chronicle | 原定义保持；不因工具不参与而触发本协议校验 |
| S03 | 已配置，第 5.2 节每个参与工具 | 新估计 required 恰好一次，短记是 string 但非无条件 required |
| S04 | 已配置，第 5.3 节每个不参与工具 | 本协议前后定义无增量，原业务字段不变 |
| S05 | 未判定动态工具 | 不装饰；清点报告明确列出未判定 |
| S06 | 假名 `js-new-role`、`read-anything`、MCP read 别名 | 不因前缀或相似名自动加入 |
| S07 | 原 required 包含多个业务字段 | 全部保留，只增加新估计，不增加旧字段 |
| S08 | 重复装饰同一新版定义 | 属性、required 与说明不重复 |
| S09 | 原 `additionalProperties:false` | 不放宽；业务参数仍按原合同验证 |
| S10 | 参与工具新估计同名冲突 | 明确失败，不覆盖工具业务定义 |
| S11 | 参与工具 self_note 同名冲突 | 明确失败，不用字符串类型相同就当同语义 |
| S12 | 不参与工具有两个同名业务属性 | 完全保留，不报本协议字段冲突 |
| S13 | `required` 非数组或根 schema 不可扩展 | 不发布半装饰 schema，不静默跳过参与合同 |
| S14 | jsonSchema、plain parameters、Effect 三条路径 | 最终 provider 使用的视图正确；原业务 decoder 仍可运行 |
| S15 | 双 JSON 视图都被使用 | 字段、required、短记省略性一致 |
| S16 | 组合 schema/nullable/引用约束 | 原语义不被展平或放宽；不支持时明确阻塞 |
| S17 | 中文与英文定义 | 属性和附加段均对应语言；规则一致 |
| S18 | 两个 owner 并发获取定义 | 无 session/role 污染，无动态预算写入共享 schema |
| S19 | Manager 阶段转移 | 调查时 js-manager 正确；收尾工具无协议且权限仍收窄 |
| S20 | Blogger Main 与新 life | chronicle 不含本机制字段，原 tip/RulebookRevision 冻结仍成立 |
| S21 | Orchestrator 的真实可见工具 | 每个实际工具按独立期望清单判断，不统一加字段 |
| S22 | strict 适配实际发送 0 并省略短记 | schema 被接纳且工具执行；被自动改为 nullable/required 必须失败 |

不参与工具的“无增量”需要保留原始业务 self_note 的反例，不能简单断言所有不参与定义里永远没有这个名字。

### 16.2 A：新调用参数与短记

主要归属：`host-boundary` [032]、`speculative-investigation` [001/002/012]。

| 编号 | 输入/场景 | 必须断言 |
| --- | --- | --- |
| A01 | 0，短记缺失 | 合法，无短记自动补值 |
| A02 | 1、2、7，合法非空短记 | 合法，数值原样保留 |
| A03 | `2147483647`，合法短记 | 合法，证明没有业务小上限 |
| A04 | 负数、小数、越界值 | 拒绝，不取整、不钳制 |
| A05 | 数值字符串、布尔、数组、对象 | 拒绝；生产 before 和 capture 使用同一结论 |
| A06 | 缺失或 null 数值 | 参与新调用拒绝，不补 0 |
| A07 | JS 边界 NaN/Infinity | 拒绝，不强转成有效数 |
| A08 | 0 配任意出现的短记 | 拒绝，包括空串、空白和 null |
| A09 | 正数缺失短记 | 拒绝，业务 body 没有执行 |
| A10 | 正数配空串、空格、换行空白 | 拒绝，不自动生成计划 |
| A11 | 正数配数字/对象/数组/null 短记 | 拒绝，不 stringify |
| A12 | 短记含首尾空格、中文、引号和换行 | 验证后原文保留，trim 不改写证据 |
| A13 | 不以“我/I”开头的有效展望 | 不因前缀被拒绝 |
| A14 | own-property self_note=undefined | 按存在字段处理；与属性缺失区分 |
| A15 | 只有旧字段的新参与调用 | 拒绝，不作为新别名 |
| A16 | 新旧字段同时出现 | 拒绝歧义，不按优先级挑一个 |
| A17 | 不参与工具含新旧同名字段 | 本协议不解码、不剥离、不产生预算 |
| A18 | 新输入参数错误 | 只进入合适的参数错误路径，不触发全进程 fuse |
| A19 | 历史 0 加短记/正数无短记 | 原样回放，不按新规则追溯拒绝 |
| A20 | 错误反馈再次生成合法 0 无短记 | 可以正常完成，不被系统诱导改成正数 |

内容质量用例与结构校验分开。例如“继续看看”是行为评估里的弱短记，不能把它加入语言黑名单后声称系统能识别所有无用文本。

### 16.3 B：完整批次与聚合

主要归属：`speculative-investigation` [002/006/009/010]。

| 编号 | 输入/场景 | 必须断言 |
| --- | --- | --- |
| B01 | 全参与 `[0,2,5]` | N=5，而不是 7、0 或最后到达值 |
| B02 | `[7,1]` 交换调用/结果顺序 | 仍为 7，原始调用顺序正确保留 |
| B03 | 全参与全零 | 明确零估计，零 child、零额外租约 |
| B04 | 全部不参与 | 无估计机会，不读取字段，不对空集合求 max |
| B05 | read=3 + join 未完成 | 不启动，不能只等 read |
| B06 | read=3 + join 已完成 | 合法续行时一次启动，N=3 |
| B07 | read=4 + edit=0 | 当前工具各执行一次，随后 N=4，不延后 edit |
| B08 | 非参与调用含数值 99 | 不影响参与 max，业务参数原样保留 |
| B09 | 一项参与估计非法，另一项合法正数 | 整批无新执行，不取合法子集假装成功 |
| B10 | 一项参与短记关系非法 | 与 B09 一致，不能只检查整数 |
| B11 | 已有其他并行调用产生效果后，一项参数失败 | 不声称回滚，不重放整批 |
| B12 | 返回普通工具错误结果 | 与“缺结果”区分；完整性判断不丢真实错误 |
| B13 | 重复 callID、重复结果、孤儿结果 | 拒绝不合法来源，不去重拼好 |
| B14 | 跨 user/provider 边界才凑齐结果 | 不拼接成一个批次 |
| B15 | 相同 arguments、不同工具名 | 正确按各工具判定，不能以 args 相等认作同调用 |
| B16 | Host tool-part 与 wire 两条路径 | 得到相同来源、顺序、估计和原始短记 |
| B17 | 不参与的收尾工具导致终态 | 不启动；区分生命周期拒绝与数值聚合 |
| B18 | 没有工具的纯文本响应 | 没有来源机会，不生成假调用 |
| B19 | 相同真实来源重复观察 | 最多一份 Requested，不重开 child |
| B20 | 同批多个正数和多条短记 | 所有短记各随原调用保留，不挑最大值的一条 |

对小批次可以枚举原始调用顺序与结果到达顺序的组合，或使用固定种子的性质测试。不能通过随机重跑直到偶然通过来证明并发正确。

### 16.4 R：原始参数、恢复和字段所有权

主要归属：`host-boundary` [032]、`speculative-investigation` [009]、provider/trace 对应条款。

| 编号 | 输入/场景 | 必须断言 |
| --- | --- | --- |
| R01 | 参与调用进入业务 body | 业务只收到原业务参数，不含两个协议字段 |
| R02 | 同一调用进入下一次 provider 历史 | 原数值、短记和业务参数均准确恢复 |
| R03 | 不参与工具自有 self_note | 不被删、不被解释、不被 vault 覆盖 |
| R04 | js-manager 同时有 contract 和估计 | 两份合同各自正确，互不覆盖 |
| R05 | before/after 重复 | 无重复字段、重复短记、残留 Symbol 泄漏 |
| R06 | after 拿到不同对象 | 原始证据仍从正确来源恢复，不依赖对象相等 |
| R07 | after 缺席、业务异常、取消 | 不串调用、不丢已经保存的原始证据 |
| R08 | 冻结对象或不可配置属性 | 明确失败，不半删后返回成功 |
| R09 | 不参与工具的冻结对象 | 本协议不触碰，不因不必要的 hide 报错 |
| R10 | 两 session 使用相同 callID | 字段不交叉恢复 |
| R11 | 支持的 Host 若同 session 可复用 callID | 用真实 run 关联，不能串旧新调用 |
| R12 | before 接纳后配置状态变化 | 正确恢复该调用已有快照，不按新配置丢字段 |
| R13 | 无对应快照的普通调用 | 不凭字段名补出估计或短记 |
| R14 | 镜像 ID 重定位 | 仅按原规则改 ID，arguments 内容不变 |
| R15 | 短记进入 XTrace 或常规压缩 | 不另设专属窗口，不补发第二份提示 |
| R16 | vault 消失的真正冷启动 | 只从已存在 canonical 事实恢复，不伪造短记 |

### 16.5 H：执行身份、权限和请求数量

主要归属：`speculative-investigation` [003/004/008/011/013]、routing/capability 对应条款。

| 编号 | 输入/场景 | 必须断言 |
| --- | --- | --- |
| H01 | 合法 owner 新估计 N>0 | 一次 Requested/Bound，对应唯一真实 child |
| H02 | Replica 发出正数估计及合法短记 | 工具可按权限执行，但不产生嵌套 Requested/child |
| H03 | owner 看见回传 Replica 的正数字段 | 不把回传历史当新 owner 来源 |
| H04 | InternalLeaf/Blogger/repair/prefix probe | 不进入新执行准入 |
| H05 | Replica 语义结束后的晚到 callback | 仍识别其身份，不回落为普通 WorkMain |
| H06 | N=1、2、3、7 | 第 N+1 次真实请求均在外发前禁止 |
| H07 | 一次请求并行十个 read/glob/grep | 消耗一轮，不消耗十轮 |
| H08 | 提前文本结束 | 该请求计数，文本不成为 owner 研究报告，无额外总结轮 |
| H09 | 已外发后失败 | 已用次数不退回，不补一轮免费重试 |
| H10 | 重复 transform/terminal | 不重复计数或收集同批材料 |
| H11 | 截断/压缩移除了可见批次 | 已用预算不下降，来源不重复消费 |
| H12 | Replica 调用 edit/write/run/js/fork/MCP 写工具 | 真实效果为零；工具 schema 与执行 gate 同源拒绝 |
| H13 | 参与源工具是 edit/run | 不因此把这些工具开放给 Replica |
| H14 | 同 provider 容量=1 | 无父等子死锁，无双占；取消准确释放 |
| H15 | 两 owner 并发 | 不串估计、短记、callID、材料、target 或资源 |
| H16 | 无 Predictor 与暂时无容量 | 两者区别保持，后者不改变 schema 或回退模型池 |
| H17 | 结果超过旧 64 KiB | 没有新增长度降零策略；完整性校验仍严格 |
| H18 | 错 target、空输出、未发送失败 | 不能 Promotion；只是 child 完成不算消费证明 |

### 16.6 P：协议升级与持久化

主要归属：`speculative-investigation` [006/007/008/010/014/015]、durable/crash 对应条款。

| 编号 | 输入/场景 | 必须断言 |
| --- | --- | --- |
| P01 | v1 历史调用 | 原字段名、数值、短记原样保留，不批量新键替换 |
| P02 | v2 新调用只用旧字段 | 不能创建新请求，不以兼容名接纳 |
| P03 | 同来源已有 v1 Requested，新逻辑计算 v2 ID | 不形成第二份预算 |
| P04 | 同来源已有任一终态 | 不因版本变化复活 |
| P05 | v1 Requested 未 Bound 升级 | 按明确协议替换原因关闭，owner 可继续 |
| P06 | v1 Bound 未 Prepared 重启 | Closed，不重跑 |
| P07 | v1 Prepared 且 exact target 有效 | 消费原材料，不重新执行，不改 digest |
| P08 | v1 Prepared 且 target 失效 | Abandoned，不给新 target 冒用 |
| P09 | v1 Promoted 未 Traced | 正确因果位置回放与 trace |
| P10 | Requested/Bound/Prepared 写入点前后崩溃 | 分别符合既有状态恢复规则 |
| P11 | 持久化追加 unknown | 先解析事实，无法证明时不外发 |
| P12 | 混合旧调用、新调用、旧事件、新事件 cold replay | 业务 Current 与 live fold 一致，不只比较事件数量 |
| P13 | 配置撤销 | 无新执行，既有 Prepared/Promoted 的恢复不依赖新配置 |
| P14 | 来源 schema 版本证据不足 | 不把历史当新调用，也不凭新键存在就获准 |
| P15 | 古老 K1/K2 迁移 fixture | 原职责与行为不回归，本次不伪造新版 Requested |
| P16 | 用新数据测试旧二进制回退 | 记录真实兼容结果，未证明时不得宣称可直接回退 |

### 16.7 现有测试具体怎样改

`requirements/host-boundary/tests/032.test.mjs`：把独立数字/短记 helper 测试扩展为完整成对合同；新增未参与字段所有权；保留原评审合同、异常、对象身份和并发证明。旧“0 加短记合法”的 fixture 只能留在历史测试，不能仍充当当前调用。

`requirements/speculative-investigation/tests/002.test.mjs`：构造完整来源调用身份，加入参与子集和非参与混合批次；不要只在测试里取 `Math.max` 来替生产 capture 证明。

`003/004/011.test.mjs`：去掉“Replica 正数是再次委托”前提；保留请求计数、权限、提前结束、晚到回调和取消。

`009.test.mjs`：保留原始参数单路映射；新增正数多短记以及旧历史的原样保留。随机普通字符串可以用作字节保真 fixture，但应明确不是在验证展望质量。

`012.test.mjs`：重写信任和同伴文字断言，验证新属性、两种语言、条件关系和增量文本无调度动机。不能只把测试名改为 outlook，正文仍证明 trust。

`013.test.mjs`：不再 `for every tool → must have budget`。独立维护预期清单，既证明该加的确实加，也证明不该加的原样；真实调用使用合法的新条件组合。

`014/015.test.mjs` 及相应历史测试：分别验证配置唯一性、旧事件保留和跨版本来源防重。不要让“历史可读”测试调用新输入解析器后再补缺失字段。

`requirements/host-boundary/tests/support/run-readonly-delegation-schema-canary.mjs`、`run-manager-review-tools-canary.mjs`、`manager-review-tools-canary-plugin.mjs`：同步输入字段、正数短记、观测字段和对工具集合的独立期待；文件名是否改动取决于职责，不为好看强改所有 import。

`requirements/verification-system/tests/e2e/scenarios/long-stroke.toml` 与 `support/long-stroke-oracles.mjs`：只改新调用场景为新字段；旧历史场景保留旧字段并标用途。oracle 必须按真实来源和工具判定取值，不能只看到任意历史里有正数就统计为 owner 新机会。

---

## 17. 真实 Host canary：按下面顺序取得证据

### 17.1 先验证运输，再评估模型

确定性的脚本 provider 可以证明真实 Host 如何发送 schema、执行工具、保存/恢复参数和继续请求；它不能证明真实 LLM 愿意或能够给出合理估计。两类证据要分开命名、分开报告。

使用临时工作目录、独立测试会话和受控文件。不要把生产仓库机密、真实用户长历史或未授权配置发送给外部服务。

### 17.2 最小但完整的 canary 场景

场景一：无 Predictor。观察最终 provider tools，与进入本机制前的定义比较；调用一个原工具，证明不需要任何新字段。

场景二：已配置 Predictor，参与的内建工具。发送新估计 0 且不含短记，真实执行并进入下一次请求；确认业务 decoder 接受，历史没有自动补空串或 null。

场景三：正数与展望。发送 read/grep 的正数估计和明确展望，观察整批完成、Requested/Bound、真实 Predictor 用途和返回工具交换。需要观察到实际外发计数，不只看插件自己打印的 N。

场景四：不参与工具。用真实 fork/join/commission/horizon 等可合法运行的测试任务，观察定义和调用。不能只在 mock 名为 fork 的空函数上证明真实协作生命周期。

场景五：Manager。在其合法评审调查阶段调用 js-manager，验证 review contract 和新字段共存；再通过真实状态转移进入收尾，观察余下工具无本协议。

场景六：Blogger。启动真实 Blogger 测试 life，记录最终 chronicle 定义，断言没有本协议增量；原 RulebookRevision、tip 和生命周期行为仍通过原断言。

场景七：混合批次。参与工具先完成，不参与工具后完成；前者完成时不能启动，后者完成且普通续行成立时最多启动一次。另测最后一个工具使 owner 终结，不启动。

场景八：未知动态工具与同名业务字段。工具发现后验证它默认不参与，自己的 self_note/数值字段在业务和后续历史保持原样；取得明确判定后再以受控定义证明选择性装饰。

场景九：Replica 正数。脚本让只读执行者在合法 read 上输出正数和短记；它的工具正常执行，但整个观察窗口内没有第二层 child 或新的来源 Requested。

场景十：异常与重启。工具抛错、after 不同对象、取消、两 owner 交错，随后独立进程恢复持久化材料；证明不是进程内 vault 让测试假通过。

场景十一：同 provider 容量为 1、长工具结果和普通压缩。证明新触发频率没有掩盖原资源、截断和复用边界。

不要求一个超长 canary 把所有场景串在一个不可诊断的测试里。按真实边界分成数个可定位失败的测试，使用同一正式宿主入口。

### 17.3 每次至少留下的证据

记录实际 Host 二进制/版本、测试配置是否有 Predictor、角色与真实请求用途、最终工具清单、选定工具的完整 schema、原始新调用、业务参数视图、后续 provider 历史、请求数量和 canonical 生命周期事件。

输出摘要可脱敏，但断言必须针对真实观察。不要只有 `console.log('passed')`，也不要把生产函数返回的“已恢复”布尔值当作原始字段真的恢复。

记录每项是通过、失败、跳过还是未执行。未安装 Host、integration tier 未开启、provider 不可用，都不等于该项通过。

---

## 18. 模型行为验证：不能把参数填满当成成功

### 18.1 先分清观察与解释

用户观察到当前 LLM 基本填 0，这是本次修订的直接动因。模型把 delegate 联想到 subagent，是有待实验支持的解释，不是已被本次源码阅读证明的心理事实。

新名字和说明的工程正确性可以由测试验证，是否改善估计和任务过程需要真实模型行为样本。报告时不要把两者混成一个“全部验收通过”。

### 18.2 测试任务应覆盖的场景

| 场景 | 要观察的行为 |
| --- | --- |
| 信息已经足够的小修改 | 能合理填 0，不为了迎合功能虚构调查 |
| 必须沿多个调用关系查证 | 能给正数及具体后续展望，不把普通选文件都算复杂判断 |
| 当前批次含多项并行读写 | 估计针对整批之后，不重复计算当前调用 |
| 必须先运行测试或命令 | 停在命令边界，不把后续调查跨界算入 |
| 纯分析且无需 edit | 能估计到结论/关键判断，不因永不 edit 而失去定义 |
| Manager 评审前与收尾后 | 调查阶段填写有用途，收尾工具没有多余参数 |
| Orchestrator/Blogger | 不出现无意义的填写负担 |
| 某条查询提前排除假设 | 允许早停，不为兑现数字继续读 |
| 信息不足且需要用户决定 | 停在确认边界，不凭短记替用户做决定 |

任务材料固定，模型/配置尽量相同，记录运行顺序和随机性影响。比较旧版与新版时使用独立测试运行或测试 checkout，不加生产 A/B 开关，也不在活跃任务中混合两版 schema。

确有必要区分名称和其他变化的贡献时，可在独立 harness 中比较“旧合同”“只改名字”“完整新合同”。这是研究设计选项，不是上线的第三套生产兼容逻辑。

### 18.3 观察指标

记录参与调用数量、0/正数分布、条件短记的合法率、短记是否指出具体证据和停点、实际连续只读调查长度、提前结束原因、边界误判、无用读取、重复工具效果、任务正确性、总耗时与实际资源消耗。

预算会截断观察窗口，所以实际读了 N 轮不证明“准确预测了恰好 N 轮”。标明窗口是否被预算截断；需要估计自然调查长度时，采用独立对照运行，不从截断数据计算漂亮的精确误差。

短记质量应给可审阅的样例，不只给一个主观总分。合法率和语义有用性分开：一段非空废话可以通过结构校验，但不算好的展望。

正数增加但无用读取、任务失败或确认边界越界增加，不能判为成功。大多为 0 也不必然失败，先看任务是否真的还需要只读查证。

### 18.4 结果会改变什么

若仍持续在明确调查场景填 0，先看最终 wire 是否确实送达新定义、旧增量话术是否残留、普通调查是否被误定义成关键判断。不能直接加“必须大于 0”。

若数字普遍偏大且调查无效，检查连续前缀和停止条件的说明及真实早停行为，不重建隐藏成本模型。

若短记充满泛话，调整示例和说明；不靠长度增加制造质量假象，也不要求完整思考过程。

若某个工具的估计几乎没有用途，拿具体调用轨迹重开那一行工具判定，不把一个工具的问题推广成所有工具统一启用或关闭。

本次文档交付没有这些实验结果。工程实现交付时可写“合同与链路已验证，真实模型效果尚未测量”，不能由文案测试推导“模型已经更愿意调查”。

---

## 19. 验证命令：使用正式 runner，不绕过 freshness

以下命令供未来源码实施后使用，不是本次已经执行的命令。均在仓库根目录运行，不硬编码个人机器路径。

### 19.1 基线与构建

```bash
git status --short
git rev-parse --short HEAD
git diff --check

# 本仓是 Fable 编译目标。
node scripts/build.mjs
```

禁止 `dotnet build`、手改 dist、跳过 build freshness 或复制旧 dist 使测试假绿。代码未改、只写本指南时，不需要为了制造工作量运行源码全套门禁。

### 19.2 首批定向回归

当前正式 runner 的 `TESTS_MJS_FILES` 是逗号分隔的显式文件列表，不是 glob，也不是空格分隔列表。

```bash
TESTS_MJS_FILES='requirements/host-boundary/tests/032.test.mjs,requirements/speculative-investigation/tests/002.test.mjs,requirements/speculative-investigation/tests/012.test.mjs,requirements/speculative-investigation/tests/013.test.mjs' \
  node requirements/verification-system/tests/run.mjs
```

共享合同、执行与恢复接通后，扩到主包对应条款：

```bash
TESTS_MJS_FILES='requirements/speculative-investigation/tests/001.test.mjs,requirements/speculative-investigation/tests/002.test.mjs,requirements/speculative-investigation/tests/003.test.mjs,requirements/speculative-investigation/tests/004.test.mjs,requirements/speculative-investigation/tests/005.test.mjs,requirements/speculative-investigation/tests/006.test.mjs,requirements/speculative-investigation/tests/007.test.mjs,requirements/speculative-investigation/tests/008.test.mjs,requirements/speculative-investigation/tests/009.test.mjs,requirements/speculative-investigation/tests/010.test.mjs,requirements/speculative-investigation/tests/011.test.mjs,requirements/speculative-investigation/tests/012.test.mjs,requirements/speculative-investigation/tests/013.test.mjs,requirements/speculative-investigation/tests/014.test.mjs,requirements/speculative-investigation/tests/015.test.mjs' \
  node requirements/verification-system/tests/run.mjs
```

执行前核对文件仍存在、条款归属仍正确。不能为了保留上述命令而造空测试文件，也不能漏掉本次新增的正式测试。

### 19.3 真实 Host tier

```bash
WXS_TIER_INTEGRATION=1 \
TESTS_MJS_FILES='requirements/host-boundary/tests/032.test.mjs,requirements/speculative-investigation/tests/013.test.mjs' \
  node requirements/verification-system/tests/run.mjs
```

这些环境变量是既有测试入口开关，不是新功能的生产开关。`integrationTest` 在 tier 未开启时会 skip；还可能因缺少实际 Host 二进制而 skip，必须阅读结果。

对 capacity、Blogger、语言、权限、trace、prefix、恢复等直接受影响测试，用相同正式 runner 显式选择实际文件。本文不凭空为尚未核对的包指定条款编号。

### 19.4 总门禁与发布

```bash
npm run format-build-test
git diff --check
git status --short

# 仅在准备发布且环境满足真实 Host/E2E 要求时执行。
npm run verify:release
```

`verify.mjs` 的总门禁不是用 `TESTS_MJS_FILES` 偷偷缩小范围的入口；定向回归直接走正式测试 runner。长程 E2E 场景通过仓库已有发布入口运行，不臆造一个未存在的 `npm test:delegate`。

记录每个命令、退出码、实际执行数量、跳过数量和失败标题。已有失败明确说明，不用“最后一次是绿的”掩盖前面的偶发失败。

---

## 20. 清理搜索与人工审阅

搜索用于找遗漏，不代替行为测试，也不授权脚本批量改写代码。

```bash
# 旧模型可见字段与协作私有符号。
rg -n 'delegate_readonly_rounds|englishCollaboration|chineseCollaboration|readonlyRoundsDescription' \
  src/Wanxiangshu requirements resources

# 新字段必须在统一合同、必要适配和测试中出现，不能各写一份宽松解释。
rg -n 'estimated_readonly_rounds|self_note' \
  src/Wanxiangshu/OpenCode src/Wanxiangshu/Strength requirements

# 查旧叙事；逐项看归属，不能全局替换系统其他模块的合法文本。
rg -n 'retain control|build trust|every tool call|further delegation|建立信任|每个工具调用都要|固定填写 0' \
  src/Wanxiangshu requirements resources

# 检查所有清理和来源消费入口，而不只看 schema。
rg -n 'snapshotOfArguments|sanitizeSnapshot|ReadonlyDelegationContract\.hide|budgetOfCall|batchBudgetOfCalls|contractRevision' \
  src/Wanxiangshu
```

人工审阅每个命中，将其分成当前新协议、合法历史读取、历史 fixture、负面测试、旧文档说明、无关功能六类。新调用热路径不能继续解析旧别名；其他类别不能为追求搜索清零而误删。

重点检查以下成对关系：schema 参与与 before 参与是否一致；before 校验与 capture 校验是否一致；原始证据与业务参数是否分离；完整批次等待与参与子集 max 是否分离；输入估计与执行权限是否分离；新合同版本与同来源防重是否同时改动。

最后看实际 diff，不只看测试输出。确认没有把其他工作区变更一起提交，没有把用户 MJS 配置、历史目录、生成物、真实 canary 凭证或临时日志放进提交。

---

## 21. 常见错误：出现一项就不能称为完成

| 看似完成的做法 | 实际遗漏 |
| --- | --- |
| 全仓把 delegate 字符串改成 estimate | 没有改变意愿叙事、逐工具选择和短记关系 |
| schema 去掉 fork/join 参数 | before 仍全局剥离，capture 仍要求每个调用填值 |
| 所有只读工具都加，所有写工具都不加 | 混淆当前动作与未来调查；read-terminal 与 edit 的边界被判断反了 |
| Manager 全部不加 | 丢掉真实 js-manager 评审调查入口 |
| Blogger 不会创建 child，所以已经满足要求 | chronicle schema 仍可能带两个无意义字段 |
| 0 时自动把短记删掉 | 隐瞒非法输入，改写原始证据，没有落实模型填写规则 |
| 正数时自动补“继续调查” | 人工伪造模型展望，还会让行为评估失真 |
| self_note 加入全局 required、允许 null | 违背零值省略，也把 provider 限制藏成产品语义 |
| 副本继续固定填 0 | 用不真实的估计维持防递归表象 |
| 不参与工具有同名字段也顺手截取 | 侵占其他工具的业务字段 |
| 参与工具完成就启动 | 剩余不参与调用可能还没完成，或即将结束会话 |
| 只比较 arguments 判断同一来源 | 不同工具、调用和 provider 响应可能有相同参数 |
| max 空集合默认塞 0 | 混淆无估计机会与明确零值，掩盖遗漏 |
| 任何历史正数都可重新利用 | 回传的 Replica 或旧协议历史会变成新执行来源 |
| 只按新 DecisionId 查重 | 版本变化可让同一真实来源再执行一次 |
| 旧字段历史全部改名 | 篡改事实，还可能破坏 digest 与已证明的消费位置 |
| 只有 mock schema 测试 | 不知道真实 provider 是否接受省略，不知道业务 decoder 是否看到多余字段 |
| canary 全部工具都遍历了一遍 | 没有独立预期清单，可能把同一个错误谓词当 expected |
| 真实 Host 用脚本 provider 成功 | 只能证明运输和控制流，不能证明 LLM 行为改善 |
| 正数比例提高就宣布成功 | 可能只是多做无用调查，甚至越过确认边界 |
| 跑 node:test 时全部绿 | 可能使用旧 dist、绕过正式 freshness 或跳过 integration |

---

## 22. 最终交付检查表与报告模板

### 22.1 实现完成的检查表

- [ ] 每个实际可见工具都有明确判定；未判定动态工具被列出，未自动加入。
- [ ] fork、join、协调收尾工具和 Blogger chronicle 无本机制增量；js-manager 的合法调查入口仍可用。
- [ ] 新调用只使用 `estimated_readonly_rounds`，没有当前输入旧别名。
- [ ] 模型可见新增文字只描述事实性估计与后续查证，不解释执行分工动机。
- [ ] 0 必须省略短记；正数必须有非空白展望；两个条件由生产边界执行。
- [ ] 条件关系按单个调用判断，不被批次 max 反向覆盖。
- [ ] 未配置、未参与、未判定路径不误改同名业务字段。
- [ ] 原始字段与业务参数分离；异常、重复、并发与对象差异经过真实 Host 验证。
- [ ] 完整来源保留工具名、ID、arguments、顺序与真实 provider 身份。
- [ ] 等待完整批次，聚合参与子集；空机会、零值、正值和非法输入区别明确。
- [ ] 当前批次各工具只执行一次，非法元数据不引起整批重放。
- [ ] Replica 正数不产生递归；回传历史和晚到回调不冒充 owner 新来源。
- [ ] 请求计数、N+1、提前结束、只读权限、容量和取消不回归。
- [ ] 新版本按真实来源跨版本防重；旧 pending 有明确处置。
- [ ] 旧调用和历史材料原样保留，Prepared/Promoted 正确恢复，不假造新授权。
- [ ] 规范、源码、Surface、owner shard、canary、E2E oracle 与例子同步。
- [ ] 正式构建、定向测试、真实 Host、冷启动和适用总门禁都有真实结果。
- [ ] 行为效果单独报告；未测不冒称已改善，不把正数比例当唯一标准。
- [ ] 只提交本任务范围，没有旧路径残骸、临时脚本、生成物、机密或无关改动。

### 22.2 未来实现交付报告

```text
代码基线与最终提交：
本次实际修改的合同与文件：
工具清点：已参与 / 已排除 / 未判定，附新增或变化的具体工具。
模型可见文字：实际 wire 版本与语言样例。
条件短记：合法与非法案例的生产边界结果。
批次与身份：混合批次、Replica 正数、跨版本同来源结果。
历史恢复：旧 Requested/Bound/Prepared/Promoted 的实际处理。
构建与测试：命令、退出码、执行/跳过数量、失败标题。
真实 Host：二进制版本、配置、场景与证据位置。
行为实验：已执行的任务与观察；没有执行则明确未测。
已知阻塞与不支持组合：
升级/回退限制：
未改动的用户文件与数据：
```

不要求报告重复本指南全部内容。必须让接手者知道哪些是代码事实、哪些经过测试、哪些仍只是预期。

### 22.3 本文件交付与实现交付不是一回事

本文件可以在文档结构、路径引用和 diff 检查后作为方案交付；这不表示上面的实现检查项已经完成。

将来施工的最小起点是 P0 工具清点与 P1 失败测试，然后落地一份共享合同。不要先全仓重命名，再回头补语义。

完成后的机制应当能用一句话说清楚：模型在合适的工具上估计接下来还有多少轮只读查证，只有正数时留下具体展望；宿主按完整来源和既有权限把估计用于一次有上限的只读执行，无意义的工具不承受这套参数，历史也不因此被改写。
