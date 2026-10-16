# 抽吸看板：唯一施工状态

盘点基线：`wanxiangshu@8f7f9be77`、`wanxiangli@46d9c07 / 0.5.2`，2026-10-15。**这是首次建板，除历史 CUT-01 外，尚未替任何人认领或宣称验收。** 后续改动由当轮协调者串行登记。

返回 [施工指南](README.md)；具体工作见 [职责卡](CARDS.md)。

## 列与流转

```text
BACKLOG → READY → DOING → LIB_READY → INTEGRATING → REVIEW → DONE
                        ↘        BLOCKED 作为原列上的阻塞标记        ↗
```

| 状态 | 进入条件 | 离开条件 |
|---|---|---|
| BACKLOG | 蓝图已列范围；尚未完成本卡准备 | 当前契约/调用者/反例/写集合及前置核对完毕，协调者改 READY |
| READY | 可认领；不等于已经分配 | 认领记录与公共编辑面预约已合入，才改 DOING |
| DOING | 一个 owner 正在准备/实现最小闭环 | 独立职责、产物和拆除测试通过，改 LIB_READY |
| LIB_READY | 基础库已证明独立，主仓该职责尚未全部切换 | 已取得被测精确包与接线预约，改 INTEGRATING |
| INTEGRATING | 在独立主仓候选上接线和清旧 | 受影响链与旧退出证据完整，进入 REVIEW |
| REVIEW | 非作者审查，候选已固定 | 集成到最新 master 并核验后的范围才改 DONE |
| DONE | 本卡范围内旧 owner 已退出；有主线提交和证据 | 出现新反例另开关联修复卡，不抹去原证据 |
| DECISION | 抽取收益或产品去留尚需裁决 | 形成明确的抽取子范围后转 BACKLOG/READY，或记 RETAINED/REMOVED |
| OPTIONAL | 不在当前主线完成条件中 | 明确授权和真实前提具备后转 BACKLOG/READY |
| RETAINED / REMOVED | 已批准保留 / 已批准并完成删除 | 写清主线提交、理由及承接义务；两者均不记为“抽取成功” |

BLOCKED 不另吞掉原列：写 `DOING + BLOCKED`，保留 owner、阻塞原因、解除证据和下一检查人。受阻的公共预约主动释放或明确保留，不默认霸占。支持性卡 CUT-00、32、33、37 可不经过 LIB_READY，但仍须真实接线/使用与评审。

## 总看板

“前置”是本卡**完成主仓切换**所需的先行卡处置，不是要求所有前置完成才能只读调查，也不是要求本库 import 前置库。CUT-00 统一管准备；其他“—”仅表示没有额外前置。父卡出现 RETAINED 时，调用方要改成明确的既有协议，不能把“不抽”当成“新库已可用”。

