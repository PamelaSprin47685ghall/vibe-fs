# N06-B1：真实 Host 接手与首次接纳施工卡

2026-10-06，B1-H0a 的原 owner、公有提示和 observed execution 已有限验收；下一主线是 **H0b 的 canonical 原子首次接纳能力**，随后接 effect 前持久绑定。本卡优先于历史 H0 顺序。前置 A2 不重复施工；完整034、公开执行与首个答案仍未完成。

## 0. 已完成与下一认领

H0a 先取得旧 Adapter 的三个正式业务红：InquiryId 被当作 owner、private label map 到达实际 SendPrompt、无 active authority 的 owner 创建并发送。现在借用原 SyncDelegateRuntime，在 Invoke 前核验实际 owner，只送 public instruction；返回原 Admission/Completion。独立 Provider 假完成 cell 与无人消费的 receipt/status facade 已退出。受控删除前置检查仍会抓到实际 child 创建，拒绝 oracle 在 admission 后取证。

gen190 完整 Sphinx+delegation 为52/52排空、328pass/0fail/35TODO；全 Fable/check/Fantomas 通过。原红、变异、freshness 与有限边界见[源验收收据](../archive/2026-10-06/sphinx-host-owner/source-receipt.txt)。这是调用原生产运行时的受控 Host port，尚无 installed Host、canonical receipt 落盘或公开 Sphinx 工具链证据；034 TODO 保留。

**H0b 先定存储合同，不能直接给 Adapter 加锁取绿。** `Append Ok` 且 `Cuts=[]` 也可能表示重复命令或 no-op，并不授予新的模型调用。Append 内持锁却不刷新 Current；现 RefreshCurrent 是完整读取/校验/replay，不能偷偷塞进每次 guarded append 违反 durable-events017 的成本合同。[深入接手卡](../archive/2026-10-06/sphinx-host-owner/vibe-fs-h0b-store-owned-next-card-20261006.txt)取代[初次审计](../archive/2026-10-06/sphinx-host-owner/vibe-fs-b1-h0b-fresh-capability-audit-20261006.txt)的施工顺序，并保留二者源码身份。

先做两项独立前置，再开放首次派发：

