# N06-B1：actual Host 接线的下一有限工作包（只读接手卡）

2026-10-06，下一产品施工入口；本卡B1-H0尚未实施或验收。前置A2的R0/R1/R2、本地Fork资源与相邻Unknown Root清理已取得gen184相关246/246、1290/0有限证据，28skip/137TODO仍保留，见[正式记录](../archive/2026-10-06/Sphinx真实恢复与资源前置-2026-10-06.md)。本卡不得把源码已出现的方法写成Sphinx已具备公开执行或首个答案。

## 1. 已有可复用的真实 owner

- `src/Wanxiangshu/OpenCode/Plugin/PluginSessionWiring.fs:attach` 在同一 durable AgentJournal 上构造原 `PromptDispatcher`、`DelegationHandoffLedger.port`、`SyncDelegateRuntime`，并 `scope.AttachSyncDelegateRuntime`。它供应真实 Sessions、family root、标准 Engineer tool map、原 work-record、provider retry。复用该实例，不另造第二业务 Runtime。
- `src/Wanxiangshu/Execution/Delegation/SyncDelegate/Runtime.fsi:InvokeObservedPrepared(ownerSessionId:SessionId,charge:string,prepareProviderPrompt:unit->Task<LlmFacing.Document>,?isCancelled:unit->bool)` 返回 `SyncDelegateObservedExecution`。
- `Model.fsi` 中 `Admission` 是实际 `Accepted | NotDispatched | Refused | Unconfirmed`；Accepted 保存原 `SessionId/PromptKey/HostOutcome` 与真正 `PhysicalUserMessageId/AuthorityRootUserMessageId`。Completion 保存 actual session、physical、root、provider run 和 formal text。receipt、physical 与 provider run不是一种 ID。该 API 固定 Engineer。
- 原 Runtime 通过 `sessions.FamilyRootOf owner` 观察 family children、`CreateChildSession(owner,...)` 取得真实 child；fresh root seed来自实际 owner's active durable profile，复用 child走正式 ManagedDelegationAssignment continuation。终端订阅与恢复 successor 归属由原 owner处理。
- `src/Wanxiangshu/OpenCode/Plugin/PluginHostWiring.fs:Host.SnapshotOpt` 可提供真实 `ISessionSnapshotPort.GetMessages`；`Host/SessionSnapshot.fsi` 的 message/provider-parent/PromptKey/tool-part 字段和 `locateToolCall` 的 Missing/Ambiguous拒绝可以复用，不能从 idle 推答案。
- `Sphinx/V2/Composition/Bind` 与 `Persistence/Integrator` 已是共享 canonical store / 唯一 Current，不重新扫描或折叠历史。

## 2. 现有 Adapter 的真实缺口

`Sphinx/V2/Hosts/OpenCode/Adapter.fs` 当前只持 `ISessionHostPort`，以 `SessionId.create(InquiryId.value inquiryId)` 猜 owner；CreateSiblingSession 后直接 SendPrompt，没有其自己持有的原 terminal subscription；将 publicEnvelope 与 privateTicket拼接给 worker；Agent/Metadata/Tools均 None。ReadStatus是Unknown、ReadResult/Reconcile具名拒绝。`Hosts/OpenCode/Surface.capabilities` 只测试这些声明。

`Sphinx/V2/Hosts/Provider/Adapter.fs` 接纳 receipt后用 `HostForkRunLifecycleSurface.create(box receipt)` 新造一个与 actual execution不相连的 pending cell；不能作为真正完成 observer。两条死路径应在其接线包一起清理，而不是保留成 fallback。

全仓未有叫 `DispatchAdapter` / `ExecutionObserver` 的已实现模块；04分册这些词是职责规划。也没有生产 Sphinx工具调用原 SyncDelegate observed API的消费者。StaticTools的 sphinx权限名称不等于工具注册。Commands现在只持Store/AuthorizedStartConfiguration；createdBy/authorizationRef/configHash均不是物理 owner SessionId。work_next/work_submit仍 unsupported。

## 3. 推荐下一提交：B1-H0，只闭真实 owner 与 observed execution 交接

先冻结接口，再改 Adapter及调用方；不把B1-H0叫完整034或首个答案。

输入必须包含：

