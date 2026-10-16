# implicit-control-flow — Lesson

## 评价

### 定义

控制流隐式化，不是因为“用了 callback/hook”。真正的问题是：正确性依赖某个 happens-before，而源码没有一个清楚的 owner 把这条因果关系表达出来。

注册顺序、import side effect、framework lifecycle、global startup phase、observer priority 都可能承载时序。但如果读者必须靠框架 folklore 才知道“为什么 B 一定在 A 之后”，系统其实把 protocol 藏进 ambient environment 里了。

### 支配原则

依赖顺序才成立的正确性，应当把顺序变成一等公民。隐藏的排序把因果变成环境知识：读者看得见组件，却看不见让它们正确的时序关系。于是系统表现得像一个没有显式协议的分布式协议——事件局部合法、全局却错误，因为没有谁拥有 happens-before 关系。

### 何时触发

- hook 必须按某个 registration order 才正确，却没有显式 ordering contract；
- import 某 module 会偷偷注册行为，漏 import 就静默少一步；
- startup/dispose 次序靠“目前文件加载顺序刚好如此”；
- callback chain 中一个 side effect 必须在另一个前完成，但 relation 无类型、无状态、无显式 composition；
- framework phase 改变后，业务正确性跟着悄悄变化。

### 不应触发

- framework lifecycle 本身就是稳定、文档化且启动时机械校验的 contract；
- 普通高阶函数的 caller 明确传入 continuation，顺序一眼可见；
- event-driven system 不必强行改成 imperative，只要真正重要的 causal edge 被明确建模；
- 没有 correctness dependency 的 observer 顺序无需人为固定。

### 与相邻规则区分

`implicit-convention-magic` 主要隐藏“谁参与”；这里隐藏“谁先谁后”。

`callback-pyramid` 是 sequencing 被 lexical nesting 淹没；这里即使代码很平，也可能靠 ambient lifecycle 隐藏真正时序。

### 判定程序

问：**如果把两个 callback/hook 的执行顺序交换，业务结果会不会变？**

会变，就必须回答谁拥有这条 ordering law，以及源码在哪里让它成为显式事实。

### 例子

- 正例：插件 A 必须先把 request normalize，插件 B 才授权，但两个 hook 都注册在一个数组里，顺序只来自 import order。
- 近邻：Host 明确定义 transform phase → authorize phase，并拒绝非法 phase registration。
- 反例：两个 metrics observer 顺序无业务含义，谁先上报都一样。

## 建议

### 现在该做什么

把 correctness-critical ordering 从 ambient convention 搬到显式 workflow、phase type、ordered composition 或 causal event contract 中。让一个 owner 能回答：A 为什么必须先于 B、违反时会怎样、谁验证这条关系。

### 为什么重要

隐式时序最容易制造“每个局部都对、整体却错”的系统。A 单看合法，B 单看也合法；只有 A/B 顺序是隐藏前提。这样的 bug 往往在升级 framework、并行化 startup、调整 import、增加第三个 hook 时出现，而且代码 diff 看不出业务协议被改了。

### 修复策略

- 若是单一 workflow，直接把 sequence 写在一个结构化流程里；
- 若是 extensible hooks，用 phase/priority contract 表达真正必要的 ordering，并机械拒绝非法组合；
- 若是 event protocol，用 durable/typed causal fact 表达 prerequisites；
- 若顺序其实无意义，删除不必要的 order assumption，让 handlers 可交换。

### 决策分支

- 若顺序是真实的领域协议，把它编码成由单一 sequencer 拥有的命名 phases 或直接调用；
- 若 framework 已经机械地守护顺序，保留那份 contract，不要再加第二套隐藏顺序。

### 常见假修复

- 在 README 写“不要调整这两个 registration 的顺序”。
- 用数字 priority `10/20/30`，但没有语义 phase 与 invariant。
- 把隐式 hook 链包进一个叫 `Pipeline` 的对象，却仍靠 insertion order。
- 加 sleep 等“等前一个大概做完”。
- 把所有 handler 强行串行，掩盖真正只需要一两条 causal edge 的事实。

### 验证

对关键 participant 做 reorder/omit/add 的测试：

- 真正必需的顺序若被破坏，应在 construction/startup/typed transition 处明确失败；
- 无关 participant 的顺序变化不应改变业务结果；
- 新增 hook 不该靠知道历史 insertion folklore 才能安全接入。

### 完成条件

重要的 happens-before 关系有名字、有 owner、有机械表达；读者不需要了解 import 顺序或 framework 内幕才能解释系统为什么按这个顺序运行。

### Nudge

因果关系如果重要，就应该成为程序结构；不能只存在于“大家都知道这个 hook 会先跑”的组织记忆里。
