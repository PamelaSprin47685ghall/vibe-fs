# sphinx-v2 测试证明范围

本目录是取代旧 epistemic-reasoning 的活动测试。WHAT 保持新上游 36 条合同，旧价格公式、阶段工具和内核不得通过测试迁回复活。历史对应见 [SUPERSEDES](../SUPERSEDES.md)。

本轮按当前条款修正错配：provider usage 归004，旧011的意图恢复分类归010，旧012的空工作分类归017，旧022的abstain/tie解码归025，Bayes局部模型计算归026。选择用例调用真实 Decision 选择路径；旧021的 Surface 自行排名/数值判别不再充当两阶段解释证明，也不保留其错误参数签名。

- `001/002`：目标与profile局部边界，不证明用户授权经过真实入口。
- `004/010/017/021/034`：用量分类、恢复动作与停止原因，分别保留真正运行/持久化/取消链的TODO。分类器返回“等待终止”不表示已经等待过子工作。
- `013/029`：给定候选与估值后的实际选择；不证明计划生成、Agenda、依赖成功或渲染预算已贯通。
- `014/023/025/026`：实际数值和协议函数的局部正反例；不把后验归一、标签拒绝、票型解码或数值稳定当作完整实验与保证传播。Bayes新增不完整/非法likelihood和零partition反例。
- `018/036` 的局部测试：直接调用 `Tool.fsi` 声明的七个 parser 与 refusal accessor，使用真实 `Tool_decode*` / `Tool_refusal*` 具名导出。018 保留 Worker 夹带目标、预算、证书、事件变动的具名拒绝及合法结果正例。036 验证闭合工具名、各自必需字段、goal amendment 授权字段、读取模式，以及 status/export 对明确附带命令或变动字段的 ingress 拒绝；不把 commandId/workId/attempt 等孤立身份字段当作写动作，不定义所有未知键一律拒绝。**parser 成功或拒绝都不证明真实读取零副作用**：lease、model call、business mutation 的 Runtime 证据仍是 TODO。
- `034`：取消分类为「等待终止」而非「已取消」；attempt/fence/schema 引用非法或缺失即按字段拒绝，不默认、不取整、不从兄弟字段回填。新增 `Hosts/OpenCode/Surface.{fsi,fs}` 只调用真实 `OpenCodeHostPort.Capabilities()`，将 F# list 转为原生数组，不暴露 Adapter 对象图。正向断言 dispatch/request-cancel 存在，空集不能假绿；负向断言不宣称 session port 无法观测的读取。这里仅证明能力声明，不证明派发、abort、实际 receipt、真实 child 终态等待或迟到结果隔离，也不证明 schema hash 已匹配真实内容。
- `011/012/022`：真实结果幂等、同轮乱序与measurement/intervention留为TODO，没有用无关分类器填补。

## 当前生产路径与未证边界

此前核对的 Server 快照调用点是 `Hosts/Mcp/Server.fs` 的 `registerTools/statusResult/cancelResult`；schema owner 正在独立修改，以下不是修后产物证书：七个工具分别解码；status 读取 canonical Current 并调用 Runtime 分类；cancel 调用 Admission、Codec 和共享 store.Append。start/work_next/work_submit/goal_amend 当前仍返回具名 unsupported refusal，export 返回 traceUnavailable。这些调用点存在，**不等于其业务路径已被运行验证**，本轮未修改 Server/Core/Persistence。

不能再用「四个终态，所以 cancel 可 replay」解释现状。此前核对的持久化修复前快照中，`Persistence/Integrator.fs` 的 `bodyOf` 只有 CancelRequested、InquiryCancelled、InquirySuspended、InquiryFailed 四个分支；CancelRequested 是请求，InquirySuspended 也不是终态。`Codec.bodyCanonical/encode` 当前规范化的是 DU 和 wire record，Integrator 则读取显式小写 wire 字段并把这四类 payload 当 string 解码；`textOf` 失败还会回填空串，`typedEvents` 的 Parent 为 None，`replayBatch` 逐 body 推进 revision。因此，单个 tag 分支绝不能证明创建前置、reason 字节、父链、batch revision、command receipt 或完整取消回放正确。持久化修复与真实创建→取消→重开证明由其 owner 接续，本目录不宣布完成。

`Tool.unsupported` / `Tool.traceUnavailable` 是当前实现限制的拒绝，不是有意豁免的已完成业务。018/034/036 不锁其临时码或文案，也不把拒绝当作 start/claim/submit/amend/export 已实现。后续移除条件是对应真实 Runtime 行为及 durable/trace 合同被接通并得到证据，不是仅在 bodyOf 增加几个 case。export 仍须真实 accepted-envelope 序列及三哈希，不能以空 trace 代替。

