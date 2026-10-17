# work-record 测试

WHAT 定义记录归属与材料范围；本文说明当前证明，不能新增行为义务。

合同适配现状：cognitive-workspace 已退休持久画板，context-compression-017/028 规定真实 Opening 永久保留、成功 native todowrite 以固定 K=3 窗口选择普通历史 cutoff；本包 [009]/[013] 仍描述旧 BlindPlan T1 的原始 Opening 区间。不能把旧 T1 机械替换为首次 checkpoint，也不能借压缩许可删除 Opening。009 保留显式构造材料的局部渲染证明与生命周期 TODO。

- 001 的渲染对照只证明同一输入的视图差别；新增真实 journal 提交后 payload 篡改，完整和 bounded materializer 都不得返回缺损记录。
- 002/004/007/011/016 使用实际 journal、capture、BlogObservation 与 bounded materializer，检查跨 invocation、终态、caller charge 和已知 range。004 的跨界反例已转正，覆盖起点/终点/两端、连续摘要、正式陈述、真实 durable squash、错绑 digest 与冷恢复；GAP-111 的历史表示字节冻结另留 TODO。
- 003 的 Chronicle/Recent work 组合保留纯渲染范围。005 改为实际多个 part 同属一 turn，推进 RecordCoverage 而 PrefixCoverage 留零，验证真实物化器的 gap 选择。
- 006 真实省略 Opening 后重开 journal，再次包含 Opening，证明视图没有删去事实。008 实际捕获原文和已有 requirements 编号；完整原始区间来源仍 TODO。
- 009 只证明显式供应的 constitutive 工具记录不会被渲染器过滤，不证明首次 accepted planComplete 如何冻结 Opening。015 改走真实 journal/capture/materializer，确认工具回合与后续 trace 增长不吞掉 Opening 后的普通工作；没有构造真实 Assume 提交，不能替完整阶段生命周期背书。
- 010 通过实际 SyncDelegate 完成与 Fork resume/Join 消费同一生产物化器，并独立冷重开对照；scoped journal 借用在回调成功或抛错后失效，原 owner 继续可用。旧 Inspector 已退役，未重新开放；Opening 等完整产品链仍归 GAP-109。011 保留实际三段输出与 terminal 补全；正控的 caller/current frame 分开，不能把跨 caller 的摘要冒充仅 assistant 的工作范围。
- 012 同时接受普通散文及自愿使用 Summary/Tests 等标题；禁止强制 schema 不等于禁止出现这些词。013 检查 trace owner 的 tool 过滤；并不机械证明任意 Y 散文都没有抄写工具内容。
- 014 现有 pure fixture 自己选择 gap 起点，因此只证明 slice/render；实际 coverage 选择由新 005 补充。两种 coverage 的类型隔离和所有消费者仍需联审。
- 017 原 Surface 完全忽略 lane 数据，只打印 takeoverStatement；该测试专用算法已删除，真实 Fission 汇聚留 TODO。

`support/record.mjs` 只负责提交测试事实，不复制记录选择算法；错绑 digest 用两个实际 payload，磁盘 fixture 也允许提交后篡改 payload，以区分存储提交拒绝与读取损坏。未执行真实 provider、完整 Fission 或人工验收。

节点先正式构建，再用正式 runner 与 `TESTS_MJS_FILES` 运行本目录及受影响消费者。TODO 不计通过。GAP-109—111 与发布节点记录未闭合范围。
