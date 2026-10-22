# 抽吸看板：唯一施工状态

盘点基线：`wanxiangshu@8f7f9be77`、`wanxiangli@46d9c07 / 0.5.2`，2026-10-15。**这是首次建板，除历史 CUT-01 外，尚未替任何人认领或宣称验收。** 后续改动由当轮协调者串行登记。

返回 [施工指南](README.md)；具体工作见 [职责卡](CARDS.md)。

<a id="upstream-merge-20261022"></a>
## 2026-10-22 upstream 合并计划

[本批计划](UPSTREAM-MERGE-2026-10-22.md)固定自有主仓 `87be7c641` / 库 `45849f5` 与上游主仓 `939ada1eb` / 库 `663ba59`。三方预演主仓 182、库 39 个冲突路径。用户已授权实施、验收后提交推送；真实合并在独立工作树施工，原运行目录仍在 `8db33b342`。下表是当前施工状态，前述上游 CI 和模块完成数仅为调查时快照。

| 包 | 状态 | 证据与剩余边界 |
|---|---|---|
| M22-0 调查与计划 | DONE（计划） | 两仓 fetch、固定 SHA、CI 日志、静态契约核对及[三方预演](../../evidence/upstream-merge-plan-20261022/preflight.json)；未运行产品测试 |
| M22-1 基线与构建 | [见验收结果](../../evidence/upstream-merge-plan-20261022/final-result.json) | 闭包、冷缓存、损坏产物与失败不覆盖反例；以同一冻结输入的正式结果为准 |
| M22-2 独立库 | [见验收结果](../../evidence/upstream-merge-plan-20261022/final-result.json) | 两父库提交 `0ef8ad1` 已普通推送；Node全库1018 pass/0 fail/7 skip、Bun视图/预算29 pass/0 fail、真实包外Node22/Bun消费已过。主仓精确gitlink与消费者独立记验收；Bun既有两项post-await P1不计通过、不关闭MF-01；[视图收据](../../wanxiangli/evidence/upstream-merge-20261022/view32-followup.md) |
| M22-3 编程、交付与恢复 | [见验收结果](../../evidence/upstream-merge-plan-20261022/final-result.json) | reportMapping、voided、封存材料、真实终态及capture→durable commit→publish；不以历史关键批次代证最终候选 |
| M22-4 执行与宿主接线 | [见验收结果](../../evidence/upstream-merge-plan-20261022/final-result.json) | Int64唯一owner、真实process/PTY、Git分路、持久切点及Host接线；HNI与真实OMP边界分列 |
| M22-5 清旧与完整验收 | [见验收结果](../../evidence/upstream-merge-plan-20261022/final-result.json) | 固定目标 `32ec14237`；原300秒及5秒失败保留，用户已明确准许本次本机15分钟整轮例外；单项5秒及真实回收保持。以完整正式阶段和全部skip/TODO分列为准；[施工记录](../../evidence/upstream-merge-plan-20261022/execution.md) |
| M22-6 源码交付 | [见验收结果](../../evidence/upstream-merge-plan-20261022/final-result.json) | 库先可获取、主仓精确gitlink、普通非强制推送和精确SHA的CI分别记录；运行版本保持原值 |

本批状态不改变下方 MF、UM、CUT 等历史范围和验收结论。自有已验证修复的保留义务、各包依赖与停止条件均在本批计划中维护。

## 2026-10-21 已拆模块修正施工

**用户已于2026-10-21恢复施工。** 源码`764e6778e`、库`45849f5`/0.11.0；gen107完整本地发行通过，证据归档gen108后783编译产物原字节不变。候选`a6e3917ca`已以原SHA/force=false快进自有master及PR分支，完整Linux CI39539917238成功；PR #62描述已同步。原长测取消记录保留，新实验从零计时。当前入口见[恢复记录](MODULE-CONSTRUCTION-2026-10-21.md#resume-20261021)。

用户已授权按[本轮施工范围](MODULE-CONSTRUCTION-2026-10-21.md)持续施工至少五小时（全部计划提前完成除外），允许subagent与独立测试。固定主仓c78c9d53c、库a12ff4a，上游只读参照5880b824f/202a41ab；原运行目录保持原版。

| 包 | 状态 | 证据与剩余边界 |
|---|---|---|
| MF-00 | DONE（基线） | c78c9d53c完整CI成功；两仓输入、精确写集合和保留合同已登记 |
| MF-01 | DOING（物理隔离前置） | A的Closing真实红例已修，各runtime12+8全绿；B各16+7机制通过，class/toJSON兼容仍待后续。C最终源独立各16+2全绿、36PID回收，仍不等于生产接线；按用户流程优先要求D只存设计，边缘先记录。权限/ledger、完整sanity、启动与内存边界未闭合 |
| MF-02A | REVIEW（限定修复） | gen95目标集合38/0；严格S4夹具已证明持久准入；Host缺原子attempt中断，check到abort窗口仍在 |
| MF-02B | REVIEW（delivery/prefix/compression） | 库3db7b43及后续c4bc2ca已推；18入口公开包及Node/Bun各73+独立6项通过；gen102完整发行通过，旧TODO另列；projection后续分包审查 |
| MF-03A | REVIEW（续接恢复） | 新增6个原生write/fsync/unlink切点用例，本机gen106发行通过；Linux四位PID夹具已修，148adbb42完整远端CI通过，生产身份规范未放宽；不等于断电或完整GAP-151闭合 |
| MF-03B | REVIEW（限定长测） | 新Process/Supervision在Node22和Bun各满2h+2h通过，独立keeper各满4h，实际EOF/hash/TERM-KILL/Closed/Wait0通过；归档见本轮收尾。PTY2h交互/EOF经过，但暂停custodian后的超时recover占位失败，原exit1及唯一短例完整保留，按用户要求记P2不修。旧取消时长不累计；双路spool与Git大输出P2仍留档 |
| MF-04 | REVIEW（retention/防回退） | 库c4bc2ca/0.9.2已推，20入口包消费通过；数值ObservedAt反例已修，主仓9项011进gen101单元；021保住exact cut guard。legacy/series活动选材P2另记，多writer/GAP-151不销项 |
| MF-05 | REVIEW（已验子包） | permit/routing已接，库45849f5/0.11.0已推，22入口真包通过；gen107完整发行单元4537/0、集成725/0，旧skip/TODO单列；capacity/queue暂保留原owner，未证明收益且桥会缩窄Int64，其余authority/dispatch/session未认领闭合 |
| MF-06 | REVIEW（wait/Casebook/消融窄修） | gen102完整发行通过；消融严格集合22/0/1既有integration skip，4/4回收；独立真实ToolRegistry拒绝无效profile。hash碰撞及borrowed_surface边界P2另记，不为同构纯包增加适配 |

先前N1—N6和UM记录保留历史含义，发行阻断以最终通过CI的精确候选为准，不重复认领已修问题；历史TODO、异常收尾证据和全链缺口不自动销项。

<a id="upstream-view-delivery-20261020"></a>
## 2026-10-20 最新上游：view、交付与权限

[本批计划](UPSTREAM-VIEW-DELIVERY-MERGE-2026-10-20.md)固定自有 `f04553e5a` 与上游 `11052493b`。用户已授权逐包合并，root 在原计划独立工作树施工；前批接收和下方模块改进保持原状态，不重复认领 N1 的发行调查。

| 包 | 当前状态 | 证据与剩余边界 |
|---|---|---|
| UM-0 固定版本与合并计划 | DONE（计划） | fetch 与静态源码核对；新增 17 提交、386 路径；三方预演 162 冲突路径；未运行产品测试 |
| UM-1 Pair Hint 与保证书清理 | READY（限定） | gen43 构建/格式/静态通过；正式七文件175/0/2旧TODO，实际Host两文件109/0/1旧TODO，均排空回收；[原始证据](../../evidence/upstream-view-delivery-20261020/README.md)，完整发行及部署待后续 |
| UM-2 view/Anchor 与 Plan 编程面 | SOURCE_READY_LIMITED | 整合分支39b5dd04d；gen48核心165/0/1skip/1旧TODO，checkpoint集成23/0；证据um2-delivery.json；不算完整验收，不单独推master |
| UM-R 完整模型与旧功能退役 | SOURCE_READY_LIMITED | 固定11052493b真实合并，库fc75b85已推送，15入口及包外消费62/0/4skip；最终主仓证据见[收据](../../evidence/upstream-view-delivery-20261020/umr-delivery.json) |
| UM-3 Manager reviewOnly | SOURCE_READY_LIMITED | 原生真实Host逐次授权、零拒绝读取/启动、绑定方法、park与重开通过；保留本地受监管rg；正式监督结果见最终收据 |
| UM-4 交付见证与真实工作收束 | SOURCE_READY_LIMITED | 结算后入格、精确provider围栏、自然stop不完成、冷回收不补造交付、Advisor实际intent归属已落实；不接半成品reportMapping，最终收据列明持久/进程链证据 |
| UM-5 同候选验收与源码集成 | RESULTS_IN_RECEIPT | 定向验收、完整发行、普通推master的最终状态均以[收据](../../evidence/upstream-view-delivery-20261020/umr-delivery.json)为准；控制流风格债不改基线掩盖。未切换原运行目录，未部署 |
| UM-6 合并复核与 PR | RESULTS_IN_REVIEW_RECEIPT | 修复控制流、持久交付、冷续接和资源关闭阻断，迁移退役模型的验证夹具；各轮通过/失败证据、PR及剩余优先级见[复核收据](../../evidence/upstream-view-delivery-20261020/umr-review.json)。固定目标之外的新上游仍有冲突，最终预演与GitHub状态单列，不把本地check通过等同上游CI或可合并 |

用户已明确选择完整对齐上游新模型，包含旧功能退役；此前暂缓大范围退役的处置被替代。UM-2—UM-R—UM-4 作为一致候选进入 master，最终验收和推送结果以收据为准。旧 GAP 不一并清零；超过Int32的便捷数字cursor投影与既有发行门禁另记，64位持久cut仍验证。最终下载测试使用已有临时GNU wget，未增添产品依赖。原运行目录保持原版本。

<a id="module-improvement-20261020"></a>
## 2026-10-20 已拆模块改进

当前范围和依赖见[下一批计划](MODULE-IMPROVEMENT-2026-10-20.md)。以主仓 d69fa8ef3、库 05f6b5a 为起点；本次完成计划及源码同步，未实施下列产品修复。旧卡的历史 DONE/REVIEW、旧失败及 GAP 不因新计划而改变。

| 包 | 当前状态 | 证据与剩余边界 |
|---|---|---|
| N0 两仓同步与入口 | DONE（源码同步） | 独立库及子模块 HEAD/main/origin/main 均为05f6b5a；旧修复分支保留；gitlink不变，未部署 |
| N1 验证与发行阻断 | PLANNED（首包） | E03/E04；先定因，原5000ms静默门和旧TODO保留 |
| N2 物理模块与启动成本 | PLANNED | 依赖N1相关入口；E07有限取证，期限/取消/背压不退化 |
| N3 持久归属与真实退役 | PLANNED | 依赖N1/N2；承接E02的一个真实组合，不关闭整个PTY/GAP |
| N4 原件与独立进程历史读取 | PLANNED | 依赖N1；承接E01的一条现存消费链，不重建退役LWR |
| N5 rg窄范围易用性 | BACKLOG | 依赖N2；E06按实际需要选常用参数，其余继续拒绝 |
| N6 B2原子决策前提 | BACKLOG（先原型） | 依赖N3/N4持久边界；沿用freshness与成本合同，不直接接全历史重放 |

N1—N4为待认领顺序，本次不填写作者以外的复审完成记录。公共契约、构建图和两仓集成串行；独立调查可并行。计划中的每包证据在其对应段落追加，跨包和完整发行结论分别报告。

## 上游语义整合批次