1. 原公开 Host入口的实际 `HostToolContext.SessionId` 经现成 ingress核验取得typed SessionId，或composition明确注入的等价真实owner能力。它与 InquiryId故意不同；独立MCP没能力就具名拒绝，不加虚构owner默认。
2. 从唯一 accepted Current读到的 exact DispatchRecord.Request：inquiry/work/attempt/fence/intent、公有envelope、Host-only ticket、当前预留。查询无current/cut/fork/未知work一律拒绝。
3. 真实并已验证的 schema/profile/prompt准备能力；本包若只验证 Adapter就显式供应合法已持久请求，不能靠公开 Driver的空模板造请求。
4. 原 PluginScope持有的 SyncDelegateRuntime及其原journal；同canonical文件但另实例不冒称相同dispatcher。effect lifetime归原Plugin owner，普通返回不dispose共享Runtime。

调用链：原Host实际context → 共享Commands/effect owner只读Current → 持久BudgetReserved+DispatchRequested（已有B0）→ 原SyncDelegateRuntime.InvokeObservedPrepared(actualOwner,actualCharge,public-only LlmFacing.Document) → actual Admission/Completion → canonical batch保存真实binding。PrivateTicket不进入Document；同intent已存在或结局unknown不能无证据再次Invoke。

**两道门的时点必须分开。** 原 `Workflow.acquireAndRun` 先 `Attached.GetOrCreate`，可能已实际创建 child，之后才执行 `PrepareProviderPrompt`；最终发送阶段才核 owner seed。因此实际owner核验、accepted intent与其预留检查必须在 `InvokeObservedPrepared` **之前**完成，不能藏进prompt producer。最终提示快照及intent→actual child/PromptKey的可await持久关联则属于最终render后、SendPrompt前的第二道门；unit诊断callback不能替代它。源码核对见[最小接缝审计](../archive/2026-10-06/sphinx-recovery-r0-r1/vibe-fs-b1-h0-next-audit-20261006.txt)，尚未实现或正式验收。

先定义有限的 native receipt schema文档与真实hash。Binding载荷精确保 actual childSessionId、PromptKey、原闭合HostOutcome、actual physical/root。`PhysicalRef`采用已定义的实际物理定位身份，不能填transport receipt或InquiryId。ProviderRun只有Completion取得后才能写，不能从receipt造。Accepted正常路由可保存Receipt/Running；Unconfirmed不得包装为已接纳或failed，预留仍保留。

建议局部文件边界：

- 单人 owns `Hosts/OpenCode/Adapter.fs/.fsi`、`Hosts/Provider/Adapter.fs/.fsi` 与必要的共同 execution contract；如果旧ProviderAdapter仍无消费者，按实际注册图退出，不能恢复未关联cell。
- 同人或串行交接 owns `Composition/Commands.fs/.fsi`、`Runtime/Ports.fs/.fsi` 的真实执行能力与receipt映射。原store/Current不增副本。
- 最后串行 wiring `OpenCode/Plugin` 的实际工具factory/Scope依赖及Sphinx owner shard。公共contract先冻结；实际工具注册文件需按现 registry确认，而非凭StaticTools猜。
- 测试用窄 `Hosts/OpenCode/Surface.fs/.fsi` 只装配原owner、调用实际Adapter；`requirements/sphinx-v2/tests/034.test.mjs`只用WHAT034；effect前append拒绝归010，MCP/JS共同调用归036，各文件唯一对应WHAT。

正式正反验收：

- 正控：实际owner与InquiryId不同、owner自己为nested session；ListChildren用其真实family root，CreateChild的parent仍是该nested owner，不能把二者混同。原Host观察Engineer、model=None、PromptKey metadata、原订阅在send前；合法call实际physical与formal terminal完成，binding原字节cold reopen可读。
- 反控：owner缺失/无active authority/wrong owner在claim和Host前拒绝；private ticket含独特秘密字节，实际Host最终prompt完全不含该字节；不能只测独立formatter。
- 原生产反例：旧Adapter确实以InquiryId当owner、拼接private ticket；新thin fixture必须调用旧production path取得业务失败。仅缺新API/import报错是能力证据，不能记业务红。
- Native Submitted receipt仅作transport证据；Unknown保actual key/Pending且不再send；NotDispatched/Refused/Unconfirmed/Accepted分型，无假physical。receipt落盘拒绝保原intent且不再次创建/发送。
- status/export在同handle有真正dispatch与正向模型观察后，查询前后journal/Hostsend/lease/model次数均不变；不是用永远零counter自证。
- 两次复用同delegate，原A terminal/old恢复successor不能完成B，B自身actual successor仍可完成；继承本轮A2生产回归，不重新用scriptRetry冒充。

停止条件：没有真实 owner、profile/schema、原runtime/Host能力就具体拒绝；新接口只有声明不能把034 TODO删掉；受控端口和实际安装版Host分别结算。B1-H0可先只验证fresh正常binding与Unknown不重发，不宣称完整cancel/receipt-loss recovery。

