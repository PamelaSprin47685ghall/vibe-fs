# degeneration-guard 测试说明

[WHAT](../WHAT.md) 是规则来源。测试分别证明数值算法、局部传感器和 Host 组合，不将一种证据冒充另一种。

| 条款 | 已有证据 | 限制 |
|---|---|---|
| 001/003/005 | 有限重复/多样性样本、普通代码样本、严格边界、独立末次出现权重公式、词表计数 | 样本正常不保证所有正常输出无误杀；有限测试不证明渐近复杂度，须审查更新算法 |
| 002 | 实际 delta codec 与传感器的文本/非文本区别 | 未证明所有真实 Host 输入都是 assistant、无权威旁路 |
| 004 | 实际 selector 的正反路径、tracked read 后解码/过滤、字节摘要、并行编码等价、worker 失败清理、当前仓库重新派生及自洽先验 | 重新派生不等于同一 staged build 的完整 lineage/traversal；通用产物验证器见 structured-workflow[015] |
| 006/008 | 独立对象、显式 reset/drop、精确 session/run 的消费与重复拒绝 | 独立对象不是 OS 重启；已完成任务的删除不是在途取消；真实 run 边界仍待接线证明 |
| 007/009 | 实际 sensor 的 interrupt、reconcile、continuation、拒绝诊断及受控暂停 | 两项已知合同反例保留为可执行 TODO；发送端口的 Result 不能证明真实 transport acceptance 分类 |
| 010 | 注入 eligibility 的正反控制 | 不代表生产 Owned/parent/managed/compaction 选择完整 |
| 011 | 两种语言的完整资源投影，按公开读取语义去首尾空白 | 资源内容与翻译语义人工审阅；真实 authority 与资源选择仍待证 |
| 012/013 | 013 直接比较成功/抛错诊断下的相同控制 trace，覆盖 continuation 成功与拒绝 | 012 原源码词形与重复局部场景不证明真实 turn/fission 无第二恢复，已转 TODO |

所有局部异步用例等待实际 owned task 或使用受控 Promise，删除了固定15毫秒等待。观察列表保留实际调用次数。测试辅助代码只构造事件、控制外部端口和记录事实，不复制 guard 状态机。

007 当前两个反例：Host 拒绝 interrupt 后重复 delta 再次中断同 run；pending interrupt 尚未返回就消费匹配 cause，会提前进入 continuation。它们违反现有合同，不能改为接受现实现。生产 HostTurnObserver 也先 consume、后等待当前 task，等待位置不足以排除这条路径；完整 Host 场景仍待补，不把局部反例称作已发生的真机事故。

运行前执行 `node scripts/build.mjs`；通过正式 runner 的 `TESTS_MJS_FILES` 选择本目录13个编号文件，并设置 `WXS_TIER_INTEGRATION=1` 执行004当前仓库派生。未启用 integration 必须报告该缺失。TODO 阻断整体验收。新基线验证范围及剩余问题见[本批记录](../../../proposals/35模块PR施工记录-2026-09-28.md)和 GAP-145—147。
