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
| CUT-00 | 两仓准备、差异裁决与并行开工 | 协作 | DONE | — | eng-cut00 | manager（[CUT-00](cards/CUT-00.md)） |
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
| CUT-09-b | interaction-authority-018 关闭见证精确匹配 | 权威 | BACKLOG | CUT-09, CUT-11 | — | 2026-10-21 从 CUT-09 拆出：五类 ExpectedClosureKind 与 source witness 精确匹配，宿主需先补齐事实；见 cards/CUT-09.md §6 |
| CUT-10 | 模型路由与精确容量许可 | 执行 | DOING | CUT-07 | eng-cut10e-goose（接手 eng-cut10d-goose、eng-cut10c-goose、eng-cut10b-goose、eng-cut10-goose） | 2026-10-21 认领；执行准入等待队列已交 wanxiangli/admission-queue 0.37.0（57fbca343），eng-cut10e-goose 接手③容量账本/托管/provider-step 借用（wanxiangli/borrowing）；①②③已合入（f982f8070 routing、9b4b64dad permit 0.33.0、807a3db6a capacity 0.35.0），eng-cut10d-goose 接手收尾：子范围①调度器 ABI（协议版本、导出形状、target 解码、同步调用、理论容量查询）抽入 wanxiangli/routing（0.25.0，主仓接线随本次交接合入）；②精确执行许可（签发/提交/两种互斥释放）抽入 wanxiangli/permit 进行中；③容量账本与 provider-step 借用随后。写集合 `OpenCode/Host/ModelRouting.fs(.fsi)` 的 ABI 段、`OpenCode/Host/ModelCapacity/*`；wanxiangli `src/routing/`、`src/permit/` |
| CUT-11 | 派发认领与外部效果记账 | 执行 | DOING | CUT-09, CUT-10 | eng-cut09e-goose | 2026-10-21 认领；子范围①已合入：认领作用域与 PromptKey 原文、ClaimSequence 消费、四态结算的投影编辑、精确 occasion 准入与恢复核对只剩 wanxiangli/dispatch 0.40.0 一个 owner（Model.fs/Run.fs/Recovery.fs 手写规则已删）。投影差分 1,200,000 次、恢复差分 200,000 次均 0 处不同，11 个变异全抓住。子范围②已合入：Host 发送回答的认领账动作、"确定未发送"与 Detached 结论只剩 wanxiangli/dispatch 0.42.0 `answer`（Send.fs 四处手写分支已删），差分 90 次 0 处不同、6 个变异全抓住。余项③Work.operation 关联 ④旧写入口退出与投影重置，见 cards/CUT-11.md |
| CUT-12 | 会话关联与执行事实 | 执行 | DOING | CUT-04, CUT-11 | eng-cut12-goose | 2026-10-21 子闭环 2/4 落地：Accepted/ProviderStarted/Terminal 单赋值律（Fold、ProviderLifecycle、PreProviderSettlement 三份手写 match）只剩 wanxiangli/chat-execution 0.44.0 一个 owner；差分 7,656 次 0 处不同，7/7 变异抓住。余：关联先于首 prompt、exact 结算与复用、删除与重启 Host 链；整卡 DONE 待 CUT-11。见 cards/CUT-12.md |
| CUT-13 | 委派、复用与结果认领 | 执行 | BACKLOG | CUT-06, CUT-12 | — | — |
| CUT-14 | 失败预算、恢复决议与结算 | 执行 | BACKLOG | CUT-10, CUT-11, CUT-12 | — | — |
| CUT-15 | 前缀世代与候选提交 | 上下文 | DONE | CUT-03 | eng-cut15c-goose | 2026-10-21 rebase 到 e4ea42ddf 后重测通过；2026-10-20：前缀世代、候选/提交、吸收策略、检查点去重与同代追加律抽入 wanxiangli/prefix（库 7aac7b8 / 0.10.0 发布 770eea6）；主仓 Epoch/PhaseWindow/isAppendOnlyPrefix 改为投影、HostCompactionPolicy.nextReanchor 删除。修复 cutover 后折叠从不推进 PrefixEpoch 导致同一压缩反复重锚、清空重建检查点（5 个反例 base 红/candidate 绿）；差分 65000 次 0 差异；证据见 cards/CUT-15.md |
| CUT-16 | 压缩覆盖与 checkpoint | 上下文 | DONE | CUT-03, CUT-06, CUT-15 | eng-cut16d-goose（接手 eng-cut16c-goose / eng-cut06b-goose） | 2026-10-21 子范围①合入：ANCHOR 视图覆盖定律（view 声明形状、存活锚点分区、永久区间替换计划与折叠）抽入 wanxiangli/compression 0.21.0（库 a8292d9 / 发布 468991f），Anchor 四文件改为组合者、旧折叠与手写校验删除；编号台账、o200k 计量、宿主消息重建留主仓；32 文件 base/candidate 逐用例一致，差分 270000 次 0 分歧；子范围②view 检查点追加准入抽入 compression 0.25.1（库 5f15de3 / 发布 a699b95），PluginHooks 只留 journal 写入与在途会合，72 文件 base/candidate 逐用例一致、集成层 005 三变异体均被抓到；③核对后无剩余可切：todo 失败封口已在②，候选接受/Blogger 已由 [003][008] 退役，重启按 [031] 重置、窗口归 prefix，见 cards/CUT-16.md §6 |
| CUT-17 | 请求投影与信息边界 | 表达 | DONE | CUT-05, CUT-06, CUT-08, CUT-15, CUT-16 | eng-cut17f-goose（接手 eng-cut17e/d/c/b-goose、eng-cut16d-goose） | 2026-10-21 DONE：任期过滤 tenure 0.28.0、意图规划 plan 0.34.0、布局 layout、退休 attempt 归属 retired 0.39.0、规范线格式/语义字节 wire/semantic 0.41.0（ec036a317）均归 wanxiangli/projection；宿主 codec/Trace/journal 留主仓；退出核对与证据见 cards/CUT-17.md §8 |
| CUT-18 | 规范事实编码与校验 | 存储 | DONE | CUT-00 | eng-cut18 | 2026-10-15 主线提交 2f98049e5：wanxiangli events 模块合入 0.7.0，库侧 34 测试 + durable-events 244 项回归全绿，证据链见 cards/CUT-18.md §10 |
| CUT-19 | 多 writer 存储与收敛 | 存储 | DONE | CUT-18 | eng-cut19d-goose（接手 eng-cut19c-goose；前三任 eng-cut19/19b/19c-goose 均已中断退出，工作树干净、提交均在主线） | 2026-10-21 按缩小后的范围关闭（README §6 末段）：①k-way 归并（wanxiangli/convergence 0.14.0）②整条 writer UTC 日保留（retention 0.16.0）③已落盘行闭包（events.checkClosure 0.19.0）④尾行活动判定（retention.lastActivity 0.19.0）⑤段命名与分组律（segments 0.23.0），五段旧算法均已删除、主仓只剩 facade；跨进程锁、原子追加/fsync/rename/utimes、解码前缀缓存与目录读写登记为宿主 I/O 明确保留（RETAINED，理由见 cards/CUT-19.md §16）；未闭合部分拆为 CUT-19-a（段共存字节裁决与写入流成帧）、CUT-19-b（Integrator 规则隔离与 cut-tail 载荷律）、CUT-19-c（Journal/旧 Store 逐事实族退出与残留 Git 类型去向），不藏在本卡 DONE 之下 |
| CUT-19-a | 段共存的字节裁决与 writer 流成帧律 | 存储 | DONE | CUT-19 | eng-cut19d-goose | 2026-10-21 完成：成帧（尾 LF、无空内行、逐行规范解码）与共存裁决（series / legacy / continuation / diverged）抽入 wanxiangli/segments 0.24.0（库 57c16ed / 发布 40265dd）；主仓读取与整理两份手写裁决合为一次库调用，删 7 个私有函数；影子双算 1500 轮读取、整理与落盘字节全等；durable-events/convergence 252/0。见 cards/CUT-19.md §17 |
| CUT-19-b | Integrator 规则隔离与 cut-tail 载荷律 | 存储 | DONE | CUT-19 | eng-cut27d-goose | 2026-10-21 完成：(rule, FaultScope) 隔离账、隔离期跳过、`ProjectionCutTail` 载荷编解码与截尾生效条件抽入 wanxiangli/quarantine 0.38.0（3d6a83f）；主仓 master `c52bff68a` 删手写 Thoth 编解码，Faults 改为库账；规则本体、Current、唯一一次全量重放预算与 PrepareLive 留主仓组合者；影子：编解码 3000/3000、账 1.8M 次查询逐项相等；durable-events/convergence 252/0。见 cards/CUT-19.md §18 |
| CUT-19-c | Journal/旧 Store 逐事实族退出与残留 Git 类型去向 | 存储 | BACKLOG | CUT-19, CUT-11, CUT-12, CUT-27, CUT-29, CUT-31 | — | 2026-10-21 拆出：`Persistence/Journal`（约 3.5k 行、主仓 188 处 open）与 EventStore `Store`/`Surface` 的每个事实族在其 owner 卡完成后逐批退出；`GitTypes`/`StoreRef`/`PublishError` 只剩 EventStore 内部与 JS 证明面引用（在线 Git 投影已删），需判定删除或保留。见 cards/CUT-19.md §16 |
| CUT-20 | PTY 与完整输出外存 | 物理 | DONE | CUT-02, CUT-04 | eng-cut20b-goose（接手 eng-cut20-goose） | 2026-10-20 spool：wanxiangli/spool（库 20e0372，0.13.0 发布 6bf65d3），主仓 f7d112dad，修复 Spooled git 输出被吞成空串与 spool 文件泄漏；PTY：wanxiangli/pty（库 e383e93，0.15.0 发布 4f8e316），主仓 PtySupervisor 改为路由、物理终端交给库，修复 bun-pty 在缺失 cwd 时于别处执行命令；PTY 整体去留是产品裁决，不在本卡；证据见 cards/CUT-20.md |
| CUT-21 | 显式时间与因果等待 | 物理 | DONE | CUT-00 | — | 2026-10-15 库侧 time/wait 模块随 0.8.0 发布（库 commit 8422617，全量 460 测试绿）；主仓 NodeTiming/Deadline/VirtualTiming/Wait Registry/CompletionMailbox 桥接切至 0.8.0，干净 HEAD worktree 验证 time-capability 24/0、causal-wait 52/0、subsystems OK；主仓共享工作树全量 build 因他人 Interaction 进行中改动阻断（非本卡引入），证据链见 cards/CUT-21.md §10 |
| CUT-22 | attention、concern 与邮箱归属 | 权威 | REMOVED | CUT-09 | eng-cut09c-goose | 2026-10-21 REMOVED（不记为抽取成功）：两包已是退休边界，运行时/工具/持久投影早由 94f994ed8 等删除，无活算法可抽；本卡删掉最后的死残留（Host 事实上恒 None 的 `ConcernPlacement`、无生产者的 `DeferredWorkPresentation` continuation），补 2 条先红后绿的回归测试；日志兼容 150 例 + 真实日志重放通过。见 cards/CUT-22.md |
| CUT-23 | Git 工作树、委任与集成 | 仓库 | BACKLOG | CUT-12, CUT-13 | — | — |
| CUT-24 | 文件读取、锚点与原子编辑 | 仓库 | DONE | CUT-00 | eng-cut24 | 2026-10-15 主线提交 2f98049e5：wanxiangli fs 模块合入 0.7.0（38 测试绿），repository-programming 158 项回归全绿，证据链见 cards/CUT-24.md §10 |
| CUT-25 | 受限 JS 编程面与执行组合 | 仓库 | DOING | CUT-02, CUT-08, CUT-24 | eng-cut25b-goose | 2026-10-21 沙箱+WeakMap 已堵；schema/admission/generator 已统一到 admitsProgrammingSurface；描述/基类同源与库抽取继续 |
| CUT-26 | 需求发现与材料溯源 | 仓库 | DONE | CUT-00 | eng-cut26 | 2026-10-15 主线提交 2f98049e5：wanxiangli grounding 模块合入 0.7.0（13 测试绿）；主仓 requirement-grounding 残留 002/007 两项为 GAP.md 既有登记缺口（GAP-085，非本卡回归），证据链见 cards/CUT-26.md §10 |
| CUT-27 | Casebook / 知识复用去向 | 产品裁决 | DONE | CUT-06, CUT-19, CUT-24 | eng-cut27d-goose（接手 eng-cut27c-goose；前任 eng-cut19d/27b/27c-goose 均已中断退出） | 2026-10-21 裁决“保留产品功能、分步抽取”并完成：①Index 纯投影归 wanxiangli/casebook 0.26.0（顺带修复代理对截断致 shelfmark 失效）②Fetch 差异决策归 casebook-maintenance 0.29.0（400 组主仓黄金向量逐字节相等；修复截断切开代理对）③实质访问判定归 casebook-access 0.31.0（影子 16868 观测，差异仅为反引号/选项值被误记为文件的有意修复）；master `dc23ec407`/`699609cfd`；④Bookkeeper/Settlement/Host 接线按裁决 RETAINED 留主仓随 CUT-35；既有 7 个 knowledge-reuse-010 失败 base 同因未变。见 cards/CUT-27.md §1–§3 |
| CUT-28 | Fission 多 lane 执行 | 执行 | BACKLOG | CUT-03, CUT-10, CUT-12, CUT-13 | — | — |
| CUT-29 | Strength / 投机调查去向 | 产品裁决 | DECISION | CUT-06, CUT-10, CUT-13, CUT-19 | — | — |
| CUT-30 | Rulebook、指导与退化观察去向 | 产品裁决 | DONE | CUT-03, CUT-05, CUT-16, CUT-19 | eng-cut06-goose | 2026-10-20 裁决并完成：Rulebook/Enforcer、Blogger 诊断、guidance-delivery、behavior-diagnosis 已由仓主于 6eef89d1e 删除（REMOVED）；degeneration-guard 检测算法已抽入 wanxiangli/degeneration 0.12.0（a0f0de9），LoopDetector 改为组合者，语料/包络派生与分词、LoopSensor 中断/续发留主仓；差分逐位相等，见 cards/CUT-30.md |
| CUT-31 | Sphinx 与认识运行时去向 | 产品裁决 | DECISION | CUT-09, CUT-10, CUT-11, CUT-13, CUT-19 | — | — |
| CUT-32 | 可独立验证的执行设施 | 验证 | DONE | CUT-00 | eng-cut06-goose | 2026-10-20 裁决 RETAINED：测试运行/计数、静默看门狗、进程组回收、日志与 verdict 分离、固定输入已由主仓 supervisor 与 verification-system 016/021/006 承担，不新增库、不重写 runner；窄入口为 `TESTS_MJS_FILES=… run.mjs`（README §9），本机 Node 20 下 006 有 7 项环境性失败如实记录，见 cards/CUT-32.md |
| CUT-33 | 两仓构建、打包与发布闭环 | 验证 | DONE | CUT-00 | eng-cut06-goose | 2026-10-20：按仓主现行规则（submodule + `file:wanxiangli`，禁止 .tgz）重判；库 5edd3c7 加 SPEC[build-008] exports/files/模块一致性门禁（三种变异实测报红），主仓 verify-package 实测 bundle 0.9.0 通过，指南分发段校正；gitlink 祖先校验等残项见 cards/CUT-33.md §5 |
| CUT-34 | OpenCode 适配边界 | 宿主 | BACKLOG | CUT-11, CUT-12, CUT-17, CUT-25 | — | — |
| CUT-35 | 顶层组装与逐域恢复 | 宿主 | BACKLOG | CUT-13, CUT-14, CUT-16, CUT-17, CUT-19, CUT-19-c, CUT-22, CUT-23, CUT-27, CUT-28, CUT-29, CUT-30, CUT-31, CUT-34 | — | — |
| CUT-36 | 第二宿主真实 canary | 可选 | OPTIONAL | CUT-34, CUT-35 | — | — |
| CUT-37 | 消融、诊断与实测成本 | 验证 | DONE | CUT-00 | eng-cut06b-goose | 2026-10-20 完成（接手 eng-cut06-goose）：消融 DAG 定律（主节点/父链、环检测、三态边约束）抽入 wanxiangli/ablation 0.17.0（库 2dfb15d / 发布 6013c9a），Ablation/Manifest 改为组合者、旧 DFS 与校验删除；清单读取、门禁投影留主仓，Host 诊断与 collect-incident RETAINED；13 文件 base/candidate 逐用例一致，差分 30000 次 0 分歧，见 cards/CUT-37.md |
| CUT-38 | 全仓残余职责与终局交账 | 收尾 | BACKLOG | CUT-00, CUT-01, CUT-02, CUT-03, CUT-04, CUT-05, CUT-06, CUT-07, CUT-08, CUT-09, CUT-10, CUT-11, CUT-12, CUT-13, CUT-14, CUT-15, CUT-16, CUT-17, CUT-18, CUT-19, CUT-19-a, CUT-19-b, CUT-19-c, CUT-20, CUT-21, CUT-22, CUT-23, CUT-24, CUT-25, CUT-26, CUT-27, CUT-28, CUT-29, CUT-30, CUT-31, CUT-32, CUT-33, CUT-34, CUT-35, CUT-37 | — | — |

