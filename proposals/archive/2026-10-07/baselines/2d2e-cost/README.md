# 2d2e CI 成本只读调查

本目录保存 2026-10-07 冻结的调查卡与派生表，逐文件从 `/private/tmp/vibe-fs-n00-2d2e-cost-20261007/` 按原字节复制。原始 CI 日志与身份、清理证据见[2d2e 收据](../2d2e-ci/receipt.txt)。

PR run `37563886360` 实际 checkout 为 `34f0ef3bf5da11d94e61ceb5c3feacda8a538148`，对应 source `2d2e6f730614f0c127d95ea62e83aee14eee23ce`。format/check/build 通过；unit 达到原 300 秒物理预算，738/824 文件排空、2 active、84 queued，没有全局 summary。已收到的真实断言失败是 requirement-system017 对 Host033 双 WHAT 锚点的拒绝；不能将失败统称为超时或 pending。

[共同样本表](common-comparison.txt)只比较 2d2e、35d、106ed 的 737 个共同完整 worker，核验同 PID 的原始 pre-import/exit、累计自身 CPU 差值及独立 parent callback wall。active、missing、invalid、queued 均排除。输入与执行宿主不同，差值只是观测，不能认定唯一原因；006/010/016 未准入，也没有它们的成本或收益证据。

[下一有限工作卡](readonly-card.txt)提出先观察迁移 CLI 在非法版本拒绝前的真实模块加载，要求原生加载记录、观察器就绪及合法 dry-run 非零正控，并保留原错误、无报告和备份字节不变断言。尚未实施，也未证明 CPU 收益。延后加载会改变“dist 损坏且参数非法”时的错误优先级，必须先明确合同。

本调查不构成 CI 通过证书，不表示 N00 或相关 TODO 已闭合。各 `*-metadata.txt`、`*-valid-complete.tsv`、包汇总及源码 blob 表提供可复核的派生数据；原始官方日志仍是证据来源。