| ID | 职责 | 泳道 | 当前列 | 切换前置 | Owner | Reviewer |
|---|---|---|---|---|---|---|
| CUT-00 | 两仓准备、差异裁决与并行开工 | 协作 | DONE | — | eng-cut00 | manager（历史登记；原 `cards/CUT-00.md` 当前缺失） |
| CUT-01 | 工作生命周期与原子日志接线 | 已交付 | DONE | — | 历史已合入 | 证据见收尾记录 |
| CUT-02 | 非 PTY 进程执行 | 物理 | DONE | CUT-00 | eng-cut02 | manager |
| CUT-03 | 原始轨迹与固定区间 | 上下文 | DONE | CUT-00 | eng-cut03 | manager |
| CUT-04 | 唯一资源归属与关闭 | 执行 | DONE | CUT-00, CUT-01 | eng-cut04 | manager |
| CUT-05 | 双语资源与包内定位 | 表达 | READY | CUT-00 | — | — |
| CUT-06 | WorkRecord 取材与视图 | 上下文 | BACKLOG | CUT-03 | — | — |
| CUT-07 | 参与者身份与执行绑定分离 | 权威 | READY | CUT-00 | — | — |
| CUT-08 | 能力事实与双侧门禁 | 权威 | BACKLOG | CUT-07 | — | — |
| CUT-09 | 来源、逻辑根与关闭权威 | 权威 | BACKLOG | CUT-07 | — | — |
| CUT-10 | 模型路由与精确容量许可 | 执行 | BACKLOG | CUT-07 | — | — |
| CUT-11 | 派发认领与外部效果记账 | 执行 | BACKLOG | CUT-09, CUT-10 | — | — |
| CUT-12 | 会话关联与执行事实 | 执行 | BACKLOG | CUT-04, CUT-11 | — | — |
| CUT-13 | 委派、复用与结果认领 | 执行 | BACKLOG | CUT-06, CUT-12 | — | — |
| CUT-14 | 失败预算、恢复决议与结算 | 执行 | BACKLOG | CUT-10, CUT-11, CUT-12 | — | — |
| CUT-15 | 前缀世代与候选提交 | 上下文 | BACKLOG | CUT-03 | — | — |
| CUT-16 | 压缩覆盖与 checkpoint | 上下文 | BACKLOG | CUT-03, CUT-06, CUT-15 | — | — |
| CUT-17 | 请求投影与信息边界 | 表达 | BACKLOG | CUT-05, CUT-06, CUT-08, CUT-15, CUT-16 | — | — |
| CUT-18 | 规范事实编码与校验 | 存储 | READY | CUT-00 | — | — |
| CUT-19 | 多 writer 存储与收敛 | 存储 | BACKLOG | CUT-18 | — | — |
| CUT-20 | PTY 与完整输出外存 | 物理 | BACKLOG | CUT-02, CUT-04 | — | — |
| CUT-21 | 显式时间与因果等待 | 物理 | READY | CUT-00 | — | — |
| CUT-22 | attention、concern 与邮箱归属 | 权威 | BACKLOG | CUT-09 | — | — |
| CUT-23 | Git 工作树、委任与集成 | 仓库 | BACKLOG | CUT-12, CUT-13 | — | — |
| CUT-24 | 文件读取、锚点与原子编辑 | 仓库 | READY | CUT-00 | — | — |
| CUT-25 | 受限 JS 编程面与执行组合 | 仓库 | BACKLOG | CUT-02, CUT-08, CUT-24 | — | — |
| CUT-26 | 需求发现与材料溯源 | 仓库 | READY | CUT-00 | — | — |
| CUT-27 | Casebook / 知识复用去向 | 产品裁决 | DECISION | CUT-06, CUT-19, CUT-24 | — | — |
| CUT-28 | Fission 多 lane 执行 | 执行 | BACKLOG | CUT-03, CUT-10, CUT-12, CUT-13 | — | — |
| CUT-29 | Strength / 投机调查去向 | 产品裁决 | DECISION | CUT-06, CUT-10, CUT-13, CUT-19 | — | — |
| CUT-30 | Rulebook、指导与退化观察去向 | 产品裁决 | DECISION | CUT-03, CUT-05, CUT-16, CUT-19 | — | — |
| CUT-31 | Sphinx 与认识运行时去向 | 产品裁决 | DECISION | CUT-09, CUT-10, CUT-11, CUT-13, CUT-19 | — | — |
| CUT-32 | 可独立验证的执行设施 | 验证 | BACKLOG | CUT-00 | — | — |
| CUT-33 | 两仓构建、打包与发布闭环 | 验证 | BACKLOG | CUT-00 | — | — |
| CUT-34 | OpenCode 适配边界 | 宿主 | BACKLOG | CUT-11, CUT-12, CUT-17, CUT-25 | — | — |
| CUT-35 | 顶层组装与逐域恢复 | 宿主 | BACKLOG | CUT-13, CUT-14, CUT-16, CUT-17, CUT-19, CUT-22, CUT-23, CUT-27, CUT-28, CUT-29, CUT-30, CUT-31, CUT-34 | — | — |
| CUT-36 | 第二宿主真实 canary | 可选 | OPTIONAL | CUT-34, CUT-35 | — | — |
| CUT-37 | 消融、诊断与实测成本 | 验证 | BACKLOG | CUT-00 | — | — |
| CUT-38 | 全仓残余职责与终局交账 | 收尾 | BACKLOG | CUT-00, CUT-01, CUT-02, CUT-03, CUT-04, CUT-05, CUT-06, CUT-07, CUT-08, CUT-09, CUT-10, CUT-11, CUT-12, CUT-13, CUT-14, CUT-15, CUT-16, CUT-17, CUT-18, CUT-19, CUT-20, CUT-21, CUT-22, CUT-23, CUT-24, CUT-25, CUT-26, CUT-27, CUT-28, CUT-29, CUT-30, CUT-31, CUT-32, CUT-33, CUT-34, CUT-35, CUT-37 | — | — |

CUT-02/03/04 的 LIB_READY 仅指现有基础版；满足主仓更大契约所需的补足在各卡内执行。完成 CUT-00 和本卡接线准备后，可认领并直接进入 INTEGRATING；发现基础协议不成立时回退 DOING，不用历史测试成绩代替新需求。

<a id="source-audit-repairs"></a>
## 2026-10-15 源码复核后的关联修复

