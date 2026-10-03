# Upstream同步：e1e7dd3f1（2026-10-03）

本批从干净的master `faae4a602df3161f12f37d829454705d6e31d9b9`接续，合并upstream/master `e1e7dd3f113a22f37ea5790c8325ae5dbb7e1126`。使用普通merge保留双方历史，不覆盖上一批Guard交接、Node判决输送和参数回滚修复。后续施工仍从[总计划](../../TODO施工总计划-2026-10-03.md)开始。

## 合入范围

| 提交 | 变化 |
|---|---|
| `08a9297c0` | 只读Replica的空stop物理终态正常收口，避免把InteractionRepair候选当作仍在运行 |
| `c8c582295` | 重启后收养的空闲DevOps句柄不阻塞Manager Join；真实pending工作和可Join结果仍需等待/消费 |
| `2c4ff4c36` | Predictor正文降为Main reasoning，原生reasoning不回传；持久frame、往复恢复、重复正文次数和物理传输坐标同步 |
| `e1e7dd3f1` | Sphinx七工具解码与typed拒绝、canonical Persistence、representation/body DTO、scoped delegation工作及相关正式回归和编译登记 |

upstream相对共同祖先共154文件变化，不等于154个本地冲突。六个文本冲突为GAP台账、capability/delegation/Sphinx测试说明、cognitive-workspace/007和Sphinx/036；逐处按现行WHAT与双方正式证据合并。

## 合并规则与兼容边界

- 保留本地GAP-064/069的正常退出复核、GAP-216全局构建关闭、GAP-217真实参数回滚、GAP-218持久失败传播及GAP-223关闭，不用上游较旧的“待运行/未修复”描述覆盖。
- Sphinx承接真实SDK和canonical存储基础层，撤下“MCP handler忽略输入”的旧诊断，GAP-219改PARTIAL。start/claim/submit/amend驱动仍unsupported，export traceUnavailable；OpenCode缺结果/dispatch lookup端口，Unknown与明确拒绝不是结果可观察。GAP-222承接合同仍OPEN。
- 保留capability/021的seed、60条生成轨迹及有限reporter让步；017的one-shot与pre-acceptance release冲突仍待合同裁决。opaque token JSON拒绝只是进程内能力的一项物理边界，不等于全权限矩阵已证。
- cognitive-workspace/007保留TodoSink/MagicTodo及具体已退休压缩桥的禁止断言，不恢复对任意`obligations`字面的无关禁词。
- Predictor共享实例按新WHAT将真实正文写入一次Prepared、分别降为两个消费者的reasoning；保留一次Requested/Bound/bootstrap、原生reasoning不跨、零错误Closed及资源卸载断言。
- 委托稳定handle和单次work须分开；exact AdmittedWork不能由裸Root/link推测。加载期void、迟到结果和结果消费必须沿新scoped事实处理，不放宽新fold恢复旧实现。

## 验证记录

首轮Fable构建gen69通过（1550解析输入、169 Surface、828模块）；以下正式用例先在该实际产物上取得目标红，使用直接Node22 `node --test`作因果诊断，最终验收另走官方runner：

| 红例 | 原因与实际owner修复 |
|---|---|
| Sphinx/036 | 初次仅导出名加载失败，手工对齐018/034/036实际签名后才取得4 pass/4 fail/2 TODO的目标红。注册器zod Emit形参错位导致进程TypeError；计数Number误unbox为int64；控制命令缺expectedRevision。修复显式zod参数、保留越权字段供decoder拒绝、可选replacement/必要revision及原生MCP result协议，不接未实现业务driver |
| Sphinx/019 | 14项中12项在合法canonical创建前即被UnknownEventType拒绝；共享EventStore只登记@1，上游producer已改严格@2。补登记@2、保留legacy@1明确semantic cut；WHAT019同步说明当前严格载体和不自动迁移的历史边界 |
| crash/018、020 | 合法physical admission及cold reopen后，旧ChildRunVoided无法关闭scoped child authority。加载owner改从实际Active work+child root/logical/handle核对取得已有AdmittedWork，写exact ChildWorkVoided；无scope合法旧历史才走原分支，不从Root临时造新准入 |
| capability/019 | native JSON、真实codec及独立process回归3项失败：Fable将toJSON编成静态函数，没有原生prototype成员。AttachMembers使真实JSON调用拒绝序列化；permit的消费/归还/新attempt语义不改，10-D1仍TODO |

旧批255文件、376项integration和harness285只属于`faae4a602`，不能冒充本批结果。首轮check实际发现上游带入26处control-pyramid与2处未登记Persistence Surface引用；没有调低门禁或增加baseline。

gen70修复后定向MCP018/034/036为50 pass、0 fail、3 TODO；加载恢复018/020启用integration为10 pass、0 fail、3 TODO。格式统一后另暴露4处inline分支展开的control-pyramid，按各自业务拆出纯计算/校验或原锁内等待者选择，保持原拒绝次序，不修改baseline。gen71构建通过；其官方446文件实际为2546 pass、18 fail、43 skip、207 TODO，5000ms静默窗、默认并发均未调整。此红例保存在[原始日志](baselines/2026-10-03-sync-e1e7dd3f1-unit-gen71-red.log)，不能因目标Sphinx或capability通过而掩盖。

该轮失败集中于scoped工作引入后旧无scopefixture的准入、固定DevOps的空闲可见性、取消/PTY运行接线，及一个测试标题重复WHAT锚点。范围复核补入JournalWriter的execution-failure-policy、Surface登记相关distribution/verification-system，以及实际调用新Journal参数的relay-retirement和Scope调用方action-affordance，共510个编号文件。