- **U0-JC：Snapshot 编译边界有限验收完成。** 原plan排除红与原实际flat Fable边界红→native5/0；gen206正式7/7、37/0、4skip/4TODO。只提取原Snapshot.fs/fsi project，依Identity/Message/OpencodeTypes，恢复66→51；完整HostPort保原private accepted witness，不改源/API或66上限。89a CI暴露Host026旧直接归属断言，本轮SW012另暴露旧HostPort inclusion；两者按实际Snapshot边界修正，gen210产品176/176、1096/0，原pending保留。022独立真实flat六叶加父7/0，unit integration仍skip；见[新记录](../archive/2026-10-06/Journal合同与初次捕获-2026-10-06.md)。
- **U0-J0-L0：Journal结果类型归属有限验收完成。** 四个原类型与六段原诊断移至Persistence/Journal/Outcome，Foundation.Outcome只留中性运行结果；纯合同仅依Identity与残余Foundation Outcome。51源/31工程原子迁引用，case顺序、payload、公开分类及canonical codec未改，未留旧alias。Journal闭包7fs，Snapshot5、recovery51、完整Host39，各ratchet不变；gen210正式产品绿及022真实正反编译见本批记录。此项只是合法归属，不实施Release/Commit新case、不授fresh派发权。
- **U0-C0：下一有限前置，尚未实施。** 按[纯append-result接手卡](../archive/2026-10-06/journal-outcome-contract/vibe-fs-u0-c0-append-result-locality-card-20261006.txt)，只改三个工程声明：原StoreTypes.fs/fsi唯一编译owner改为纯结果工程，原Port/Handle仍归原port合同；清理Model与Port未使用的Foundation Outcome引用。F#源、namespace、API、compile-order及19个port消费者不改，不能先添加尚未使用的Journal→新结果边。先补022实际plan排除反例，再用同一真实IEventStore probe证明原Port能编译而纯结果不能，另证明纯结果正控；要求真实flat Fable及相邻023/028、公开007/008分类回归。只读模拟Model7→3、Port10→6不是验收证书。此包闭合后才进入下面U0-A0类型与Store/consumer原子迁移。
- **U0 原生 Release 两业务红已取得，仍未实施 typed outcome。** 原Node22/原Store下，fresh先证明真实append/fsync、完整字节、live Current/head及独立cold；exact duplicate则证明零新append/fsync且字节原封。两者都原生删除实际own lock后抛同一Error，旧Task直接reject，typed settlement oracle失败；normal正控通过。[原始收据](../archive/2026-10-06/sphinx-u0-preconditions/vibe-fs-u0-native-release-receipt-20261006.txt)及source/dist SHA保完整身份。这是native pending证据，未注册正式006，不冒称formal green。新[合同卡](../archive/2026-10-06/sphinx-u0-preconditions/vibe-fs-u0-contract-red-audit-20261006.txt)明确 fresh Unknown、duplicate/no-op无新写释放失败、原StorageInvalid与Release双因果、exact request/prepared/phase/cause；先完成JC，再原子迁Store与实际consumer。fsync/CurrentCommit/双故障/坏尾行和全部consumer仍待正式红绿。
- **U0：真实提交与释放后的 Unknown，待完整实现。** 物理 append 已开始后，Current commit 或 native Release 出错都不能称未写入。catch 必须覆盖完整锁任务，保原 request、prepared envelopes、失败阶段与 cause。Release两项native业务红见上项；append/fsync/Commit及双因果仍待正式证明。[历史只读接手卡](../archive/2026-10-06/sphinx-host-owner/vibe-fs-u0-unknown-contract-next-20261006.txt)保当时源码因果，现实施依据由新合同卡和JC卡接续。全部 AppendError 消费者须按类型迁归，不能把Store局部新case再字符串化后记闭合。duplicate/no-op释放失败不得猜新写未知或旧事实未提交，RuntimeStarted未知也不能误称同次business已尝试；generic锁helper仍有payload/Git消费者，保持边界。此包不提供首次派发许可。
- **017-I0：已激活 append 的 I/O，有限验收完成。** 正式017新增8/128条真实历史、独立meter/cold进程，原activation读取正控后清零；正常append零旧事件内容读取、零Git访问/子进程，真实新writer写入252字节且fsync，完整事件/head与cold链相等。原gen197 native4/0；手工在原Append加ReloadLocal，gen198正式2/2失败，写入及cold正控在成本断言之前仍成立；已逐字节还原，gen199/200正式正常4/0，最终输入收据见本次合并记录。[原记录](../archive/2026-10-06/sphinx-host-owner/append-io/)保实际PID与计数。此包不证明内存中的历史fold、payload成本或完整首次接纳；WHAT017整体不关闭。
- **017-I1：历史fold成本仍待证明。** 沿原canonical积分规则取得真实、可归因的成本观察；没有公开观察能力前不能用自建fold计数、wall阈值、源码token或未捕获调用的“零”补证。不得把I0升级为不重fold的证明。
- **fresh authority。** 优先考虑锁内拒绝 stale、锁外正规刷新，但仍须把唯一 Current 与已验证 physical read-set/retention cut 绑定。现 Git/stat cache 没有这枚能力；metadata 相同只可作线索，不能授予许可。缺 witness 时保持具体拒绝，不以第二 registry 补洞。

旧 unified-store-gate 对 ReloadLocal 直接调用 replay 的源码形状检查不是产品前提。若改 canonical 刷新，须以所有 production Current、heads、accepted trace 的冷重放等价及 retention/cut 反例替换该旧证明；不单删 gate，也不为了 regex 另造 API。

按以下顺序认领：

