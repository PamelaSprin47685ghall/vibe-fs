# epistemic-reasoning 测试

WHAT 是规则所有者。本目录说明测试实际证明到哪一层，不给实现追加准入规则。测试输入中的文献、回答、模型与 treatment 均为受控材料，不是现实世界事实。

## 入口与证明范围

| 条款 | 当前证据 | 尚未证明 |
|---|---|---|
| [001]—[008]、[011]—[013] | 真实 Legacy Kernel/Session/MCP handler 的认识基底、阶段拒绝、语义分布、证据去重、Gateway 与闭包；部分另走真实 MCP 子进程 | canonical generic Runtime 与插件端到端闭包；充分统计量的完整性仍需语义审查 |
| [009]—[010] | 真实 Bayes、A*、MCTS 算法的受控正反例：独立 normalized-product oracle、underflow、重开、全局 frontier、共享统计与描述性误差范围 | 有限样例不是全部数学性质的证明；适用假设和独立性仍需审阅 |
| [014] | 实际 MCP initialize、tools/list、版本与协议能力协商 | 未协商能力的未来扩展需另作回归 |
| [015]—[017] | 正式 ID 解码、canonical hash、Core reducer、插件锁与证书槽；明确 graph 事件后才可改该节点证书 | 源码词形不证明 Core 不解释语义；声明字段存在不证明实际 provider 实验遵守它们 |
| [018] | 待证 | MCP/OpenCode 真实入口尚无相同 accepted-event 的接线证明；删除了自写 fold 伪装共享 reducer 的用例 |
| [019] | 真实 MCP 服务进程接纳结果，关闭并等待退出后，新进程从同一 EventStore 恢复；冲突 revision 不改变导出；合法新 revision 能继续 | append 故障与响应/Current 的整链顺序；WorkId/attempt 幂等见 GAP-172 |
| [020]—[022] | 正式 PluginRegistry、Core 工作迁移与 Agenda 的正反例；选入本批不等于依赖已完成 | 这些 reducer/调度用例尚未接到 generic MCP 与真实 Engineer 执行链 |
| [023]—[026]、[029] | 正式插件的随机分配、带方向估计、Borda/BTL、评分、固定点和 stop 计算；输入有正反控制 | `committedBeforeStimulus` 等声明只证明函数检查声明，未证明真实密封先于 provider stimulus；统计保证需审查适用假设 |
| [027] | 待证 | 实际 blind child、重试快照、递归拒绝、capacity 与 drain；已删除直接返回 aborted/drained 的假接缝 |
| [028] | 实际 MCP export 保留已接纳材料；失败 TODO 显示它不提供规定的 research bundle | 缺少研究导出、真实 claim 分类与新进程从 bundle 重建答案（GAP-173） |
| [030] | 冻结 transcript 的黄金轨迹、MCP Legacy 接口与实际重启恢复 | 兼容成功不表示高层流程已经程序化 |
| [031]—[032] | 待证 | 端到端程序驱动和受预算/取消约束的只读 Engineer；Legacy 协议适用域待审（47-D1） |
| [033] | 真实 generic MCP 的已知反例：重复结果再次追加、未派发身份被接纳（GAP-172） | 已接纳工作恢复后不重新派发或付费；不能从重放 hash 相同推出接纳幂等 |
| [034] | 真实 handler 取消后拒绝 stale/current/future revision 的提交；实际服务进程取消落盘后重启仍拒绝晚到结果 | 在途 Engineer 的 abort、资源 drain 和结果持久化竞态 |

`test.todo` 表示未建立证明。带 `{ todo: 'GAP-…' }` 且含断言的用例保留已观察到的失败，不能计作通过。GAP-172 与 GAP-173 是生产入口缺陷；GAP-174（取消事件身份冲突）与 GAP-175（纯 scheduler 过早纳入依赖项）已有修复，仍须本轮重新构建后的正式检查确认。

## 接缝与清理

`support.mjs` 包装现有 Legacy Surface；`gec-support.mjs` 只提供静态 Surface 和 fixture 读取。`support/mcp.mjs` 直接调用真实 MCP handler，或启动正式服务入口并通过 JSON-RPC 操作。子进程停止后等待退出，临时 EventStore 随测试清理。它不实现业务判定，也不扩大正式监督时限。

移除了只供测试调用的 GecHost/GecStore：前者伪造 child、abort/drain 和 fold，后者自建 append/Current/export；它们并非生产 Host 与 EventStore 接线。GecSurface 的数组长度哈希和遇到未知节点就造节点的重试也已删除。现有 reducer 证据仍需与实际 MCP 通路分开报告。

## 运行

先由统一施工流程构建，再用 verification-system 的正式 runner 执行本包 `001.test.mjs`—`034.test.mjs`。涉及持久化的检查需允许测试自己的临时目录和子进程；无需真实 provider。可用 `node --test requirements/epistemic-reasoning/tests/*.test.mjs` 做局部诊断，但局部通过不代替正式 runner，旧 dist 不能验证本轮 F# 修改。