夹具先补真正的Host recording port：现SyncDelegate Surface只公开key/origin，未公开最终prompt/options，而且明确拒绝CreateSiblingSession。直接叠用它会让旧Adapter先因夹具拒绝而停下，取不到owner/private-ticket业务红。新窄装配须让旧Sibling路径和新实际runtime路径均能运行，捕获真实最终发送、parent与订阅，不复制PromptKey或acceptance算法。034最小闭环为两个旧业务红（nested owner、private ticket）及Submitted→真实managed ingress→exact terminal→cold binding正控；010另证真实append拒绝时Create/Send都为零，并用成功append使计数非零。Unknown/丢receipt必须保守零再发；实际lookup尚无时具体报告RecoveryIncomplete，完整恢复仍待第二道门。

## 4. 现 Observed API 尚不足的两个具体前置

### 4.1 effect前持久关联与丢receipt对账

Observed Admission在send/physical后结算，没有“effect前可await的 Sphinx intent→actual child+PromptKey”的公开钩子。原`onSendObserved`是内部unit观察，不能当异步durable barrier。`onDelegatePrompt`是诊断callback，也不是持久提交合同。

因此Current.Request存在且Receipt=None不能判断Host nevercalled。原Recovery.DispatchPending枚举只是描述，不是实际证据；重开一律先RecoveryIncomplete/对账、零再发，不能套MaySpend=true。完整C包须由原Dispatcher/Sync owner提供exact intent关联与before-effect提交/receipt恢复合同；不得另设globalregistry、全扫session/Journal、按最新child/receipt推归属。

### 4.2 真实模型可见字节

Runtime.fs `sendDelegatePromptCore` 会在原prepareProviderPrompt之后，执行 `DelegationHandoff.appendParentDelta(request.ProviderPrompt,prepared.ParentRecord)` 再 `LlmFacing.render`。因此只hashpublicEnvelope的JSON或pre-handoff Document，不能证明 ContextSnapshot.ModelVisibleBytesHash覆盖实际Host prompt。

要先定由原最终send owner冻结/返回实际字节与同一次handoff证据的接口，或将该真实preparation阶段纳入本包同owner。不能重调用 handoff.Prepare/读新parent head后手工重建“应当发送的文本”。正式022可见输入回归与023 worker-private隔离回归应捕获 actual SendPrompt最终字节、parent delta前后区分、private ticket隔离与冷重开snapshot；每个文件只锚自己的现行WHAT，不沿Context.fs旧006注释编号。ModelVisibleBytesHash的计算对象来自该字段现有公开类型说明，不在卡里增添新评分或blinding义务；本包没有接口时留该前提未完成。

## 5. B1之后的最短首答案链

- B2-P0：只选一个有限真实可执行profile，冻结canonical schema文档/ImplementationHash/ABI/prompt版本、原Goal正文与可核对material内容、授权/实际资源、Delegated模式。现Registry是锁定工具，Inquiry Plan/Render是validator，没有可用的完整profile实现。原PluginContext只带GoalRevision但不带Goal正文，不能只给空Graph让插件猜任务。规划含真实成本的answer.now，Unestimated保持缺值。
- B3-C0：共享Commands先定WHAT012 `ticketHash/scopeId` 的真实来源和公开dto/持久匹配（现WorkSubmitArgs缺这两项），再接claim、submit、唯一driver的合法有限步骤；BudgetReserved+DispatchRequested同batch提交后才effect，拒绝零Host。Driver `Hash="empty"`、`plugin="unbound"`和空interpretation不得进入该路径。
- B4-O0：actual Completion/HostSnapshot核exactchild+physical+providerRun+worktoken+attempt+schema；原canonical结果先保存ResultAccepted+InterpretationPending，locked Observe再产生真实delta并完成pending。Core目前 InterpretationApplied/Failed no-op须先修真正转移，失败仅重解释，不另买响应。无usage保留预留，不填零。
- B5/B6：真实renderer work结果accepted后由已完成Core守门提交exact resultObservationId的AnswerCommitted，公开取实际正文；017从真正start/claim/submit入口闭环，036跨SDK/JS+新OS进程cold reopen。至少两个不同原目标，不直接append答案/固定文本冒充profile。全链取消/late usage/乱序/receipt-loss恢复另按C/D完成，不升级有限证明。

## 6. 当前状态与认领边界

04、07与总计划已同步本轮真实R0/R2 red/green、lease/Fork及Unknown Root相邻清理的有限证据。D0/D1和R0/R1/R2不重复认领；07保留实施顺序作为回归依据。下一产品入口是本卡B1-H0；没有effect前持久关联、最终可见字节与真实Host binding证据，不能提前关闭整个B1/GAP。
