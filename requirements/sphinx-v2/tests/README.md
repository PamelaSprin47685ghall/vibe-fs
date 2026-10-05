# sphinx-v2 测试证明范围

本目录是取代旧 epistemic-reasoning 的活动测试。WHAT 保持新上游 36 条合同，旧价格公式、阶段工具和内核不得通过测试迁回复活。历史对应见 [SUPERSEDES](../SUPERSEDES.md)。

2026-10-05 N06-B0有限验收完成：完整派发Request与可选完整Receipt由同一Reducer保存至唯一Current。004证明每work预留不混入其它work合计，新增预留不重复扣旧值；007证明缺Some round及batch内逆序引用拒绝、None独立work允许；010覆盖当前Spec/attempt/fence/依赖/状态/本work预留、intent内容绑定与receipt物理冲突，exact replay不抹掉已有receipt；019保留全部30body公开编码/持久恢复及实际gen143旧seal，020核对完整bindings只改变规定的三哈希对象。正式红例与原始失败见[本批记录](../../../proposals/archive/2026-10-05/Sphinx派发事实与资源预留-2026-10-05.md)。gen149全部Sphinx+J11为23/23排空、230pass/0fail、15TODO、group accepted=true；宽选集75/75、516pass/1fail，唯一durable-events/014性能失败，不能将目标证书写成宽选集绿色。

native complete state仍用physicalBindings字段，空bindings与gen143原seal字节一致；非空值为完整request及receipt option，内部不是第二truth。旧缺round派发产生unknown-round cut，旧错误aggregate预留产生post-state-mismatch cut；历史原envelope保留，不忽略指纹或另造legacy fold。此处数据来自实际旧生产prepare/append/Current，不是手算旧seal。B0证明canonical事实接纳，尚未证明Host实际来源、append拒绝零effect、receipt丢失对账、取消/usage/容量全链；004/010/034完整TODO、T406/T411与GAP-219继续保留。

2026-10-05 N06-A真实创建与读取有限验收完成：gen142正式73/73排空、437pass/0fail、24skip/29TODO、5.85s wall；Fable/check/format与前后freshness通过，原进程组accepted=true，exit1仅pending。MCP与JS入口共享`Composition/Commands`、canonical store和唯一Reducer；start保存原目标与显式授权引用，以命令命名空间+commandId定位inquiry，完整请求和实际启动配置绑定receipt。唯一Integrator在接受迁移后成对发布state/envelope；status/export只刷新并查询该Current，沿exact accepted parent导出，unknown/cut/fork均拒绝，不读History或另fold。020用独立canonical JSON/SHA-256 oracle核对三哈希，036用真实SDK、JS交叉入口和新OS服务进程核对原receipt与冷重开。输入身份、完整失败与剩余范围见[本批记录](../../../proposals/archive/2026-10-05/Sphinx持久创建与读取-2026-10-05.md)；下面gen70/gen131证书仍只属于其原输入。

MCP启动继续必需`SPHINX_COMMON_DIR`。可选`SPHINX_START_CONFIG`是显式JSON：`commandNamespace`、`createdBy`、`profileRef="sphinx.default@2"`、`executionMode="delegated"|"independent"`、非空`resourceSpecs`和`renderReserve`。资源项形如`{"name":"calls","kind":{"case":"consumed","payload":"calls"},"authorizedLimit":0}`，容量kind使用`capacity`；额度为有限非负数，reserve为资源名到有限非负数的对象且不超授权。零额度也必须显式供应。缺配置只允许读取，start返回`CONFIG_REQUIRED`；非法配置启动失败。JS使用`create(commonDir,writerId,configuration)`返回opaque资源句柄，随后`start/status/exportInquiry`接原生JSON参数；`dispose`结束句柄。无store的旧模板成功入口已删除。

创建诚实返回`active/no-runnable-work`，本批不生成计划、不派发模型、不宣称完成答案。Full包含真实accepted envelopes与完整Current，三个hash覆盖各自明确对象；`requires-external-inputs`、`externalInputsComplete=false`和说明明确startup配置、schema文档、plugin实现及外部材料未bundled，已知外部输入列表不声称完整。Summary只给语义DTO与hash，不能用完整physical state冒充semantic view。取消请求可推进控制状态并改变trace/state hash，而没有新语义事实时semantic hash保持。Independent只是显式配置，未证明实际blinding/独立派发。

