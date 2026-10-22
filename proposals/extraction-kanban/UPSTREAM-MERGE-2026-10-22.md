# 2026-10-22 upstream 状态与合并计划

初次核对截至 2026-10-22 11:21（Asia/Singapore）；下文调查结论保留该时点。用户随后授权实施、验收后提交推送，现已在独立工作树执行真实合并，运行版本不动。用户再次要求检查并合入后续更新，新增范围见第 10 节。施工状态见 [BOARD](BOARD.md#upstream-merge-20261022)，版本及完整初始冲突清单见[预演记录](../../evidence/upstream-merge-plan-20261022/preflight.json)。

## 1. 判断

原模块拆分已进入收尾：上游看板 47 项中 45 项 DONE、1 项 REMOVED、1 项 DOING，独立库已到 0.66.0、52 个公开入口。但当前 master 同时包含尚未验收完成的宿主中立接口改造（HNI）、编程接口与子工作终态变更，以及最新的增量构建改造，不能把整批更新当成文件搬迁。

建议固定这一批输入，在独立工作树完成两仓真实合并。先保住库契约与构建证据，再处理交付、恢复和 Host 接线；同一主仓候选验收通过后才进入自有 master。后续 upstream 新提交另开增量，不在施工中持续移动目标。原运行工作树继续使用原版本，源码合并与运行升级分开。

## 2. 固定输入与实际差异

| 对象 | 自有版本 | upstream 版本 | 共同祖先 |
|---|---|---|---|
| vibe-fs | `87be7c64106c8af62cb7eec2b4d5f4e0098d0475` | `939ada1ebac6765b50543dc42f9e795866d7cb91` | `11052493bedb4edbcbe0c341fa677afd44c486f1` |
| wanxiangli | `45849f51778f6a12dcafbc12ce1c8c7f7f65502b` / 0.11.0 | `663ba59b8282db5e3eeccda0c1100ea8cdca0dd0` / 0.66.0 | `a8b89a45607572a62e1ca085c907fb02c878e6c0` |

- 主仓自共同祖先分出自有 118 个、上游 490 个提交。上游实际增量涉及 1,470 个路径，新增 64,096 行、删除 27,185 行；按路径变更为 433 新增、911 修改、57 删除、69 重命名。
- 主仓三方预演产生 182 个冲突路径：源码及编译清单 94、项目文件 2、requirements 74、resources 8、计划入口 2、锁文件 1、库 gitlink 1。独立库另有 39 个冲突路径。
- 两个 tip 直接相减会把上游不存在的自有文档、测试和证据全部算作删除，不能用这种差异决定接收范围。以共同祖先做三方合并，并逐项记录自有改进的去向。
- 原运行工作树仍在 `8db33b3429a70d5cdbea30e393363666b3649573`，有用户未跟踪文件 `万象术独特功能导论.md`。本计划工作树从最新自有 origin/master 建立，不移动原工作树，也不处理该文件。

预演使用 `git merge-tree --write-tree --name-only <ours> <theirs>`，没有执行 checkout、修改索引或在产品工作树落下冲突。返回码 1 表示发现冲突，不是一次失败的产品构建。临时对象和原始输出不入库。

## 3. 上游进展与证据边界

### 已进入收尾的模块拆分

固定版本的[抽取看板](https://github.com/PamelaSprin47685ghall/vibe-fs/blob/939ada1ebac6765b50543dc42f9e795866d7cb91/proposals/extraction-kanban/BOARD.md)已将 CUT-19-c、23、28、29、31、34、35、38 收尾：Git 协议、fission-group、warm-start、ordinal/bayes 等有了去向；不再使用的算法和导出被删除，Journal 保留明确的事实适配职责。DONE 中包含限定保留、删除和盘点，不能全部计为“已经抽成库”。

库从自有 22 个入口增至上游 52 个入口。接收要核对真实消费者及旧职责退出，不能以版本号或入口数代替收益。0.66.0 的版本提交明确没有运行 build/test；CUT-38 的历史盘点也记录了失败基线，不是当前 tip 的完整发行证明。

### HNI 仍在施工

[HNI 看板](https://github.com/PamelaSprin47685ghall/vibe-fs/blob/939ada1ebac6765b50543dc42f9e795866d7cb91/proposals/host-neutral-interface/BOARD.md)中，01—04、07 为 DONE；05、06、08、09、11 为 DOING；10、12 为 BACKLOG；13、14 为 DECIDED。

- 工具执行门、准入表已移到 `Execution/Tool`，交付 attempt ledger 移到 `Mission/Work`；这部分要接收实际职责迁移，同时删除旧路径。
- 插件激活、配置、压缩已接上部分 `Host/Ports`，P-06 transform 和 P-07 signal 尚未完成。Fork 的中立 session/transcript 消费接线、JoinTool 部分产品规则仍未迁完，HNI-14 的端口形状收敛也未实现。
- 最后新增的 `939ada1eb` 将产品诊断接入 P-12 `IDiagnosticSink`，涉及 6 个文件；已纳入最终预演。验收需覆盖无日志能力、sink 抛错、重启安装/清除、脱敏与字段白名单，避免诊断失败影响主流程或重复路由。
- 中立 `Interrupt` 在当前 OpenCode v1 适配里仍调用 session abort。名称变化不等于已有原子的 attempt 定向中断，不能据此关闭本地 MF-02A 的窗口问题。
- OMP 18.12.4 第二宿主仍未通过真实 smoke。源码已存在 F# 适配和三个 owner 编译项目，旧看板中“未入编译图”的说法落后于代码；但加入编译图仍不证明产物可加载、真实绑定和调用成功。本批保留已落地代码及诚实能力声明，不把完成新宿主作为 OpenCode 合并的必需范围。

### 当前 CI

| 固定候选 | 结果 | 能说明什么 |
|---|---|---|
| 自有 `87be7c641` | [CI 39554912942 成功](https://github.com/dyx13/vibe-fs/actions/runs/39554912942) | 自有当前发行基线可用；已有 skip/TODO 和已记录限制仍保留 |
| 上游当前 `939ada1eb` | [CI 39716220780 运行中](https://github.com/PamelaSprin47685ghall/vibe-fs/actions/runs/39716220780) | 11:21 尚无完成结果，不计通过 |
| 上游前一提交 `9ffb26a66` | [CI 39714617853 失败](https://github.com/PamelaSprin47685ghall/vibe-fs/actions/runs/39714617853) | `verify:release` 在 `format:check` 因 19 个文件未格式化而停止；后续编译和行为测试未获得本次 CI 证明 |

更早的上游候选 `03944549b` 也在同一类格式门失败。`939ada1eb` 只修改诊断接线和文档，没有修改前次被报告的 19 个格式文件；这是尚存格式风险的静态依据，不能替代新 CI 的实际结果。不能从格式失败推断行为测试必坏，也不能把未运行的阶段算作通过。本次没有运行产品构建或测试。

## 4. 合并时先裁决的行为

### 编程面与报告材料

按上游 [repository-programming WHAT](https://github.com/PamelaSprin47685ghall/vibe-fs/blob/939ada1ebac6765b50543dc42f9e795866d7cb91/requirements/repository-programming/WHAT.md) [053]、[054]、[056] 接收新模型，并让定义、实现、调用方、双语资源和测试一起切换：

- `fork/resume/commission/devops/dag` 使用成员层 `reportMapping`，`deliver(reportMapping)` 同样使用这份映射。它是编号到 null/非空摘要的记录，必填，空对象合法，不含 `prefixUntil`。
- 当前会话顶层的 `prefixUntil + thenMap` 仍是另一层声明。不能删除这对参数，也不能把它误当成旧报告形状。旧 `view` 包装不留隐式兼容通道。
- 委派统一走编程成员，清理独立旧工具注册；生成接口、运行时绑定、模型教学和真实 Host 入口同时核对。`dag` 的共享映射、park/unpark/abandon 恰好消费一次以及未消费 handle 阻止交付要有反例。
- 保留我们已验证的材料封存、精确 provider/call 绑定、peek 不消费、仅消费捕获 keys、迟到散文不改写已交付材料；不能因换参数形状丢掉这些保证。

### voided 与重启恢复

这项是行为变更，不能只消解 `crash-reconciliation`、`delegation` 和 Fold 的文本冲突。

计划采用上游“交付已不可能则 voided”的子工作结算，同时保留本地目标连续性和未知效果保护。仍可交付的自然 stop 保持活跃；取消、abort 或重启加载时确实已不能交付的旧子工作，以真实身份作废。它不产生完成材料或完成见证，join 报告一次后持久退役，第二次加载不再报告、重复作废或重新执行。

自有旧合同允许未交付工作跨重启续接；需要把“仍可续接的逻辑目标”与“已失去交付可能的那次子工作”分开。恢复路径不得从 Root 猜造准入身份，也不得靠重放未知发送恢复进度。新输入若需续做，应按合法身份建立后续工作，不能复活已作废记录；用户目标不会因一个 child voided 自动完成。

验收同时覆盖：自然 stop、显式 ask 与真实回复、交付后尚无终结事实即被取消、未交付重启、仍可继续的工作、首次及二次冷加载、一次 join 退役、迟到回执、未知派发不重发。保留本地 write/fsync/unlink 切点和跨进程读取证据，按新合同改预期，不能删除反例求绿。

### deliver、relay 与物理中断

接收 relay 后继先排队、Relayed 不触发终结中断、Delivered 后持久退役等上游修正。交付已提交、中断已请求、物理停止已观察必须分开；中断失败不回滚交付，session 级 abort 不冒充精确 attempt 中断。S1/S2/S3/S4 的上下文和后继行为沿真实调用链验证。

## 5. 自有改进的保留清单

依据 [10-21 施工与收尾记录](MODULE-CONSTRUCTION-2026-10-21.md)，逐项给出“迁入新 owner / 仍由原 owner 承担 / 被等价实现替换”的证据，不整文件选择 ours 或 theirs。

| 边界 | 不能退回去的行为 | 接收时的处理 |
|---|---|---|
| permit / routing | number 或 bigint 的 Int64 精确性；`false` 拒绝且不进入 scheduler；非法返回不能被当作成功 | 先合库实现、声明、反例，再改主仓调用者；检查模型标识中的冒号和持久解码 |
| delivery / prefix / compression | 封存材料、一次消费、精确调用归属、冷恢复见证 | 迁入新模块与 reportMapping 接线，保留跨边界测试 |
| eventstore / retention / quarantine | 写入与 fsync 边界、legacy/series 续接、时间类型、exact FailedEventId 围栏 | 保留原生切点与二次恢复；旧 cut 不能解除新故障 |
| process / supervision / PTY | EOF 和完整输出、背压、TERM→KILL、真实 Closed/Wait、结束与停止请求区分 | 保留 Node/Bun 实际监督链；库未提供等价职责时保留唯一自有 owner |
| time / resources | observer 不改调用者期限、必要 drain 存活、Date 语义；模板替换前裁剪、替换值原字节保留 | 不因上游入口清单不同删除 `time-node`、`time-testing` 或物理适配 |
| sandbox / parked handle | 真实期限、取消、现场回收与权限；严格 handle 归属 | 上游 membrane 不代证无限循环可终止；独立 guest 原型尚未生产接线，不夹带进入本批 |
| ablation / Casebook | 无效 profile fail-closed、Unicode 边界 | 合并已有修复和反例；不扩大成整库重写 |
| Git / 运行数据 / 分发 | 真实 Git I/O 和锁、主工作树 `.Herdchestra`、源码与运行分离、干净克隆可构建 | 接收 git-porcelain/integration 时保留物理边界；不恢复已退役 `.git` 运行数据，不提交 tgz |

现存限制仍单列：Host attempt 中断窗口、暂停 custodian 后的 PTY recover 占位、混合 spool 的 Git 大输出通道丢失、部分历史 retention 取材和 Casebook hash 碰撞。合并不自动修完它们；涉及的行为若出现新退化，则阻断本次交付。

## 6. 施工顺序与完成条件

以下是一个主仓集成候选内的处理与验证顺序，不把尚未完成冲突消解或编译的中间状态推到 master。公共类型先定合同，调用方随后，旧实现最后退出；独立读取和验证可并行，同一公共文件的修改串行。

| 包 | 前置 | 工作与写集合 | 完成条件 |
|---|---|---|---|
| M22-0 调查与计划 | — | 两仓版本、CI、冲突清单、语义处置与本计划 | 已完成静态调查和非落盘合并预演；不代证产品验收 |
| M22-1 基线与构建 | 0 | `scripts/build.mjs`、`scripts/lib/owner-compile.mjs` 及实际构建支持文件、相关测试、编译图 | 记录两侧已有失败；接收前核对影响闭包、缓存和冷启动合同。上游格式问题在候选中逐项处理；不得提高预算或改失败基线伪造通过 |
| M22-2 独立库 | 1 的合同核对 | wanxiangli 的 39 个冲突；各新增公开入口及消费者清单；库 manifest/lock | 精确 Int64、材料/时间/物理监督等自有合同保住；受影响模块、真实包外消费及 Node/Bun 验证通过；库提交固定且可从自有远端取得 |
| M22-3 编程、交付与恢复 | 2 | Repository 编程面、Delegation、Mission/Work、Composition/Durable、Context、资源和对应 WHAT/测试 | reportMapping、voided、deliver/relay 作为一致语义闭环；冷加载、一次退役、未知效果不重发与材料封存通过 |
| M22-4 执行与宿主接线 | 2、3 的契约 | Routing/Capacity、Dispatch、Session、Authority、Failure、Fission、Git，以及 `Host/Ports`、`Host/Domain`、OpenCode/Plugin/Tools | 每个新库/port 有真实消费者；完整主流程通过，物理边界未退化；HNI 尚未完成范围如实保留，不靠无损性未知的适配假装迁完 |
| M22-5 清旧与完整验收 | 1—4 | fsproj/compile-order、旧路径和重复职责、主仓 manifest/lock/gitlink、文档与证据 | 唯一 owner、正确构建闭包、公开包安装、定向与完整发行在同一候选上通过；既有失败/skip/TODO 分列 |
| M22-6 源码交付 | 5 | 自有库 main、主仓 master、收据与本看板 | 库先可获取，主仓记录精确 gitlink；常规非强制推送，远端精确 SHA 的 CI 通过；源码交付和运行升级分别报告 |

M22-1 的构建改动没有文本冲突，但风险不能省略。`9ffb26a66` 移除了 `--clean` 和全量回退路径，改为按影响闭包编译；必须验证空缓存/无产物、产物缺失或损坏、删除源文件、修改 `.fsi`/项目依赖、工具链变化、失败构建不冒充有效产物。干净克隆应能按同一规则生成所有必要产物，不要求恢复已经删除的命令开关。

M22-2 先在自有库当前 main 上整合固定上游，保留双方历史。`supervision`、`time-node`、`time-testing` 等自有职责是否退出，以调用方迁完和物理证据为准。主仓 `.gitmodules` 保持自有库地址，npm 保持 `file:wanxiangli`；更新 gitlink 时同时核对 manifest、锁和真实消费。库若有更新，先正常 fast-forward 再核对差异，不覆盖工作或改写 main。

M22-3 与 M22-4 有跨文件接线关系，实际修改可在同一候选串行交错；切勿为了凑独立提交保留旧 facade、双重注册或两份状态 owner。主仓采用真实 merge 保留共同祖先，不用 reset 覆盖自有分支，也不把 490 个提交逐个摘取当成默认路径。

## 7. 验证安排

先按受影响合同运行 requirement 套件；定向通过后再对同一最终候选跑完整发行。测试映射以标题中的 `WHAT[PREFIX-NNN]` 为准，不只看文件名。新增用例沿用 `NNN.test.mjs`，未知、未运行和 TODO/skip 不计为通过。

| 主题 | 必查 requirement 包 / 证据 |
|---|---|
| 构建与公开包 | `structured-workflow` [011]/[012]、`verification-system`、`distribution` [011]；库公开入口包外导入、声明与实际导出一致，冷克隆安装 |
| reportMapping 与工作终态 | `repository-programming`、`delegation`、`context-compression`、`work`、`crash-reconciliation`、`durable-events`、`obligation-ledger` |
| 权限与调度 | `execution-model-routing`、`capability-enforcement`、`interaction-authority`、`dispatch-protocol`、`effect-accounting`、`execution-failure-policy` |
| Host/provider 与上下文 | `host-boundary`、`host-provider-failure-ownership`、`managed-chat-execution`、`managed-session-lifecycle`、`provider-attempt-recovery`、`provider-projection`、`prefix-stability` |
| 物理执行与资料 | `process-execution`、`time-capability`、`change-integration`、`durable-convergence`、`knowledge-reuse`、`feature-ablation`；保留既有真实进程、文件系统和 Git 反例 |

实际插件/公开安装入口至少覆盖：启动与配置、一次普通工具调用、fork→deliver→join、无交付的自然 stop、取消后 voided、重启后二次读取、ask 后真实回复、relay 后继，以及错误/未知派发。只有纯函数或手工 stub 通过不替代接线证据；不重复执行真实外部效果作新旧比较。

构建使用 Fable 的 `node scripts/build.mjs`；日常验证使用 `npm run format-build-test`，最终发行使用仓库现有 `npm run verify:release`，不运行 `dotnet build`。先读当前验证器与目标套件的运行约定，不绕开监督器制造“测试通过但进程未回收”。完整发行通过后，仅在新修改、新失败或未解疑点出现时补跑相关验证。

两小时/四小时历史长测不重复计时，也不把旧时长计入新候选。若物理监督、PTY、持久日志实现被实质改动，按改动影响重跑相应真实场景；否则保留既有证据并补跨包接线验证。完整产品部署、OMP 真模型调用、guest sandbox 生产接线以及与合并无关的旧 P2 修复不夹带入本计划。

## 8. 集成、收据与停止条件

1. 施工开始时重新核对两仓工作区及远端。自有 master 若已前进，先接收新自有提交，再重做差异与冲突清单；上游目标仍固定，新增提交另列。每包记录 why、合同、写集合、候选 SHA、命令/退出码及剩余边界。
2. 在独立候选完成库合并及公开包验证，再推进主仓真实合并；公共 manifest、锁、gitlink、编译图和看板由同一集成顺序维护。原运行目录及用户文件不动。
3. 不使用整树 ours/theirs，不删除失败测试、不提高超时或基线求绿，不替未知持久事实补造身份，不复制真实副作用作双算。不再被使用的实现、工具和依赖明确删除。
4. 发布前核对远端 main/master 可快进；若有并发更新，先整合并重验受影响范围。库先普通推 main，主仓随后普通推 master，均不 force push。临时包、编译产物、冲突对象和调试文件不提交。
5. 最终收据区分本地定向测试、完整发行、远端 CI、源码推送和部署状态。任一主要流程的新退化、无可复现构建、丢材料、重复效果、提前清账或物理资源未收尾，均阻断交付。既有未解决限制按原证据登记，不自动关闭整个 CUT/GAP。

源码回退用后续 revert 提交并恢复成对的主仓/库版本，不改写 master；运行仍在原目录时无需操作运行数据。若以后单独执行运行升级，须另记精确源码、入口、状态兼容性与回退步骤；不能因为本计划提交或源码合并成功就宣称已部署。

## 9. 本次交付范围

M22-0 已完成。M22-1—6的状态、验收证据与剩余边界统一见[最终结果](../../evidence/upstream-merge-plan-20261022/final-result.json)。主仓 merge 从计划提交 `540fe20e7` 合入固定 upstream，原运行目录及用户未跟踪文件保留。

库的首个真实两父合并为 `c1aa9af87a61b0a37fc712daf1dc346fe1f7cbd5`，55 个公开入口、版本 0.66.1；其[首轮证据](../../wanxiangli/evidence/upstream-merge-20261022/README.md)仅覆盖该次提交。最终库候选 `0ef8ad1608615118b178e148263b56660d04be4a` 已普通推送，包含 whole-run 恢复预算与新增视图法则：Node 全套1018 pass/0 fail/7 skip，Bun视图/预算29 pass/0 fail，真实包外Node22/Bun消费通过。Bun两项既有硬期限失败继续单列，M22-2仍待主仓同一gitlink的完整消费验收。

关键25文件历史批次为275 pass/0 fail/2 skip/6既有TODO，不能代证最终完整发行。完整发行第六轮N2在300秒总门禁完成58/766，第七轮N4与第八轮N3分别触发真实场景5秒门禁，均无权威总数。用户随后明确允许本次本机整轮采用CI现有15分钟总限时；这只准本次运行配置例外，单项5秒、有限物理期限、真实回收、源码默认预算及失败基线不变。最终正式阶段与源码交付见[最终结果](../../evidence/upstream-merge-plan-20261022/final-result.json)，历史失败和处置见[施工记录](../../evidence/upstream-merge-plan-20261022/execution.md)。

明确保留唯一的本地 Int64 CapacityLedger/Queue/Borrowing、失败预算重放和物理监督 owner；上游浮点包装不能等价表达这些已验证合同，不借目录迁移宣称职责已抽完。最终收据分别记定向、完整发行、源码推送、远端 CI 与未部署。此前 MF、CUT、HNI 或 GAP 不因本批合并自动关闭。

## 10. 用户追加的 upstream 更新

本次 fetch 发现主仓新增 29 个提交，目标扩展到 `155309b880b3684255204bff429278814db4c308`；相对原输入涉及 231 个文件、8,240 行新增、2,768 行删除。独立库 main 没有前进，仍为 `663ba59b8282db5e3eeccda0c1100ea8cdca0dd0`，自有库固定 `cf54390`。

保留已解决的三方合并和本地修复，再对原 upstream 到新目标的差异做三方整合，冲突逐项手工处理。新增权威终态来源、Host 接线、交付策略和验证器因果状态都进入本次验收范围；不能仅因文本合并成功沿用旧候选结论。验收通过后才创建主仓交付提交，并确保新目标成为真实历史祖先，普通推送 master。若补记新 upstream 历史时树内容发生变化，须重验变化范围。

用户已明确仅在本机验收。远端隔离目录只做过工具准备，候选源码未获传输授权，停止该路径。验收、提交、推送与部署仍分别记录，运行目录继续保持原版本。

随后复查又发现 `13c00cb8437b4332939ed38f839bca39b1f252cf`，本次目标再次扩展至该提交（较前次目标新增 1 提交、23 文件）。交付经过截断后仍须保留材料，同轮前序散文归属一并验证；与本地已验证边界冲突的“交付立即完成”和“未知 life 默认 Child”不能照搬，须保持真实终态及唯一归属证据，并同步需求和新反例。合并、完整验收和历史祖先记录均以这个新目标为准。

再次按用户要求检查后，目标扩展为 `720ef576db4b5577712c1b0cbaf85f72a0f59373`，比13c新增5提交、51文件。新JS单命令、命名park、字面where及manager closure kind进入本次范围；实际进程关闭、完整采集、取消和lease不变量必须继续成立。本fork保留已验Callable工程循环，并列接入直接字符串命令，两者共用真实资源内核；name是可读标签，实际handle每次唯一，不让旧unpark重放恢复新现场。上游最近用户猜归属、用view填补散文、交付代替Host终态及直接spawn伪造关闭不接收。真实散文归属成立后允许空sealed View；空mapping的真实测试不能冒充空sealed View证明。

该批库gitlink指向 `b1ba965`，只改AGENTS两行。已从已验 `003995a` 普通两父合并为 `63286b49217b6c4e0f6be084779fd8e1f2296a1b`，除AGENTS外全树blob与003995a一致；不重写已验库历史。主仓最终两父提交、完整验收及祖先检查均以720ef576为目标，运行版本仍不动。

验收前最后一次只读复查发现 `32ec1423745602ce1ea433bade781b8f3d941bb5`（比720新增1提交、12文件），本批最终固定于此。其库 `c2afb1af` 改为未列thenMap的锚点不进入下游，null才是显式保留；prefixUntil保护段、历史区间及零锚点规则须保。库代码、主仓规范/资源与真实消费者一起合入并重新验收，不能仅改提示词或把空数组统一当新预期。主仓最终两父提交与祖先检查以32ec14237为准；运行版本不动。
