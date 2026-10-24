# context-compression 测试

WHAT 拥有输入、提交与生命周期边界；本文只说明现有测试如何观察这些边界，不补充验收规则。

| 文件 | 已有证据与范围 |
|---|---|
| 001 | 原容量词汇扫描已撤下；真实依赖隔离待证。 |
| 002 | Host compaction 设置判别与重锚幂等性。 |
| 004、005、007 | 终态文本判别、失败预算及 typed RequestKind 分派（区分 WorkMain 与 InteractionRepair，clean-break 退役种类拒绝）。 |
| 006、008 | 角色固定性、连续失败预算消耗及主会话无压缩请求。 |
| 013 | 当前诊断 emit 的非致命行为。诊断失败或文本改变不影响真实决策仍待证；删除未进入产品的常量对象比较及在此重复的 Host fatal 展示。 |
| 017 | 真实用户消息保留规则（操作员用户消息永不删除且按序夹在 resetScope 之间，断言需重写适配 resetScope 语义）。 |
| 019 | 辅助注入的 epoch 退休与 tentative horizon。 |
| 023 | typed park 返回及取消；事件驱动依赖与 durable producer 判据仍待完整验证。 |
| 030 | assume 调用与结果原样保留（活跃 scope 内保留，resetScope 清理时需逐字抄写；断言需重写适配 resetScope 语义）。 |
| 031 | （条款已废止，编号永久空缺；原测试已删除） |
| 032 | （条款已废止，编号永久空缺；原测试已删除） |
| 033 | （待实现）resetScope 屏障执行、旧 scope 清理与操作员用户消息/成对 reset 结果保留。 |
| 034 | （待实现）turnBudget 递减、`\0\uFEFF` 后缀追加、用户消息尾巴提醒与零预算弱提醒。 |
| 035 | （待实现）ScopeReanchored 事实提交、epoch 推进与前缀缓存幂等清空。 |
| 036 | （待实现）跨会话 full post-scope view 的 Synthetic TOML 渲染与传递。 |
| 037 | （待实现）子智能体自然停机完成、无 nudge，及末轮 assistant 散文以 `#` 注释交回父会话（无 `[[session]]` 表）。 |

| 038 | HNI 中立 scope 计划；用户线／工具结果线的返回位置、重置后预算与新 prompt 的作废。 |
| 039、040 | 实体 Host physical canary：OpenCode provider 请求数组的原地更新、旧历史不变，以及 SessionMessage 同批调用/结果配对（`requirements/host-boundary/tests/scope-hni-physical.test.mjs`）。 |
| 041 | 物理会话删除事件清理对应 ScopeSession，不牵连其他会话（同一 Host physical canary）。 |

完成节点后先正式构建，再通过 `requirements/verification-system/tests/run.mjs` 与 `TESTS_MJS_FILES` 运行本目录及受影响所有者。TODO 不计通过，GAP-104/105记录未闭合状态；旧发布清单的DONE不作当前证据。未执行真实 provider 或人工验收。