033覆盖缺durable目录、11类非法配置、显式零额度、cold readonly、合法`__proto__`资源的reserve内容绑定、真实ServeEntry坏JSON/空白/null/非法mode退出，以及dispose后有效调用拒绝。036补实际JS与MCP非法InquiryId/ArtifactRef的`INVALID_SCHEMA`及精确字段路径；constraints/materialRefs缺失或非数组、非string元素均拒绝，不抛异常或强制转字符串。目标与约束正文中的合法空白不套用identity规则。新增startup/dispose覆盖在原产物已通过，单独记green，不捏造red；creation、reserve与公开输入缺陷均保留正式失败日志。

2026-10-05 N06-B前置Core答案来源守门已验收：gen131完整Sphinx套件纳入正式212/212、1547/0，31skip/94TODO；Fable/check/freshness前后通过。AnswerCommitted必需`resultObservationId`，Core要求当前work同attempt成功，accepted结果的work/attempt/fence/完整SchemaRef匹配。017的prepare/append/Current、durable semantic cut和合法冷重开已证，019同步全部body DTO；旧@2缺字段严格cut，不补填来源。见[本批记录](../../../proposals/archive/2026-10-05/实际读取版本与答案来源-2026-10-05.md)。

这一切片只证明成功工作与已接受结果的来源关联；Core不猜某个capability字符串必定代表renderer。N06-A另接公开start和读取；实际profile的renderer选择、claim/submit、Runtime完成与资源/Host取消仍未接通，017/T406等业务TODO继续保留，不据此声称已有首个公开持久答案。

本地前轮按当前条款修正错配：provider usage 归004，旧011的意图恢复分类归010，旧012的空工作分类归017，旧022的abstain/tie解码归025，Bayes局部模型计算归026。选择用例调用真实 Decision 选择路径；旧021的 Surface 自行排名/数值判别不再充当两阶段解释证明，也不保留其错误参数签名。读取源码、寻找类型名或匹配分支文本不能证明运行行为；撤下这些伪证明后保留正式可执行TODO，不以新增局部绿色删除未接通的业务义务。本次合并接纳上游新增的parser、canonical persistence和真实SDK协议回归，以下分别说明它们能证明什么。

- `001/002`：目标与profile局部边界；001新增真实EventStore写入和新writer重开，核对原文、提议不改目标与显式修订。它不证明用户授权经过MCP/OpenCode真实命令入口，也不证明所有依赖估值已失效。
- `004/010/017/021/034`：用量分类、恢复动作与停止原因，分别保留真正运行/持久化/取消链的TODO。分类器返回“等待终止”不表示已经等待过子工作。
- `013/029`：给定候选与估值后的实际选择；不证明计划生成、Agenda、依赖成功或渲染预算已贯通。
- `014/023/025/026`：实际数值和协议函数的局部正反例；不把后验归一、标签拒绝、票型解码或数值稳定当作完整实验与保证传播。Bayes新增不完整/非法likelihood和零partition反例。
- `018/036` 的局部测试：直接调用 `Tool.fsi` 声明的七个 parser 与 refusal accessor，使用真实 `Tool_decode*` / `Tool_refusal*` 具名导出。018 保留 Worker 夹带目标、预算、证书、事件变动的具名拒绝及合法结果正例。036 验证闭合工具名、各自必需字段、goal amendment 授权字段、读取模式，以及 status/export 对明确附带命令或变动字段的 ingress 拒绝；不把 commandId/workId/attempt 等孤立身份字段当作写动作，不定义所有未知键一律拒绝。**parser 成功或拒绝都不证明真实读取零副作用**：lease、model call、business mutation 的 Runtime 证据仍是 TODO。
- `034`：取消分类为「等待终止」而非「已取消」；attempt/fence/schema 引用非法或缺失即按字段拒绝，不默认、不取整、不从兄弟字段回填。新增 `Hosts/OpenCode/Surface.{fsi,fs}` 只调用真实 `OpenCodeHostPort.Capabilities()`，将 F# list 转为原生数组，不暴露 Adapter 对象图。正向断言 dispatch/request-cancel 存在，空集不能假绿；负向断言不宣称 session port 无法观测的读取。这里仅证明能力声明，不证明派发、abort、实际 receipt、真实 child 终态等待或迟到结果隔离，也不证明 schema hash 已匹配真实内容。
- `011`：新增真实durable command的内容绑定receipt和跨后续revision、新writer重放；worker结果的work/attempt/fence幂等仍为TODO。`012/022`的同轮乱序与measurement/intervention也保留TODO，没有用无关分类器填补。
- `019`：调用正式Persistence Surface和实际EventStore，核对原子多body batch、父链、单batch revision、post-state fingerprint、未知body的durable semantic cut、identity collision、合法并发heads及冷重开；30种body实际经过编码、append和新writer回放。测试准备明确的领域输入，不创建第二fold，不把Surface准备的InquiryCreated当作真实MCP start已接通。

