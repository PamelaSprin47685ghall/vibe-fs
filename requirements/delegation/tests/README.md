# delegation 测试与证明范围

规则来自本包 WHAT。用例标题中的 WHAT 锚点是映射；本页说明当前证据，不增加合同。TODO 是未完成，不计通过。本站对应人工巡检 42，完整 Manager/Orchestrator 场景仍需后续站收证。

| 文件 | 当前实际观察 | 尚未证明 |
|---|---|---|
| 001、004、017、020、030 | 明确待证 | 全体委托四要素、全局工具合同唯一、返回不扩权、换工具仍守约、真正 fatal settlement/report/kill |
| 002、032 | 生产权限投影及兼容 calling | 实际直接/包装/转发请求的全链权限；权限表不证明模型自行返回 |
| 003、006、027 | 真实 fork/resume 工具接收、拒绝 calling、复用 Byname、busy 拒绝、完成后无需 join 即续做 | 同一道路的产品判定、跨进程恢复绑定；同进程 reopen 不是 crash |
| 005、013、014 | 真实 renderer、独立 TOML 解析、handle fold；实际 Join 队列上限与完整剩余项消费；commission mailbox 的 FIFO、余项与空队列 | 全部参数/错误不泄漏拓扑、真实 agent completion 的 owner/CAS/恢复；commission 完整入口接线 |
| 007 | 真实同步普通/批次入口拒绝已废止 Coder/Inspector，拒绝前不创建 child、不发 prompt；Engineer 正例 | Sphinx 受管入口、无环与 family-root 放置的整链约束；032 的标准权限投影不替代入口授权 |
| 008—012 | 真正同步调用：相反到达顺序仍按给定 Host 顺序合并、一次发送、scope 隔离、复用新 assignment、普通完成、canonical 正文与 sibling 引用 | Host 实际收集调用列表的接线、系统模型调度与持久 binding |
| 015 | 实际 Join 等待器、受控 PTY 端口完成、虚假唤醒、三种中断、锁与 permit 拒绝；实际 commission mailbox 中断后 job 保留、完成与中断竞争 | 真实 Host 用户输入/取消接线及 agent authority；端口没有启动 OS PTY |
| 019、021 | 生产 typed renderer 的两种语言、敌意 TOML 样本、指令与数据平面分离、空背景省略 | 真正首提示读取最新 durable parent、指定 peer 的材料来源、权限不克隆 |
| 022 | 完整语言资源选择、estimate fold 的去重与零饱和 | 归零后仍能真实执行、同一 durable estimate 的替换/重开 |
| 023 | 同步 workflow 消费注入 retry verdict，恢复成功或耗尽后返回 | 真实恢复 engine 各路径与最终失败组合 |
| 024—026 | parent delta、own child delta、因果完成/失败、实际 fork 接收与不确定发送后果；Host event port 保留身份 | 所有 terminal 分支、进程 crash cuts、未知接收的后续收敛 |
| 028、029 | 当前编译清单的直接/传递闭包、额度及反向注入检测；部分源码词形审计 | 各 locality 独立真实编译、类型隔离和实际 capability 行为；路径/词形只是当前结构审计 |
| 031 | 连续完成、独立 checkpoint 的 Committed/NotCommitted 和身份、旧 root/取消、完成与清理 | 真正重复 terminal、在同一次完成中注入 NotCommitted/Unknown 后仍交付、Unknown 重读、PhaseConflict 真正熔断；SyncDelegate 不替 fork 证明 |

`support` 只建立真实 runtime 所需的临时 journal、可控物理端口和清理。tool schema stub 仅支持构造工具，不证明真实 Host schema validation。等待同时考虑 prompt 与 invocation 拒绝，避免将合法拒绝误作挂死。新建角色限制不改变历史解码。

Join 的条数上限、显示窗口和 WorkRecord 本身的有界物化不是同一预算。renderer 保留单个大 entry 的测试只证明未截断调用方给的材料，不能证明材料已由真实 bounded projector 限定。

014/015 保留 PR #42 新增的四项 mailbox 回归。三方合并曾各复制两遍；核实两份逐字相同后只删除重复副本，断言均保留一次。028 保留上游 Host adapter ratchet 312；旧说明中的 307 是此前认知 runtime 引入时的历史值，312 来自后续 prefix shard 显式依赖。

旧 chooseRoad/evidenceBoundary 的固定答案、serializationDecision 的复制判断、reuseBinding 的单次读取及 batchOrder 的过滤器已移除；无用 deferred-result exports 同步删除。实际受控队列与有效渲染证据保留。源码中有方法名、WHAT 自我匹配、手写 descriptor 都不算实现达标。

本轮真实回归发现已废止角色先创建 child、到发送阶段才拒绝。007 先断言零副作用，再检查拒绝结果，使该反例直接可见。生产同步 workflow 在普通与批次共用的 admission 处拒绝 Coder/Inspector，不进入 child 创建；历史角色解码仍保留。修前 childCount 为1，修后结果由本轮统一新构建复验（GAP-154）。完整待补范围见 GAP-153；编译额度归属沿用 22-D1，DevOps binding 与 work handle 复用沿用 34/35 待决，不在测试中另设规则。旧发布清单及fatal库存的历史状态不能代替当前真实执行证明。

本次结果见[迁移记录](../../../proposals/20模块迁移-恢复与委托-2026-09-28.md)。直接 `node --test` 只用于局部调试；正式结论使用 verification-system runner，TODO 会使它返回未完成。