CUT-02/03/04 的 LIB_READY 仅指现有基础版；满足主仓更大契约所需的补足在各卡内执行。完成 CUT-00 和本卡接线准备后，可认领并直接进入 INTEGRATING；发现基础协议不成立时回退 DOING，不用历史测试成绩代替新需求。

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
| D-02 | 03、06、15、16、28 | 单观察者 Trace 与并发捕获、推理、retry 排除、Fission 合流、Opening 范围不完全等价 | 先做差异表与最小闭合子范围 | 待 CUT-03 |
| D-03 | 02、20、21 | 基础 pipe 留尾不等于主仓硬期限、完整外存及 PTY | 暂留未覆盖 owner；分别切换 | CUT-20：完整外存由独立 `wanxiangli/spool` 承接（不用 pipe 留尾冒充），PTY 由独立 `wanxiangli/pty` 承接（真实 tty，非 pipe）；主仓硬期限仍由 CUT-02 的组合者持有。见 cards/CUT-20.md |
| D-04 | 18、19 | 单流 SQLite CAS 与多 writer 合法分叉不是同一承诺 | 两种协议分开，不用前者替代后者 | 待 CUT-18/19 |
| D-05 | 27、29、30、31 | 新库草稿“不再建”与仍生效的产品功能冲突 | 默认暂留并维护；抽取/删除需逐卡裁决 | 待认领；不是删除批准 |
| D-06 | 32、33、38 | 相对主线净改进与完整发布验收 | 合并可用净改进准入，验证结果仍原样报告 | 用户 2026-10-14T23:59:13Z 授权；[比较记录](../lifecycle-cutover-closeout.md) |
| D-07 | 00、34、36 | 库规则仍写旧仓修改待授权、宿主名尚未确认 | 本计划不偷偷覆盖规则；各任务按当前授权更新过时范围，第二宿主先明确对象 | CUT-00 已落盘（cards/CUT-00.md）；主仓与 wanxiangli 按当前授权推进，CUT-36 保持 OPTIONAL |

## 更新纪律

认领/换人/换 base/变更协议/发布包/合入主线/关闭旧 owner 时更新看板。单卡实现日志不直接写进总表，只链接本卡证据。没有真实日志的计数留空，不写零；没有人接手的字段保持 `—`。

看板是协作状态的唯一来源；CARDS 是范围，COVERAGE 是义务索引，GAP 是证明债务，它们不维护竞争的“完成率”。