## 当前生产路径与未证边界

当前Server七工具分别解码；start/status/export进入共享Commands，cancel仍在MCP内调用Admission、Codec和同一store.Append，跨入口先刷新。work_next/work_submit/goal_amend仍具名unsupported，JS submitResults也拒绝，不返回空receipts成功。036新增现存inquiry的真实读取与导出；完整业务driver、Host取消和模型/lease观察器仍未接通。源码存在不等于全部业务已验收。

上游现已用完整BodyDto、Codec和单一Reducer取代此前只识别四个取消类body的Integrator快照。每个canonical envelope携带完整TransitionBatch，Integrator从其exact accepted parent恢复基态后整体应用；合法fork保留多个heads，canonical Current明确返回DomainConflict，不选一个分支当当前真相。019新增的真实创建→请求取消→取消终态→新writer重开轨迹验证这层持久化行为，CancelRequested仍只是请求，InquirySuspended也不冒充已取消。这些证据不证明Host abort已收到、全链资源已停止或尚未接通的claim/submit/amend driver；start与读取另由本批真实入口证明。

`Tool.unsupported`是剩余实现限制，不是已完成业务。旧traceUnavailable分支已由真实accepted trace导出及unknown/cut/fork拒绝替换；无配置start具名拒绝不代替配置后的创建正例。claim/submit/amend移除unsupported仍须对应Runtime与durable合同真正接通，不能仅在bodyOf增加case，也不能以空trace代替实际序列。

本地 MCP SDK 1.30.0 / Zod 4.5.4 链路为 registerTool → normalizeObjectSchema → validateToolInput/safeParseAsync → callback(parsed args)。raw shape会被归一成默认object schema，额外字段可在Tool之前被剥离；完整object.passthrough schema才保留到callback。SDK没有额外的角色字段拦截器。036真实callTool要求明确的越权字段到达Tool并返回具名拒绝，不定义所有未知键一律拒绝；还要求replacementText optional而非nullable、expectedRevision公开必需且为严格版本串。注册成功、字段保留和合法MCP result必须由协议回归证明，不能只靠源码检查。

## 真实 SDK stdio 适配器合同

036 新增一条真实协议路径。进程 fixture 就地封装在该测试文件，不新增公共测试入口，也不修改共用 support。所有协议子测共享一次 ServeEntry 生命周期：已安装的 SDK Client/StdioClientTransport 直接以 Node 启动最终产物 `dist/Sphinx/V2/ServeEntry.js`，不使用 eval/import wrapper、不调用私有 handler、不替换 Server/schema/SDK。ServeEntry 源码和当前产物都有 runIfEntryPoint，直接执行它会走生产 serveDefault。每次用 mkdtemp 创建隔离目录，将它同时作为 cwd 和 SPHINX_COMMON_DIR；transport 只继承 SDK 的默认安全环境，不继承父测试的 WANXIANGSHU_NO_FATAL_EXIT、NODE_OPTIONS 等旁路。

