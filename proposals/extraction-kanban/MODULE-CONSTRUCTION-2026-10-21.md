# 已拆模块修正与接收施工

本轮由用户于2026-10-21授权，按上一轮模块分析继续施工，允许subagent调查、实现和独立测试。开始于2026-10-20T21:56:35Z；除非下列全部计划提前完成，持续施工至不早于2026-10-21T02:56:35Z。实际投入、等待及验收分别记账，不用空等或重复全量测试代替工作。

## 固定输入

- 主仓已验候选：`c78c9d53c7aa3037995f2ec09848adaca9407ff7`；其完整GitHub CI [39500822963](https://github.com/dyx13/vibe-fs/actions/runs/39500822963)已完成且成功。
- 本地库：`a12ff4a99d401d9a8e8d9c57cb9a0eb1173a7d1b`，15个公开入口，保留Process/Supervision、Trace、Ownership、时间分居和真实资源关闭保证。
- 本批上游主仓参照：`5880b824f52d6f7bab5ac36dfcb7eff9966fdc41`；其库gitlink为`6c9845db29f2c47ae6b6627111463db73b9abbfb`（0.39.0，35入口）。
- 上游库最新只读参照：`202a41ab39460980ba053e55a9b3780bbaf411ca`（0.40.0，36入口）；dispatch尚未在上述主仓完成接线，单列待交接。
- 原运行目录`/Users/yuanxi/Workwork/vibe-fs`保持`8db33b3429a70d5cdbea30e393363666b3649573`，既有stash保留。施工不等于部署。

固定上游快照将CUT-10/11/12/17/25标为DOING，但这不是实时同事状态；后续核对发现其中已有完成子范围，不能由整卡状态阻止独立接收。本地只对已固定源码做隔离修正，不修改同事的工作树或向其发消息。公开接口、包元数据、锁、gitlink、构建图与最终集成由root串行处理。

## 工作包和验收

状态仅在BOARD维护，本文保存范围、命令和证据，不把库内通过写成主仓整链完成。

| 包 | 范围及当前owner | 完成条件 |
|---|---|---|
| MF-00 | root：基线、职责与补丁保留表 | 固定两仓输入、证据入口、运行时与写集合；原工作区不变 |
| MF-01 | root：独立sandbox及真实主仓接线 | 接收上游已修边界，保留deadline借用、park/unpark/abandon和真实api语义；独立库、包外消费及真实Host正反例均通过 |
| MF-02A | delivery_audit：交付后停止的物理输入绑定 | A交付等待期间B准入，A的完成不得指向B；同A正控实际登记并只停止一次 |
| MF-02B | root：delivery/prefix/compression；projection待交接 | 独立状态职责承接sealed材料和captured keys，持久见证和冷恢复继续使用原材料；旧owner退出；真实父工作与Join验收 |
| MF-03A | physical_storage_audit：legacy续接崩溃恢复 | series已接入legacy完整前缀后中断仍能重开，只补缺失后缀；同id异字节、分叉、半行继续拒绝 |
| MF-03B | root：spool/PTY/time等待组合 | 输出逐字节完整、Git双路语义、真实取消/期限/EOF及Unknown保留；不替换已验ProcessResult和custodian协议 |
| MF-04 | root：events/convergence/retention/segments/quarantine | 按事实族接收纯规则，锁和持久副作用仍有明确owner；真实多writer、崩溃重开与坏记录反例通过 |
| MF-05 | 待交接：identity/authority/routing/permit/capacity/admission/dispatch/session | 许可精确释放一次、旧回执不影响新运行、未知发送不重发；同事未交接部分不假定已完成 |
| MF-06 | 后续：Casebook、resources/fs/wait余项及degeneration/ablation成本 | 先收束真实功能链，按可复现测量选择优化；不因模块数或行数改写已可用实现 |

第一批同时施工的精确文件互不重叠：root处理库sandbox与主仓Process/JsSandbox；MF-02A处理ToolRegistry/ToolRuntimeScope及work-026；MF-03A处理ProcessEventLog及durable-events-005。主仓构建前全部源码写入者暂停，冻结同一候选；不边改边验。时间、启动和物理期限的敏感实验串行。

## 必须保留的已验合同

resources替换前裁剪模板，注入值原字节不裁剪；Time/NodeTime/TimeTesting分居，UtcNow实际声明为Date；Wait只消费Observer且不写调用方deadline；owned drain用raceExitKeepingAlive保持期限结果可达。上游目录覆盖会回退这些修复。

交付视图必须在exact provider/call结算时封存材料及keys；peek不推进，消费只推进已封存keys；迟到Anchor/prose不得改写结果；冷恢复读取持久witness.View。上游delivery纯格尚不承接全部上述保证，不能直接替换本地wrapper。

Process保留真实End/StopRequested/Output/Fault结果、compiled Bun Worker私有运行模式、背压、逐路EOF和未知close留账。PTY关闭请求不是物理终态，不能丢弃Unconfirmed。

## 已观察的问题与处置

| 记录 | 证据与后续 |
|---|---|
| MF-D1 | 异步交付后才读取ambient physical可能把A的停止登记到B；MF-02A做真实交错反例 |
| MF-S1 | series接完legacy一条后中断、未unlink时重开报diverged；MF-03A保留base-red后修恢复协议 |
| MF-S2 | 同inode等长改写历史前缀再追加，热读旧前缀而新进程读新前缀；暂记P2，核对允许的外部写入边界和成本合同，不用每次全扫掩盖 |
| MF-P1 | 上游spool合并双路，GitAdapter据此取stdout；接收前必须保住机器输出语义 |

原始证据按候选、运行时、命令、退出码、pass/fail/skip/TODO、文件排空与进程收尾记录。普通失败不能转TODO/skip；既有TODO不算通过；同一用例的重复执行不相加。

## 交付纪律

### 第一组验收

MF-02A与MF-03A的统一候选经gen95正式入口验证：durable-events/005、work/026、039共38通过、零失败、零skip/TODO，3/3文件排空，进程组回收通过。严格S4夹具直接传播准入错误并检查持久authority/physical Accepted；它不证明provider已经开始执行。独立交付邻域gen94为21/0；存储邻域为32/0/3既有TODO，严格出口1，不能算全绿。原始日志、红例与边界见[验收收据](../../evidence/module-construction-20261021/delivery-storage-receipt.json)。

交付修复还包含fresh Started之后的持久Accepted结算、重读blocker、Unknown保留不重发、NotStarted清理后拒绝，以及精确退休事实回放。停止登记只用owning assistant的真实user parent，不借当前输入。Host仍只有session级abort，不能闭合检查后接纳新输入的原子窗口。

续接修复仅承认完整规范字节一致的series后缀/legacy前缀；重复旧ID、同ID异字节、分叉和半行继续拒绝。跨段、Unicode、独立冷读与二次恢复已验；完整native write/fsync故障切点矩阵仍未完成。

### 第二组候选

库`3db7b439283779f6037244d57cf05e6ec3e7ab31`已普通推自有main并核对远端确切SHA。18模块同源构建，prefix/compression/delivery在Node22与Bun各73/0，独立反证各6/0；完整tarball的18个JS/types入口与安装后字节一致，公开导入及strict TypeScript正反消费通过。临时包不入Git。

主仓gen96与原检查器/债务基线通过。目标正式集合为：prefix/Host/存储6文件48/0；PTY cwd与wait 3文件42/0；compression/delivery 2文件23/0；冷journal/Join 2文件16/0。均零skip/TODO、排空并回收通过。冷journal重开不等于真实provider/OS全链重启；后续完整发行及新候选的精确结果另记。

首组完整CI在`8fa662c9e`因ProcessEventLog四处控制流嵌套失败；本轮已提取窄helper并在gen96重建后验证续接48项集合，未更改检查器或基线。PTY相邻003为59/1：旧probe把尚未创建的control目录当cwd，新拒绝合同正确阻止启动，夹具需改用独立已存在workspace；原失败保留，不能记全绿。

quarantine当前只有Integrator一个消费者，本地已有exact FailedEventId guard。固定上游纯包加consumer实测会被旧cut解除新故障；本轮补021独立OS冷重放保护，不为模块数量接弱wrapper。wait只删除proof Surface对caller deadline的计数字段写入；生产Await行为与库相同，保留现状。

隔离sandbox又复现Host异步调用Callable.next中的死循环在Node22/Bun均需外部SIGKILL（2500ms），所以Node基础14/0不构成完整期限证书。Bun文档的afterEvaluate声明与本机1.3.14实测不一致；候选继续隔离，不能通过skip或降低旧Host功能来接收。

先反例、合同、实现，再独立产物与主仓组合。程序代码手工局部编辑；只用Fable正式构建入口，不运行dotnet build。库提交先验证并普通推自有main、核对可获取SHA，再更新主仓确切gitlink；主仓按包普通推master，禁止force push。临时tarball不入Git。

### 第三组候选与交叉复核

库`40434d4`接收Casebook纯索引规则，`c4bc2ca`接收retention，均已普通推自有main并核对确切SHA，最终0.9.2为20个公开入口。Casebook保留160个旧hash向量，修正截断Unicode造成的UTF8 locator/fetch断链；主仓继续持有实际Case、容量、dirty/frozen状态和int64 epoch。retention只接UTC日/活动时间与尾行分类；数值ObservedAt不再经Date.parse强转，无法解释的尾行仍回退producer mtime，保留原磁盘反扫、跨段、锁与canonical拒绝。

durable-events/005新增六个实际write/fsync/unlink故障切点：原生调用成功后通过IPC停在真实worker，父进程核对锁owner再SIGKILL；独立OS进程冷读、恢复及二次幂等，合计七次真实强杀。public writerEventIds DTO走完整readStreams，不消费Fable内部tuple。证据只支持这些进程崩溃切点，不支持断电、任意半字节写入、多writer完整收敛或GAP-151销项。

交叉复核保留P2：Casebook既有32位hash碰撞可令第二locator取第一答案；合法legacy为series前缀时，旧legacy活动可能覆盖series的新活动；同inode历史等长重写与热cache差异；最新未处理prefix candidate可依次选择较旧观测；164KiB真实Git完整stdout因mixed spool安全围栏不可用。上游合流spool不能恢复丢失的通道，不能把它猜成stdout。readLatestTail虽为全文件异步扫描，但当前零生产调用，64MiB温缓存约14.5ms只是P3潜在成本，不改为热路径优化。小边界暂记，不扩大本批。

degeneration递推/阈值与主仓同构，生产token和包络仍来自仓库唯一源；ablation纯DAG规则亦同构，主仓还须持有registry/profile/环境与借用面。本轮不为模块数增加适配。另已复现无效profile加载失败后空registry缺项默认Active的真实放行链；已落实已映射feature缺项拒绝的窄修，保住无配置全Active及未映射tag行为，待最终验收。

### 发行入口与门禁记录

release-02在755/756文件排空时撞本机300秒兜底，没有总通过数；016并非卡死，184次工具启动均观察到退出/排空。曾尝试拆分016，但requirement-system/018要求同条款在单文件，release-03如实报4519/2，760/760排空；拆分已撤回，结构门禁未放松。改为自动发现时按静态文件体积优先调度，显式文件选择保序；这只是启发，不把体积或调度当作因果推进。

OpenCode 023改用owned有限物理等待，原5秒普通静默不变，gen98正式2/0、1/1排空回收，真实canary约5.5秒。npm版本探测补现有5秒截止；持有CLI反例红转绿、正常版本断言保留。delegation/031迟到完成夹具在取消前保存真实child，禁止给null session伪造交付。

release-04/gen101的格式、静态、构建通过；单元4521/0、757/757排空回收，219.3秒，另110 skip/277既有TODO由原政策单列。集成suite在38/40文件排空时达到本机300秒总兜底，022编译证明与016工具夹具仍活跃，没有总通过数；残留组被回收但accepted=false，不能算通过。distribution子检查通过，harness为291/0。最终候选按`.github/workflows/ci.yml`原有CI环境（静默30000ms、总兜底900000ms、WXS_ACCEPT_TODO=1）重验，预算表与工作流不因本轮改变。原本机失败保留。

两项长时物理验收运行中，尚未验收：固定3db7包的Node22/Bun各2h Process Capture接2h Supervised Capture；gen100隔离复制产物的真实Bun PTY持续2h，完成后验证EOF、Unknown与恢复回收。后续c4bc2ca未改process/supervision/time/native源码或构建脚本；不能用监测快照或一次存活冒充长测通过。

本组原始收据、交叉审查、失败尾部及完整日志摘要见[补充收据](../../evidence/module-construction-20261021/supplemental-receipt.json)。其中producer收据的未推送/待接线字段保留交接时原状，由root收据另记之后的确切发布和集成，不能把不同时刻混作同一验收。

release-05/gen102按原有CI配置完整通过，849.3秒：单元4524/0、757/757排空；集成suite725/0、40/40排空，distribution子检查及harness291/0，e2e与最终包验证通过。既有unit 110 skip/277 TODO、integration 7 skip/7 TODO单列，不算已验事实。消融四条需求另以原5秒静默取得22/0/1既有integration skip、4/4排空回收；独立真实ToolRegistry在解析错误body之前拒绝无效profile。细节见[完整发行收据](../../evidence/module-construction-20261021/gen102-release-acceptance.json)。这不代表部署、新上游可合并或长测完成。

MF-05已转交接前准备：CUT-10的routing/permit/capacity/queue存在固定已合入子范围，borrowing另列；CUT-11库dispatch先行不等于固定主仓已接；CUT-12没有独立execution-session入口，现有session实为Ownership；CUT-17有完成子范围可独立审；CUT-25还余完整基类/generator。permit下一子包可完整移走旧许可lifecycle owner，但上游float桥会缩窄本地Int64 credit/fence，须先补exact bigint合同、独立包及主仓邻域证据，不能只为入口数量迁移。

各包完成追加实际证据和剩余范围。批次末做统一CI及PR状态核对；部署另列。本轮新上游差异不自动扩大固定输入，也不关闭历史GAP或把局部修复算作全系统验收。
