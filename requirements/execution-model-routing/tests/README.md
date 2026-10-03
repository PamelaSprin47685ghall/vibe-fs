# execution-model-routing 测试说明

WHAT 定义权威。配置路径、公开 ABI、exact identity 与次序属于合同；当前模板选了哪些供应商、多少并发、内部函数叫什么，不是新的通用规则。

| 范围 | 已有证据 | 边界 |
|---|---|---|
| 001—002 | 实际磁盘加载/原子创建/已有文件保留，scheduler ABI 正反例 | 直接 invoke 的 ABI 测试不代替角色准入；新 fixture 使用当前角色 |
| 003、006 | 真实 routing owner、两个插件共享、子进程重新初始化、exact retry 与 previous/witness | 003 子进程验证本地容量新建，不是完整 durable recovery；33-009 才使用同一持久仓库的实际崩溃 |
| 004—005 | 真实 pending/supersede/唤醒；只更换策略就改变 target 和容量限制 | optional reservation 与“未接收不占容量”冲突，见 GAP-130 |
| 007—009 | 真实释放 owner、Host decoder、SDK adapter、真实插件的配置优先权与消息投影 | decoder 与释放分别通过不代表接线全程已证；低层 SDK 可编码 model 不代表内部 synthetic send 可以携带 model |
| 010 | 真正 token ledger/借贷、显式 lender、公平等待、召回及有界随机序列 | 已撤下内部名字/文件名单/至少 600 文件检查；真实 tool 入口交接仍 TODO |
| 011—012 | 012 真实 fence 身份、旧 epoch、跨 owner 拒绝与单次结算 | 011 原错误 fence 用例与 012 重复，未证明准入顺序；完整顺序与负向编译 TODO |
| 013—015 | 实际队列上界/先后、immutable snapshot、无修复 reconciliation、diagnostic 投影 | fixture 的压力规模和当前内部数量不是产品上限；快照不作为修改状态的能力 |
| 016 | TODO | 原手写 fatal descriptor 不代表生产入口或 physical kill |
| 017—018 | 真实失败 witness 消费、保留/驱逐及当前/历史角色准入 | 旧角色可读与可准入仍由身份与生命周期包分别负责 |
| 019 | 本地绑定可用时拒绝冲突；策略变化的可执行失败 TODO | 尚未证明道路持久化/换物理 session/真实恢复；现代码可覆盖绑定，见 GAP-129 |

GAP-128—131 记录缺证和待裁决，不能把 TODO 算作通过。004 的局部 reservation adoption 用例仅保存当前机制的可观察行为；旁边的反例明确指出该机制尚不满足当前前置准入约束。019 不再把自动换模型当成满足“永不换模型”的正例。

014 新增满队列回归：旧 Guard 已 committed 且 provider step 在途，32 个真实 pending demand 占满队列；新 Human 因无 target 被拒绝时，旧 exact lease 与完整容量快照保持不变。另证相同满队列前态下可立即取得 target 的新输入仍成功替代，原 3840-operation soak 保留。该文件修复后 8/8；真实 Manager 单槽 G/H 交接由 Host 033 的 installed canary 证明，辅助角色使用独立模型池。两层范围见[本批记录](../../../proposals/archive/2026-10-03/Host就绪与Guard替代修复-2026-10-03.md)。
