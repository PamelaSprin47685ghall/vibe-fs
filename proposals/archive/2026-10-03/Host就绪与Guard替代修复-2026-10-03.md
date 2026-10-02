# Host 就绪与 Guard 替代修复（2026-10-03）

本批接续 `863b848bb`，范围为项目就绪观察和 GAP-223。后续排期使用[现行总计划](../../TODO施工总计划-2026-10-03.md)的 R01—R04；产品语义由相关包现行 WHAT 定义。此记录保留红例、实际修复及证明限制，历史测试数字不与本批相加。

## 原因与修复边界

Host `/global/health` 和 `/path` 原来每次请求只给 100ms，合法初始化期间的请求会在 5000ms 阶段预算到期前反复被主动取消。另有 HTTP 非成功、字段不完整及目录别名的判断问题。现在一个在途请求使用剩余阶段预算；只有明确未就绪或连接尚未建立才轮询，不续期。`/path` 核对五个公开路径字段及 canonical workspace；提前退出披露真实 child cause 和有限输出。

这证明请求切片缺陷，并不证明它是上轮全量首次项目健康失败的唯一原因。受控八 Host 旧/新观察中，旧版 123 次请求有 115 次被切片取消；新版八次请求均收到 2.397—2.717 秒的响应，首次又发现 `/var` 与 `/private/var` 的合法目录别名被拒绝。canonical 修复后八 Host 全部在原五秒内就绪。该后轮生成输入不同，不作为严格性能比较。

Guard G 与真实外部用户 H 交错时，容量 owner 正确替代了 G，旧 provider reader 却没有终局交接路径；安装版 OpenCode 的同 session loop 让旧 transform 的拒绝传给新 `/message`。新准入现在凭不可伪造 Accepted witness 和容量 owner 的 Admitted/Queued 替代证据，先持久结算 G，再等待旧物理 attempt 排空。外部 Human 是窄授权；自动 Guard、retry、HostInternal 及普通 root InterruptAttempt 仍无此权限。

单槽容量要求在等待 H 租约之前排空占槽的 G；pending 等待不能占住会话准入序列。第三输入 J 可以替代排队 H，H 恢复时须重新经过有限的 exact 投影与结算。QueueFull 真正拒绝 H 时保留 G；队列已满但可立即给 target 时仍准入。交接失败由 transaction 先写 H Failed，再归还其 exact 资源；提交未知保留资源和原始原因。

旧 G 的 session-only abort/idle 只唤醒。公开 cancelled assistant 必须仍拥有对应执行才可撤当前资源；idle 只使用公开 SDK 最新完成 assistant 的 parent，并以读取前后同一 attempt 的许可作验证。真实反例曾出现 H 已 HTTP200、收到自己的正确回答，却被 G 的迟到 idle 写成 Failed；HTTP 成功不能替代账本证明。

另一个真实 STARTED 反例发现：Host 先发布 assistant 创建事件，transform 后冻结 attempt plan；首次事件缺 plan，挂住的 provider 没有后续事件，所以实际 HTTP 已到 provider，账本仍无 ProviderStarted。该前置不能靠等 collector、多次轮询或削弱阶段断言补齐，须在真实 effect 前落实 exact 生命周期所有者。

transform 现在先冻结 plan，再读取一次公开 SDK snapshot，核对最新、未完成 assistant 的 exact parent，等待 ProviderStarted 持久提交并再次核对租约后才交还 Host。缺失或 foreign identity 明确拒绝；同一 physical 的后续 assistant 沿用既有启动事实。相邻测试夹具须在 transform 前提供实际 SDK assistant，不能把稍后才创建的 assistant 或已结束的旧批当作当前身份，也不能为了让旧夹具通过而放宽生产边界。

正式定向回归还发现两个准入问题。Fable 的 Task 是 Promise，`.IsCompleted` 不是可用的完成观察；并发 hook 因此提前失败，旧 `Promise.all` 又让测试先删目录而剩余写入仍在执行，锁重试保持进程存活。现在 flight 自己记录运行阶段，测试即使失败也先等待两条真实 hook 收束。另有模型 fanout 结束但 flight 尚可登记新 output 的微任务窗口，以及同一 HumanRoot 物理消息重放时被错判为 HumanMessage 的身份冲突；它们必须分别由准入 owner 的注册封口和 exact durable Accepted 身份解决，不靠删除重放断言。

