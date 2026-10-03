# sphinx-v2 测试证明范围

本目录是取代旧 epistemic-reasoning 的活动测试。WHAT 保持新上游 36 条合同，旧价格公式、阶段工具和内核不得通过测试迁回复活。历史对应见 [SUPERSEDES](../SUPERSEDES.md)。

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

当前合并的 Server 调用点是 `Hosts/Mcp/Server.fs` 的 `registerTools/statusResult/cancelResult`：七个工具分别解码；status 读取 canonical Current 并调用 Runtime 分类；cancel 调用 Admission、Codec 和共享 store.Append。start/work_next/work_submit/goal_amend 仍返回具名 unsupported refusal，export 返回 traceUnavailable。这些调用点存在，**不等于其业务路径已被运行验证**；本次真实stdio回归证明的是注册、ingress与拒绝响应，尚未证明现存inquiry读取或真实Host取消。

上游现已用完整BodyDto、Codec和单一Reducer取代此前只识别四个取消类body的Integrator快照。每个canonical envelope携带完整TransitionBatch，Integrator从其exact accepted parent恢复基态后整体应用；合法fork保留多个heads，canonical Current明确返回DomainConflict，不选一个分支当当前真相。019新增的真实创建→请求取消→取消终态→新writer重开轨迹验证这层持久化行为，CancelRequested仍只是请求，InquirySuspended也不冒充已取消。这些证据不证明Host abort已收到或全链资源已停止，更不证明尚未接通的start/claim/submit/amend/export driver。

`Tool.unsupported` / `Tool.traceUnavailable` 是当前实现限制的拒绝，不是有意豁免的已完成业务。018/034/036 不锁其临时码或文案，也不把拒绝当作 start/claim/submit/amend/export 已实现。后续移除条件是对应真实 Runtime 行为及 durable/trace 合同被接通并得到证据，不是仅在 bodyOf 增加几个 case。export 仍须真实 accepted-envelope 序列及三哈希，不能以空 trace 代替。

本地 MCP SDK 1.30.0 / Zod 4.5.4 链路为 registerTool → normalizeObjectSchema → validateToolInput/safeParseAsync → callback(parsed args)。raw shape会被归一成默认object schema，额外字段可在Tool之前被剥离；完整object.passthrough schema才保留到callback。SDK没有额外的角色字段拦截器。036真实callTool要求明确的越权字段到达Tool并返回具名拒绝，不定义所有未知键一律拒绝；还要求replacementText optional而非nullable、expectedRevision公开必需且为严格版本串。注册成功、字段保留和合法MCP result必须由协议回归证明，不能只靠源码检查。

## 真实 SDK stdio 适配器合同

036 新增一条真实协议路径。进程 fixture 就地封装在该测试文件，不新增公共测试入口，也不修改共用 support。所有协议子测共享一次 ServeEntry 生命周期：已安装的 SDK Client/StdioClientTransport 直接以 Node 启动最终产物 `dist/Sphinx/V2/ServeEntry.js`，不使用 eval/import wrapper、不调用私有 handler、不替换 Server/schema/SDK。ServeEntry 源码和当前产物都有 runIfEntryPoint，直接执行它会走生产 serveDefault。每次用 mkdtemp 创建隔离目录，将它同时作为 cwd 和 SPHINX_COMMON_DIR；transport 只继承 SDK 的默认安全环境，不继承父测试的 WANXIANGSHU_NO_FATAL_EXIT、NODE_OPTIONS 等旁路。

Client.connect 完成 initialize 并发出 initialized，然后 listTools 检查七件套的精确名单。独立 metadata 子测检查 goalAmend 的 replacementText 不在 required 中、expectedRevision 在 required 中；metadata 红灯不吞掉后面的协议反例。接着用真实 callTool 观察：

