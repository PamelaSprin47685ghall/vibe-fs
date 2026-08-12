# spike-not-cleaned — Main 中文版

## 现在该做什么
把 spike 当研究笔记而不是实现基线：提取它证明的事实，列出 temporary assumptions，然后围绕真实 boundary、ownership、failure、recovery、security、lifetime 重建最小 production design。知识迁移完后删 prototype。

## 为什么这很重要
Prototype 优化的是学习速度，因此“先 hardcode”“先全局变量”“先不处理 crash”在那个阶段完全合理。问题出在这些选择没有经过 production 标准重新决策，却因为代码可运行而被误认成设计结论。

## 常见假修复
- 只 rename `spike` 文件并搬进 `src`。
- 给 prototype 加更多 tests，却不重审 model/boundary。
- 一边保留 spike path，一边另写 production path，形成 dual architecture。
- 认为“rewrite 浪费”所以所有 exploratory choices 都必须保留。

## 验证
每个曾经的 spike assumption 都应有一个结局：被真实 contract 证明、被 production design 删除，或被明确建模成限制。不能再有“当时为了快所以这样”的 silent dependency。

## 完成条件
production 保留 spike 学到的知识，不保留 spike 为了学习而欠下的假设与捷径。