补齐相邻 speculative-investigation-013 的合法 SDK assistant 后，原有共享仓库并行用例暴露真实 owner 幂等漏洞：首次 prepare flight 已结束、同 Decision 已 active，第二次 prepare 却被拒为“owner already has an active decision”；Delegate 随后按 Requested 追加 CannotContinue，与已 Bound 的事实发生冲突。该红例保留两个插件实例、真正并行 transform 和完整事实，不通过串行化夹具掩盖。修复须保留原决定的语义 completion 到持久发布/关闭确认，不把物理准备 flight 的结束当成决定已消失。

## 正式证明与限制

| 范围 | 证据与结果 |
|---|---|
| Host readiness | 新增合法慢响应、HTTP/JSON/路径拒绝、同 child 提前退出及有限诊断；Node22/26 定向各 10/10，完整 harness 285/285 |
| 容量满队列 | execution-model-routing-014 正式红例旧 G 被错误退休；修复后 8/8，保留原 3840-operation soak |
| 未发出的旧 Guard | dispatch-protocol-002 调用实际 ManagerWorkflow owner：新 Human 撤销旧许可后 SDK 零发送，claim 精确 Abandoned；有效许可对照只发送一次并消费。隔离副本恢复旧调用路径时两条行为断言失败，恢复后 7 pass、0 fail、1 原有 TODO；另有 Surface 契约 28 pass、0 fail、2 原有 TODO |
| 生命周期与 Host 注册 hook | SDK abort拒绝、terminal unknown/unavailable、迟到coarse/exact G、当前operator cancellation、同physical下一step与旧idle snapshot；连同相邻包的正式定向255文件：1387 pass、0 fail、19 skip、133 TODO，全部排空，清理accepted=true |
| 真实安装版 Host | OpenCode1.18.29。自然ManagerGuard，分别暂停transform、挂住实际provider、暂停H hook返回后输入J；默认容量和Manager单槽容量共六个独立Host，最终完整integration全部通过。每例核对新回答的session/parent/finish/error/text、旧G一次Cancelled、实际STARTED证据及零ProviderRetry |
| 完整 integration | 35/35文件：376 pass、0 fail、0 skip、15 TODO，全部排空，清理accepted=true；distribution 3 pass、0 fail、1 TODO，harness 285/285。总入口退出1来自TODO，不能算完整产品验收 |

GAP-223在上述有限边界关闭。最后一次gen64完整integration先出现两个capability-enforcement-006 integration-only夹具失败：transform前没有公开SDK assistant；补齐exact parent/time身份后，原工具输出及NUL/BOM指导后缀断言不变，定向16/16通过。随后gen65完整integration取得上表376/0结果，保留前次失败日志，不以重跑替代因果解释。

## 输入与复核入口

生产修改接续HEAD `863b848bbaa069b7fcd1cc66036c92fce8426ba9`。Fable build12的gen64包含1540解析输入、166 Surface及823链接模块；255文件定向结果绑定该输入。只修上述两个integration夹具后build13刷新到gen65，最终完整integration绑定gen65。文档闭合后gen66及澄清H Accepted顺序后的gen67均复核：Node22/26正式十文件各90 pass、0 fail、4 skip、2 TODO，10/10排空且清理accepted=true，exit1只因TODO；Node22直接开启integration的degeneration-guard/004为6 pass、0 fail、1 TODO，公开材料与当前repository derivation一致；check通过。源代码不再修改，不无故重跑完整35文件。收尾记录与日志索引自身再次刷新构建，最终生成输入及材料复核见`baselines/2026-10-03-r04-final-input.log`。