2026-10-19 用户已授权按[整合计划](UPSTREAM-SEMANTIC-MERGE-2026-10-19.md)推进 SM-01—SM-07，具体处置见[语义处置表](UPSTREAM-SEMANTIC-DISPOSITION-2026-10-19.md)。owner：本 chat 的 root；SM-01已提交，SM-02A/B/C库候选05f6b5a限定验收LIB_READY，SM-03至SM-06限定范围READY，SM-07候选进入REVIEW；完整发行未通过，未切换运行。证据与未决范围只在整合计划更新。公共编辑面为两仓 manifest/锁/gitlink、构建图、实际插件接线与本看板，串行集成。原 CUT/GAP 的状态、失败和剩余范围继续保留；本批局部通过不销父项。Blogger 全退役不在范围。

2026-10-20 用户已授权下一批 M1—M6，唯一进度及证据索引为[增量接收计划](UPSTREAM-INTAKE-2026-10-20.md)，语义去向见[处置表](UPSTREAM-INTAKE-DISPOSITION-2026-10-20.md)。固定参照 upstream 0932e0c08，库保持 05f6b5a；小的边缘问题记录后继续，期限、取消与历史读取退化阻断交付，原 CUT/GAP 不自动改变。

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
| REVIEW | 本卡必要证据齐备，候选已固定，进入非作者审查 | 复核及最终主仓集成候选核验通过，满足下述清单后才改 DONE |
| DONE | 冻结范围的证据门禁通过；拆分/接收卡四类证据齐备，有主线提交 | 出现新反例关联修复卡并注明失效承诺；历史 DONE 不再代证当前完整职责 |
| DECISION | 抽取收益或产品去留尚需裁决 | 形成明确的抽取子范围后转 BACKLOG/READY，或记 RETAINED/REMOVED |
| OPTIONAL | 不在当前主线完成条件中 | 明确授权和真实前提具备后转 BACKLOG/READY |
| RETAINED / REMOVED | 已批准保留 / 已批准并完成删除 | 写清主线提交、理由及承接义务；两者均不记为“抽取成功” |

BLOCKED 不另吞掉原列：写 `DOING + BLOCKED`，保留 owner、阻塞原因、解除证据和下一检查人。受阻的公共预约主动释放或明确保留，不默认霸占。支持性卡 CUT-00、32、33、37 可不经过 LIB_READY，但仍须真实接线/使用与评审。