1. 先完成C0，再认领U0-A0：冻结exact request/prepared envelopes/物理失败阶段/原cause合同；先将现有Release两native业务红注册正式006，再补append/fsync/CurrentCommit/StorageInvalid+Release双故障。随后同包迁原Store、writer、AgentJournal和实际消费者，不能把新typed cause又字符串化。fresh unknown、duplicate/no-op无新写释放失败及RuntimeStarted未知分别裁决；含semantic cuts的Unknown须交原fatal owner，不沿旧WriteUnknown丢cut。该批不接Host effect。
2. U0有限验收后，原EventStore owner冻结“持实际 canonical 锁读新事实→纯准入→append→发布唯一 Current→返回确属本次 fresh acceptance”的合同。exact duplicate 独立返回，不能给它新的 spend witness。不同 handle、不同 OS process 必须共享同一所有权，Adapter-local semaphore 不足。不能跳过C0/U0或把本轮类型迁归当作首次接纳完成。
3. 明确新字节增量与检查点前提，沿原 canonical storage 实现；不另建 Sphinx log/registry、不在每次派发全扫历史、不改变既有 fork/cut/idempotence。
4. 先在010用两个真实 writer、再用两个 OS process 的 readiness barrier 证明：同 intent 不同 command 只能一次 fresh；exact replay、stale handle、CommitUnknown、cut 与冷 Pending 均零 Create/Send。有成功实际调用的非零正控。
5. 原锁释放后才调用 Host。Host pending 时另一 writer 的合法 append 必须仍可完成，不把磁盘锁持到网络/terminal。
6. 有 fresh witness 后，composition 才能在 Invoke 前核对实际 owner、exact Request、当前预留与请求身份。既有 Request 且 Receipt=None 具体报 RecoveryIncomplete，零盲重发。
7. 另定第二道门：原最终 handoff/render 之后，SendPrompt 之前，可 await 保存 exact child、PromptKey、最终 UTF-8 字节与同次 handoff 证据。unit 诊断 callback 不充当 durable barrier。
8. actual Admission 才映射 receipt；其落盘失败保留已发生的物理证据，不改称 NotDispatched、不另发送。Running 需要真实相应事实，不能由 receipt 名称隐式推出。
9. 每个子包独立红绿、正式 requirement 验收并更新状态；两道门、installed Host 与完整034分别记账。不能用 H0a 证书关闭它们。

纯 Interpretation 状态账 B4-O0a 和原Registry有限守门 B2-P0a 已分别验收；[交付收据](../archive/2026-10-06/sphinx-interpretation/final-receipt.txt)保存最终静态收束与输入。真实 Observe/模型执行仍依上述 H0b→B2→B3→B4 顺序接通。

## 1. 已有可复用的真实 owner

- `src/Wanxiangshu/OpenCode/Plugin/PluginSessionWiring.fs:attach` 在同一 durable AgentJournal 上构造原 `PromptDispatcher`、`DelegationHandoffLedger.port`、`SyncDelegateRuntime`，并 `scope.AttachSyncDelegateRuntime`。它供应真实 Sessions、family root、标准 Engineer tool map、原 work-record、provider retry。复用该实例，不另造第二业务 Runtime。
- `src/Wanxiangshu/Execution/Delegation/SyncDelegate/Runtime.fsi:InvokeObservedPrepared(ownerSessionId:SessionId,charge:string,prepareProviderPrompt:unit->Task<LlmFacing.Document>,?isCancelled:unit->bool)` 返回 `SyncDelegateObservedExecution`。
- `Model.fsi` 中 `Admission` 是实际 `Accepted | NotDispatched | Refused | Unconfirmed`；Accepted 保存原 `SessionId/PromptKey/HostOutcome` 与真正 `PhysicalUserMessageId/AuthorityRootUserMessageId`。Completion 保存 actual session、physical、root、provider run 和 formal text。receipt、physical 与 provider run不是一种 ID。该 API 固定 Engineer。
- 原 Runtime 通过 `sessions.FamilyRootOf owner` 观察 family children、`CreateChildSession(owner,...)` 取得真实 child；fresh root seed来自实际 owner's active durable profile，复用 child走正式 ManagedDelegationAssignment continuation。终端订阅与恢复 successor 归属由原 owner处理。
- `src/Wanxiangshu/OpenCode/Plugin/PluginHostWiring.fs:Host.SnapshotOpt` 可提供真实 `ISessionSnapshotPort.GetMessages`；`Host/SessionSnapshot.fsi` 的 message/provider-parent/PromptKey/tool-part 字段和 `locateToolCall` 的 Missing/Ambiguous拒绝可以复用，不能从 idle 推答案。
- `Sphinx/V2/Composition/Bind` 与 `Persistence/Integrator` 已是共享 canonical store / 唯一 Current，不重新扫描或折叠历史。

## 2. 已退出的旧 Adapter 与仍缺的公开入口

旧 Adapter 从 InquiryId 猜 owner、直接 Sibling SendPrompt、拼接 privateTicket 的路径已删除。当前 OpenCodeHostPort 内部接原 SyncDelegateRuntime 与 typed owner/DispatchRequestedBody，能力只声明 dispatch；034通过实际 recording/managed ingress/exact terminal 验证交接，不再只有能力声明。