已核对的本地 MCP SDK 1.30.0 / Zod 4.5.4 链路为 registerTool → normalizeObjectSchema → validateToolInput/safeParseAsync → callback(parsed args)。先前 Server 传 raw shape，SDK 将其归一成默认 object schema，额外字段可在 Tool 之前被剥离；完整 object.passthrough schema 才保留到 callback。SDK 没有额外的角色字段拦截器。这是本地源码事实，不是当前 Server 已成功注册或公开 refusal 已被运行证明。修法及生产 Server 写入由 schema owner 负责；本轮只写协议测试，且不定义所有未知键一律拒绝。Tool 的 replacementText optional 与 Server 原先 required 的差异，必须依 goalAmend 参数契约修正：仅补约束允许省略文本，显式 null 不等于省略。新协议测试还检查 expectedRevision 的公开必需性并始终传合法严格版本串，不发明默认修订。

## 真实 SDK stdio 适配器合同（已写，未运行）

036 新增一条真实协议路径。进程 fixture 就地封装在该测试文件，不新增公共测试入口，也不修改共用 support。所有协议子测共享一次 ServeEntry 生命周期：已安装的 SDK Client/StdioClientTransport 直接以 Node 启动最终产物 `dist/Sphinx/V2/ServeEntry.js`，不使用 eval/import wrapper、不调用私有 handler、不替换 Server/schema/SDK。ServeEntry 源码和当前产物都有 runIfEntryPoint，直接执行它会走生产 serveDefault。每次用 mkdtemp 创建隔离目录，将它同时作为 cwd 和 SPHINX_COMMON_DIR；transport 只继承 SDK 的默认安全环境，不继承父测试的 WANXIANGSHU_NO_FATAL_EXIT、NODE_OPTIONS 等旁路。

Client.connect 完成 initialize 并发出 initialized，然后 listTools 检查七件套的精确名单。独立 metadata 子测检查 goalAmend 的 replacementText 不在 required 中、expectedRevision 在 required 中；metadata 红灯不吞掉后面的协议反例。接着用真实 callTool 观察：

- 合法 work_submit 形状的当前 unsupported 分支是正向 ingress 对照，不是结果接纳；schema hash 用给定 canonical schema 文档的真实 SHA-256，不能据此声称该 schema 已注册或匹配真实 work ticket。
- 同一合法形状分别携带 certificatePatches、budgetDebit、events、goalRevision、goalAmendment：必须收到 WORK_RESULT_EXCEEDS_ROLE，path 精确为各字段。SDK 剥字段后落入 unsupported 分支会红，不能只靠 parser 测试给它开绿灯。
- status 与 summary/full export 分别携带 command、commands 及上述五类变动，必须收到 INVALID_SCHEMA 和精确字段 path。干净 status 的未知 inquiry、干净 export 的 trace-unavailable 只证明当前读取/拒绝分支可被真实客户端观测；不证明已有 inquiry 的读取、导出或任何副作用不变量。
- goalAmend 使用显式合法版本串 expectedRevision='0'。省略 replacementText 和提供非空文本应越过 parser；当前未接业务时是 unsupported 对照。replacementText 是 optional、非 nullable：显式 null 与空白文本均不得当作省略。直接公开 Tool parser 对两者仍严格断言 INVALID_SCHEMA/refusal.path=replacementText，真实 callTool 的空白文本也保留此具名业务拒绝。null 可由 SDK 类型 schema 拒绝：接受公开 McpError/InvalidParams，或原生 isError=true 的正式结果，但必须有非空可读错误，指出 replacementText 并说明类型或 null 原因；空错误、其它字段错误、进程失败和其它协议错误仍使测试失败。无需把 SDK 参数错误转换成业务 JSON，不能为此 advertise nullable 或绕过 SDK 类型检查。WHAT018/034/036 完整正文没有要求所有 schema 错型均返回业务 JSON。

Tool 业务拒绝的响应 oracle 仍要求原生 MCP result、isError=true，以及 structuredContent 内的 apiVersion/outcome/refusal.code/path/message，或 content 中一份非空、可读的同合同 JSON。它不读取 DU tag/fields，不接受只有空 content 的拒绝或 isError=false 的假成功；纯正负对照让空内容、非 JSON、缺字段和空文案成为反例。上述 null schema 错型测项单独遵守 SDK 参数错误合同，不将这条宽容分支用于 work_submit 五类夹带写字段或 status/export 明确写意图；后两类仍必须到达 Tool 并返回精确 code/path，SDK 通用参数错误不能代替。当前 unsupported/trace-unavailable 码只用于识别尚未接通的分支，不是已完成业务的永久产品承诺；业务 owner 接通后须用已验证的对应分支替换这些对照，保留全部 ingress 拒绝断言。

具名红灯保留因果：MCP_ENTRY_UNAVAILABLE 是最终产物缺失；MCP_BOOT_OR_REGISTRATION_FAILED 带 initialize 等待点、原始 cause 与 stderr 尾部，可定位注册阶段的 Emit 参数错误；MCP_INGRESS_REFUSAL_MISMATCH 报告字段拒绝未到达（包括 strip 后落入 unsupported/unknown）；MCP_RESPONSE_LAYOUT 表示 SDK 已交回 result 但业务 JSON/refusal 布局不合合同；MCP_PROTOCOL_RESPONSE_FAILED 保留 SDK 本身对非法 MCP response 的拒绝。它们不把所有启动错误都猜成 Emit，也不把所有错码都断言为 strip。