拆分/接收卡按[上游拆分交付清单](UPSTREAM-DELIVERY-CHECKLIST.md#done-gate)判定 DONE：真实消费者、公开产物、关键反例、旧职责退出缺一不可。相关失败、TODO/skip、未运行或失效证据不能计通过；不得在失败后自行缩小承诺或仅挂子卡就关闭父卡。事先定义的调查、取证与原型子卡按各自结果验收，不代替生产父项。本次只收紧流转规则，不批量改写历史卡片状态。

## 总看板

“前置”是本卡**完成主仓切换**所需的先行卡处置，不是要求所有前置完成才能只读调查，也不是要求本库 import 前置库。CUT-00 统一管准备；其他“—”仅表示没有额外前置。父卡出现 RETAINED 时，调用方要改成明确的既有协议，不能把“不抽”当成“新库已可用”。

| ID | 职责 | 泳道 | 当前列 | 切换前置 | Owner | Reviewer |
|---|---|---|---|---|---|---|
| CUT-00 | 两仓准备、差异裁决与并行开工 | 协作 | DONE | — | eng-cut00 | manager（历史登记；原 `cards/CUT-00.md` 当前缺失） |
| CUT-01 | 工作生命周期与原子日志接线 | 已交付 | DONE | — | 历史已合入 | 证据见收尾记录 |
| CUT-02 | 非 PTY 进程执行 | 物理 | DONE | CUT-00 | eng-cut02 | manager |
| CUT-03 | 原始轨迹与固定区间 | 上下文 | DONE | CUT-00 | eng-cut03 | manager |
| CUT-04 | 唯一资源归属与关闭 | 执行 | DONE | CUT-00, CUT-01 | eng-cut04 | manager |
| CUT-05 | 双语资源与包内定位 | 表达 | DONE | CUT-00 | — | 2026-10-15 主线提交 2f98049e5：wanxiangli 0.7.0 resources 模块合入，库侧 8 测试 + 主仓 provider-language 25 项回归全绿，证据链见 cards/CUT-05.md §10 |
| CUT-06 | WorkRecord 取材与视图 | 上下文 | DONE | CUT-03 | eng-cut06-goose | 2026-10-20：三段式 WorkRecord 已由仓主于 master 6eef89d1e 退役（REMOVED，非本卡抽取）；现行继任者「交付视图格」（会话视图、调用围栏、已交还游标）抽入 wanxiangli/delivery（库 1206e57 / 0.9.0 发布 2564387），主仓 JsDeliveredValues 改为组合者，旧 Dictionary owner 删除；相关回归 base/candidate 逐用例一致（含 master 既有失败），新旧双算 7×4000 步一致；证据链见 cards/CUT-06.md |
| CUT-07 | 参与者身份与执行绑定分离 | 权威 | DONE | CUT-00 | eng-cut07b-goose（接手 eng-cut07-goose） | 2026-10-20 wanxiangli 54dcc34 / 0.11.0 identity；主仓切换合入 master，旧 owner 退出；Origin/IdentitySeed 接线移交 CUT-09，ExecutionBinding 接线移交 CUT-10/12；证据 cards/CUT-07.md §11 |
| CUT-08 | 能力事实与双侧门禁 | 权威 | DONE | CUT-07 | eng-cut08c-goose（接手 eng-cut07b-goose） | 2026-10-21 完成（范围按 cards/CUT-08.md §6 收窄）：矩阵与 Manager 事实门禁（wanxiangli 0.18.0）、能力↔工具名唯一映射（0.20.0）、实用工具 skill/assume 准入（0.22.0，修复 Distiller 请求工具表与准入分叉）都只剩库一个 owner，schema 投影/请求工具表/执行准入读同一份；宿主手写角色表已删。编程面准入归 CUT-25，fork 系 spec 准入归 CUT-13，描述语义评审留 GAP-078 |
| CUT-09 | 来源、逻辑根与关闭权威 | 权威 | DONE | CUT-07 | eng-cut09c-goose（接手 eng-cut09b-goose、eng-cut08c-goose） | 2026-10-21 完成（范围按 cards/CUT-09.md §6 收窄）：逻辑根注册、精确关闭（显式/子工作/持久重放）与来源解析优先级只剩 wanxiangli/authority 0.28.0 一个 owner；Run.fs 与 Fold.fs 的两份手写裁决已删。D-01：UnknownOrigin 失败关闭不变。差分 24,684 次 0 处不同，5 个变异全抓住。入口准入决策拆 CUT-09-a，018 关闭见证拆 CUT-09-b |
| CUT-09-a | 入口准入决策（ChatAdmission/Intent）的纯裁决 | 权威 | DONE | CUT-09 | eng-cut09e-goose（接手 eng-cut09d-goose、eng-cut09c-goose） | 2026-10-21 完成：入口准入固定次序（畸形载体、HumanRoot 重放、缺会话/快照/物理 id、已接受/已落地、pending claim 会话与来源可准入性、Host 内部、未 claim/授根/UnknownOriginWhileActive/省略 participant 续接）只剩 wanxiangli/authority 0.36.0 `admit` 一个 owner；Intent.fs 9 个手写裁决函数已删。差分 1,290,240 次 0 处不同，6 个变异全抓住。见 cards/CUT-09-a.md |
| CUT-09-b | interaction-authority-018 关闭见证精确匹配 | 权威 | DONE | CUT-09, CUT-11 | eng-cut25h-goose | 2026-10-21 完成（范围按 cards/CUT-09-b.md §5 收窄）：五类 ExpectedClosureKind、source witness 拼法与精确匹配只归 wanxiangli/authority 0.57.0；Manager life 关闭写规范 LifeCompleted witness，durable 重放只认本类精确 witness（旧 fold 任意 witness 都能关运行）；其余四类的 durable terminal 是 GAP-123 产品缺口 |
| CUT-10 | 模型路由与精确容量许可 | 执行 | DONE | CUT-07 | eng-cut10g-goose（接手 eng-cut10f-goose、eng-cut10e-goose、eng-cut10d-goose、eng-cut10c-goose、eng-cut10b-goose、eng-cut10-goose） | 2026-10-21 完成：①调度器 ABI → wanxiangli/routing（f982f8070）②精确执行许可 → permit 0.33.0（9b4b64dad）③容量不变式核对 → capacity 0.35.0（807a3db6a）④执行准入等待队列 → admission-queue 0.37.0（57fbca343）⑤容量账本/执行托管/provider-step 借用 → borrowing 0.43.0（1601da81c，差分 360,000 步 0 处不同）⑥a provider-run 见证与原样重试绑定 → run-witness 0.47.0（差分 720,000 步 0 处不同，7/7 变异）。DevOps 绑定因 GAP-129/34-D1 未裁决留宿主；continuation input/superseded/stopped 交 CUT-12/14；见 cards/CUT-10.md §5 |
| CUT-11 | 派发认领与外部效果记账 | 执行 | DONE | CUT-09, CUT-10 | eng-cut09f-goose（接手 eng-cut09e-goose） | 2026-10-21 完成：派发认领账规则（作用域、PromptKey 原文、序号、四态结算编辑、occasion 准入、恢复核对）、Host 发送回答、运行边界重置，都只剩 wanxiangli/dispatch 一个 owner（0.40.0、0.42.0、0.45.0），Model.fs、Run.fs、Recovery.fs、Send.fs 的手写规则已删。各项差分 0 处不同：投影差分 1,560,000 次、恢复差分 200,000 次、发送差分 90 次。变异 11+6+7 个全部抓住。③Work.operation 与派发认领经静态扫描核实互不重叠，关联已记录。见 cards/CUT-11.md |
| CUT-12 | 会话关联与执行事实 | 执行 | DONE | CUT-04, CUT-11 | eng-cut12d-goose（接手 eng-cut12c-goose、eng-cut12b-goose、eng-cut12-goose） | 2026-10-21 完成：四个子闭环各只剩 wanxiangli 一个 owner——①Work↔卫星关联律 → association 0.46.0 ②Accepted/ProviderStarted/Terminal 单赋值律 → chat-execution 0.44.0 ③exact 租约复用与新子回收 → attachment-lease 0.50.0 ④重启恢复决议 → chat-execution recover() 0.46.0。差分（vs master b10da13c9）4,800 / 7,656 / 90,112 / 19,200 次 0 处不同，变异 21/21 抓住。SharedTerminalBus/SharedState 是宿主物理登记，归 CUT-34/35；见 cards/CUT-12.md §6–§9 |
| CUT-13 | 委派、复用与结果认领 | 执行 | DONE | CUT-06, CUT-12 | eng-cut12d-goose（接手 eng-cut12c-goose） | 2026-10-21 完成：三个子闭环各只剩 wanxiangli 一个 owner——①handle/work 单赢家生命周期 → handle-lifecycle 0.52.0（差分 8,547 次 0 不同，9/9 变异）②restart 子会话恢复决议 → handle-lifecycle recoverChild() 0.53.0（12,691 次 0 不同，6/6）③SyncDelegate 语义批次准入 → delegate-batch 0.55.0（240,000 次 0 不同，10/10）。库草稿 src/delegation 退役；DevOps resume 复用归 attachment-lease（CUT-12），答复扇出 RETAINED；见 cards/CUT-13.md §5–§7 |
| CUT-14 | 失败预算、恢复决议与结算 | 执行 | DONE | CUT-10, CUT-11, CUT-12 | eng-cut14c-goose（接手 eng-cut14b-goose、eng-cut10g-goose） | 2026-10-21 完成：①一次精确执行失败的唯一决议（十一类失败 × 四个持久阶段 → 五路结论 + breaker + fence 保留/释放 + 结算后致命 + 重试授权绑定）归 wanxiangli/failure 0.49.0，差分 600,000 次 0 处不同，7/7 变异抓住；②Logical Run 的 provider 失败预算（精确 attempt 去重、单次推进、耗尽、成功清零、重放应答）归 wanxiangli/failure-budget 0.54.0，已编译旧宿主对库约 605,000 步、宿主端到端约 605,000 步、重放约 909,000 次，均 0 处不同，7/7 + 3/3 变异抓住；③下一 attempt 的 `ProviderAttemptRecovery.next` 已是唯一 owner，RETAINED；④Host 内建 retry 边界已核实（chatMaxRetries=0，无第二 writer），GAP-143/144 保留不关。受影响 59 个测试文件 236 通过 0 失败。见 [cards/CUT-14.md](cards/CUT-14.md) |
| CUT-15 | 前缀世代与候选提交 | 上下文 | DONE | CUT-03 | eng-cut15c-goose | 2026-10-21 rebase 到 e4ea42ddf 后重测通过；2026-10-20：前缀世代、候选/提交、吸收策略、检查点去重与同代追加律抽入 wanxiangli/prefix（库 7aac7b8 / 0.10.0 发布 770eea6）；主仓 Epoch/PhaseWindow/isAppendOnlyPrefix 改为投影、HostCompactionPolicy.nextReanchor 删除。修复 cutover 后折叠从不推进 PrefixEpoch 导致同一压缩反复重锚、清空重建检查点（5 个反例 base 红/candidate 绿）；差分 65000 次 0 差异；证据见 cards/CUT-15.md |
| CUT-16 | 压缩覆盖与 checkpoint | 上下文 | DONE | CUT-03, CUT-06, CUT-15 | eng-cut16d-goose（接手 eng-cut16c-goose / eng-cut06b-goose） | 2026-10-21 子范围①合入：ANCHOR 视图覆盖定律（view 声明形状、存活锚点分区、永久区间替换计划与折叠）抽入 wanxiangli/compression 0.21.0（库 a8292d9 / 发布 468991f），Anchor 四文件改为组合者、旧折叠与手写校验删除；编号台账、o200k 计量、宿主消息重建留主仓；32 文件 base/candidate 逐用例一致，差分 270000 次 0 分歧；子范围②view 检查点追加准入抽入 compression 0.25.1（库 5f15de3 / 发布 a699b95），PluginHooks 只留 journal 写入与在途会合，72 文件 base/candidate 逐用例一致、集成层 005 三变异体均被抓到；③核对后无剩余可切：todo 失败封口已在②，候选接受/Blogger 已由 [003][008] 退役，重启按 [031] 重置、窗口归 prefix，见 cards/CUT-16.md §6 |
| CUT-17 | 请求投影与信息边界 | 表达 | DONE | CUT-05, CUT-06, CUT-08, CUT-15, CUT-16 | eng-cut17f-goose（接手 eng-cut17e/d/c/b-goose、eng-cut16d-goose） | 2026-10-21 DONE：任期过滤 tenure 0.28.0、意图规划 plan 0.34.0、布局 layout、退休 attempt 归属 retired 0.39.0、规范线格式/语义字节 wire/semantic 0.41.0（ec036a317）均归 wanxiangli/projection；宿主 codec/Trace/journal 留主仓；退出核对与证据见 cards/CUT-17.md §8 |
| CUT-18 | 规范事实编码与校验 | 存储 | DONE | CUT-00 | eng-cut18 | 2026-10-15 主线提交 2f98049e5：wanxiangli events 模块合入 0.7.0，库侧 34 测试 + durable-events 244 项回归全绿，证据链见 cards/CUT-18.md §10 |
| CUT-19 | 多 writer 存储与收敛 | 存储 | DONE | CUT-18 | eng-cut19d-goose（接手 eng-cut19c-goose；前三任 eng-cut19/19b/19c-goose 均已中断退出，工作树干净、提交均在主线） | 2026-10-21 按缩小后的范围关闭（README §6 末段）：①k-way 归并（wanxiangli/convergence 0.14.0）②整条 writer UTC 日保留（retention 0.16.0）③已落盘行闭包（events.checkClosure 0.19.0）④尾行活动判定（retention.lastActivity 0.19.0）⑤段命名与分组律（segments 0.23.0），五段旧算法均已删除、主仓只剩 facade；跨进程锁、原子追加/fsync/rename/utimes、解码前缀缓存与目录读写登记为宿主 I/O 明确保留（RETAINED，理由见 cards/CUT-19.md §16）；未闭合部分拆为 CUT-19-a（段共存字节裁决与写入流成帧）、CUT-19-b（Integrator 规则隔离与 cut-tail 载荷律）、CUT-19-c（Journal/旧 Store 逐事实族退出与残留 Git 类型去向），不藏在本卡 DONE 之下 |
| CUT-19-a | 段共存的字节裁决与 writer 流成帧律 | 存储 | DONE | CUT-19 | eng-cut19d-goose | 2026-10-21 完成：成帧（尾 LF、无空内行、逐行规范解码）与共存裁决（series / legacy / continuation / diverged）抽入 wanxiangli/segments 0.24.0（库 57c16ed / 发布 40265dd）；主仓读取与整理两份手写裁决合为一次库调用，删 7 个私有函数；影子双算 1500 轮读取、整理与落盘字节全等；durable-events/convergence 252/0。见 cards/CUT-19.md §17 |
| CUT-19-b | Integrator 规则隔离与 cut-tail 载荷律 | 存储 | DONE | CUT-19 | eng-cut27d-goose | 2026-10-21 完成：(rule, FaultScope) 隔离账、隔离期跳过、`ProjectionCutTail` 载荷编解码与截尾生效条件抽入 wanxiangli/quarantine 0.38.0（3d6a83f）；主仓 master `c52bff68a` 删手写 Thoth 编解码，Faults 改为库账；规则本体、Current、唯一一次全量重放预算与 PrepareLive 留主仓组合者；影子：编解码 3000/3000、账 1.8M 次查询逐项相等；durable-events/convergence 252/0。见 cards/CUT-19.md §18 |
| CUT-19-c | Journal/旧 Store 逐事实族退出与残留 Git 类型去向 | 存储 | DONE | CUT-19, CUT-11, CUT-12, CUT-27, CUT-29, CUT-31 | eng-cut09k-goose（接手 eng-cut09j/09i-goose） | 2026-10-21 裁决并完成：亲眼核对后 `Persistence/Journal` 不是旧存储，而是唯一 EventStore 之上的类型化事实层（唯一生产 writer `EventStoreJournalWriter`），RETAINED，边界：不得长出自己的文件格式或第二个 writer；EventStore `Store`/`Surface` 与 `StoreRef.canonical` RETAINED；零消费者的 `GitObjectId`/`RootOid`/`StoreSnapshot`/`PublishError`/远程跟踪 ref REMOVED（master `f0d6ded47`，不记为抽取）。前置 11/12/27/29/31 均 DONE。见 cards/CUT-19.md §19 |
| CUT-20 | PTY 与完整输出外存 | 物理 | DONE | CUT-02, CUT-04 | eng-cut20b-goose（接手 eng-cut20-goose） | 2026-10-20 spool：wanxiangli/spool（库 20e0372，0.13.0 发布 6bf65d3），主仓 f7d112dad，修复 Spooled git 输出被吞成空串与 spool 文件泄漏；PTY：wanxiangli/pty（库 e383e93，0.15.0 发布 4f8e316），主仓 PtySupervisor 改为路由、物理终端交给库，修复 bun-pty 在缺失 cwd 时于别处执行命令；PTY 整体去留是产品裁决，不在本卡；证据见 cards/CUT-20.md |
| CUT-21 | 显式时间与因果等待 | 物理 | DONE | CUT-00 | — | 2026-10-15 库侧 time/wait 模块随 0.8.0 发布（库 commit 8422617，全量 460 测试绿）；主仓 NodeTiming/Deadline/VirtualTiming/Wait Registry/CompletionMailbox 桥接切至 0.8.0，干净 HEAD worktree 验证 time-capability 24/0、causal-wait 52/0、subsystems OK；主仓共享工作树全量 build 因他人 Interaction 进行中改动阻断（非本卡引入），证据链见 cards/CUT-21.md §10 |
| CUT-22 | attention、concern 与邮箱归属 | 权威 | REMOVED | CUT-09 | eng-cut09c-goose | 2026-10-21 REMOVED（不记为抽取成功）：两包已是退休边界，运行时/工具/持久投影早由 94f994ed8 等删除，无活算法可抽；本卡删掉最后的死残留（Host 事实上恒 None 的 `ConcernPlacement`、无生产者的 `DeferredWorkPresentation` continuation），补 2 条先红后绿的回归测试；日志兼容 150 例 + 真实日志重放通过。见 cards/CUT-22.md |
| CUT-23 | Git 工作树、委任与集成 | 仓库 | DONE | CUT-12, CUT-13 | eng-cut25j-goose（接手 eng-cut25i-goose） | 2026-10-21 子范围① Git porcelain 读法归 wanxiangli/git-porcelain 0.48.0；子范围② commission 工作树绑定、Integration Gate 判定、冲突归属、崩溃恢复续做归 wanxiangli/integration，工作树回收选择归 git-porcelain（0.61.0；894add5c5，差分 0 差异，定向失败与基线同名）；子范围③ Git 动词收口：可执行文件只由 GitSubject.Executable 拼写，工作树根查询一个动词（882dc531c）；余留诚实：IntegrationGate 跨进程锁与 Orchestrator 三处 I/O 间等值守卫留宿主；见 cards/CUT-23.md |
| CUT-24 | 文件读取、锚点与原子编辑 | 仓库 | DONE | CUT-00 | eng-cut24 | 2026-10-15 主线提交 2f98049e5：wanxiangli fs 模块合入 0.7.0（38 测试绿），repository-programming 158 项回归全绿，证据链见 cards/CUT-24.md §10 |
| CUT-25 | 受限 JS 编程面与执行组合 | 仓库 | DONE | CUT-02, CUT-08, CUT-24 | eng-cut25h-goose（落地；实施 eng-cut25g/f/c/b-goose） | 2026-10-21 完成：沙箱 membrane 归 wanxiangli/sandbox 0.23.2（已知逃逸先红后绿，await 后死循环会超时）；编程面投影、准入、成员目录归 wanxiangli/programming；schema、公开基类、运行时基类、运行时 api 安装都读同一份目录，差分 22/0、11/0 不同。残余：node:vm 不是 OS 级隔离（已记入卡片）。见 cards/CUT-25.md |
| CUT-26 | 需求发现与材料溯源 | 仓库 | DONE | CUT-00 | eng-cut26 | 2026-10-15 主线提交 2f98049e5：wanxiangli grounding 模块合入 0.7.0（13 测试绿）；主仓 requirement-grounding 残留 002/007 两项为 GAP.md 既有登记缺口（GAP-085，非本卡回归），证据链见 cards/CUT-26.md §10 |
| CUT-27 | Casebook / 知识复用去向 | 产品裁决 | DONE | CUT-06, CUT-19, CUT-24 | eng-cut27d-goose（接手 eng-cut27c-goose；前任 eng-cut19d/27b/27c-goose 均已中断退出） | 2026-10-21 裁决“保留产品功能、分步抽取”并完成：①Index 纯投影归 wanxiangli/casebook 0.26.0（顺带修复代理对截断致 shelfmark 失效）②Fetch 差异决策归 casebook-maintenance 0.29.0（400 组主仓黄金向量逐字节相等；修复截断切开代理对）③实质访问判定归 casebook-access 0.31.0（影子 16868 观测，差异仅为反引号/选项值被误记为文件的有意修复）；master `dc23ec407`/`699609cfd`；④Bookkeeper/Settlement/Host 接线按裁决 RETAINED 留主仓随 CUT-35；既有 7 个 knowledge-reuse-010 失败 base 同因未变。见 cards/CUT-27.md §1–§3 |
| CUT-28 | Fission 多 lane 执行 | 执行 | DONE | CUT-03, CUT-10, CUT-12, CUT-13 | eng-cut28d-goose（接手 eng-cut28c/28b-goose、eng-cut12d-goose） | 2026-10-21 完成：Fission 组折叠律、lane 请求/完成路由/环次序/终局 lane/后继、Fission/Turn 结算与收束前提只归 wanxiangli/fission-group（0.59.0/0.60.0，gitlink 0.61.0）；两个宿主不再各自推导收束前提。差分 480,000 步与 521,062 次比较 0 差异，拓扑穷举 8,829 项 0 差异（另 44 项为单列的有意修复），变异 23/23 抓住；定向套件无新增失败。master `c581ce038`/`5354e53e0`/`bf5a8ea91`；Admission/Runtime RETAINED 随 CUT-35，宿主适配归 CUT-34。见 [cards/CUT-28.md](cards/CUT-28.md) |
| CUT-29 | Strength / 投机调查去向 | 产品裁决 | DONE | CUT-06, CUT-10, CUT-13, CUT-19 | eng-cut09k-goose（接手 eng-cut09j/09i/09h/09g-goose） | 2026-10-21 裁决并完成：Strength/估计/预算/Replica/投机调查已由仓主 6eef89d1e 删除（REMOVED，非抽取），本卡删 `DeniedStrength` 死键与资源；Warm-start 关键词规范化、每词 top-k、稳定去重、24 条上限、整条删减到 64 KiB 与直接消费者判定只归 wanxiangli/warm-start 0.51.0（差分 20000 轮生产形输入 sha256 全等，8/8 变异抓住）；只读调查契约与 Semble MCP 传输 RETAINED。CUT-13 已 DONE，Fork 是唯一消费者，`RepositoryWarmStart` 公开签名未变。见 [cards/CUT-29.md](cards/CUT-29.md) |
| CUT-30 | Rulebook、指导与退化观察去向 | 产品裁决 | DONE | CUT-03, CUT-05, CUT-16, CUT-19 | eng-cut06-goose | 2026-10-20 裁决并完成：Rulebook/Enforcer、Blogger 诊断、guidance-delivery、behavior-diagnosis 已由仓主于 6eef89d1e 删除（REMOVED）；degeneration-guard 检测算法已抽入 wanxiangli/degeneration 0.12.0（a0f0de9），LoopDetector 改为组合者，语料/包络派生与分词、LoopSensor 中断/续发留主仓；差分逐位相等，见 cards/CUT-30.md |
| CUT-31 | Sphinx 与认识运行时去向 | 产品裁决 | DONE | CUT-09, CUT-10, CUT-11, CUT-13, CUT-19 | eng-cut09k-goose（接手 eng-cut09j/09i/09h/09g-goose） | 2026-10-21 裁决并完成：保留 Sphinx v2 产品，按层裁决。①Ordinal 似然（BTL/tie-aware/Plackett–Luce/MaxDiff/复合对/Borda）只归 wanxiangli/ordinal 0.56.0（影子 9 族×100000 例 differences=0，6/6 变异杀死）；②精确 Bayes 后验只归 wanxiangli/bayes 0.58.0（影子 50000 例，未声明差异 0，两处有意修正单列，8/8 变异杀死）；Ordinal Fit/DesignCheck 与 A*/MCTS 零生产引用、实测或零断言，REMOVED（不记为抽取）；Questionnaire/Probes/Inquiry/Provider 与 Core/Runtime/Persistence/Composition/Wire/Hosts RETAINED（WHAT 009 单 reducer/driver，随 CUT-35 复核）。master `1b7b10c7d`。见 [cards/CUT-31.md](cards/CUT-31.md) |
| CUT-32 | 可独立验证的执行设施 | 验证 | DONE | CUT-00 | eng-cut06-goose | 2026-10-20 裁决 RETAINED：测试运行/计数、静默看门狗、进程组回收、日志与 verdict 分离、固定输入已由主仓 supervisor 与 verification-system 016/021/006 承担，不新增库、不重写 runner；窄入口为 `TESTS_MJS_FILES=… run.mjs`（README §9），本机 Node 20 下 006 有 7 项环境性失败如实记录，见 cards/CUT-32.md |
| CUT-33 | 两仓构建、打包与发布闭环 | 验证 | DONE | CUT-00 | eng-cut06-goose | 2026-10-20：按仓主现行规则（submodule + `file:wanxiangli`，禁止 .tgz）重判；库 5edd3c7 加 SPEC[build-008] exports/files/模块一致性门禁（三种变异实测报红），主仓 verify-package 实测 bundle 0.9.0 通过，指南分发段校正；gitlink 祖先校验等残项见 cards/CUT-33.md §5 |
| CUT-34 | OpenCode 适配边界 | 宿主 | DONE | CUT-11, CUT-12, CUT-17, CUT-25 | eng-cut34c-goose（接手 eng-cut34b-goose、eng-cut34-goose、eng-cut09i-goose、eng-cut17f-goose） | 2026-10-21 完成：门禁 ⑦ host-sdk-boundary（7660ff3e4）与 ⑧ adapter-import-ratchet（2cec07be9）常驻；内核→`Wanxiangshu.OpenCode` 引用基线 31 → 0（空表）。中性的宿主契约（终端信号、会话端口、会话契约）移入 Foundation；业务律（受管 agent 名册与角色、准入决策、语言偏好阶梯、模型路由与容量、诊断汇、捕获线格式）移入各自领域；XWire 按适配代码归入 `Context/Prefix/OpenCode`。负控 N1–N8、P1 PASS；定向套件无新增失败。SDK 传输与 Hook 膜按 D-07 留在适配层；见 [cards/CUT-34.md](cards/CUT-34.md) §10 |
| CUT-35 | 顶层组装与逐域恢复 | 宿主 | DONE | CUT-13, CUT-14, CUT-16, CUT-17, CUT-19, CUT-19-c, CUT-22, CUT-23, CUT-27, CUT-28, CUT-29, CUT-30, CUT-31, CUT-34 | eng-cut35b-goose（接手 eng-cut28d-goose） | 2026-10-21 完成：逐个核对 `Composition/`、插件组装根与作用域、逐域恢复接线的每个公开名字。删除无消费者的部分：Fold 双份折叠合一、`FamilyRecoveryCoordinator`、孤儿 `ToolSchemaJson`（418 行）及随之无人导入的 `effect` 依赖、从未注册的 `scopeDisposers`/`sessionCleanups` 挂钩、只写不读的 Attachment/Companion 插槽、只经无人导入的 Surface 才能到达的 attempt-plan 写入口。只在本文件用的 helper 不再导出。没有新运行时、总线或恢复程序计数器。66 个定向文件 base/候选同为 58 条既有失败，无新增。CUT-27④ Bookkeeper/Settlement/Host、CUT-28 Admission/Runtime、CUT-31 Sphinx Core/Runtime 复核后 RETAINED 为明示参与者（理由见卡 §9）。master `6f2e8b8f2`/`1e4f43d2e`/`38892ddb2`/`36c3f3b3f`/`9b8564b9d`/`144e7d020`/`cc59d5de5`/`22f6e8ac5`。见 [cards/CUT-35.md](cards/CUT-35.md) §9 |
| CUT-36 | 第二宿主真实 canary | 可选 | DOING | CUT-34, CUT-35 | — | 2026-10-21 仓主授权：第二宿主指认为 omp 18.12.4（锁定版本），canary 所需真实模型调用已获授权，本卡由 OPTIONAL 转 DOING。历史：同日早前的可行性核对（eng-cut34b/34c-goose）曾以“宿主未指认 / 真实调用未授权 / CUT-35 未 DONE”为由不启动，该结论已被取代（见 [cards/CUT-36.md](cards/CUT-36.md) §6 历史与 §7 当前范围）。当前实现范围：产品侧 `src/Wanxiangshu/Host/SecondHost/HostPlugin.fs` 已有 `IHostPlugin` 实例但尚未纳入编译分片；omp 侧仍是类型检查 + 离线 fixture 通过的骨架，O-3 仍开放；真实 smoke 尚未通过，故本卡不转 DONE，也不宣称已支持第二宿主 |
| CUT-37 | 消融、诊断与实测成本 | 验证 | DONE | CUT-00 | eng-cut06b-goose | 2026-10-20 完成（接手 eng-cut06-goose）：消融 DAG 定律（主节点/父链、环检测、三态边约束）抽入 wanxiangli/ablation 0.17.0（库 2dfb15d / 发布 6013c9a），Ablation/Manifest 改为组合者、旧 DFS 与校验删除；清单读取、门禁投影留主仓，Host 诊断与 collect-incident RETAINED；13 文件 base/candidate 逐用例一致，差分 30000 次 0 分歧，见 cards/CUT-37.md |
| CUT-38 | 全仓残余职责与终局交账 | 收尾 | DONE | CUT-00, CUT-01, CUT-02, CUT-03, CUT-04, CUT-05, CUT-06, CUT-07, CUT-08, CUT-09, CUT-10, CUT-11, CUT-12, CUT-13, CUT-14, CUT-15, CUT-16, CUT-17, CUT-18, CUT-19, CUT-19-a, CUT-19-b, CUT-19-c, CUT-20, CUT-21, CUT-22, CUT-23, CUT-24, CUT-25, CUT-26, CUT-27, CUT-28, CUT-29, CUT-30, CUT-31, CUT-32, CUT-33, CUT-34, CUT-35, CUT-37 | eng-cut35b-goose | 2026-10-21 终局交账（W6：未宣称“抽干”）：现行 49 个需求包与 18 个源码顶层目录逐项归入 DONE / RETAINED / REMOVED / 后继项；仓主在看板外删除 5 个包与 3 个目录（记 REMOVED，不记为抽取），新增的 `advisor` 包未覆盖，转 CUT-39。清理刀：删除 129 个零消费者导出，以及删空的 `Change/Prompts`、`Sphinx/V2/Runtime/Agenda`；撤下 180 个仅本模块使用的导出。全量 765 个文件的套件中候选失败名单为 base 的子集，无新增；`check.mjs` 与 master 同为 pyramid 门禁红（看板外引入）。发布序列（wxl test、format、check、unit、integration 红，build-clean、verify-package 绿）与 base `9679f699e` 同命令复跑对比：全部是既有问题；唯一本卡引入的是 `Git/Subject.fs` 的 format，已由 `1954613ef` 修复（§7.7）。后继项 F38-01…09（含 CUT-39、CUT-40）与 RETAINED 登记见 [cards/CUT-38.md](cards/CUT-38.md) §7。master `a157ab133`/`7f0dd759d`/`4ec0145fa`/`1954613ef` |
| CUT-39 | advisor 专席去向（建板后新增需求包） | 执行 | DONE | CUT-13, CUT-38 | eng-cut39-goose | 2026-10-21 裁决：advisor 是现行产品功能，不删。纯规则（材料身份、follow-up 增量、按调用 session 的已交付账本、散文读回）抽到 `wanxiangli/advisor`，库提交 `5bcace5`，发布 0.63.0（`7ed7df2`）。主仓 `Cursor`、`AdvisorView.plan`、`AdvisorSeat` 读回与 `AnchorView.materialKey`（handoff 共用）改为委托库。权限表、view 生成与渲染、专席 child 与派发、终态围栏 RETAINED。差分：3000 组 7010 步 base 与候选 sha256 全等；库变异 14/14 被抓；接线变异使主仓 advisor-003 与 repository-programming-059 变红。64 个文件的定向套件 base 与候选同为 402 tests、1 个既有失败。**F39-01**：终态围栏拿 PromptKey 与 assistant id 比较，永不命中，真实咨询全部拒绝或等满 120 s；已由 `b9772a9f5` 修复。**F39-02**：follow-up 咨询的 view prompt 无 model-routing lease，报 EMR-010；已由 `a8f431bd` 修复（rebase 后落为 `e11fc96c5`：每次咨询的每个 prompt 都是自己的 authority run：view 自己的终态结束 view 阶段，随后持久关闭 child 的 logical run（`ExecutionFact.ChildRunVoided`），再发 intent，让 intent 自己 root 起新 run 并持有自己已提交的 lease；不放宽 EMR-010）。本机未跑构建与测试，仅静态检查通过。详见 [cards/CUT-39.md](cards/CUT-39.md) |
| CUT-40 | 域内适配子目录棘轮 | 宿主 | DONE | CUT-34, CUT-38 | eng-cut40-goose, eng-cut40-fu | 2026-10-21：新增门禁 ⑨ `domain-adapter-import-ratchet`（基线 `scripts/checks/domain-adapter-import-baseline.json`，只降不升；与 ⑧ 不同，`Composition/` 与 `*Surface.fs` 也计入），如实登记起点 16（8 内核含 Composition + 8 Surface）→ 终点 5。内核 8→0：`HostSessionNudge`、`PromptMetadataCodec`、`HostJoinGuard` 及其 journal port、`JsDescriptionAssets`、`JsToolWorkflow`/`JsParkedSites` 经核对都不碰宿主，属于放错目录，移回本域（A）；无 B。余下 5 个 Surface（Dispatch、Bookkeeper、Projection、SyncDelegate、CleanBreak）引用的是真正的宿主编解码或工具绑定，记 RETAINED（C）。12 个包 1592 个用例对 base 无新增失败，`check.mjs` pyramid 122 条与 base 逐条相同。后继 F40-01…03 见 [cards/CUT-40.md](cards/CUT-40.md)。master `d1180b3e4`/`2e0da3f1f`/`827c68dba`/`3add5700a`。**2026-10-21 后继收口（eng-cut40-fu）**：F40-01…03 全部落地，9 个提交上 master。⑨ 5 → 0（`DispatchSurface` 删重复解码器、`SyncDelegate/Surface` 删无消费者包装、`BookkeeperSurface` 工具绑定拆到 `Casebook/OpenCode/BookkeeperToolSurface`、`XWireSurface` 拆成宿主中立的 `Context/Prefix/WireReconciliation` + 薄的 `Context/Prefix/OpenCode/XWireHostSurface`、`ProjectionSurface` 整体删除）；⑧ 3 → 0（F40-03 起 ⑧/⑨ 同扫 `.fsi`）；**新增门禁 ⑨b `adapter-namespace-folder`**（命名空间不得藏起自己的 `OpenCode/` 目录，反之亦然；base 上 6 条违例 → 0）；⑨ 诚实起点 17（F40-02 让 `Wire.fs` 的命名空间露出 `…Prefix.OpenCode`，原先躲着的消费者 `XWireSurface` 被计入）。门禁 ⑩ 只降不升：product 引用 695 → **651**，无端口适配器文件 151 → **144**，基线文件未改。master `444ce53d8`…`4018499d6`，另加收尾 `f74f6060e`（4 个 master 上原本就 fantomas 红的文件格式化，纯空白）与 `727fdc8a7`（修 `compile-order.txt` 回归）。`check.mjs` 与 base 的发现集合一致，无新增。**2026-10-21 落地复测（base = 当时的 `origin/master`）**：master 并入 HNI-08 后基线降到 689/150，候选实测 **596 / 137**（仍只降不升，四个基线 JSON 与 master 逐字节相同）；`check.mjs` base 123 条 / 候选 123 条，新增 0、消失 0、fatal error 0；**全树** `fantomas --check` 通过（另修 8 个 master 上原本 fantomas 红的文件，纯空白 token 流不变，并修掉 `Persistence/Journal/CodecSurface.fsi` 的悬空文档注释——fantomas 6.3.0 在它上面不收敛）。**本机未跑编译与测试（owner rule），交付团队必须自行编译并跑测试。** |
| CUT-41 | GAP 台账逐条核实与精确重定向 | 收尾 | DONE | — | gap-ledger | 2026-10-21：交接未完事项第 6 条点名的六条台账（GAP-005/015/050/075/088/123）逐条核实并按台账允许的 C 处置**精确重定向**，无一豁免、无一改记 CLOSED：GAP-005/050 记录「无 faithful mechanical oracle」+ 人工审阅 + 机器落点的规范侧前置；GAP-015 随 `584c30c61be54ad08f96263410cf28257e22b096` 删包而命题退役，改指 `feature-ablation` [005] 与 `capability-enforcement/tests/005.test.mjs` 的退役 kind fail-closed 承载；GAP-075 残余收窄为「真实并发 overlap」「跨进程能力隔离」两条；GAP-088 逐条列名四条 todo（006:135 / 017:21 / 025:305 / 025:306），并移除已过时的「027 仍为 todo」；GAP-123 记录为**产品缺口**（库侧 `wanxiangli/src/authority/ClosureLaw.fs` 五类已就绪，宿主仅有 HumanRootManagerLife 窄端且硬编码 `RootKind="HumanRoot"`），逐条列名八项剩余工作且状态保持 OPEN。连带修正 GAP-183/184 与校正段三条摘要。证据见 [cards/CUT-41.md](cards/CUT-41.md) 与 [evidence/GAP-audit-2026-10-21.md](evidence/GAP-audit-2026-10-21.md) 　2026-10-22（gap-075-088 跟进）：上述 GAP-075 / GAP-088 的登记残余已各自落地为真实独立 oracle——GAP-075 真实并发 overlap（`capability-enforcement/tests/025.test.mjs:422`：`SIGSTOP` 暂停在飞 `rg`、窗口切换前落盘接纳、重叠调用 `REVIEW_ONLY`、恢复后在飞调用放行）与跨进程能力隔离（`026.test.mjs:371` + 新夹具 `support/devops-dispatch-isolation.fixture.mjs`，env `WXS_DEVOPS_ISOLATION_DIRECTORY`/`WXS_DEVOPS_ISOLATION_PHASE`）；GAP-088 四条 todo 全部转真测试并附根因（`repository-programming/tests/006.test.mjs:163`、`017.test.mjs:32`、`025.test.mjs:325`、`025.test.mjs:413`）。**仅静态验证**（7 个 `.mjs` 全部 `node --check` 通过），**未**在已编译 `dist/` 上真实运行（缺 `dist/`，编译触及 180 s 槽位上限，按站规停止），故两行**均保持 PARTIAL**、不改记 CLOSED。详见 [cards/CUT-41.md](cards/CUT-41.md) §7 |

CUT-02/03/04 的 LIB_READY 仅指现有基础版；满足主仓更大契约所需的补足在各卡内执行。完成 CUT-00 和本卡接线准备后，可认领并直接进入 INTEGRATING；发现基础协议不成立时回退 DOING，不用历史测试成绩代替新需求。

<a id="source-audit-repairs"></a>
## 2026-10-15 源码复核后的关联修复

复核基线：主仓 `7d8feff03`、独立库 `46d9c07 / 0.5.2`。[问题报告与修复计划](SOURCE-AUDIT-2026-10-15.md) 已确认 CUT-02/03/04 仍有接线缺陷及未承接职责。上表 DONE 保留历史合入记录，**不再作为对应完整职责已闭合的证明**；后续依赖这些能力的卡须同时检查下列修复卡。CUT-01 的迁移边界未被推翻，新增的是独立套件的验证缺口。

源码报告轮次仅交付复核与计划。2026-10-15 后续已获用户授权开始修复；以下认领只覆盖明确工作包，整卡须分别取得库、生产接线及旧职责退出证据。进度与原始命令摘要见 [修复记录](REPAIR-PROGRESS-2026-10-15.md)。

| ID | 职责与范围 | 当前列 | 完成前置 | Owner | Reviewer |
|---|---|---|---|---|---|
| [CUT-01-R1](SOURCE-AUDIT-2026-10-15.md#repair-lifecycle) | Journal 测试关闭同步、构造锁竞争和 Process 平台证明 | DONE | 库完整候选与精确包 Node/Bun 验证通过；未测平台不扩张 | root；audit_process（复审） | 交叉复审 |
| [CUT-02-R1](SOURCE-AUDIT-2026-10-15.md#repair-process) | 无损输出、Draining 物理监管、相对 PATH；补 D-03 | DONE | 所选非 PTY 生产接线、192 项需求及实际安装包验收；原有 5 TODO 保留 | audit_process；audit_trace（监管）；root（集成） | 交叉复审 |
| [CUT-03-R1](SOURCE-AUDIT-2026-10-15.md#repair-trace) | 原件权威、完整 Opening、稳定 cut、Proxy 重入证明；补 D-02 | DOING | 原件接管及所列局部修复已验收；完整GAP-101/109/111由新R7、CUT-06-R5、R8/R9逐包承接。GAP-108入口范围已验，Bun差异另归CUT-17-R3，不借局部证明关闭父卡 | root；audit_trace（已交接） | 交叉复审 |
| [CUT-04-R1](SOURCE-AUDIT-2026-10-15.md#repair-ownership) | PTY 真实终结、早到回执、持久预留/恢复、统一 blocker | DOING | O-02/O-03/O-04 已修；R6-A/B/C 已完成所列实现及独立复审；原自然 Unknown 按用户决定单列待办，脱离控制终端及整体闭合未完 | root（剩余监管/恢复）；audit_ownership（已交接） | 交叉复审 |
| [CUT-01-R2](SOURCE-AUDIT-2026-10-15.md#second-review-fixes) | J-01：原生 statement 释放及 Unknown 后即时恢复写入 | DONE | 0.6.2 源码及精确包的非作者复验、真实 get 释放 oracle 和去 finalize 突变通过；新发现另记 R3，不覆盖 0.6.1 历史 | root | independent_journal_r2、independent_boundaries_r2 |
| [CUT-04-R2](SOURCE-AUDIT-2026-10-15.md#second-review-fixes) | O-05/06/07、B-01：收尾确认、共享端口隔离、永久准入关闭及依赖声明 | DONE | 非作者确认原限定范围成立；真实 focused 778 与 0.6.2 主仓包消费通过；收尾新反例记 R3，O-01/T-01 不销项 | root | independent_pty_r2、independent_boundaries_r2 |
| [CUT-01-R3](SOURCE-AUDIT-2026-10-15.md#independent-review-fixes) | J-02/E-03/E-04：显式关闭重试、真实追加竞争与 Bun 释放 oracle | DONE | 库 eef4825 / 0.6.2 已交付，320 项及非作者 30 项、25 轮竞争和七种突变通过；主仓 7d12e3462 固定包、隔离消费通过 | root | independent_journal_r2、independent_boundaries_r2 |
| [CUT-04-R3](SOURCE-AUDIT-2026-10-15.md#independent-review-fixes) | O-08/09/10：Drain 退订、Cancel Unknown 后实际回收、共享端口订阅寿命 | DONE | 主仓 7d12e3462 及 481 项归属链、193 项进程套件；最终候选 61 项由非作者复验，59 pass、2 TODO；O-01 活后代反例不计通过 | root | independent_pty_r2、independent_boundaries_r2 |
| [CUT-04-R4](SOURCE-AUDIT-2026-10-15.md#terminal-cancellation-review) | O-11：实际完全取消后拒绝旧 PTY 入口，保留 canonical DevOps 的可复用分支 | DONE | 主仓本提交；四项先红、实际 focused 778、485 项归属链及精确主包消费通过；非作者原反例零 Spawn、DevOps 连续复用和 12 个测试 PID 收尾通过 | root | independent_pty_r2 |
| [CUT-02-R5](REGRESSION-REVIEW-2026-10-16.md) | 全部修改双版复审；P-04 首次 Spool 创建失败清理 | DONE | 原 lock 历史构建、双版实跑、首次写失败先红及非作者清理通过；最终 196 项进程、format/check、主包隔离消费通过；主仓本提交 | root | regression_process_r5、regression_trace_journal_r5 |
| [CUT-04-R5](REGRESSION-REVIEW-2026-10-16.md) | O-13/O-14 修复引入的回退；O-12 原真实回调漏清理及冷无绑定防错 | DONE | 真实 fd、实际 terminal、held/retry/cancel/旧token及冷 guard 正反对照通过；492 项归属链、主包消费通过；主仓本提交，O-01 不销项 | root | regression_ownership_r5、regression_process_r5 |
| [CUT-02-R6-V](REMAINING-REPAIRS-2026-10-16.md) | Capture 的 Bun 错误事件注入夹具 | DONE | 库 b6984d9；Node Process/Supervision 69/69、Bun Capture/Supervision 19/19；生产包未变，Bun 另四项不销项 | root | 本轮自检；未另作非作者复审 |
| [CUT-04-R6-A](PTY-SUPERVISION-2026-10-16.md) | O-01 活监管实例与控制终端会话内后代终结 | DONE | 本提交修正独立复审发现的两项 P1 和一项 P2；709 项相关测试 676 pass / 0 fail / 5 skip / 28 TODO；clean 509 精确包 Bun/OpenCode 消费、Linux 物理口九项通过；仅关闭声明的会话范围，脱组及 R6-B/C 不销项 | root | pty_scope_review_r8、pty_integration_review_r8、pty_host_evidence_review_r8 |
| [CUT-04-R6-B](PTY-RUN-ADMISSION-2026-10-16.md) | O-01 跨进程每轮 PTY 准入 | DONE | 本提交修正两项新增回退和既有 Load void 遗漏；clean 523，1175 项相关测试 1112 pass / 0 fail / 16 skip / 47 TODO；非作者反例、真实双进程 CAS/Bun PTY、精确包隔离消费通过；R6-C/O-01 整体未闭合 | root | r6b_admission_review、r6b_unknown_review、r6b_requirements_review |
| [CUT-04-R6-C](PTY-RESTART-RECOVERY-2026-10-16.md#r12-u1-分层取证与本轮验收2026-10-17) | O-01 跨重启物理接管 | DONE | 已确认的发布缺陷已修；1232 项相关回归 0 普通失败，精确包 Node/Bun/OpenCode 消费通过，三名非作者复查完成。按用户决定验收本轮范围；R12-U1 单列待办，O-01/GAP-133 整体不销项 | root | r6c_protocol_review、r6c_ownership_review、r6c_observation_review |
| [CUT-04-R12-U1](PTY-RESTART-RECOVERY-2026-10-16.md#r12-u1-分层取证与本轮验收2026-10-17) | 原自然 Unknown 追踪 | BACKLOG | 用户决定暂存；原 441 行没有内层现场，仍未唯一归因。已保全分层证据并实演 Unknown 捕获；有新现场或用户重启此包时再处理，不把未复现写成已修复 | root（后续） | 待重新认领 |
| [CUT-03-R6](TRACE-ORIGINALS-2026-10-17.md) | T-01 原件捕获、冷恢复、消费者切换和旧路径退出 | DONE | 原 T-01 N3–N6、复审候选 N7–N9 及既有 T01-04/05/06 均已修并分开记录。库0.6.4、2270项0普通失败、两项integration、非作者Node/Bun及精确包仓外强杀恢复均通过；R12-U1、GAP-101 不销项，未部署用户Host | root | t01r_originals；capture/recovery 初审后参与修正 |
| [CUT-02-R6-A](PROCESS-CONTRACT-2026-10-18.md) | GAP-091及Process契约统一、独立编译与消费者切换 | DONE | f0bfa9bbb四文件纯闭包、真实Git/PTY消费者及隐藏入口退出已验；Node128文件897pass/0普通fail/25skip/72TODO、Bun72pass、真实Host及2e500包通过。非作者独立编译2/2、实际Node/Bun冷接管与无owner拒绝3/3；正式包装器既有缺口归CUT-00-R1，不关闭整个Process | root | review_process_a |
| [CUT-02-R6-B](PROCESS-WIRE-BUDGET-2026-10-18.md) | GAP-092 完整wire总预算与执行前拒绝 | DONE | 双语最小预算、实际TOML与Host窗口、BOM和预取消已验；129文件911pass/0普通fail/15skip/29TODO，正式96pass/1skip，真实Host及独立编译通过。b1f7804固定包Node/Bun各46/46、128MiB五次、PTY124/124，非作者双语各18观察；R6-D/E旧P1单列，随本原子提交交付 | root | review_guidance_r3、review_process_a |
| [CUT-02-R6-E](PROCESS-CANCELLATION-2026-10-18.md) | P1：真实取消监听及排队准入 | DONE | 真实await监听、局部typed取消及许可交接零启动已修；正式113pass/1skip、10/10/owned accepted，Bun79/79、真实Host1/1。4d594fd精确包双运行时各61+4通过、128MiB五次、PTY124/124；非作者各8观察通过，R6-D/R12不销项，随本原子提交交付 | root | review_guidance_r3 |
| [CUT-02-R6-D](PROCESS-TERMINATION-2026-10-18.md) | P1：保留原生exit与signal区别 | DONE | D1/D2声明的文本与binary原生终结事实已验收，随本主线提交交付；scope/capture、PTY持久结算、R12及完整Process不随D关闭 | root | review_process_a（方案）、review_guidance_r3（原型）；D1/D2复审见子项 |
| [CUT-02-R6-D1](PROCESS-TERMINATION-D1-2026-10-18.md) | 目标终结事实及单次执行/文本 Git 消费者 | DONE | 54c7a9266后发现的stop假完整回退已修；652红、660物理反例和663字节承接均长期保留。最终663相关229/0/0TODO、12/12、owned accepted；replay路径修正，原652收据不覆盖，不销R12 | root | d1_review_host、d1_review_git、d1_review_tests |
| [CUT-02-R6-D2](PROCESS-BINARY-D2-2026-10-18.md) | 二进制 GitRawRunner 的原生事实 | DONE | 随本提交交付，库8bb1021/0.6.12已正常推原分支；663原017 Node正式及Bun原生/非作者各19/0、独立闭包20/0、Host27/0，真实消费者/四cut/冷读/精确包通过，旧ODB及raw CAS退出。661/662尾17红经EOF余量确证iterator漏交后修正，受控事件不冒称OS读错。存储52/0另3TODO与P2性能代价保留 | root；d1_review_host（原型/包验收） | d1_review_git、d1_review_tests（非作者）；host消费者验收不作算法非作者 |
| [CUT-02-R6-C](PROCESS-RUNTIME-2026-10-18.md) | Node/Bun环境、参数和stdin的真实回执边界 | REVIEW | 环境/argv/stdin及第二跳实现与非作者复审通过；0.6.6放大原有提前清组竞态，继续由R7-A修到0.6.7。最终82项双运行时、347库项、900主仓普通项及精确消费者通过；完整隔离025仍有失败未归因，验收不销项；Bun背压与正式监督另列 | root | review_process_a、review_bun_adapter_c |
| [CUT-02-R7](REMAINING-REPAIRS-2026-10-16.md#bun-capture) | P1：输出真实性与Bun上游背压 | REVIEW | R7-A/B实现及非作者限定范围验收通过；当前0.6.8主包完整PTY124/124，但旧6cc27首轮隔离失败仍未归因，父项不以新绿覆盖旧失败；不延伸完整RSS或脱组监管保证 | root | 三名非作者分界复核 |
| [CUT-02-R7-A](PROCESS-OUTPUT-EXTENT-2026-10-18.md) | P1：逐路extent阻止自然清组截断 | REVIEW | 实现及非作者复审通过：0.6.7双运行时各82/82、精确包26项及128MiB矩阵12/12。后续0.6.8保留extent并通过独立包及完整PTY；旧6cc27首轮隔离失败未归因，验收仍不销项，R7-B所列范围另包DONE | root | review_process_a、review_bun_adapter_c |
| [CUT-02-R7-B](PROCESS-BACKPRESSURE-2026-10-18.md) | P1：Bun实际上游背压及有界输出 | DONE | 库df14f48/0.6.8正常推送；354项349pass/5skip、Bun物理88/88。三名非作者核对源码、精确安装、32流/取消/fd复用、四场双路128MiB与Linux双运行时各14场景。主包0d60/2076文件，正式76pass/1skip、真实Host、五次128MiB及完整PTY124/124；work011五fail修前同现另记。远端自有生成目录删除待工具人工确认，未部署Host | root | review_bun_adapter_c、review_process_a、review_guidance_r3 |
| [CUT-00-R1](FINITE-PHYSICAL-WAIT-R1-2026-10-19.md) | P2：正式监督器对确切有界物理等待的契约 | DONE | 689七文件321/0及最低Node22.13完整006独立84/0；后续修正本代理引入的endACK/下一begin跨通道竞争，702原七文件323/0/0TODO，Node26/真实22.13受控native与socket非作者对照通过。原红与夹具误判保留，不关R2/R3/R12 | root | d1_review_git、d1_review_tests、d1_review_host |
| [CUT-00-R2](REMAINING-REPAIRS-2026-10-16.md#bun-verification) | P2：Bun真实verdict与完整运行监督 | READY | 非作者反例证明新run-bun可在import阶段无界挂起，已删除入口及正式声明；Bun原生业务通过仅作物理证据。下一包接真实verdict、文件完成/汇总和owned收尾，不解析pass文本或伪心跳 | root（计划） | review_guidance_r3确认边界 |
| [CUT-00-R3](REMAINING-REPAIRS-2026-10-16.md#isolated-pty-evidence) | P2：旧精确包隔离 PTY 失败的因果取证 | READY | 固定 6cc27 包、夹具和运行环境，保存完整内层输出/状态/收尾，比较原生与正式入口及前序组合；后续同包、新包 124/124 不解释首轮失败。预设有限取证矩阵，无新因果证据则保留未知与 C/R7-A REVIEW，不重开 R12-U1 | root（计划） | 待复核 |
| [CUT-00-R4](WORK-REVIEW-FIXTURE-2026-10-18.md) | P2：work011评审夹具缺part身份、篡改历史及SDK封装错误 | DONE | 旧/新包及工作区原样五fail同现；三处输入前提已修，保留完整历史及真实派发次序，Work159pass/1TODO、非作者最终shared011为5pass/1TODO。错误filter归因及作者提前取gate回退已撤回修正，不放松生产绑定或原件校验 | root | review_process_a |
| [CUT-06-R1](WORK-RECORD-RANGES-2026-10-17.md) | GAP-111 跨 invocation frame 的 bounded 取材与 coverage | DONE | 本原子提交修正范围与正式陈述，1199项0普通失败；Node/Bun核心28pass/0fail/2TODO，非作者源/消费者/独立新进程恢复及精确包外Node/Bun均通过，库0.6.4未变。只关闭本包范围，不关闭整项GAP-111/109 | root | gap111_review |
| [CUT-06-R3](WORK-RECORD-RANGES-2026-10-17.md#independent-review) | GAP-111 独立复审：部分摘要及formal-only Terminal去重 | DONE | e7新回退与输入已有的formal-only重复均已修；最终核心Node/Bun各56pass/0fail/2TODO，相关1227项0普通失败；非作者570范围/冷进程/消费者及eeaa8b精确包外14材料组、真实Sync/Fork和新进程冷读通过，随本原子提交交付；R2不销项 | root | g111r_ranges、g111r_consumers、g111r_contracts |
| [CUT-06-R2](WORK-RECORD-HISTORY-2026-10-17.md) | GAP-111 已完成范围的历史表示稳定性 | DONE | Terminal时原子固定WorkRecord摘要引用，真实Sync/Fork使用确切完成终点；Node/Bun核心69pass/1TODO、相关1239项0普通失败，非作者源/暖冷行为及旧精确包blob升级消费通过。575精确包df0ccd隔离默认入口和同包非作者Node/Bun实际消费者均通过，随本原子提交交付；历史关联缺失不自动迁移，R12-U1暂缓 | root | g111r_contracts |
| [CUT-06-R4](WORK-RECORD-HISTORY-2026-10-17.md#r4-review) | R2 独立复审：并发事实下的完成因果边界 | DONE | 并发晚摘要/原件、多head父边及完成歧义已修；1311项0普通失败、Bun核心91pass/1TODO、真实checkpoint/消费者/冷恢复、旧R2包升级及229980精确包独立Node/Bun消费通过。三名非作者复验完成，撤回的五个越界白盒用例不计正式验收；GAP-111/109、旧缺边历史与R12-U1边界保留，随本原子提交交付 | root | r2_freeze_review、r2_replay_review、r2_consumer_review |
| [CUT-17-R1](GUIDANCE-INGRESS-2026-10-17.md) | GAP-108 指导呈现回流的真实 Host 路径核查 | DONE | 正常Host原件隔离成立，原直接capture TODO改为保真负控和实际Host证明；引用误删P1已修。1350项相关回归0普通失败、实际Host网络/持久/冷读、Trace正反编译及f047精确包隔离Node/Bun消费通过；旧错历史及完整CUT-17不销项，R12-U1暂缓，随本原子提交交付 | root | root自审；未做非作者复审 |
| [CUT-17-R2](GUIDANCE-INGRESS-2026-10-17.md#r2-review) | R1 独立复审：来源身份、Host 链路与验收归因 | DONE | 四项R1回退已修，捕获前还原删除，原件/呈现职责分开；O1撤为review误判。600核心Node/Bun25pass/1skip、真实Host1/1、1363项0普通失败及2e0f精确包非作者Node/Bun各12组暖/新OS冷读通过；三名非作者复验完成，旧错历史/完整CUT-17不销项，R12-U1暂缓，随本原子提交交付 | root | g108r_provenance、g108r_host、g108r_evidence |
| [CUT-17-R3](GUIDANCE-BUN-LIFETIME-2026-10-18.md) | Bun指导整文件挂起的原因与生命周期修正 | DONE | 缺模拟终态累积16枚Retiring与启动HOME前提已修；同包20/5超时→25/0，单槽先红转绿。Bun原生26pass/1skip、Node相关265pass、正式runner100/100、实际Host及2e0f包通过。非作者确认fixture边界；发现新无界入口后已删除，正式Bun监督归CUT-00-R2 | root | review_guidance_r3 |
| [GAP-085](GROUNDING-SCOPE-GAP085-2026-10-19.md) | 规则包整个自子树的作用域校验 | DONE | 候选667：整目录及未来路径逐规则校验，首]字符类泄漏修正；14文件128/0/0TODO、owned accepted，三名非作者Node/Bun规则、真实Plugin读写及冷重放通过；2076文件同一、D2和库字节未改。按本卡范围随本提交交付，完整WHAT002异常包名旧边界另账 | root | d1_review_git、d1_review_tests、d1_review_host |
| [GAP-159](PREDICTOR-OFFICE-GAP159-2026-10-19.md) | 显式 office 配置与真实 Fission 准入 | DONE | 候选680：Predictor私有身份/隐藏模型槽/公开准入一致；修正670空reset与673旧后cut释放早期故障两项本轮回退。35文件327/0普通fail/15既存TODO，owned accepted；三名非作者类型、Node/Bun真实入口与旧原件冷读通过，同事件cut正控保留。不关闭完整GAP-158或其余TODO | root | d1_review_git、d1_review_tests、d1_review_host |
| [GAP-140](REMAINING-REPAIRS-2026-10-16.md#gap140) | 持久失败预算、耗尽与重放裁决 | DOING | A已验；[B1已验](SINGLE-JOURNAL-RECOVERY-G140-B1-2026-10-19.md)同live Journal non-idle send2→1/接管，700独立22项、706长期JSON边界及永久6项绿。710相邻148文件655pass/0普通fail+6skip95TODO、收尾通过；021改真实旧原件读取，历史类型解码TODO保留。B2跨OS facts2且不同身份count1冲突已确认，须先freshness/真实writer原型；普通冷Workflow缺Host-stop仍待验 | root | A/B1非作者通过；B2原子命令及durable跨OS发送权未闭 |
| [CUT-03-R7](REMAINING-REPAIRS-2026-10-16.md#trace-opening) | Opening 完整原件范围与 checkpoint 语义 | READY | 对账现行 WHAT；注册 Host 捕获→提交→新 OS 冷读逐字和范围一致，缺证/冲突拒绝，旧 cut 保持；不包办下游视图 | root（计划） | 待非作者复审 |
| [CUT-06-R5](REMAINING-REPAIRS-2026-10-16.md#trace-consumers) | inspect、Resume、Casebook 与 Fork/Join 范围消费 | BACKLOG | R7 后；逐个真实入口验证同 invocation、完成表示及暖冷一致，保留范围隔离与完成冻结，不借单一 materializer 代证全部消费者 | root（计划） | 待非作者复审 |
| [CUT-03-R8](REMAINING-REPAIRS-2026-10-16.md#trace-compaction) | 实际 compaction/reanchor、coverage 与呈现 | BACKLOG | R5 后；真实 Host 覆盖旧 epoch、失败 append、原文、持续追平、provider wire 与冷恢复，不恢复捕获前正文还原；Fission 另验 | root（计划） | 待非作者复审 |
| [CUT-03-R9](REMAINING-REPAIRS-2026-10-16.md#trace-fission) | 真实 Fission 汇聚与唯一完成前沿 | BACKLOG | R8 后；实际 lane 乱序/迟到/失败/取消/N−1 takeover、唯一完成和新 OS 冷读，Trace 不复制 lane 状态机，不关闭其他 GAP-158 条款 | root（计划） | 待非作者复审 |
| [CUT-04-R7](REMAINING-REPAIRS-2026-10-16.md#bootstrap-recovery) | 实际Bootstrap恢复、准入和持久终结 | READY | 可独立调查；两个真正Host的Reserve/物理启动/Started/Ended崩溃矩阵、认证与留账、命令不重发，协议切换有原实例退役步骤 | root（计划） | 待非作者复审 |
| [CUT-04-R8](REMAINING-REPAIRS-2026-10-16.md#detached-processes) | 主动脱组监管父项 | BACKLOG | 拆成原型 A 与生产 B；A 的肯定结论不算生产完成，否定结论保留能力缺口；父项只按实际声明平台和接线证据关闭 | root（计划） | 待非作者复审 |
| [CUT-04-R8-A](REMAINING-REPAIRS-2026-10-16.md#detached-prototype) | 主动脱组可行性原型 | BACKLOG | R7 身份/恢复协议固定后，先定平台、权限和有限矩阵；真实 setsid、leader 先退、忽略 TERM、Host 强杀及无关进程隔离。交付肯定或否定结论，不改生产 | root（计划） | 待非作者复审 |
| [CUT-04-R8-B](REMAINING-REPAIRS-2026-10-16.md#detached-integration) | 已证实机制的生产接线 | BACKLOG | 依 A 肯定结论及冻结承诺；真实 Host、关闭中崩溃、认证接管、发布失败及持久清账，精确包和非作者复验；旧路径退出，在途实例有处置 | root（条件满足后） | 待非作者复审 |
| [CUT-04-R9](REMAINING-REPAIRS-2026-10-16.md#platform-capacity) | 声明平台、运行时与有限容量实测 | BACKLOG | 先冻结版本、并发/输出规模及资源阈值，再验证精确包/PTY/恢复/背压/收尾；RSS 与队列分开。脱组仅在 R8-B 已交付的平台纳入，不承诺未测平台或无限规模 | root（计划） | 待复审 |

2026-10-18 复盘修订：依据[修复原因与改进措施](LOCAL-FIRST-FOUR-MODULES-REPORT-2026-10-18.md#repair-retrospective)，已完成项退出待施工顺序；每包明确范围、关键反例、验收及排除项。下一 P1 为 D1，再接 D2；验证设施与旧失败取证独立，Trace 按 R7→R5→R8→R9，Bootstrap 另包，脱组分原型/接线两包。C/R7-A 保持 REVIEW，R12-U1 继续暂缓。本次只更新文档，不新增验收结论、不合并 upstream。[当前唯一施工计划](REMAINING-REPAIRS-2026-10-16.md#current-plan)。

2026-10-19施工交付：D1停止完整性及D2 binary Git声明范围随本提交验收，候选663、库8bb1021/0.6.12。原661/662尾17红、EOF因果证据、修复回退及review误判分开长期记录；独立闭包20/0、原017两Host19/0、D1相关229/0、Host27/0，存储3TODO与P2延迟代价保留。D1/D2退出待施工顺序，其余P1按现行计划继续；R12-U1仍暂缓。

2026-10-18 上游样本评估：CUT-05 resources 与 CUT-21 time/wait 的[只读接收评估](UPSTREAM-INTAKE-ASSESSMENT-2026-10-18.md)完成，两项接收均暂缓。resources 需修参数裁剪并验证实际资源入口；time/wait 需对齐公开合同、能力边界和旧算法归属后再接线。原卡施工列及当前 16 包状态不变；未认领新的生产修改，未合并 upstream。[候选范围、反例、验收与预算](REMAINING-REPAIRS-2026-10-16.md#intake-candidates)已补入现行计划。

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
| `Mission/WorkRecord/{Materialize,Surface}.fs(.fsi)`、`Context/Companion/Blogger/Projection.fs` 的范围说明、`Persistence/Journal/Surface.fs(.fsi)` 的 scoped 借用、`Execution/Delegation/{SyncDelegate/Surface,Fork/OpenCode/ToolSurface}.fs(.fsi)` 的观察接缝、work-record WHAT/004/010/011/辅助夹具及本包文档 | `CUT-06-R1` | root | `53cba28de` | 实际跨界反例、冷恢复和同步/异步消费者验收及非作者、精确包复验均通过，随本原子提交交付；库 10bbb32 / 0.6.4 未改 | `已释放` |
| `Mission/WorkRecord/Materialize.fs`、work-record 004/010 测试及 GAP/本包进度文档 | `CUT-06-R3` | root | `e7a150937` | 两项Terminal重复反例、1227项相关需求、非作者最终候选及eeaa精确包外复验完成，随本原子提交交付；其他代理只读 | `已释放` |
| `Mission/WorkRecord/{CompletionProjection,Materialize}.fs(.fsi)`、`Composition/Durable/{Projection,DomainFamilyBridge}.fs(.fsi)`、SyncDelegate/Runtime、Fork/Host/{Runtime,RunLifecycle}、Fork/OpenCode/ToolSurface、Journal/Surface的既有ContextReanchored解码、对应owner编译清单、work-record WHAT/004/010及本包证据文档 | `CUT-06-R2` | root | `2ec1a5400` | 完成表示、确切终点、真实消费者和独立冷恢复、1239项相关回归及精确包非作者验收完成，随本原子提交交付；库0.6.4不变 | `已释放` |
| EventStore 的因果成员契约/Integrator、各规则签名、Journal writer/接线、Trace/WorkRecord 完成投影及相关规范/门禁/证据 | `CUT-06-R4` | root；r2_consumer_review 仅 work-record/004 末尾与独占测试 helper | `49b64879c` | 正式新增22项、1311项相关需求、三名非作者和229980精确包隔离消费通过，随本原子提交交付；越界白盒测试已撤回，库0.6.4不变 | `已释放` |
| `OpenCode/Host/PairProgrammingThoughtTransform.fs`、`Context/Trace/Materialization.fs(.fsi)`、prefix-stability/014及独占辅助夹具、旧grounding-007/Host-032与共享Plugin夹具、相关契约与证据 | `CUT-17-R1` | root | `2aaaf6077` | 真实Host网络/原件/引用/冷读、1350项相关回归及f047精确包隔离消费完成；归因与边界同步，随本原子提交交付，库0.6.4未改 | `已释放` |
| `src/Wanxiangshu/Foundation/CanonicalJson.fs(.fsi), src/Wanxiangshu/Persistence/EventStore/CanonicalEventCodec.fs(.fsi)` | `CUT-18` | `eng-cut18` | `62dbebc5d`（认领时未运行 git，base 以 DevOps 核实为准） | `CUT-18 完成 wanxiangli/events 0.6.0 验证、vendor tarball 落位与 durable-events 测试回归`（2026-10-15 随 0.7.0 主线提交 2f98049e5 合入，durable-events 244 项全绿，证据见 cards/CUT-18.md） | `已释放` |
| `src/Wanxiangshu/Process/{Deadline,NodeTiming,VirtualTiming}.fs(.fsi), src/Wanxiangshu/Execution/Session/Wait/{Registry,CompletionMailbox}.fs(.fsi)` | `CUT-21` | `devops`（受委派集成） | `2f98049e5` | `CUT-21 完成 wanxiangli 0.8.0 time/wait 发布与主仓桥接切换`（2026-10-15 库 commit 8422617，干净 HEAD worktree 验证全绿后主线合入） | `已释放` |
| `src/Wanxiangshu/Foundation/{Roles,RolesSurface}.fs(.fsi), src/Wanxiangshu/Participant/Persona/{Catalog,Identity,ManagedCatalog,Surface}.fs(.fsi), src/Wanxiangshu/Interaction/Authority/{IdentitySeed,Origin}.fs(.fsi); wanxiangli src/identity/*` | `CUT-07` | `eng-cut07b-goose`（接手 eng-cut07-goose） | `48dff9349` / wxl `a8b89a4` | `CUT-07 主仓切换合入 master 并更新卡片证据`（2026-10-20 wanxiangli 54dcc34 / 0.11.0，主仓切换合入，证据见 cards/CUT-07.md §11） | `已释放` |
| `src/Wanxiangshu/Process/{Pty,PtyApi,PtyBackend,PtySession,PtySupervisor,PtyTypes,Spool,ProcessOutput}.fs(.fsi)`；wanxiangli `src/pty/`、`src/spool/` | `CUT-20` | `eng-cut20-goose` | `48dff9349` | `CUT-20 完成 PTY/spool 库实现、主仓接线与旧 owner 退出` | `已释放` |
| `src/Wanxiangshu/Persistence/EventStore/{EventKWayMerge,MergeSurface,IntegratorEngine}.fs(.fsi)`、`Wanxiangshu.Owner.durable-convergence.eventstore-merge-runtime.fsproj`；wanxiangli `src/convergence/` | `CUT-19` | `eng-cut19b-goose`（接手 eng-cut19-goose） | `701e69551` / wxl `c9a943d` | `CUT-19 KWayMerge 子范围合入 master 并更新卡片证据` | `已释放`（2026-10-21 CUT-19 按缩小范围关闭） |
| `src/Wanxiangshu/Persistence/EventStore/ProcessEventLog.fs(.fsi)` 的共存裁决与成帧段；wanxiangli `src/segments/` | `CUT-19-a` | `eng-cut19d-goose` | `636f8d847` / wxl `d2a9f3b` | `CUT-19-a 合入 master 并更新卡片证据` | `已释放` |
| `src/Wanxiangshu/Repository/Knowledge/Casebook/{Index,Model,Capture,Bookkeeper}.fs(.fsi)`；wanxiangli `src/casebook*/` | `CUT-27` | `eng-cut27d-goose`（接手 eng-cut27b/27c-goose） | `699609cfd` / wxl `a3664e2` | `CUT-27 子范围②③合入 master 并更新卡片证据` | `已释放`（2026-10-21 CUT-27 完成） |
| `src/Wanxiangshu/Repository/Programming/Js/DeliveredValues.fs(.fsi)`；wanxiangli `src/workrecord/`→`src/delivery/` | `CUT-06` | `eng-cut06-goose` | `993e93683` / wxl `a8b89a4` | `CUT-06 交付视图格接线合入 master 并更新卡片证据`（2026-10-20 wanxiangli 2564387 / 0.9.0，主仓接线合入） | `已释放` |
| `src/Wanxiangshu/Execution/Session/{LoopDetector,LoopDetectorSurface}.fs(.fsi)`；wanxiangli `src/degeneration/` | `CUT-30` | `eng-cut06-goose` | `fa08a6100` / wxl `5edd3c7` | `CUT-30 检测算法接线合入 master 并更新卡片证据` | `已释放` |
| `src/Wanxiangshu/Ablation/Manifest.fs(.fsi)`；wanxiangli `src/ablation/` | `CUT-37` | `eng-cut06b-goose`（接手 eng-cut06-goose） | `6859577a7` / wxl `6013c9a` | `CUT-37 消融 DAG 定律接线合入 master 并更新卡片证据`（2026-10-20 wanxiangli 6013c9a / 0.17.0） | `已释放` |
| `src/Wanxiangshu/Interaction/Authority/{Run,Origin}.fs(.fsi)`；wanxiangli `src/authority/` | `CUT-09` | `eng-cut09c-goose`（接手 eng-cut09b-goose） | `13944a5db` / wxl `351e3c5` | `CUT-09 子范围①接线合入 master 并更新卡片证据`（2026-10-21 已合入） | `已释放` |

## 两仓集成队列

一个候选发布到主仓后，再拉下一项。库实现 PR 可以多个待审；同名版本发布和主仓锁更新不能并发。填写完整性摘要，不以文件名作唯一身份。

| 排位 | 卡 | 库 commit / version / tarball SHA-256 | 主仓 base / candidate | Reviewer | 剩余门禁或历史例外 | 状态 |
|---|---|---|---|---|---|---|
| 1 | CUT-02 | — | 4be1c4105 | manager | GAP-092（仅 015 预声明 todo，无新增门禁） | 已合入 |
| 2 | CUT-03 | — | 042ae616a | manager | 无新增门禁（GAP-101 均为预声明 todo） | 已合入 |
| 3 | CUT-04 | — | 6b5b71cc7 | manager | 无新增门禁（GAP-133 均为预声明 todo） | 已合入 |
| 4 | CUT-05/07/18/24/26 | wanxiangli 8422617 之前的主线形态（0.7.0 tarball） | 2f98049e5 | manager | GAP-085（requirement-grounding 002/007 为 GAP.md 既有登记缺口，非本批回归） | 已合入 |
| 5 | CUT-07 主仓切换 | wanxiangli 54dcc34 / 0.11.0 / 无 tarball（submodule，master gitlink c9a943d 包含） | 959f05643 / 本卡接线提交 | 自治（仓主授权） | 相关回归 delegation 23 fail/33 cancelled、managed-session-lifecycle 1/69、work 9、dispatch-protocol 1 cancelled 为 base 既有（用例名 diff 为空） | 已合入 |
| 6 | CUT-21 | wanxiangli 8422617 / 0.8.0 | 2f98049e5 之上的本轮主仓提交 | manager | 主仓共享工作树全量 build 因他人 Interaction 改动中间态阻断；本卡验证已在干净 HEAD worktree 完成（time-capability 24/0、causal-wait 52/0、subsystems OK） | 已验证待合入 |
| 7 | CUT-06 | wanxiangli 2564387 / 0.9.0 / 无 tarball（submodule + `file:wanxiangli`，仓规禁止跟踪 .tgz） | ef0476046 / 本卡接线提交 | 自治（仓主授权） | 相关回归 9 fail/5 cancelled 为 master 既有（base 同） | 已合入 |
| 8 | CUT-30 | wanxiangli a0f0de9 / 0.12.0 / 无 tarball（submodule） | 959f05643 / 本卡接线提交 | 自治（仓主授权） | degeneration-guard 10 fail 为 master 既有（base 同，006/007/009 对账 WIP） | 已合入 |
| 9 | CUT-20 | wanxiangli 20e0372→6bf65d3 / 0.13.0（spool）；e383e93→4f8e316 / 0.15.0（pty）；submodule + `file:wanxiangli`，无 tarball | spool f7d112dad；pty 本卡接线提交 | 自治（仓主授权） | 无新增门禁：check 154 与 base 相同；PTY 相关 36 个文件与 base 逐项一致（既有 7 fail/48 cancelled） | 已合入 |

## 边界修订与产品裁决登记

这里只记协作决策与来源，不建立第二份业务规则。实际语义须落实到原 WHAT 或新库局部规则。详细差异见 [COVERAGE.md](COVERAGE.md#draft-conflicts)。

| 决策号 | 影响卡 | 待判问题 | 当前处置 | 决策/证据 |
|---|---|---|---|---|
| D-01 | 07、09、11 | 草稿中的未知来源回退 human 与现行权威证据冲突 | 保留现行拒绝/明确未知；开工时裁定精确接口 | 待认领；不得先删权威闭包 |
| D-02 | 03、06、15、16、28 | 单观察者 Trace 与并发捕获、推理、retry 排除、Fission 合流、Opening 范围不完全等价 | Opening、固定cut及原件接管按CUT-03-R6验收；来源/身份/canonical覆盖的独立复审与旧历史边界已补。Fission/Invocation完整产品链的GAP仍保留 | [本批范围与下一包](REPAIR-PROGRESS-2026-10-15.md) |
| D-03 | 02、20、21 | 基础 pipe 留尾不等于主仓硬期限、完整外存及 PTY | 默认基础版保持留尾；显式 Capture/监管承接所选非 PTY 完整采集和同组期限/取消。PTY、脱组后代、宿主崩溃恢复不扩成已承接 | [精确包与真实反例](REPAIR-PROGRESS-2026-10-15.md) |
| D-04 | 18、19 | 单流 SQLite CAS 与多 writer 合法分叉不是同一承诺 | 两种协议分开，不用前者替代后者 | 待 CUT-18/19 |
| D-05 | 27、29、30、31 | 新库草稿“不再建”与仍生效的产品功能冲突 | 默认暂留并维护；抽取/删除需逐卡裁决 | 待认领；不是删除批准 |
| D-06 | 32、33、38 | 相对主线净改进与完整发布验收 | 合并可用净改进准入，验证结果仍原样报告 | 用户 2026-10-14T23:59:13Z 授权；[比较记录](../lifecycle-cutover-closeout.md) |
| D-07 | 00、34、36 | 库规则仍写旧仓修改待授权、宿主名尚未确认 | 本计划不偷偷覆盖规则；各任务按当前授权更新过时范围，第二宿主先明确对象 | CUT-00 已落盘（cards/CUT-00.md）；主仓与 wanxiangli 按当前授权推进。2026-10-21 更新：CUT-36 的“宿主未指认 / 无真实调用授权”限制已由仓主解除（omp 18.12.4 + 真实模型调用获授权），CUT-36 转入 DOING；D-07 对其他卡与对 CUT-36 其余护栏（不得编造宿主 API、不得用模拟回调冒充 canary、不得宣称已支持第二宿主）不因此放宽 |

## 更新纪律

认领/换人/换 base/变更协议/发布包/合入主线/关闭旧 owner 时更新看板。单卡实现日志不直接写进总表，只链接本卡证据。没有真实日志的计数留空，不写零；没有人接手的字段保持 `—`。

看板是协作状态的唯一来源；CARDS 是范围，COVERAGE 是义务索引，GAP 是证明债务，它们不维护竞争的“完成率”。