Client.connect 完成 initialize 并发出 initialized，然后 listTools 检查七件套的精确名单。独立 metadata 子测检查 goalAmend 的 replacementText 不在 required 中、expectedRevision 在 required 中；metadata 红灯不吞掉后面的协议反例。接着用真实 callTool 观察：

- 合法 work_submit 形状的当前 unsupported 分支是正向 ingress 对照，不是结果接纳；schema hash 用给定 canonical schema 文档的真实 SHA-256，不能据此声称该 schema 已注册或匹配真实 work ticket。
- 同一合法形状分别携带 certificatePatches、budgetDebit、events、goalRevision、goalAmendment：必须收到 WORK_RESULT_EXCEEDS_ROLE，path 精确为各字段。SDK 剥字段后落入 unsupported 分支会红，不能只靠 parser 测试给它开绿灯。
- status 与 summary/full export 分别携带 command、commands 及上述五类变动，必须收到 INVALID_SCHEMA 和精确字段 path。干净未知inquiry的status/export均为UNKNOWN_INQUIRY；历史trace-unavailable对照已替换。未知分支只证明拒绝可达，已有inquiry读取/导出与业务零写入由N06-A的配置创建、跨入口及冷进程轨迹另证。
- goalAmend 使用显式合法版本串 expectedRevision='0'。省略 replacementText 和提供非空文本应越过 parser；当前未接业务时是 unsupported 对照。replacementText 是 optional、非 nullable：显式 null 与空白文本均不得当作省略。直接公开 Tool parser 对两者仍严格断言 INVALID_SCHEMA/refusal.path=replacementText，真实 callTool 的空白文本也保留此具名业务拒绝。null 可由 SDK 类型 schema 拒绝：接受公开 McpError/InvalidParams，或原生 isError=true 的正式结果，但必须有非空可读错误，指出 replacementText 并说明类型或 null 原因；空错误、其它字段错误、进程失败和其它协议错误仍使测试失败。无需把 SDK 参数错误转换成业务 JSON，不能为此 advertise nullable 或绕过 SDK 类型检查。WHAT018/034/036 完整正文没有要求所有 schema 错型均返回业务 JSON。

Tool 业务拒绝的响应 oracle 仍要求原生 MCP result、isError=true，以及 structuredContent 内的 apiVersion/outcome/refusal.code/path/message，或 content 中一份非空、可读的同合同 JSON。它不读取 DU tag/fields，不接受只有空 content 的拒绝或 isError=false 的假成功；纯正负对照让空内容、非 JSON、缺字段和空文案成为反例。上述 null schema 错型测项单独遵守 SDK 参数错误合同，不将这条宽容分支用于 work_submit 五类夹带写字段或 status/export 明确写意图；后两类仍必须到达 Tool 并返回精确 code/path，SDK 通用参数错误不能代替。unsupported只识别剩余未接通的操作，不是已完成业务的永久承诺；trace-unavailable已删除，后续owner接通时保留全部ingress拒绝断言。

具名红灯保留因果：MCP_ENTRY_UNAVAILABLE 是最终产物缺失；MCP_BOOT_OR_REGISTRATION_FAILED 带 initialize 等待点、原始 cause 与 stderr 尾部，可定位注册阶段的 Emit 参数错误；MCP_INGRESS_REFUSAL_MISMATCH 报告字段拒绝未到达（包括 strip 后落入 unsupported/unknown）；MCP_RESPONSE_LAYOUT 表示 SDK 已交回 result 但业务 JSON/refusal 布局不合合同；MCP_PROTOCOL_RESPONSE_FAILED 保留 SDK 本身对非法 MCP response 的拒绝。它们不把所有启动错误都猜成 Emit，也不把所有错码都断言为 strip。

监督复用 verification-system/tests/e2e/support/watchdog.js 的 Watchdog，不覆盖默认静默窗口。只有 initialize、listTools、callTool 的实际完成续期，stderr、诊断及后台噪音不续期，没有另加固定总墙时或放大 timeout。成功和失败均在 finally 中关闭 Client/transport，再等待预先订阅的 transport.onclose；SDK 1.30.0 的 client/stdio.js 在底层 ChildProcess close 后才调用它，shared/protocol.js 的 connect 会保留旧回调。SDK close 在 SIGKILL 后可能先返回，因此只等 close() 或 pid=null 不算实际退出。cleanup 用同一默认 Watchdog 有界等待，未观测退出即 MCP_SERVER_EXIT_UNOBSERVED 并保留目录，不伪报收束；外部执行监督器仍负责异常不可回收进程组。

