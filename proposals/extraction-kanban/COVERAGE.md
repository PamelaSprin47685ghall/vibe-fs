# 范围、去向与旧计划对账

盘点基线：主仓 `8f7f9be77`，独立库 `46d9c07 / 0.5.2`。返回 [看板](BOARD.md) / [职责卡](CARDS.md)。

本表回答“现有职责由谁接手”，不宣称已经遍历全部函数调用或决定删除全部代码。路径和 WHAT 在建板时核对；具体实现与活跃工作在认领时重新核对，特别是不把旧计划里某日的“下一步”当成当前 owner 已同意交接。

**主责卡**负责承接条款与证明去向，并非独占整个需求包的编辑权；跨域行为仍由原需求包定义。相邻卡可以共同提供证据，但不能维护同一事实的两份权威。

## 1. 全部现行需求包

| 需求包与正式合同 | 主责卡 | 相关卡 / 去向边界 |
|---|---|---|
| [action-affordance](../../requirements/action-affordance/WHAT.md) | CUT-08 | 05、25；描述不谎报能力，高风险动作有真实后果说明 |
| [attention-regulation](../../requirements/attention-regulation/WHAT.md) | CUT-22 | 09、16；defer/assume 不变成新 authority 或第二份 todo |
| [behavior-diagnosis](../../requirements/behavior-diagnosis/WHAT.md) | CUT-30 | 05、16；先裁决，Rulebook/Chronicle 义务不自动退役 |
| [capability-enforcement](../../requirements/capability-enforcement/WHAT.md) | CUT-08 | 07、10、25；可见面与执行 gate 同源 |
| [causal-wait](../../requirements/causal-wait/WHAT.md) | CUT-21 | 12、13、32、37；等待/诊断没有业务权威 |
| [change-integration](../../requirements/change-integration/WHAT.md) | CUT-23 | 12、13、35；真实 Git 集成与冲突归属 |
| [cognitive-environment](../../requirements/cognitive-environment/WHAT.md) | CUT-05 | 17、30；五层认知材料的语义边界保留 |
| [cognitive-workspace](../../requirements/cognitive-workspace/WHAT.md) | CUT-16 | 22、38；不重建已退役 canvas，旧只读兼容单列 |
| [concern-routing](../../requirements/concern-routing/WHAT.md) | CUT-22 | 09、17；消息归属与呈现，不扩权 |
| [context-compression](../../requirements/context-compression/WHAT.md) | CUT-16 | 03、06、15、30；原文、覆盖、候选、真实失败分开 |
| [crash-reconciliation](../../requirements/crash-reconciliation/WHAT.md) | CUT-35 | 01、04、11—14、19、34；每卡仍自己证明恢复 |
| [degeneration-guard](../../requirements/degeneration-guard/WHAT.md) | CUT-30 | 14、37；有界真实文本观察与手动包络 |
| [delegation](../../requirements/delegation/WHAT.md) | CUT-13 | 06、09—12、23、28；新建/续做/委任不混合 |
| [dispatch-protocol](../../requirements/dispatch-protocol/WHAT.md) | CUT-11 | 09、10、12、34；唯一写入口和真实承接 |
| [distribution](../../requirements/distribution/WHAT.md) | CUT-33 | 05、25、32、34、38；真实 tarball 与隔离消费者 |
| [durable-convergence](../../requirements/durable-convergence/WHAT.md) | CUT-19 | 18、23；合法 writer 分叉不被 CAS 单流替代 |
| [durable-events](../../requirements/durable-events/WHAT.md) | CUT-18 | 19、35；字节协议、合法性与物理提交分别承接 |
| [effect-accounting](../../requirements/effect-accounting/WHAT.md) | CUT-11 | 01、04、12、14、23；意图不等于确认 |
| [epistemic-reasoning](../../requirements/epistemic-reasoning/WHAT.md) | CUT-31 | 09、11；认识状态与 runtime continuation 分离，先裁决 |
| [execution-failure-policy](../../requirements/execution-failure-policy/WHAT.md) | CUT-14 | 10—12、19；先结算再 fatal，保留原错误 |
| [execution-model-routing](../../requirements/execution-model-routing/WHAT.md) | CUT-10 | 07、08、11、14；策略/许可/执行 profile 分开 |
| [feature-ablation](../../requirements/feature-ablation/WHAT.md) | CUT-37 | 08、30、35；消融不是跳过必要正确性 |
| [guidance-delivery](../../requirements/guidance-delivery/WHAT.md) | CUT-30 | 05、15、17；Frontier/Coverage 与实际投递分开 |
| [host-boundary](../../requirements/host-boundary/WHAT.md) | CUT-34 | 11、12、17、25、35；逐项真实 Host canary |
| [host-provider-failure-ownership](../../requirements/host-provider-failure-ownership/WHAT.md) | CUT-14 | 34；宿主内建 retry 的未证/不符不得删除 |
| [interaction-authority](../../requirements/interaction-authority/WHAT.md) | CUT-09 | 07、11、12、22、35；来源、根和 exact closure |
| [intra-participant-parallelism](../../requirements/intra-participant-parallelism/WHAT.md) | CUT-28 | 03、10、12、13；同一 participant 的多 lane |
| [js-semantic-surface](../../requirements/js-semantic-surface/WHAT.md) | CUT-25 | 08、32、33；全部库共同遵守公开产物边界 |
| [knowledge-reuse](../../requirements/knowledge-reuse/WHAT.md) | CUT-27 | 03、06、19、24；先裁决，保留原 Casebook 证据 |
| [managed-chat-execution](../../requirements/managed-chat-execution/WHAT.md) | CUT-12 | 04、10、11、14；exact key 与 Terminal 单赋值 |
| [managed-session-lifecycle](../../requirements/managed-session-lifecycle/WHAT.md) | CUT-12 | 04、13、34；关联、复用、清理不可只由资源表代替 |
| [obligation-ledger](../../requirements/obligation-ledger/WHAT.md) | CUT-16 | 22；原 todo 原样交 Host，不建第二份 desired/applied |
| [office-capability](../../requirements/office-capability/WHAT.md) | CUT-08 | 07、25；按后果定义 Office，不按模型选型扩权 |
| [participant-horizon](../../requirements/participant-horizon/WHAT.md) | CUT-17 | 05、06、13、22；跨库数据不直接变 provider DTO |
| [participant-identity](../../requirements/participant-identity/WHAT.md) | CUT-07 | 08—14；逻辑身份与机器执行绑定分离 |
| [prefix-stability](../../requirements/prefix-stability/WHAT.md) | CUT-15 | 03、16、17；同 epoch 字节前缀与已提交冷边界 |
| [process-execution](../../requirements/process-execution/WHAT.md) | CUT-02 | 04、20、21、25；pipe、PTY、外存分别兑现 |
| [provider-attempt-recovery](../../requirements/provider-attempt-recovery/WHAT.md) | CUT-14 | 07、10、11、34；一次失败只推进一次 logical budget |
| [provider-language](../../requirements/provider-language/WHAT.md) | CUT-05 | 08、17、30；当前全局偏好、成对资源、零静默回退 |
| [provider-projection](../../requirements/provider-projection/WHAT.md) | CUT-17 | 03、05、06、08、15、16；语义与 Wire 分层 |
| [repository-investigation](../../requirements/repository-investigation/WHAT.md) | CUT-29 | 24、26、27；调查与推理分离，低信任 warm-start |
| [repository-programming](../../requirements/repository-programming/WHAT.md) | CUT-24 | 08、25；文件行为和沙箱/编程面分卡，不丢义务 |
| [requirement-grounding](../../requirements/requirement-grounding/WHAT.md) | CUT-26 | 17、24；需求路径、内容身份及实际投递 |
| [requirement-system](../../requirements/requirement-system/WHAT.md) | CUT-26 | 00、32、38；全卡承接原编号，不按目录搬家关闭 GAP |
| [semantic-trace](../../requirements/semantic-trace/WHAT.md) | CUT-03 | 06、15—17、27—30；单一原始 source 与范围 |
| [session-ontology](../../requirements/session-ontology/WHAT.md) | CUT-04 | 07、12、13、34；scope、容器和身份不合并 |
| [speculative-investigation](../../requirements/speculative-investigation/WHAT.md) | CUT-29 | 10、13、19、28；只读估计与真实授权分开 |
| [sphinx-v2](../../requirements/sphinx-v2/WHAT.md) | CUT-31 | 09—14、19、34；目标/证据/资源/执行逐域承接 |
| [structured-workflow](../../requirements/structured-workflow/WHAT.md) | CUT-35 | 00、32；全部实现遵守原生控制流与显式组合 |
| [time-capability](../../requirements/time-capability/WHAT.md) | CUT-21 | 02、10、12、14；时间显式，不充当权限 |
| [verification-system](../../requirements/verification-system/WHAT.md) | CUT-32 | 00、33、37、38；验证过程失败与被测行为失败分开 |
| [work](../../requirements/work/WHAT.md) | CUT-01 | 04、06、09、11—17、23、35；生命周期已接回，不等于所有 work 证明完成 |
| [work-record](../../requirements/work-record/WHAT.md) | CUT-06 | 03、13、16、27；原始范围、三段记录和方向视图 |