- 合法 work_submit 形状的当前 unsupported 分支是正向 ingress 对照，不是结果接纳；schema hash 用给定 canonical schema 文档的真实 SHA-256，不能据此声称该 schema 已注册或匹配真实 work ticket。
- 同一合法形状分别携带 certificatePatches、budgetDebit、events、goalRevision、goalAmendment：必须收到 WORK_RESULT_EXCEEDS_ROLE，path 精确为各字段。SDK 剥字段后落入 unsupported 分支会红，不能只靠 parser 测试给它开绿灯。
- status 与 summary/full export 分别携带 command、commands 及上述五类变动，必须收到 INVALID_SCHEMA 和精确字段 path。干净 status 的未知 inquiry、干净 export 的 trace-unavailable 只证明当前读取/拒绝分支可被真实客户端观测；不证明已有 inquiry 的读取、导出或任何副作用不变量。
- goalAmend 使用显式合法版本串 expectedRevision='0'。省略 replacementText 和提供非空文本应越过 parser；当前未接业务时是 unsupported 对照。replacementText 是 optional、非 nullable：显式 null 与空白文本均不得当作省略。直接公开 Tool parser 对两者仍严格断言 INVALID_SCHEMA/refusal.path=replacementText，真实 callTool 的空白文本也保留此具名业务拒绝。null 可由 SDK 类型 schema 拒绝：接受公开 McpError/InvalidParams，或原生 isError=true 的正式结果，但必须有非空可读错误，指出 replacementText 并说明类型或 null 原因；空错误、其它字段错误、进程失败和其它协议错误仍使测试失败。无需把 SDK 参数错误转换成业务 JSON，不能为此 advertise nullable 或绕过 SDK 类型检查。WHAT018/034/036 完整正文没有要求所有 schema 错型均返回业务 JSON。

Tool 业务拒绝的响应 oracle 仍要求原生 MCP result、isError=true，以及 structuredContent 内的 apiVersion/outcome/refusal.code/path/message，或 content 中一份非空、可读的同合同 JSON。它不读取 DU tag/fields，不接受只有空 content 的拒绝或 isError=false 的假成功；纯正负对照让空内容、非 JSON、缺字段和空文案成为反例。上述 null schema 错型测项单独遵守 SDK 参数错误合同，不将这条宽容分支用于 work_submit 五类夹带写字段或 status/export 明确写意图；后两类仍必须到达 Tool 并返回精确 code/path，SDK 通用参数错误不能代替。当前 unsupported/trace-unavailable 码只用于识别尚未接通的分支，不是已完成业务的永久产品承诺；业务 owner 接通后须用已验证的对应分支替换这些对照，保留全部 ingress 拒绝断言。

具名红灯保留因果：MCP_ENTRY_UNAVAILABLE 是最终产物缺失；MCP_BOOT_OR_REGISTRATION_FAILED 带 initialize 等待点、原始 cause 与 stderr 尾部，可定位注册阶段的 Emit 参数错误；MCP_INGRESS_REFUSAL_MISMATCH 报告字段拒绝未到达（包括 strip 后落入 unsupported/unknown）；MCP_RESPONSE_LAYOUT 表示 SDK 已交回 result 但业务 JSON/refusal 布局不合合同；MCP_PROTOCOL_RESPONSE_FAILED 保留 SDK 本身对非法 MCP response 的拒绝。它们不把所有启动错误都猜成 Emit，也不把所有错码都断言为 strip。

监督复用 verification-system/tests/e2e/support/watchdog.js 的 Watchdog，不覆盖默认静默窗口。只有 initialize、listTools、callTool 的实际完成续期，stderr、诊断及后台噪音不续期，没有另加固定总墙时或放大 timeout。成功和失败均在 finally 中关闭 Client/transport，再等待预先订阅的 transport.onclose；SDK 1.30.0 的 client/stdio.js 在底层 ChildProcess close 后才调用它，shared/protocol.js 的 connect 会保留旧回调。SDK close 在 SIGKILL 后可能先返回，因此只等 close() 或 pid=null 不算实际退出。cleanup 用同一默认 Watchdog 有界等待，未观测退出即 MCP_SERVER_EXIT_UNOBSERVED 并保留目录，不伪报收束；外部执行监督器仍负责异常不可回收进程组。