旧 Hosts/Provider/Adapter 的无关联 pending cell 已按无消费者的注册图移除；原 Plugins.ProviderAdapter 用量分类与 Fork lifecycle owner 保留，各自承担原职责。

全仓未有叫 `DispatchAdapter` / `ExecutionObserver` 的已实现模块；04分册这些词是职责规划。也没有生产 Sphinx工具调用原 SyncDelegate observed API的消费者。StaticTools的 sphinx权限名称不等于工具注册。Commands现在只持Store/AuthorizedStartConfiguration；createdBy/authorizationRef/configHash均不是物理 owner SessionId。work_next/work_submit仍 unsupported。

## 3. H0a 的有限合同与 H0b 后续接线

以下是 H0b 后续生产入口的完整合同；H0a 只证明原 owner/public prompt/observed 交接，不含已落盘 DispatchRecord 的新派发许可。先冻结接口，再改调用方；不把有限交接叫完整034或首个答案。

输入必须包含：

1. 原公开 Host入口的实际 `HostToolContext.SessionId` 经现成 ingress核验取得typed SessionId，或composition明确注入的等价真实owner能力。它与 InquiryId故意不同；独立MCP没能力就具名拒绝，不加虚构owner默认。
2. 从唯一 accepted Current读到的 exact DispatchRecord.Request：inquiry/work/attempt/fence/intent、公有envelope、Host-only ticket、当前预留。查询无current/cut/fork/未知work一律拒绝。
3. 真实并已验证的 schema/profile/prompt准备能力；本包若只验证 Adapter就显式供应合法已持久请求，不能靠公开 Driver的空模板造请求。
4. 原 PluginScope持有的 SyncDelegateRuntime及其原journal；同canonical文件但另实例不冒称相同dispatcher。effect lifetime归原Plugin owner，普通返回不dispose共享Runtime。

调用链：原Host实际context → 共享Commands/effect owner只读Current → 持久BudgetReserved+DispatchRequested（已有B0）→ 原SyncDelegateRuntime.InvokeObservedPrepared(actualOwner,actualCharge,public-only LlmFacing.Document) → actual Admission/Completion → canonical batch保存真实binding。PrivateTicket不进入Document；同intent已存在或结局unknown不能无证据再次Invoke。

**两道门的时点必须分开。** 原 `Workflow.acquireAndRun` 先 `Attached.GetOrCreate`，可能已实际创建 child，之后才执行 `PrepareProviderPrompt`；最终发送阶段才核 owner seed。因此实际owner核验、accepted intent与其预留检查必须在 `InvokeObservedPrepared` **之前**完成，不能藏进prompt producer。最终提示快照及intent→actual child/PromptKey的可await持久关联则属于最终render后、SendPrompt前的第二道门；unit诊断callback不能替代它。源码核对见[最小接缝审计](../archive/2026-10-06/sphinx-recovery-r0-r1/vibe-fs-b1-h0-next-audit-20261006.txt)，尚未实现或正式验收。

先定义有限的 native receipt schema文档与真实hash。Binding载荷精确保 actual childSessionId、PromptKey、原闭合HostOutcome、actual physical/root。`PhysicalRef`采用已定义的实际物理定位身份，不能填transport receipt或InquiryId。ProviderRun只有Completion取得后才能写，不能从receipt造。Accepted正常路由可保存Receipt/Running；Unconfirmed不得包装为已接纳或failed，预留仍保留。

建议局部文件边界：

- 单人 owns `Hosts/OpenCode/Adapter.fs/.fsi` 与必要的共同 execution contract；旧 `Hosts/Provider/Adapter` 已按无消费者注册图退出，不能恢复未关联cell。
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

停止条件：没有真实 owner、profile/schema、原runtime/Host能力就具体拒绝；新接口只有声明不能把034 TODO删掉；受控端口和实际安装版Host分别结算。H0a 尚未保存 canonical binding，同 intent/冷重开零再发由 H0b 新入口正式证明，不能由一次 Unconfirmed 的单次调用推得。

H0a 已补原 SyncDelegate 的窄 Host recording 装配：旧 Sibling 路径允许运行，记录原最终发送、parent、metadata 与实际订阅数，未复制 PromptKey/acceptance 算法。023证实际公私分离；034证 nested owner、未授权零effect、Submitted 等待真实 managed ingress、exact terminal 与三类非接纳证据。cold canonical binding、010实际 append 拒绝零effect、Unknown/丢 receipt 零再发仍待 H0b，不升级现有单次 fixture。