新增需求包必须在本表落主责卡；没有匹配时先增卡或明确加入已存在卡，不随手塞进公共库。删包需要逐条处置原义务，不能靠目录消失让覆盖检查归零。

## 2. 主仓源码区域

基线下 `src/Wanxiangshu` 的每个生产顶层目录均列在这里。根目录 fsproj/compile-order 是构建声明，不另计为业务域。

| 实际源码区域 | 主责卡 | 预期去向 |
|---|---|---|
| [Ablation](../../src/Wanxiangshu/Ablation/) | CUT-37 | 明确保留或小型独立策略；无用开关删除须说明后果 |
| [Change](../../src/Wanxiangshu/Change/) | CUT-23 | 发布/委任职责与 Git 物理组合分开 |
| [Composition](../../src/Wanxiangshu/Composition/) | CUT-35 | 只保留真实 C；各领域状态回各 owner，逐族处理旧桥 |
| [Context](../../src/Wanxiangshu/Context/) | CUT-03 | Trace→03、Prefix→15、Companion 压缩→16、诊断指导→30，最后17组合 |
| [Enforcer](../../src/Wanxiangshu/Enforcer/) | CUT-30 | 先产品去向裁决；不因草稿删除 Rulebook/Guidance |
| [Execution](../../src/Wanxiangshu/Execution/) | CUT-12 | Session→04/12/21、Delegation→13、Failure→14、Fission→28、LoopDetector→30 |
| [Foundation](../../src/Wanxiangshu/Foundation/) | CUT-07 | 身份→07、能力→08、时间/等待→21、共通控制流→32/35；未使用具体工具可删，不整体迁成万能 Foundation |
| [Git](../../src/Wanxiangshu/Git/) | CUT-23 | Git/worktree 物理能力；持久同步边界与19共同确认 |
| [Host](../../src/Wanxiangshu/Host/) | CUT-34 | 真宿主契约/适配，compaction和结果预算分别向16/20/25归属 |
| [Interaction](../../src/Wanxiangshu/Interaction/) | CUT-09 | Authority→09、Attempt→07/10、Dispatch→11、Repair→14、Attention/Concern→22 |
| [Mission](../../src/Wanxiangshu/Mission/) | CUT-01 | 内核已迁；WorkRecord→06，应用接线→35，Plan P 文件动作→24/25 |
| [OpenCode](../../src/Wanxiangshu/OpenCode/) | CUT-34 | SDK/codec/signal 保适配；工具与能力→08/25，容量→10，投影→17，顶层编排→35 |
| [Participant](../../src/Wanxiangshu/Participant/) | CUT-07 | Persona→07，语言→05，Attempt→10/14，Projection→17 |
| [Persistence](../../src/Wanxiangshu/Persistence/) | CUT-19 | codec/事实校验→18，存储/收敛→19；兼容读取按真实数据义务保留 |
| [Process](../../src/Wanxiangshu/Process/) | CUT-02 | pipe→02，PTY/spool→20，time→21，JS sandbox→25 |
| [Repository](../../src/Wanxiangshu/Repository/) | CUT-24 | Programming 文件→24/编程面→25；Knowledge→27；Investigation→29 |
| [Requirement](../../src/Wanxiangshu/Requirement/) | CUT-26 | 可独立的需求发现、匹配与材料身份 |
| [Resources](../../src/Wanxiangshu/Resources/) | CUT-05 | 独立定位/语言/占位符，业务文本仍归各语义 owner |
| [Sphinx](../../src/Wanxiangshu/Sphinx/) | CUT-31 | 逐层盘点后定去向，保留原用户目标与证据合同 |
| [Strength](../../src/Wanxiangshu/Strength/) | CUT-29 | 估计、预算、Replica 和 Host 各自盘点，不批量退役 |
| [Verification](../../src/Wanxiangshu/Verification/) | CUT-32 | 只承接真实测试边界，不复制生产规则做 oracle |