复核基线：主仓 `7d8feff03`、独立库 `46d9c07 / 0.5.2`。[问题报告与修复计划](SOURCE-AUDIT-2026-10-15.md) 已确认 CUT-02/03/04 仍有接线缺陷及未承接职责。上表 DONE 保留历史合入记录，**不再作为对应完整职责已闭合的证明**；后续依赖这些能力的卡须同时检查下列修复卡。CUT-01 的迁移边界未被推翻，新增的是独立套件的验证缺口。

源码报告轮次仅交付复核与计划。2026-10-15 后续已获用户授权开始修复；以下认领只覆盖明确工作包，整卡须分别取得库、生产接线及旧职责退出证据。进度与原始命令摘要见 [修复记录](REPAIR-PROGRESS-2026-10-15.md)。

| ID | 职责与范围 | 当前列 | 完成前置 | Owner | Reviewer |
|---|---|---|---|---|---|
| [CUT-01-R1](SOURCE-AUDIT-2026-10-15.md#repair-lifecycle) | Journal 测试关闭同步、构造锁竞争和 Process 平台证明 | DONE | 库完整候选与精确包 Node/Bun 验证通过；未测平台不扩张 | root；audit_process（复审） | 交叉复审 |
| [CUT-02-R1](SOURCE-AUDIT-2026-10-15.md#repair-process) | 无损输出、Draining 物理监管、相对 PATH；补 D-03 | DONE | 所选非 PTY 生产接线、192 项需求及实际安装包验收；原有 5 TODO 保留 | audit_process；audit_trace（监管）；root（集成） | 交叉复审 |
| [CUT-03-R1](SOURCE-AUDIT-2026-10-15.md#repair-trace) | 原件权威、完整 Opening、稳定 cut、Proxy 重入证明；补 D-02 | DOING | T-02/T-03/E-01 已修；D-02 对账落盘，T-01 原件接管仍待下一包 | root（下一包）；audit_trace（已交接） | 交叉复审 |
| [CUT-04-R1](SOURCE-AUDIT-2026-10-15.md#repair-ownership) | PTY 真实终结、早到回执、持久预留/恢复、统一 blocker | DOING | O-02/O-03/O-04 已修；O-01 已持久保留/阻断，完整 PTY 后代监管有实证反例；跨重启接管与跨进程每轮准入未完 | root（剩余监管/恢复）；audit_ownership（已交接） | 交叉复审 |
| [CUT-01-R2](SOURCE-AUDIT-2026-10-15.md#second-review-fixes) | J-01：原生 statement 释放及 Unknown 后即时恢复写入 | DONE | 0.6.2 源码及精确包的非作者复验、真实 get 释放 oracle 和去 finalize 突变通过；新发现另记 R3，不覆盖 0.6.1 历史 | root | independent_journal_r2、independent_boundaries_r2 |
| [CUT-04-R2](SOURCE-AUDIT-2026-10-15.md#second-review-fixes) | O-05/06/07、B-01：收尾确认、共享端口隔离、永久准入关闭及依赖声明 | DONE | 非作者确认原限定范围成立；真实 focused 778 与 0.6.2 主仓包消费通过；收尾新反例记 R3，O-01/T-01 不销项 | root | independent_pty_r2、independent_boundaries_r2 |
| [CUT-01-R3](SOURCE-AUDIT-2026-10-15.md#independent-review-fixes) | J-02/E-03/E-04：显式关闭重试、真实追加竞争与 Bun 释放 oracle | DONE | 库 eef4825 / 0.6.2 已交付，320 项及非作者 30 项、25 轮竞争和七种突变通过；主仓 7d12e3462 固定包、隔离消费通过 | root | independent_journal_r2、independent_boundaries_r2 |
| [CUT-04-R3](SOURCE-AUDIT-2026-10-15.md#independent-review-fixes) | O-08/09/10：Drain 退订、Cancel Unknown 后实际回收、共享端口订阅寿命 | DONE | 主仓 7d12e3462 及 481 项归属链、193 项进程套件；最终候选 61 项由非作者复验，59 pass、2 TODO；O-01 活后代反例不计通过 | root | independent_pty_r2、independent_boundaries_r2 |
| [CUT-04-R4](SOURCE-AUDIT-2026-10-15.md#terminal-cancellation-review) | O-11：实际完全取消后拒绝旧 PTY 入口，保留 canonical DevOps 的可复用分支 | DONE | 主仓本提交；四项先红、实际 focused 778、485 项归属链及精确主包消费通过；非作者原反例零 Spawn、DevOps 连续复用和 12 个测试 PID 收尾通过 | root | independent_pty_r2 |
| [CUT-02-R5](REGRESSION-REVIEW-2026-10-16.md) | 全部修改双版复审；P-04 首次 Spool 创建失败清理 | DONE | 原 lock 历史构建、双版实跑、首次写失败先红及非作者清理通过；最终 196 项进程、format/check、主包隔离消费通过；主仓本提交 | root | regression_process_r5、regression_trace_journal_r5 |
| [CUT-04-R5](REGRESSION-REVIEW-2026-10-16.md) | O-13/O-14 修复引入的回退；O-12 原真实回调漏清理及冷无绑定防错 | DONE | 真实 fd、实际 terminal、held/retry/cancel/旧token及冷 guard 正反对照通过；492 项归属链、主包消费通过；主仓本提交，O-01 不销项 | root | regression_ownership_r5、regression_process_r5 |

P1 行为缺陷优先；PATH、PTY 提前清账/同步完成和 Proxy 测试可先形成独立小闭环。公共文件、两仓发布及主仓集成仍串行，不能用这些局部修复关闭整张关联卡。

## 波次：什么时候拉哪张卡

波次不是日期排期，也不是整波完成才能开始下一波。以表中实际前置和编辑面为准，满足一张就拉一张。

| 波次 | 优先拉取 | 允许提前并行的工作 | 不要做 |
|---|---|---|---|
| W0 | CUT-00；复核 CUT-01 | 各 owner 只读盘点及准备反例 | 宣称所有草稿接口已定 |
| W1 | CUT-02、03、04；独立的 05、07、18、21、24、26 | 32、33、37 可调查和小步增强；27、29、30、31 可做产品盘点 | 九人同时改 ToolRegistry 或全仓 format |
| W2 | CUT-06、08、09、10、15、19、20、25 | 具体 Host 能力 canary 可只读准备 | 因未迁完 EventStore 阻塞无关内存库 |
| W3 | CUT-11、16、22；前置就绪后的12 | 请求/结果契约评审 | 两份状态账双写，以“以后同步”兜底 |
| W4 | CUT-13、14、17、23、28；已裁决的27、29、30、31 | 对应 Host 接线和最终闭包拆除准备 | 把产品裁决卡当成自动删库许可 |
| W5 | CUT-34、35，接入已就绪的真实个体 | 经明确授权的36先做 canary | 先建设万能宿主框架再找使用方 |
| W6 | CUT-38 | 未关闭项拆成明确后续责任 | 把“暂留”写成“全部抽干” |

## 冲突矩阵

| 编辑面 | 常见相邻卡 | 处理 |
|---|---|---|
| Process/Surface、进程请求与 Spool | 02、20、21、25 | 拆分实际文件；共同入口由当轮集成者串行改 |
| Trace、Prefix、Blogger、WorkRecord、消息投影 | 03、06、15、16、17、28、30 | 区间与覆盖协议先约定；捕获、压缩、投影各自一个状态 owner |
| Authority、Dispatch、ChatExecution、ModelRouting | 07—14、22、28、34 | 精确身份和结算次序共同评审；不互相补字段绕过协议 |
| Composition/Durable、Journal、EventStore | 11、12、18、19、27、29—31、35 | 一次迁移一个明确事实族；生产写入口只有一个 |
| ToolRegistry、ToolRuntimeScope、插件 Hook | 02、04—17、23—31、34、35 | 业务库可并行；接线一次只一张候选 |
| Repository/Programming、JsSandbox、能力生成 | 08、24、25、26、29 | 文件行为与权限判定分开；共享类/声明先冻结 |
| 两仓包元数据、编译清单、资源清单、vendor | 所有卡 | 唯一发布/集成负责人，禁止同时改锁与 tarball |

<a id="shared-surfaces"></a>
## 公共编辑面预约

初始没有预约。认领正文用 [模板](TEMPLATES.md#claim)，协调者在下表填实际值；“截止”写明确交接事件，不按离线时长自动剥夺。

| 编辑面或精确路径 | 持有卡 | 当前修改者 | 起始 base | 释放条件 | 状态 |
|---|---|---|---|---|---|
| `src/Wanxiangshu/Process/{ProcessRunner,NodeProcessHost,NodeProcessWait,Surface}.fs(.fsi)` | `CUT-02` | `eng-cut02` | `b322c180b` | `CUT-02 完成进程执行接线与 process-execution 测试回归`（2026-10-15 devops 构建/check/process-execution/009/025 全绿，主线提交 62dbebc5d，证据见 cards/CUT-02.md） | `已释放` |
| `src/Wanxiangshu/Context/Trace/*.fs(.fsi)` | `CUT-03` | `eng-cut03` | `b322c180b` | `CUT-03 完成确认轨迹子范围接线与 semantic-trace 测试回归`（2026-10-15 全套验证全绿含裁决后 005 重入段，主线提交 62dbebc5d，证据见 cards/CUT-03.md） | `已释放` |
| `src/Wanxiangshu/Execution/Session/Ownership.fs(.fsi), src/Wanxiangshu/Mission/Lifecycle/DurableWorkHost.fs(.fsi)` | `CUT-04` | `eng-cut04` | `b322c180b` | `CUT-04 完成资源归属接线与 session/work 测试回归`（2026-10-15 删除清单闭合与 work 009/044、025 全绿，主线提交 62dbebc5d，证据见 cards/CUT-04.md） | `已释放` |

## 两仓集成队列

一个候选发布到主仓后，再拉下一项。库实现 PR 可以多个待审；同名版本发布和主仓锁更新不能并发。填写完整性摘要，不以文件名作唯一身份。

| 排位 | 卡 | 库 commit / version / tarball SHA-256 | 主仓 base / candidate | Reviewer | 剩余门禁或历史例外 | 状态 |
|---|---|---|---|---|---|---|
| 1 | CUT-02 | — | 4be1c4105 | manager | GAP-092（仅 015 预声明 todo，无新增门禁） | 已合入 |
| 2 | CUT-03 | — | 042ae616a | manager | 无新增门禁（GAP-101 均为预声明 todo） | 已合入 |
| 3 | CUT-04 | — | 6b5b71cc7 | manager | 无新增门禁（GAP-133 均为预声明 todo） | 已合入 |

## 边界修订与产品裁决登记

这里只记协作决策与来源，不建立第二份业务规则。实际语义须落实到原 WHAT 或新库局部规则。详细差异见 [COVERAGE.md](COVERAGE.md#draft-conflicts)。

| 决策号 | 影响卡 | 待判问题 | 当前处置 | 决策/证据 |
|---|---|---|---|---|
| D-01 | 07、09、11 | 草稿中的未知来源回退 human 与现行权威证据冲突 | 保留现行拒绝/明确未知；开工时裁定精确接口 | 待认领；不得先删权威闭包 |
| D-02 | 03、06、15、16、28 | 单观察者 Trace 与并发捕获、推理、retry 排除、Fission 合流、Opening 范围不完全等价 | Opening 与固定 cut 已修；推理/retry/并发/身份/Terminal/Fission/历史对账落盘，描述符桥原件接管尚未实现 | [本批范围与下一包](REPAIR-PROGRESS-2026-10-15.md) |
| D-03 | 02、20、21 | 基础 pipe 留尾不等于主仓硬期限、完整外存及 PTY | 默认基础版保持留尾；显式 Capture/监管承接所选非 PTY 完整采集和同组期限/取消。PTY、脱组后代、宿主崩溃恢复不扩成已承接 | [精确包与真实反例](REPAIR-PROGRESS-2026-10-15.md) |
| D-04 | 18、19 | 单流 SQLite CAS 与多 writer 合法分叉不是同一承诺 | 两种协议分开，不用前者替代后者 | 待 CUT-18/19 |
| D-05 | 27、29、30、31 | 新库草稿“不再建”与仍生效的产品功能冲突 | 默认暂留并维护；抽取/删除需逐卡裁决 | 待认领；不是删除批准 |
| D-06 | 32、33、38 | 相对主线净改进与完整发布验收 | 合并可用净改进准入，验证结果仍原样报告 | 用户 2026-10-14T23:59:13Z 授权；[比较记录](../lifecycle-cutover-closeout.md) |
| D-07 | 00、34、36 | 库规则仍写旧仓修改待授权、宿主名尚未确认 | 本计划不偷偷覆盖规则；各任务按当前授权更新过时范围，第二宿主先明确对象 | 历史登记称 CUT-00 已落盘，但 `cards/CUT-00.md` 当前缺失；主仓与 wanxiangli 按当前授权推进，CUT-36 保持 OPTIONAL |

## 更新纪律

认领/换人/换 base/变更协议/发布包/合入主线/关闭旧 owner 时更新看板。单卡实现日志不直接写进总表，只链接本卡证据。没有真实日志的计数留空，不写零；没有人接手的字段保持 `—`。

看板是协作状态的唯一来源；CARDS 是范围，COVERAGE 是义务索引，GAP 是证明债务，它们不维护竞争的“完成率”。
