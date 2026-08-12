# implicit-control-flow — Main 中文版

## 现在该做什么
把 correctness-critical ordering 从 ambient convention 搬到显式 workflow、phase type、ordered composition 或 causal event contract 中。让一个 owner 能回答：A 为什么必须先于 B、违反时会怎样、谁验证这条关系。

## 为什么这很重要
隐式时序最容易制造“每个局部都对、整体却错”的系统。A 单看合法，B 单看也合法；只有 A/B 顺序是隐藏前提。这样的 bug 往往在升级 framework、并行化 startup、调整 import、增加第三个 hook 时出现，而且代码 diff 看不出业务协议被改了。

## 修复策略
- 若是单一 workflow，直接把 sequence 写在一个结构化流程里；
- 若是 extensible hooks，用 phase/priority contract 表达真正必要的 ordering，并机械拒绝非法组合；
- 若是 event protocol，用 durable/typed causal fact 表达 prerequisites；
- 若顺序其实无意义，删除不必要的 order assumption，让 handlers 可交换。

## 常见假修复
- 在 README 写“不要调整这两个 registration 的顺序”。
- 用数字 priority `10/20/30`，但没有语义 phase 与 invariant。
- 把隐式 hook 链包进一个叫 `Pipeline` 的对象，却仍靠 insertion order。
- 加 sleep 等“等前一个大概做完”。
- 把所有 handler 强行串行，掩盖真正只需要一两条 causal edge 的事实。

## 验证
对关键 participant 做 reorder/omit/add 的测试：

- 真正必需的顺序若被破坏，应在 construction/startup/typed transition 处明确失败；
- 无关 participant 的顺序变化不应改变业务结果；
- 新增 hook 不该靠知道历史 insertion folklore 才能安全接入。

## 完成条件
重要的 happens-before 关系有名字、有 owner、有机械表达；读者不需要了解 import 顺序或 framework 内幕才能解释系统为什么按这个顺序运行。