非 `src` 资产同样有 owner：`resources/` 归 CUT-05 与其内容卡；`scripts/`、`.config/`、`Directory.Build.props`、`global.json` 归 CUT-32/33；`.github/`、`Dockerfile.ci` 归 CUT-33；`wanxiangli` gitlink 与 package 元数据归 CUT-33（原 `vendor/` 已退役）；`requirements/` 语义不迁成包内副本，逐卡更新映射；`proposals/` 与 README 归 CUT-00/38。仓主已改定 `wanxiangli/` 为主仓 submodule（gitlink 跟踪库 main），见 CUT-33。

## 3. 独立库目录和草稿

下面是 0.5.2 的盘点，不是对草稿中所有选择的背书。建板未修改独立库；实际接线证明以主仓当前导入和合并记录为准，而非库内过时的 TODO 句子。

| 独立库目录 | 当前材料 | 承接卡 |
|---|---|---|
| `src/lifecycle` | 已实现，公开 `work` | CUT-01 |
| `src/eventstore` | 已实现单流 SqliteJournal，公开 `eventstore` | CUT-01；新多 writer 义务另由19承接 |
| `src/assembly` | 已实现 DurableWork，公开 `durable-work` | CUT-01、35 |
| `src/process` | 已实现 ProcessSession，公开 `process`；主仓尚未接入 | CUT-02、20 |
| `src/trace` | 已实现 Trace，公开 `trace`；与主仓范围尚有差异 | CUT-03 |
| `src/session` | 已实现 Ownership，公开 `ownership`；不是完整 session host | CUT-04、12 |
| `src/authority` | 草稿，含与现行证据要求冲突的简化 | CUT-09 / D-01 |
| `src/capability` | 草稿，角色矩阵与生成形态未定 | CUT-08 |
| `src/compression` | 草稿，必须补 coverage/原文/checkpoint 等实际义务 | CUT-16 |
| `src/delegation` | 草稿，不能先删当前 handle/准入保证 | CUT-13 |
| `src/events` | 草稿，不能强行统一不同现行格式 | CUT-18 |
| `src/failure` | 草稿，决议与精确结算尚未实现 | CUT-14 |
| `src/host-omp` | 草稿，目标环境和可用能力尚未验证 | CUT-36，当前主线不依赖它 |
| `src/identity` | 草稿，不预建全集 ID 包 | CUT-07 |
| `src/prefix` | 草稿，需承接同 epoch/候选/四类冷边界 | CUT-15 |
| `src/resources` | 草稿，成对资源与实际包定位待实现 | CUT-05 |
| `src/time` | 草稿，注入字眼不豁免协作独立性 | CUT-21 |
| `src/workrecord` | 草稿，Trace.statement 不代替完整 LWR | CUT-06 |
| 根规则提及的 `src/tests` | 规划中的跨域测试位置，不能仅凭表项视为已有实现 | CUT-32/35；沿用当前真实测试位置，不强制迁目录 |