监督复用 verification-system/tests/e2e/support/watchdog.js 的 Watchdog，不覆盖默认静默窗口。只有 initialize、listTools、callTool 的实际完成续期，stderr、诊断及后台噪音不续期，没有另加固定总墙时或放大 timeout。成功和失败均在 finally 中关闭 Client/transport，再等待预先订阅的 transport.onclose；SDK 1.30.0 的 client/stdio.js 在底层 ChildProcess close 后才调用它，shared/protocol.js 的 connect 会保留旧回调。SDK close 在 SIGKILL 后可能先返回，因此只等 close() 或 pid=null 不算实际退出。cleanup 用同一默认 Watchdog 有界等待，未观测退出即 MCP_SERVER_EXIT_UNOBSERVED 并保留目录，不伪报收束；外部执行监督器仍负责异常不可回收进程组。

在 Server/schema owner 修复并统一编译后，可由 DevOps 跑 `node --test requirements/sphinx-v2/tests/036.test.mjs`；完整本块回归仍是下节的 018/034/036 三文件命令。这个 fixture 是单一真实 MCP stdio 适配器合同，不是 Host/provider Long Stroke，也没有第二条昂贵 E2E。实际进程退出已有公开观察点，不需要另造生产默认 entry 的替身。

真实读取的 lease、模型调用和 business mutation 仍没有正向可观测对照：本组只请求未知 inquiry，并未创建业务 inquiry。036 的业务 TODO、034 的真实 Host receipt/终止等待/迟到结果 TODO，以及其余业务 TODO 均保留。后续要用真实创建前置、现存 inquiry、lease/dispatch/model 观察端口和 durable business view 做前后对照，并用真正可产生相关副作用的正向输入校验观察器；不能用拒绝请求或空计数器证明零副作用。

## 待统一登记、编译与运行

本轮不修改 compile-order、fsproj、scripts 或 GAP。统一集成需要：

- 在 `src/Wanxiangshu/compile-order.txt` 中，将 `Sphinx/V2/Hosts/OpenCode/Surface.fsi`、`Sphinx/V2/Hosts/OpenCode/Surface.fs` 按此顺序放在 Adapter.fsi/fs 之后。
- 在 Adapter 所属 `src/Wanxiangshu/Wanxiangshu.Owner.epistemic-reasoning.sphinx-v2-integration.fsproj`（如统一拆分，则其正式接替 shard）中登记同一文件对。当前该 shard 缺 `Wanxiangshu.Owner.host-boundary.host-session-contract.fsproj` 引用，`ISessionHostPort` 由该 contract 发布，需统一补齐。Runtime Ports 已在 `Wanxiangshu.Owner.epistemic-reasoning.sphinx-v2-core.fsproj`，integration 已引用 core；新 Surface 不新增除此以外的合同依赖。Tool 也已属于 core，本轮未新增 Tool 平铺层或公开签名。
- 在 `scripts/lib/test-surface-scan.mjs` 的唯一 SURFACE_MANIFEST 中正式登记 `Sphinx/V2/Hosts/OpenCode/Surface.js`：owner `sphinx-v2`，source `src/Wanxiangshu/Sphinx/V2/Hosts/OpenCode/Surface.fs`，laws `['SPHINX-V2-034']`，representation `json`，kind `pure`。034 已静态 import 该模块。不加测试白名单，也不放松 deep-dist 门禁；登记前新 import 仍会被门禁拒绝。

由 Manager 安排执行者在最终集成快照上运行项目 Fable 构建、产物链接与正式 Surface 门禁，再跑 `node --test requirements/sphinx-v2/tests/018.test.mjs requirements/sphinx-v2/tests/034.test.mjs requirements/sphinx-v2/tests/036.test.mjs`。新 Surface 缺少登记或未发射时，import/门禁红不算业务回归的反例；接线后再用旧 decoder 观察明确变动字段被静默忽略、缺失 resultSchema 抛异常等反例。能力空集、虚报 read-result/status/reconcile 必须分别使正负对照变红。

前轮源码及本轮协议测试均已落盘。本轮只修改 036.test.mjs 和本 README，018/034 及其业务断言保持不变，未修改 Server/Tool/公共配置，未新增 Surface 或白名单。Engineer 未运行命令、git、编译或测试，未取得修改后的运行结果；真实 stdio 的可执行回归已经写出但尚未取得运行证据，读取零副作用、真实 schema 内容匹配、Host receipt、全链取消与迟到结果隔离的业务 TODO 全部保留。

v2 MCP是独立接入端口；Host原生`/sphinx question`适配器不启动/注入MCP，二者边界可以并存。工具名单存在不代表Host配置需要注入MCP。

本目录完整局部套件为 `node --test requirements/sphinx-v2/tests/*.test.mjs`；本轮聚焦运行范围见上，不声称语法、编译或行为测试通过。其余实施缺口继续见 [迁移记录](../../../proposals/20模块迁移-Sphinx与Manager-2026-09-28.md)。
