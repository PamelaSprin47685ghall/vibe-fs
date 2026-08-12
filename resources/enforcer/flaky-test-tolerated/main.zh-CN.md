# flaky-test-tolerated — Main

## 现在该做什么
在“一次 experiment 对应一个可解释 verdict”恢复之前，不要继续把这条 test 当作普通、可信的 evidence。

保存 failure reproduction，找出 hidden input，然后控制它；如果做不到，就把这条 test 从 evidence chain 中移除。不要一边给它加 retry、quarantine label、团队口头说明，一边仍让它拥有“证明正确”的 authority。

## 为什么重要
Flakiness 消耗的远不只是 CI minutes。

它会腐蚀 failure 的语义。一旦工程师学会“red 可能没事”，每个真实 regression 都获得 plausible deniability。默认流程会倒转成“先 rerun，后调查”，而第一条无法解释的 red 往往恰恰是你能得到的最便宜证据——race、resource leak、stale state、missing causal signal 通常就在里面。

Flaky suite 还制造一种社会偏差：green 不需要辩护，red 必须先证明自己不是噪声。这个 selection bias 会系统性低估 defect。

## 修复策略
让 hidden inputs 变成显式、有 owner 的输入：

- 注入/freeze time，不要和 wall clock 赛跑；
- 记录 random seed，并让 replay 精确；
- 每条 test 独立拥有 filesystem/database/global state；
- 删除 order coupling 与 shared mutable fixture；
- 等待 causal signal，而不是 sleep；
- 有意控制 concurrency，并在可能时用 deterministic coordination 暴露 race；
- 当 live external dependency 不是 test 本体时，用 deterministic boundary contract 替代；
- 当 external system 本身就是 test subject 时，明确建模 transient/failure policy，不要假装环境 deterministic。

保留最初的 failure 价值。不要通过“让 observation 更看不见 defect”来修 test。

## 决策分支
- **Hidden input 已找到且可控：**把它显式化，保留 test。
- **依赖 shared residue：**给 test 独立 ownership 与 cleanup；必要时用 shuffled/parallel execution 证明 independence。
- **产品 contract 天生 probabilistic：**明确 statistical acceptance criterion、seed/sample policy。不要 ad-hoc rerun。
- **无法做到足够 deterministic 以支撑 claim：**替换或删除。Missing test 比 fake evidence 诚实。
- **修复期间必须 quarantine：**给 owner、具体 defect link、bounded exit criterion；在此期间不要把它算作 trusted coverage。

## 常见假修复
- 把 retry count 调高到 failure rate 在社交上可接受。
- 加 sleep / 扩 timeout，让 favorable schedule 更容易出现。
- 因为一条 test 漏 shared state，就把整个 suite 强制 serial；除非 serialization 真的是被测试的 product invariant。
- CI 中 catch/ignore flaky assertion，local 继续装作它有价值。
- 因为删除会“降低 coverage”而永久保留 quarantine。坏 witness 不会因为一直站在那里就变好。
- 连跑 100 次都 green，就宣称 causality repaired，却说不出到底修了哪个 hidden input。稳定性 sampling 可以在 repair 后增加 confidence，但它不是 repair。

## 验证
证明必须是 causal，而不是统计幻觉：

1. 指出使 verdict 分叉的 input / mechanism；
2. 控制或删除它；
3. 让 test 的 experiment definition 变得明确；
4. 证明一个相关 defect 仍能把它打红。

修复后的 repeated runs 可以作为 stress check，但再多 lucky green 也不能替代 1–4。

Invariant：

> 等价的 relevant inputs 产生一个 verdict，并且这个 verdict 只有一个清晰含义。

## 完成条件
Red 再次变得可行动。

没人需要“probably flaky”、rerun 按钮或运气来决定这条 test 是否在说真话。