事件格式、identity、capacity 等新 API 不在本文提前钉死；只有实际消费者与反例确定边界后，才由对应卡冻结。

<a id="draft-conflicts"></a>
## 4. 开工前必须面对的差异

| 差异 | 已读材料支持的事实 | 施工时的明确处理 |
|---|---|---|
| 未知来源 | authority 草稿允许缺标记回退 human；主仓 authority 要求真实证据，Trace 已实现也要求明确来源 | D-01：不能照草稿先改行为；保持当前安全边界，逐条裁决 |
| 权威闭包 | authority/delegation 草稿拟删除多数 provenance、claim、permit；主仓有根独占、exact closure 和效果记账义务 | CUT-09/11/13 先承接完整后果，证伪无用机制后才删，不以术语简化代替证明 |
| Trace 范围 | 库单观察者总序与 `(after, through]`；主仓有并发捕获、推理、ProviderRetryAttempt 排除、Opening 区间和 Fission 合流 | CUT-03 在 owner 边界定义显式转换/扩展；消费者不算游标公式、不丢材料 |
| 进程能力 | 库 pipe 有界留尾、wait 超时不杀；主仓有硬期限、外存和持续终端合同 | CUT-02 由 C 保持原硬顶/外存义务，CUT-20 独立完成 PTY/spool，不用删功能换接线 |
| 存储模型 | Work/Trace/Ownership 的专用顺序记录与主仓多 writer 集合合并不是同一数据模型 | CUT-18/19 分别冻结规范；不机械要求统一 Envelope 或全部转 SQLite |
| 工作记录 | 库 statement 是最后完成助手 text 视图；主仓 LWR 有 Invocation、Opening、Chronicle/Recent 分界和方向规则 | CUT-06 单独完成，不把已有 helper 当全 LWR |
| “不再建”的功能 | 新库总规则暂不建设 knowledge/strength/sphinx/repository 等；主仓仍有对应 WHAT 和消费者 | CUT-24—31 先说明收益与去向；未裁决保持功能，不默认删除 |
| 身份体系 | identity 草稿是极简不透明标识与角色绑定，主仓还有 versioned provenance 与 resolve-once identity | CUT-07 逐项保留真实所需信息，不把五元组压成裸 role 后宣称等价 |
| 旧历史 | CUT-01 已明确新 work 不翻译旧生命周期记录；其他域仍可能有真实存量/只读兼容需求 | 每卡单独盘点格式与在途状态；不得扩大 clean-break 豁免 |
| 验收与合并 | 5039 unit pass 等为当时已验证基线；verify 仍拒绝待证明项，用户允许净改进合并 | 不改验收器；新卡依当前候选证据做比较，不机械复用旧数字 |

