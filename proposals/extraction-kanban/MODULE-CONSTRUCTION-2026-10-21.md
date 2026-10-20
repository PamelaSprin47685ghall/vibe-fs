# 已拆模块修正与接收施工

本轮由用户于2026-10-21授权，按上一轮模块分析继续施工，允许subagent调查、实现和独立测试。开始于2026-10-20T21:56:35Z；除非下列全部计划提前完成，持续施工至不早于2026-10-21T02:56:35Z。实际投入、等待及验收分别记账，不用空等或重复全量测试代替工作。

## 固定输入

- 主仓已验候选：`c78c9d53c7aa3037995f2ec09848adaca9407ff7`；其完整GitHub CI [39500822963](https://github.com/dyx13/vibe-fs/actions/runs/39500822963)已完成且成功。
- 本地库：`a12ff4a99d401d9a8e8d9c57cb9a0eb1173a7d1b`，15个公开入口，保留Process/Supervision、Trace、Ownership、时间分居和真实资源关闭保证。
- 本批上游主仓参照：`5880b824f52d6f7bab5ac36dfcb7eff9966fdc41`；其库gitlink为`6c9845db29f2c47ae6b6627111463db73b9abbfb`（0.39.0，35入口）。
- 上游库最新只读参照：`202a41ab39460980ba053e55a9b3780bbaf411ca`（0.40.0，36入口）；dispatch尚未在上述主仓完成接线，单列待交接。
- 原运行目录`/Users/yuanxi/Workwork/vibe-fs`保持`8db33b3429a70d5cdbea30e393363666b3649573`，既有stash保留。施工不等于部署。

上游同事仍持有CUT-10/11/12/17/25的后续编辑面。本地只对已固定源码做隔离修正，不修改同事的工作树或向其发消息。公开接口、包元数据、锁、gitlink、构建图与最终集成由root串行处理。

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

先反例、合同、实现，再独立产物与主仓组合。程序代码手工局部编辑；只用Fable正式构建入口，不运行dotnet build。库提交先验证并普通推自有main、核对可获取SHA，再更新主仓确切gitlink；主仓按包普通推master，禁止force push。临时tarball不入Git。

各包完成追加实际证据和剩余范围。批次末做统一CI及PR状态核对；部署另列。本轮新上游差异不自动扩大固定输入，也不关闭历史GAP或把局部修复算作全系统验收。