后续因果验证区分合法夹具与生产所有者：

- dispatch/009、010与durable/023先建立真实Human Root和physical child acceptance，不能给无scope旧fixture补一个Root冒充准入。durable/023改悬置下一次确切HandleLinked提交，setup前后revision按实际事实核对；真实parent在setup已存在，不再声称尚不存在。
- lifecycle/009的正式反例在gen72看到重复abandon将revision从8增至9；Controller改按exact work状态幂等处理，旧A重试不终结新B，异child与无准入历史拒绝。009/013共用已有opaque JournalHandle，025及relay/009显式传真实journal；没有另造第二套授权。
- snapshot revision按显式十进制字符串schema导出。gen72反例是实际值为BigInt而非字符串；Fable全局BigInt.toJSON使原生JSON也能运行，不能把它写成JSON必抛异常。
- 固定DevOps道路与单次work分开：raw binding尚Active时，旧work Retired不授权新建物理child。gen72正式003为3 pass、5 fail，覆盖重开创建次数1≠0、固定回调无法完成以及detach残留2个订阅。取消只撤实际普通工作订阅并排空已入回调，保留固定工作；Detach独立结束进程观察资源。SDK夹具以具体registration移除订阅，避免Fable curry wrapper使函数相等失效而假释放。
- gen73、74进一步取得真实取消边界红：受控Host abort悬置时，新普通fork仍被宣告承接；取消失败使两个实际owner的detach漏释放固定订阅；同步cancelSignals抛错后，旧失败flight被永久缓存，第二次取消仍重抛旧错。薄Surface只注入既有真实端口，不复制状态机。实际flight必须先发布再执行，assignment入口在排空期间拒绝新工作，Detach即使取消失败也清理全部本地owner后传播原错。固定终态回调与Join观察不借此获得新授权，也不被assignment门禁关闭。
- JournalSurface的opaque类型依赖CanonicalIntegrator，不能挪到其前面。一次构建红只说明声明顺序错误；按真实依赖重排三对Surface后gen72、73构建成功，不把编译失败算业务反例。
- 七文件诊断运行在5006ms静默后中止，唯一活动文件durable/023在运行真实隔离Fable正负编译；没有可用的正式汇总。该用例原被误放默认unit，现按integrationTest登记，完整integration自动发现36文件并保留全部编译及源文件不变断言。没有加大unit窗口或并发，也没有跳过实际编译验收。

本批原始构建、红绿回归、受影响套件及物理integration日志使用同目录`baselines/2026-10-03-sync-e1e7dd3f1-*.log`。TODO和skip保留，入口非零与真正断言失败分别记账；本批不跑无关全量，也不因此宣称完整release通过。

### 当前合并的正式收口

gen75官方510文件为2818 pass、2 fail、52 skip、245 TODO，唯一失败是观察夹具的真实child准入仍使用已退休coder。改成合法Engineer后，gen76正式023为8 pass、0 fail、1 skip、1 TODO；严格listable仍要求已准入work。真实physical acceptance后，HandleLinked经生产bridge在同一提交建立exact work和child index，并非将raw binding当成新权力。

gen76完整36文件为384 pass、2 fail、16 TODO；两次Host项目`/path`启动请求在原5秒内无响应头。同产物单次schema诊断项目阶段1408ms成功，但不足以证明编译并发是唯一根因。随后全并发verbose诊断为383 pass、3 fail，实际是ProcessHost将启动日志写入stdout、破坏canary JSON协议；原bootstrap失败与仪表污染分别保留。新增两种真实ProcessHost启动回归，诊断改入stderr，保持预算、默认并发、协议和断言。

最终gen77构建成功：169个Surface、828个模块，官方入口确认1794 sources/884 artifacts新鲜。510文件全部排空，**2820 pass、0 fail、52 skip、245 TODO**，进程组回收accepted=true。完整integration自动发现36文件，**386 pass、0 fail、16 TODO**，全部排空并正常回收；package为3 pass、0 fail、1 TODO，harness为**287 pass、0 fail**。完整integration继续开启verbose，原5秒启动预算和文件并发未改。TODO使unit/integration入口仍返回1，不等于断言失败，更不是完整release验收。静态门禁、改动F#格式及非日志diff检查通过。

原始证据：[编号套件](baselines/2026-10-03-sync-e1e7dd3f1-unit-gen77-green.log)、[完整integration](baselines/2026-10-03-sync-e1e7dd3f1-integration-gen77-green.log)、[gen76启动失败](baselines/2026-10-03-sync-e1e7dd3f1-integration-gen76-red.log)、[诊断stdout反例](baselines/2026-10-03-sync-e1e7dd3f1-readiness-stdout-red.log)。gen77证据只绑定当前e1合并；施工期间另收到`b7768f478`，下一次普通merge及其重新验收另记，不把本节自动当作新上游增量的证书。

## 后续计划

未闭合GAP仍137项，因219由OPEN转PARTIAL变为9 OPEN、128 PARTIAL。424项TODO逐项表继续作为计划创建时截面；没有执行新的默认全量，不能宣称新的全仓TODO总数。下一批优先S03的T418/T419固定验证候选，之后按W1—W6推进。Sphinx先接真实业务driver、可读DTO/trace及实际物理能力，再证明create→claim→dispatch→submit→accepted renderer→answer与cancel/recovery；不重造已有canonical store。异模型切换时reasoning的当前请求传输坐标仍需独立场景，本批同模型canary不覆盖它。