复核本块时可跑 `node --test requirements/sphinx-v2/tests/018.test.mjs requirements/sphinx-v2/tests/034.test.mjs requirements/sphinx-v2/tests/036.test.mjs`；正式交付仍走受监督入口。这个 fixture 是单一真实 MCP stdio 适配器合同，不是 Host/provider Long Stroke，也没有第二条昂贵 E2E。实际进程退出已有公开观察点，不需要另造生产默认 entry 的替身。

N06-A的036新增现存inquiry重复读取：实际start与cancel提供真实业务写入正向对照，查询前后完整journal字节保持。该证据只结算本批业务零写入；lease、dispatch、模型调用还没有实际可产生它们的正向对照。036两个完整TODO、034真实Host receipt/终止等待/迟到结果及其它业务TODO继续保留，不能用永远为零的观察器关闭完整零effect义务。

## 本次合并的编译与运行

上游已在compile-order、所属core/integration项目及唯一Surface manifest登记Tool、Persistence Surface和OpenCode能力Surface，并补齐Host session contract依赖。不得另加白名单绕过门禁。统一集成先运行Fable构建、产物链接及正式Surface门禁，再跑本目录正式用例；缺登记或缺产物的加载失败不算业务反例，真实协议失败须保留原始cause和完整进程退出证据。

本次先在gen69产物上取得正式行为红灯：036为4 pass / 4 fail / 2 TODO，包含SDK真实子进程注册时报描述字符串没有`.number()`、合法数值解码触发Number/BigInt错误，以及expectedRevision未校验；见[行为红日志](../../../proposals/archive/2026-10-03/baselines/2026-10-03-sync-e1e7dd3f1-sphinx036-gen69-red.log)。此前因测试导出名称不符而发生的[加载失败](../../../proposals/archive/2026-10-03/baselines/2026-10-03-sync-e1e7dd3f1-sphinx036-loading-red.log)不计作行为证明。

修复后统一Fable构建gen70通过（169 surfaces、828 modules）。Node22.23.3在该产物上直接运行018/034/036，结果为50 pass / 0 fail / 0 skip / 3 TODO，见[定向绿日志](../../../proposals/archive/2026-10-03/baselines/2026-10-03-sync-e1e7dd3f1-sphinx-gen70-green.log)。其中036的单一SDK stdio路径实际完成七工具注册、参数保留与具名拒绝、原生MCP响应、optional而非nullable的replacementText、严格expectedRevision及真实子进程退出；入口用realpath解析最终ServeEntry，避免dist符号链接与生产entrypoint身份不一致而误跳过serveDefault。此处是定向直接运行结果，不冒称后续统一受监督测试已完成，也不与后续格式化输入的构建结果混称同一快照。

gen70当时尚未证明现存inquiry的native semantic DTO与读取；这部分及业务零写入已由N06-A补齐。真实schema内容匹配、Host receipt、全链取消、迟到结果隔离与剩余MCP业务driver仍待证。036仍有两个完整TODO（含真实lease/model正向观察器），034仍有Host合同TODO；不能用full physical state冒充semantic view或用局部证书删除这些义务。

v2 MCP是独立接入端口；Host原生`/sphinx question`适配器不启动/注入MCP，二者边界可以并存。工具名单存在不代表Host配置需要注入MCP。

本目录完整局部套件为`node --test requirements/sphinx-v2/tests/*.test.mjs`，正式交付沿verification-system受监督入口运行。历史迁移边界见[迁移记录](../../../proposals/archive/2026-10-03/20模块迁移-Sphinx与Manager-2026-09-28.md)；本次新增证据的具体生成输入及结果由本批交付记录另行绑定，不能引用上游的未运行声明作为绿色证书。