复核本块时可跑 `node --test requirements/sphinx-v2/tests/018.test.mjs requirements/sphinx-v2/tests/034.test.mjs requirements/sphinx-v2/tests/036.test.mjs`；正式交付仍走受监督入口。这个 fixture 是单一真实 MCP stdio 适配器合同，不是 Host/provider Long Stroke，也没有第二条昂贵 E2E。实际进程退出已有公开观察点，不需要另造生产默认 entry 的替身。

真实读取的 lease、模型调用和 business mutation 仍没有正向可观测对照：本组只请求未知 inquiry，并未创建业务 inquiry。036 的业务 TODO、034 的真实 Host receipt/终止等待/迟到结果 TODO，以及其余业务 TODO 均保留。后续要用真实创建前置、现存 inquiry、lease/dispatch/model 观察端口和 durable business view 做前后对照，并用真正可产生相关副作用的正向输入校验观察器；不能用拒绝请求或空计数器证明零副作用。

## 本次合并的编译与运行

上游已在compile-order、所属core/integration项目及唯一Surface manifest登记Tool、Persistence Surface和OpenCode能力Surface，并补齐Host session contract依赖。不得另加白名单绕过门禁。统一集成先运行Fable构建、产物链接及正式Surface门禁，再跑本目录正式用例；缺登记或缺产物的加载失败不算业务反例，真实协议失败须保留原始cause和完整进程退出证据。

本次先在gen69产物上取得正式行为红灯：036为4 pass / 4 fail / 2 TODO，包含SDK真实子进程注册时报描述字符串没有`.number()`、合法数值解码触发Number/BigInt错误，以及expectedRevision未校验；见[行为红日志](../../../proposals/archive/2026-10-03/baselines/2026-10-03-sync-e1e7dd3f1-sphinx036-gen69-red.log)。此前因测试导出名称不符而发生的[加载失败](../../../proposals/archive/2026-10-03/baselines/2026-10-03-sync-e1e7dd3f1-sphinx036-loading-red.log)不计作行为证明。

修复后统一Fable构建gen70通过（169 surfaces、828 modules）。Node22.23.3在该产物上直接运行018/034/036，结果为50 pass / 0 fail / 0 skip / 3 TODO，见[定向绿日志](../../../proposals/archive/2026-10-03/baselines/2026-10-03-sync-e1e7dd3f1-sphinx-gen70-green.log)。其中036的单一SDK stdio路径实际完成七工具注册、参数保留与具名拒绝、原生MCP响应、optional而非nullable的replacementText、严格expectedRevision及真实子进程退出；入口用realpath解析最终ServeEntry，避免dist符号链接与生产entrypoint身份不一致而误跳过serveDefault。此处是定向直接运行结果，不冒称后续统一受监督测试已完成，也不与后续格式化输入的构建结果混称同一快照。

读取零副作用、现存inquiry的原生semantic DTO正例、真实schema内容匹配、Host receipt、全链取消、迟到结果隔离及尚未接通的MCP业务driver TODO全部保留；不能用full physical state冒充semantic view。三文件的3 TODO分别为036的业务驱动和真实读取两项，以及034的Host合同一项。

v2 MCP是独立接入端口；Host原生`/sphinx question`适配器不启动/注入MCP，二者边界可以并存。工具名单存在不代表Host配置需要注入MCP。

本目录完整局部套件为`node --test requirements/sphinx-v2/tests/*.test.mjs`，正式交付沿verification-system受监督入口运行。历史迁移边界见[迁移记录](../../../proposals/archive/2026-10-03/20模块迁移-Sphinx与Manager-2026-09-28.md)；本次新增证据的具体生成输入及结果由本批交付记录另行绑定，不能引用上游的未运行声明作为绿色证书。
