# callback-pyramid — Enforcer 中文版

## 定义
Callback pyramid 的问题不是缩进难看，而是**时间被表示成 lexical topology**：sequence、failure、cancellation、resource lifetime 分散在多层 closure 中，没有一个 scope 能完整说明 operation 的生命期。

当 reader 必须沿着 callback 一层层下钻、再反向寻找 error/cleanup owner，control flow 已经失去结构化因果。

## 何时触发
- open → read → parse → write 多层 callback 嵌套；
- cleanup 分散在不同 inner callbacks；
- error 到底 propagate 到哪里需要追 closure；
- cancellation token 在某层丢失；
- parallel branches 与 sequential branches 混在 nesting 中，join point 不清楚。

## 不要误判
- foreign callback API 在 adapter edge 一层包住，很正常；
- event registration 本身不是 operation sequence；
- 浅层 continuation 若 lifetime 一目了然，不必为了风格改写；
- structured async 也可能写得很差，但至少问题不再由 callback nesting 本身造成。

## 刀口
尝试用一段 top-to-bottom 的 causal sentences 复述 operation：获取什么、等什么、何时释放、取消从哪到哪。若代码结构无法直接映射这些句子，而必须跳 closure，pyramid 已经在承载 protocol。

## 与近邻区分
`implicit-control-flow` 是时序藏在 framework/registration；这里时序倒是“在代码里”，但被 lexical nesting 撕碎。

`resource-not-scoped` 关注具体 resource lifetime；callback pyramid 往往让 lifetime ownership 变模糊，但两者不是同义。

## 例子
- 正例：四层 Node-style callbacks，每层都有自己的 `if (err)` 和 cleanup。
- 近邻：callback API 在边界立刻转成 Task/Promise，内部用一个 async scope。
- 反例：把 callbacks 抽成五个命名函数，但调用/cleanup 关系仍靠 continuation passing——名字多了，结构没回来。

## 提醒
目标不是“少缩进”，而是让 causality、cleanup、failure 与 cancellation 回到一个可读 lifetime 中。