这些差异已经列出，但尚未替未来卡作出全部产品裁决。基线支持不到的细节要调查，不用本文的提议覆盖正式合同。

<a id="previous-plans"></a>
## 5. 原施工计划如何接续

本蓝图是**切刀的唯一认领入口**；[原 TODO 总计划](../TODO施工总计划-2026-10-03.md)、[原分册](../TODO施工分册-2026-10-03/)、[GAP 台账](../../requirements/GAP.md) 继续保存证明义务和历史证据。原编号不批量改写，原在制工作不默认失效。协调者先问清原 owner 的分支和已验证范围，再合并成一份新认领。

| 原工作族 / 材料 | 新接手卡 | 不得借机丢掉的部分 |
|---|---|---|
| Journal J3、G0/G1、H0b 本地提交/能力 | CUT-18、19 | 真 Append/Unknown、release/fatal 同 incident、合法多 writer、fresh authority 的实际证据 |
| K1 Casebook、K2/D0 删除与 Bookkeeper | CUT-27、12、35 | 精确 Store/工作区别名、共享 flight、删除真实调用者和 independently cold 证明 |
| N00 运行器、W1 Node/npm 归档 | CUT-32、33 | 原预算、文件完整排空、子进程物理回收、原始异常与成本 |
| N04 固定/只读输入、SDK 与工具链材料 | CUT-32 | 平台限制、FD/目录身份/替换反例、未完成真实 readonly 证明 |
| Sphinx A2/B4/H0、真实 Host 接手 | CUT-31、34 | 目标/证据/预算/真实答案，不能把纯状态测试当实际 provider 执行 |
| WP-023 性能、WP-042 手动 envelope、规则资源材料 | CUT-37、30、33 | 同环境因果证据，不自动更新 envelope 或缩验证预算 |
| 根 [PLAN.md](../../PLAN.md) 的历史 Manager/Plan 重组材料 | CUT-01、06、17、35 | 以当前 work WHAT/cutover 为准；历史 G6 EventStore 方案不再指挥重写新内核 |
| [lifecycle cutover 收尾](../lifecycle-cutover-closeout.md) | CUT-01、04、11、12、32、33、35 | 已闭合缺陷不重做，仍开放 Host 在途/Long Stroke/平台证明不冒称关闭 |

每个旧 T/WP/GAP 若未在上表直接点名，按其正式需求包在第1节找到主责卡，再在该卡证据索引逐项挂接。**本表不是把旧全部 TODO 重新排成同等大小的刀，也不能用测试 TODO 数减法计算完成度。**

## 6. 完成盘点时的逐文件账

CUT-38 不能只跑 grep 验证旧符号为零。每个实际删除/保留文件都要留下：原 owner、承接卡、新公开能力或明确保留位置、剩余调用者、读取旧格式的义务、该条删除/保留的证据。grep 零命中只是辅助，动态注册、反射、包 exports、持久数据和宿主入口另查。

新增源码域、资源族、工具入口或需求包，在所属卡 PR 中同步更新本表。没有归属就不进入最终候选；需要暂留就如实写暂留，别把它算成“已经抽干”。
