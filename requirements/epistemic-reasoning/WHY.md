# epistemic-reasoning — WHY

## 不可替代的存在理由

复杂探究的原始对话含大量重复措辞；把 transcript 当状态，会把重述误算成新知识。Sphinx 因此维护事件历史关于未来探究决策的充分统计量，并把每次实验条件、观测、证书精化与资源消耗保存为可回放事实。

Sphinx 的识别目标必须诚实。若两个外部世界在所有允许协议下诱导相同回答分布，仅观察 LLM 回答的系统无法区分它们。无外部可信源时，Sphinx 识别的是模型初始判断、协议下反思判断、稳定模式、自我预测校准及问法效应；只有额外来源支持的命题才可标为 externally-grounded claim。

## Sphinx-GEC 边界

Sphinx Generalized Epistemic Calculus 是带类型超边、证书偏序和可替换消元算子的探究运行时。Core 只拥有不含认识论立场的机制：

- opaque ID、canonical envelope、因果父边与确定性 reducer；
- 图、超边、work dependency、显式 lease/retry/cancel 事实；
- 资源守恒、plugin lock、schema content hash；
- 证书槽、精化关系检查与可回放导出。

认识论全部由插件拥有：

- Finding/Evidence/Hypothesis 本体与方法库；
- 值域、价值偏序、消元、信念更新与候选排序；
- Bayes、A*、MCTS、Borda、BTL 与 truthful elicitation；
- probe、reflection、stop certificate、answer renderer；
- 对“独立”“充分”“真实判断”的操作性定义。

Bellman 是决策节点上的固定点表现，不是与 Bayes、A*、MCTS 并列的互斥模式。Bayes 是有限条件独立因子的 exact sum-product 精化；A* 是非负确定图上由 open-frontier 全局下界与 incumbent 上界构成的 min-plus 精化；MCTS 是生成式随机核上的概率性 sample-expectimax 精化。三者可同时更新同一节点的不同证书槽。

## 核心不变量

1. 状态来自 durable event fold，不来自进程内 registry、transcript 或 snapshot。snapshot/Current 仅为可丢弃投影。
2. EventStore 是唯一 durable substrate。事件必须先落盘，再替换进程内投影；同一 `workId + attempt` 重放幂等。
3. Core 不解释插件 payload，不写入方法名、证据本体、排序权重、停止阈值或答案模板。
4. 精确／有界精化必须收缩其声明的可行集合；采样精化只可声明带显式误差预算的高概率覆盖，不伪装成确定性集合包含。
5. 独立且无冲突的 delta 可交换；归一化、全局 incumbent、后验与 framing 更新按事件序列组合，不假设可加。
6. scheduler 只执行依赖、冲突、隔离、并发与预算约束。跨插件收益没有共同决策损失尺度时保留 Pareto frontier，不强造单一总分。
7. 探究协议记录共同根快照、处理分配、排列、措辞、锚题、模型参数和 seed。provider 输出不是由 seed 保证的确定值；重放复用已接受观测。
8. MCP 与 OpenCode Host 只翻译同一 Core work/event contract。相同 canonical 事件序列必须得到相同语义状态与 hash。
9. work 推进、取消和恢复只由 durable fact 与 typed physical observation驱动；wall clock、lease expiry、sleep、polling 不得裁决业务状态。
10. 最终输出区分 model-belief、reflective-model-belief、cross-branch consensus、protocol-stable judgment 与 externally-grounded claim，不把稳定相信误报成外部真理。

## 破坏形态

- Core 再次导入 Legacy、Bayes、Borda、stop 或 renderer；
- `SolverMode` 让 exact/bound/sample 互斥；
- 把模型自述置信、搜索 visit 或重复措辞升级为世界证据；
- 同源观测虚假相乘，或把 MCTS 区间宣称为必然包含真值；
- 以 `g(n)+h(n)` 充当 A* 全局最优值下界，而忽略 open frontier；
- 未锚定 BTL 位置参数、完全分离或设计矩阵秩亏；
- 用非负距离的样本均值冒充无偏 ATE；
- 让 sibling 回答、当前排名或聚合倾向泄漏到盲化 branch；
- 用 SQLite、私有日志、第二 projection formula 或 process-local handle 证明 durable success；
- 用 timeout 推断 lease 失效或 inquiry 完成；
- 两个 Host 各自实现图、证书、调度或停止逻辑。

## DEPENDS ON

`participant-horizon`

`durable-events`

`delegation`

`execution-model-routing`
