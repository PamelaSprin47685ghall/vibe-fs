# crash-reconciliation 的测试与证明范围

WHAT 是合同。这里说明当前证据，不为恢复增加隐含规则。用正式 `requirements/verification-system/tests/run.mjs` 选择本目录编号文件；先完成标准构建。TODO 表示尚未证明，不能计为验收通过。

## 当前实际执行

| 文件 | 证明对象 | 尚不能推出 |
|---|---|---|
| 001、006、008 | 实际 quiescence gate 的 owner 隔离、单次消费、active tool、撤销、exact release | OS 重启，真实 Host abort 解码及物理发送处的完整授权链 |
| 002、005、009、010、012 | 实际 child resolver、completion decoder、handle projection 和恢复结果映射 | 磁盘恢复、blob-before-fact、实际 join 消费，或整个完成投递幂等 |
| 003、007 | 实际恢复决策及 TurnUnknown 不作为业务终态发布 | 未知外部 effect 的物理查询和全体普通入口重入 |
| 011、013、014 | 实际闭包、permit membership、合并与授权函数 | 每次生产 join/await 都调用检查，或已有全家族的物理恢复证据 |
| 015 | 实际附加会话分类及 SyncDelegate 的观察/创建端口，包含并发共用创建 | 重启替换时先证明消失、Close 再 Link 的完整链 |
| 018 | 实际插件 config hook 不注册显式续传命令；另保留旧文件名的结构审计 | 真正重启归位、中断历史可见性、全部观察者不触发副作用 |
| 020 | 实际 canonical codec/fold、child load selection/void、取消结算桥接、JoinDrain；真实 child 结算的持久写入、失败传播和 journal 重开 | 插件激活顺序、普通执行前的结算门禁、真实命令不重放、模型绑定及进程崩溃 |
| 021 | durable handle 按 id/byname 查询、真实 binding cache fallback、由已折叠 Fission fact 解析 lane | 所有 reuse/placement/await 入口的接线、真实重启、物理资源所有权 |

011 的接缝构造已授权的输入夹具，调用生产 permit/membership 函数；它不实际恢复这些成员。009 的 trace 检查器确实拒绝所给的乱序夹具，但合法 fixture 不是产品真实执行记录。所谓 `crashScenario` 的旧函数名实际仅驱动 handle projection，002/012 不再把它称为进程崩溃证明。

015 的受控 SessionPort 在观察发送后明确拒绝自己挂起的请求，并等待调用结束。这样关闭测试自己创建的生命周期，不扩大正式监督时限，也不伪造 Host 已接纳。

006 的实际 gate 证据与 dispatch-protocol-002 的实际 Manager owner 接续：发送边界消费有效许可，拒绝新 Human 已撤销的旧许可。002 的 SDK 零发送及 durable claim 终态不能代替本包 OS 重启或全体发送调用者的证明；本批交叉证据见[修复记录](../../../proposals/archive/2026-10-03/Host就绪与Guard替代修复-2026-10-03.md)。

## 撤下的误导证据与缺口

004、017、019 及 020 的整链部分保留 TODO。原 JoinSurface 自行复制校验规则，未调用实际 join；旧施工已删除并同步编译和清单。原 DevOps crash helper 虽启动进程，却未运行命令，把“在途”“未重放”“唯一权威”直接写成固定答案；已删除。类型/源码名称、空数组、常量返回和独立调用的拼接，不再冒充整个恢复证明。

020/021 的 LoadRecoverySurface 保留 canonical fact 转换与投影观察；PhysicalRecoverySurface.settleChildRuns 持有真实 JournalHandle，先恢复并排空真实 PTY，再交给 ChildWorkRecovery 作废孤儿工作，不模拟 append。020 的 integration 回归通过真实 Dispatcher 建立合法 parent 与 Engineer/DevOps 子 run，重开 journal 后结算：上一 runtime 遗留、已无法交付的两个孤儿子 run 在加载期被**作废**——每条 scoped 子工作（准入身份带 scope，父会话 `parent`）追加一条 `ChildWorkVoided`，其 activeLogicalRun 随之清空、对应的具体工作单元格（`Works[exact id]`）转为 `Abandoned(Voided)`，而它承载的那份**工作义务已是终态**；底层 handle 绑定仍**可复用**（它标识可再次参与的参与者身份，不是被作废的那份工作），`summary` 投影叠盖工作状态并正确读取终态，join 以 `voided` 报告一次后退役（`Retired`），第二次结算不再追加事实；合法但无 scope 的历史在加载投影 fixture 中追加 `ChildRunVoided`（该 fixture 从不接纳已落地工作，故 scoped 的 `ChildWorkVoided` 只出现在真实 journal 块中），因其无独立工作单元格，被作废的对象就是该 handle 绑定本身：它转为 `Abandoned(Voided)` 并在单次报告后退役——**两类作废事实都走 abandon，没有一类是 abandon 规则的例外**，差别只在于被 abandon 的是工作单元格还是 handle 绑定；两者都结清工作义务生命周期，都按 event_id 核对，且都不产生 completion。另一故障用例临时以同名目录替换 events 目录下的 live 文件，造成真实物理写入失败：追加结果未知必须显式传播，原 writer 被毒化，后续追加只能返回未尝试，不能继续写入。目录在 finally 恢复后，由新开启的健康 writer 完成作废结算，且不二次派发。

这些回归调用真实 journal 结算入口，没有驱动 PluginRecoveryWiring、插件 activation 或 OS crash。crash-reconciliation-018/020 的 Load Phase 表述与 durable-events-020、host-boundary-021 的延迟激活边界仍需协调；本轮不改变激活时点和等待语义，不声称已经阻止普通执行越过未完成结算。JoinDrain 的受控 append port 仍只把实际事实交 production Fold，没有写磁盘。主动取消产生可收取 completion，ChildRunVoided 与 ChildWorkVoided 均不产生 completion，二者不是同一场景。

作废与「不写完成」的区别正在这里：作废**结清**父方的未解除工作义务（join 以 `voided` 报告一次后该工作退役），所以它不是「什么都没发生」；而「不写完成」只是没有交付，本身并不结清任何义务。**结算按孤儿幂等**：每个孤儿一生只产生一条作废事实，**重启的进程不得再次作废同一份孤儿工作**（journal 里不会出现第二条作废事实，join 也不再重复报告）；在这一次 `voided` 报告之后，该 handle 经 `durableLifecycleByname` 读出 `Retired`——**这是经 `summary` 投影读出的终态**（投影叠盖工作状态），不是从原始 handle 绑定直接读出的。

绑定/Fission 测试会在 finally 移除自己的 resolver/cache。canonical journal JSON 是持久协议载体，不是 Fable 内部 tag/fields 对象。018 的旧 `/continue` 材料链随新版合同退役；本轮逐项覆盖迁移表见[历史迁移记录](../../../proposals/archive/2026-10-03/20模块迁移-恢复与委托-2026-09-28.md)。

完整进程中断、持久事实重开、物理发送/完成提交切点见 GAP-149。33 的真实 OS crash 测试可作为相关机制证据，但不替代本包所有工具、DevOps 与 family 恢复场景。provider failure 的分类和计账归 37，已移去本包重复且不相关的断言。

019 原合同中的独立 registry、精确 source symbol/proof title 与当前规范所有权的关系待决（GAP-150）；测试不会自行创建另一份权威清单。020 沿用 DevOps 模型和 handle 身份的既有待议（GAP-129、GAP-132）。