## 4. 现 Observed API 尚不足的两个具体前置

### 4.1 effect前持久关联与丢receipt对账

Observed Admission在send/physical后结算，没有“effect前可await的 Sphinx intent→actual child+PromptKey”的公开钩子。原`onSendObserved`是内部unit观察，不能当异步durable barrier。`onDelegatePrompt`是诊断callback，也不是持久提交合同。

因此Current.Request存在且Receipt=None不能判断Host nevercalled。原Recovery.DispatchPending枚举只是描述，不是实际证据；重开一律先RecoveryIncomplete/对账、零再发，不能套MaySpend=true。完整C包须由原Dispatcher/Sync owner提供exact intent关联与before-effect提交/receipt恢复合同；不得另设globalregistry、全扫session/Journal、按最新child/receipt推归属。

### 4.2 真实模型可见字节

Runtime.fs `sendDelegatePromptCore` 会在原prepareProviderPrompt之后，执行 `DelegationHandoff.appendParentDelta(request.ProviderPrompt,prepared.ParentRecord)` 再 `LlmFacing.render`。因此只hashpublicEnvelope的JSON或pre-handoff Document，不能证明 ContextSnapshot.ModelVisibleBytesHash覆盖实际Host prompt。

要先定由原最终send owner冻结/返回实际字节与同一次handoff证据的接口，或将该真实preparation阶段纳入本包同owner。不能重调用 handoff.Prepare/读新parent head后手工重建“应当发送的文本”。正式022可见输入回归与023 worker-private隔离回归应捕获 actual SendPrompt最终字节、parent delta前后区分、private ticket隔离与冷重开snapshot；每个文件只锚自己的现行WHAT，不沿Context.fs旧006注释编号。ModelVisibleBytesHash的计算对象来自该字段现有公开类型说明，不在卡里增添新评分或blinding义务；本包没有接口时留该前提未完成。

## 5. B1之后的最短首答案链

- B2-P0：P0a只修原Registry枚举/完整manifest一致性，不代表真实profile。P0b先冻结原Goal/constraints/material内容的纯上下文和durable lock；再选有限真实可执行profile，核canonical schema文档/ImplementationHash/ABI/prompt、授权/实际资源、Delegated模式。Plan/Render仍是validator，现PluginContext只带GoalRevision，不能给空Graph让插件猜任务。规划含真实成本的answer.now，Unestimated保持缺值。
- B3-C0：共享Commands先定WHAT012 `ticketHash/scopeId` 的真实来源和公开dto/持久匹配（现WorkSubmitArgs缺这两项），再接claim、submit、唯一driver的合法有限步骤；BudgetReserved+DispatchRequested同batch提交后才effect，拒绝零Host。旧unbound/empty解释合成已退出；原派发模板 `Hash="empty"` 仍须接真实schema/profile，不得进入公开effect路径。
- B4-O0：actual Completion/HostSnapshot核exactchild+physical+providerRun+worktoken+attempt+schema；原canonical结果先保存ResultAccepted+InterpretationPending，locked Observe再产生真实delta并完成pending。O0a已让Core保存完整Applied/Failed、拒绝冲突并可冷恢复；真实Observe尚未接通。失败只在显式新派生inquiry重解释，不另买响应。无usage保留预留，不填零。
- B5/B6：真实renderer work结果accepted后由已完成Core守门提交exact resultObservationId的AnswerCommitted，公开取实际正文；017从真正start/claim/submit入口闭环，036跨SDK/JS+新OS进程cold reopen。至少两个不同原目标，不直接append答案/固定文本冒充profile。全链取消/late usage/乱序/receipt-loss恢复另按C/D完成，不升级有限证明。

## 6. 当前状态与认领边界

04、07与总计划保留 R0/R2、lease/Fork 与 Unknown Root 有限证据；本卡 H0a 原 owner/observed 交接已完成。下一产品入口为第0节 H0b 原子首次接纳合同及两道持久门，先认领 U0/017 独立前置；B4-O0a 的有限状态账验收与真实 Observe 分开记账。没有 effect 前关联、最终可见字节与 canonical Host binding 证据，整个 B1/GAP 保持未闭合。
