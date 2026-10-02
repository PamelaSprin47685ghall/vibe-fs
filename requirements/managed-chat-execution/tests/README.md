# managed-chat-execution 测试说明

规则以 WHAT 为准。本目录区分生产纯逻辑、受控端口、实际持久化和真实进程退出，不把它们相互替代。

| 条款 | 当前实际证据 | 未闭合范围 |
|---|---|---|
| 001—002、004、011 | 真实 codec、fold、准入 owner；exact key、完整身份、幂等与冲突、旧 schema 样本 | 历史样本不是所有历史版本全集 |
| 003、007 | 生产 transaction/settlement 的失败停止、terminal 与释放次序；003 实际注册 hook 的并发、完整模型/variant 投影、同 key 重放、HostInternal 与 typed 拒绝 | 全部 chat.message 到 provider body 的边界故障 |
| 005—006 | 生产 lifecycle 的提交/拒绝；公开 Host event decoder；005 实际 transform 在 plan 冻结后读取 SDK assistant 并等待 durable Started，missing/foreign 拒绝，后续 assistant 不重复 Started | 全部事件来源的 cursor、wake、提交悬置时容量不早释放及真实重启 |
| 008 | 显式 TODO | 构造无状态推进、激活失败、真实因果唤醒；常量信号名单不是证明 |
| 009 | Accepted 后真实 OS 子进程退出，再开新进程读同一仓库；绑定、容量等旧资源消失 | 只覆盖这一中断点，不能代表全部 crash cuts；三个 codec 样本的字段扫描也不是类型不可表达证明 |
| 010 | 实际 recovery Host 消费 exact cancel，验证 pre-provider/started 终态、重放及相邻执行不变 | 公开 cancel/delete 枚举、真实持有资源下的完整排空与悬置 append |
| 012 | 真实纯决策、解释器逐次端口观察、Host recovery port 接受/拒绝/缺失、实际重复终态结算 | 每个中断点真实输出驱动跨进程恢复；晚到释放与新资源所有者竞态 |
| 013 | canonical 只读诊断、实际 Host 达终态后撤销 manual | 与实际全量 incident 现场的集成另依 014 |
| 014 | 实际 incident capture/replay、redaction、版本与篡改拒绝 | 受控 Host contract 证据不等于当前真实 Host canary 已执行 |

012 的 A—E 调用生产准入事务并在受控边界停止；F—I 验证生产 lifecycle 的事实前缀。它们没有把得到的持久化输出传给后续恢复，因此不能命名为完整 crash recovery。`admissionPhaseSamples` 显式构造不同阶段的输入，重复调用真正 recovery 并逐次保留 effect；它证明的是这些输入的局部决策，不是进程重启或 durable owner 的 effect 幂等。对重复输入的检查保留整个结果序列，不先转成按 key 去重的字典。

本批 007 补准入交接失败：SDK 拒绝排空旧 attempt 后，新输入先持久 Failed，再释放自己的 admission；terminal unknown/unavailable 时不释放，并保留取消失败的原始原因。Host 033 经实际注册 hook 验证同一拒绝路径。真实 Guard/H/J 的回答 parent、ProviderStarted、一次 terminal 和零错误 retry 则由安装版 Host canary 另证，不能用 transaction 受控端口替代。日志及剩余边界见[本批记录](../../../proposals/archive/2026-10-03/Host就绪与Guard替代修复-2026-10-03.md)。

003 的并发测试即使一条 hook 失败也会等待所有已启动 hook 收束后清理目录，不让仍在写入的 journal 失去目录。模型投影结束前封口 output 登记；正式迟到 hook 在公开投影的微任务窗口进入，仍须取得自己的完整模型且容量只有一个 exact owner。旧源码词形计数已由这些行为回归替代，不能继续把它们当作调用链证明。

未闭合部分见 GAP-126/127 和正式 TODO。运行本包须开启 integration tier 才执行 009；即使普通用例全部通过，有 TODO 也不算完整验收。