正式命令：`node scripts/build.mjs`；`TESTS_MJS_FILES=<选定编号文件逗号列表> /private/tmp/node-v22.23.3-darwin-arm64/bin/node requirements/verification-system/tests/run.mjs`；完整integration为`/private/tmp/node-v22.23.3-darwin-arm64/bin/node requirements/verification-system/tests/integration/run.mjs`。均沿用原并发与监督预算。定向255文件选择host-boundary、managed-chat-execution、execution-model-routing、managed-session-lifecycle、provider-attempt-recovery、dispatch-protocol、host-provider-failure-ownership、execution-failure-policy、degeneration-guard、structured-workflow、js-semantic-surface、三个relay包、interaction-authority、speculative-investigation的全部编号文件，另加crash-reconciliation/006、context-compression/018、intra-participant-parallelism/013、behavior-diagnosis/006、capability-enforcement/006。

| 原始日志 | 对应证据 |
|---|---|
| [r04-unit-gen64-final](baselines/2026-10-03-r04-unit-gen64-final.log) | 正式255文件、1387 pass/0 fail，exit1只因133 TODO |
| [r04-integration-gen64-red](baselines/2026-10-03-r04-integration-gen64-red.log) | 六个Host、032及resident已通过；两个未提供SDK前提的旧夹具失败 |
| [r04-capability-sdk-fixture-green](baselines/2026-10-03-r04-capability-sdk-fixture-green.log) | 006定向integration 16/16，原公开行为断言保留 |
| [r04-integration-gen65-final](baselines/2026-10-03-r04-integration-gen65-final.log) | 最终完整入口376/0、distribution3/0、harness285/0及清理；TODO仍阻止完整验收 |
| [r04-gen66-node22](baselines/2026-10-03-r04-gen66-node22.log)、[r04-gen66-node26](baselines/2026-10-03-r04-gen66-node26.log) | 文档刷新输入后的同十文件正式回归，各90/0、4 skip、2 TODO，原5000ms监督预算 |
| [r04-gen66-corpus](baselines/2026-10-03-r04-gen66-corpus.log) | Node22直接node:test的生成材料与当前corpus复核；6/0、1 TODO，不冒充正式runner完整验收 |
| [r04-gen67-node22](baselines/2026-10-03-r04-gen67-node22.log)、[r04-gen67-node26](baselines/2026-10-03-r04-gen67-node26.log)、[r04-gen67-corpus](baselines/2026-10-03-r04-gen67-corpus.log) | 澄清Accepted顺序后的同一回归结果；源代码没有变化 |

其余r01—r04原始日志保存每层红例、变异、修复后局部结果与夹具前置失败；不得把不同生成输入的统计相加。

单槽场景将辅助角色路由到已有 `test/test-model-b`，Manager 的 `test/test-model` 只有一个槽。它不关闭 Blogger，也不以辅助执行占满另一个槽冒充 G/H 排队。一次容量诊断曾漏掉 ABI 要求的 `predictorConfiguration()`，在首 root 保存后 HTTP500；该次是夹具配置失败，不是目标产品红例。另一诊断让 Blogger 与 Manager 共用唯一槽，Guard 未完成准入，也不能证明 H 接续。

默认和单槽的三个场景各使用独立 Host、Git 工作区与回环服务，共六个物理生命周期。先前连续复用同一 Host 时，上一场景结束后自然派发的新 Guard 占住下一场景的唯一槽；该失败是场景隔离不足，不是 G/H 目标红例。场景内部 G、H 和 J 始终在同一物理 session 交错，所有暂停点、回答身份、启动阶段和终态断言保留。

本批不关闭 GAP-054 的全部验证阶段审阅、不关闭 GAP-139 的真实重启 boot sweep 与尚无 assistant 的 Accepted 义务。现有独立 recovery 入口及 PAR023 的原断言保留。032 正常、业务错误返回、取消的原参数断言保留；T180 的真实执行器抛错自动 after 仍待证。下一批优先 S03 的 T418/T419 固定验证输入，再按 W1—W6 推进。

安装版 binary SHA256：`2f24593f1b8e578d0b7ed7ca399440d4b6c125330eece20a69ad8d380190d669`。本批红例与最终日志存入同目录 `baselines/`，每个文件绑定对应命令和生成输入；失败编译及夹具前置错误不计行为通过。
