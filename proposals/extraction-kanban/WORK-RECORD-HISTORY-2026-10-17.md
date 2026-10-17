# CUT-06-R2：已完成范围的历史表示

输入为干净主仓 `2ec1a5400`、库 `10bbb3247853653543d26eb269ac49008e1b5623 / 0.6.4`。本包按[历史重物化计划](REMAINING-REPAIRS-2026-10-16.md#gap111-history)施工，接续 R1/R3；R12-U1 继续暂缓。当前验收进度见本文后半和[看板](BOARD.md#source-audit-repairs)。

## 问题与归因

| 编号 | 性质 | 复现与修正 |
|---|---|---|
| R2-01 / P1 | 输入已有的 WHAT[work-record-004] 缺口 | 完成范围先显示 `CURRENT_REASONING_SUMMARY`，晚到 entry 会增加 `LATE_SUMMARY`，跨 invocation squash 会将其改为原文。修前八个真实 Terminal/journal 对照全部失败，与是否提前读取、是否带 ProviderRun 无关 |
| R2-02 / P1 | 输入已有的完成消费时序缺口 | Sync 使用 capture receipt 的当前 head；Fork 等 drain 后读取当前 head。旧精确包实际 Fork 在 Terminal 后追加语义 part，再发完成通知，发布 blob 混入 `FUTURE_DETAIL`，见 `/private/tmp/g111h-old-frontier-red.log`。现在两个生产消费者都用该 run 已验证 Terminal 的确切终点 |
| R2-E1 | 前轮验收表述过宽 | 旧 Fork `settle` / `prepareTerminalDelivery` 测试接缝只捕获原件便发完成通知，未写生产 TerminalReporter 必先捕获的 Terminal。前轮完成 blob/Join 的稳定证明成立，但这两个用例不能单独证明真实 Terminal 已捕获。本轮补齐该接缝，直接 Terminal 反例和生产 TerminalReporter 接线分别核对 |
| R2-E2 | 本轮测试夹具错误，已修 | 首次 reanchor 测试把通用 JournalHandle 传给使用另一 opaque wrapper 的 Grounding surface，报 disposed 访问错误。改为现有 JournalSurface.appendAgent 解码真实 ContextReanchored；没有解包私有句柄或改 reanchor 业务规则 |
| R2-E3 | 编译规模门禁的必要契约更新 | 首轮相邻回归只有 delegation-028 失败。与 HEAD 的 ProjectReference 传递集合精确比较为391→392，新增仅纯 `Mission/WorkRecord/CompletionProjection.fs`、删除为空；按 WHAT-028 的增长规则记录原因并更新 ratchet。60%硬上限、合同及恢复隔离未放宽，日志 `/private/tmp/g111h-closure-delta.log` |

当前候选的限定验收未发现新的行为回退。首候选的 FSI 缩进编译失败和构建期间补测试导致的 mid-build mutation 都已保留日志；失败构建不算交付证据。R1/R3 的来源隔离、coverage 与 Terminal 去重继续回归。

## 完成点与职责

`CompletedWorkRecordProjection` 属于 WorkRecord，在已验证 `TerminalOutputCaptured` 成功折叠的同一次 aggregate 更新中，保存 ProviderRun、其不可变 Terminal frontier、当时的 Blogger frame 引用。它不写新事实或旁挂数据库，不按首次读取建立 range 缓存；冷恢复按原有事实顺序派生相同结果。

保存的是已有 newest-first 不可变 list 引用，后续 entry 可共享链尾；没有复制正文、BlogCoverage、PrefixCoverage 或整个 Blogger 状态。Trace 继续拥有并校验原件/Terminal，Blogger 拥有当前 frame 和 coverage，Composition 只组装两个领域的输出。拒绝 Terminal 在派生完成表示前返回，重放同 run 不覆盖已捕获引用。冻结时没有 frame 的 `Some []` 也是证据，不能退回当前摘要。

带 ProviderRun 的 bounded 读取只在终点匹配该 run 时取冻结引用。不带 run 时取该终点第一个完成事实的摘要引用，仍不借 Terminal 正文；同一终点后续 run 的完成表示可不同，但不能改掉前一 run 的表示。两条路径继续按 R1 的连续完整前缀、实际采用的 coverage 和原件补足规则物化。全 session 记忆与未完成范围保留当前视图；没有回写历史 coverage。

## 消费者盘点

| 消费者 | 完成身份与读取契约 | 本包验证 |
|---|---|---|
| SyncDelegate / Engineer、DevOps 等 | invocation 首个 StartCursor + 当前 turn.ProviderRun；Terminal 成功后以该 run frontier 关闭范围，共用 materializer | 实际复用子会话两轮，上一轮不混入、本轮正文保留；完成后追加原件/squash/另一 Terminal 再处理完成 turn，返回字节与完成时一致；新进程冷读一致 |
| Fork / resume / Join | PendingHostRun.StartCursor + 完成结果 ProviderRun；以确切 Terminal frontier 物化后写原有 v2 completion blob | 实际两轮和 Join、完成后 squash 的 Join/冷持久 consume；Terminal 与通知之间插入新原件/squash/另一 Terminal，发布 blob 不扩大范围 |
| parent bounded handoff | 已发送 frontier→当前 head；它是发送时的工作增量，未完成的父工作不是 child 完成事实 | 保留准备时的范围；若终点有 Terminal，公共 bounded 读取使用当时引用。初次 parent 全记录仍为当前记忆视图，不伪造父工作完成 |
| process review / Finality | 当前树没有原 ProcessReview / FinalityController 产品消费者；保留在需求文字及 WorkRecord-016 的范围夹具中 | 不以016夹具冒充真实工具验收，也不为已退出路径增加 facade；实际接线在 PluginSessionWiring/PluginHooks 指向上列共享消费者 |

Sync 的 Terminal 写入 NotCommitted/Unknown 分支继续按 delegation-031 交付已产生的 turn，不重执行；本包的冻结证明限于有已验证持久完成事实的路径，没有把它写成物理 Unknown 已归因。

## 历史兼容边界

原 v2 完成 blob 含 work_record、ProviderRun、child/root，但没有可证明的 Start/End 关联。原 Join、解码和 cold consume 继续消费这个已保存的确切表示，不根据当前摘要重写它。一般 range 或子范围查询不能只凭 ProviderRun 取整份旧 blob。

旧版本可能在 Terminal 后又入账摘要，再物化并发布旧完成 blob。本轮对原事件回放固定的是 Terminal 当时的引用；这种旧 blob 与派生历史查询可能原本就来自不同的摘要选择点。二者不自动迁移、不宣称升级后统一成同一份字节；该数据关联缺失仍属于显式历史迁移/完整 GAP-109 产品链的边界。没有 Terminal 的旧日志不补造完成事实。

已用修前精确 `eeaa8b…` 包生成真实 Terminal→late entry→完成 blob，退出旧进程后由新候选冷读并 durable consume。原 blob 摘要 `491189275a48a5f614f96effd8cb740c7a9ae2e59a5b84188df0561356f0cbfe`，包含 `EARLY_SUMMARY` 和 `LATE_SUMMARY`，升级后确切保留；新范围查询只使用 Terminal 当时的 `EARLY_SUMMARY`。脚本 `/private/tmp/g111h-upgrade-compat.mjs`，日志 `/private/tmp/g111h-upgrade-produce2.log`、`/private/tmp/g111h-upgrade-consume.log`。此升级对照借用主仓 runtime 依赖，单列为历史语义证据；精确新包的隔离安装由后面的独立包验收证明。首次旧包解包后的依赖缺失日志 `/private/tmp/g111h-upgrade-produce.log` 不算产品失败或成功安装证据。

## 验收进度

| 工作包 | 状态 | 证据 |
|---|---|---|
| 反例与契约 | 完成 | `/private/tmp/g111h-red.log`：原实现新增10项全部失败，八项有真实 Terminal、两项是旧 Fork 通知接缝；WHAT-004 明确完成点和历史边界 |
| 完成表示与生产消费 | 完成 | Fable 574，174 surfaces / 839 modules；`/private/tmp/g111h-build-seam.log`；基础构建573含核心生产修改，574补既有 ContextReanchored 的 JS 解码 |
| 核心 Node/Bun | 完成 | 004/010/011 各70项 / 69pass / 0fail / 1TODO，日志 `/private/tmp/g111h-core-node2.log`、`/private/tmp/g111h-core-bun.log`；唯一 TODO 为实际 inspect/Fork 同事实完整链 GAP-109 |
| 不重叠相关回归 | 完成 | 三组共1239项 / 1137pass / 0普通fail / 34skip / 68TODO；Trace/WorkRecord/prefix 341pass / 0fail / 1skip / 16TODO，durable/obligation 247pass / 0fail / 27skip / 9TODO，consumer最终549pass / 0fail / 6skip / 43TODO。首轮consumer的ratchet失败保留 |
| 非作者源与行为复验 | 完成 | `g111r_contracts` 在574固定产物独立 Node/Bun 验证真实Fork完成→squash→历史查询/Join/独立冷读/consume；空frame且无提前读取、同run重放、同终点多run、错误身份/终点及冲突Terminal被拒后的冷前缀均通过 |
| 独立编译、checkpoint、Host | 完成 | 新CompletionProjection owner仅声明闭包的94个源文件独立Fable编译通过；原Trace opaque接口正反编译1/1；真实非空Trace/Terminal checkpoint1/1；host-boundary-021为20/20（接线/前缀smoke，不代替真实消费者） |
| 旧包升级、精确新包、门禁 | 完成 | 上列升级对照通过；575精确新包2076members / 13,336,942bytes，SHA-256 `df0ccd0a0be4913df8a4c241719b0ebda1827ffcf9e36db54b0b91663751e81a`；真实pack、外部隔离安装与默认入口通过。非作者同包独立离线安装、全文件字节核对及Node/Bun实际消费者四项probe通过；format/check/diff通过 |

默认组命令使用 `WXS_ACCEPT_TODO=1 TESTS_MJS_FILES="<对应目录内编号测试的逗号列表>" node requirements/verification-system/tests/run.mjs`；三个不重叠目录组分别为 work-record/semantic-trace/prefix-stability、context-compression/delegation/participant-horizon、durable-events/obligation-ledger。TODO/skip 不记为通过。日志 `/private/tmp/g111h-{trace,consumers-final,durable}.log`；修订前consumer日志 `/private/tmp/g111h-consumers.log`。

integration 独立启用 `WXS_TIER_INTEGRATION=1`，按 `independently compile` / `completed-terminal checkpoint` 名称运行 semantic-trace-005、obligation-ledger-004；日志 `/private/tmp/g111h-integration-{trace,checkpoint}.log`。新增owner编译为仓库 `compileOwnerProject`，日志 `/private/tmp/g111h-completion-owner-compile.log`，未运行 dotnet build。格式、静态和包日志 `/private/tmp/g111h-{format-final,check-final,package}.log`；生产574、文档/测试元数据575，174 surfaces / 839 modules。

非作者探针日志 `/private/tmp/gap111r2-contracts-freeze-{node,bun}-574.log`、`/private/tmp/g111h-contracts-terminal-boundary-{node,bun}-verified-574.log`。冲突Terminal触发既有semantic-cut/SIGKILL，原未隔离的exit137日志保留；一次独立probe漏计既有coverage边界的重读reasoning，修正oracle后通过，不改产品。

同一精确包的独立安装日志 `/private/tmp/g111h-contracts-package-{ci,integrity}.log`，行为日志 `/private/tmp/g111h-contracts-package-{freeze-node,freeze-bun,boundary-node,boundary-bun}.log`。离线安装117项依赖，移除顶层wanxiangli，实际使用包内0.6.4，所有依赖及符号链接限定在隔离安装中，不借主仓node_modules；2076个安装文件与archive逐字相同。暖/冷Fork冻结和Terminal边界四项probe均exit0。产物在文档收尾后再次与完整archive核对，见 `/private/tmp/g111h-final-package-closure.log`。

ESM解析定位与默认入口在Node/Bun均通过，日志 `/private/tmp/g111h-contracts-package-isolation-verified-{node,bun}.log`；首次辅助定位误用CJS解析import-only exports、把默认导出对象当函数的两项oracle错误均保留原日志，修正为实际ESM/入口契约后通过，不算产品错误。隔离安装和全部探针Git/journal目录已清理，见 `/private/tmp/g111h-contracts-package-cleanup.log`；旧包升级探针目录也已清理，脚本与原始日志保留。

## 交付结论

CUT-06-R2 所列完成表示、确切终点、真实消费者与暖冷历史查询已通过验收，随本原子提交正常推送master，不强推。旧历史的range关联缺失按上述边界保留。库0.6.4未修改；本包没有部署运行中的用户Host。

首次核心 reanchor 夹具失败日志 `/private/tmp/g111h-core-node.log`，编译/并发输入失败分别为 `/private/tmp/g111h-build-projection.log`、`/private/tmp/g111h-build-consumers.log`。这些不覆盖后面的成功证据，也不归因给原拆分。

本包只处理有确切完成事实的历史表示与已列消费者。GAP-111、GAP-101/109、Opening/Resume/Fission 完整产品链、CUT-06 父卡和 R12-U1 不据此整体销项。提交推送与运行中用户 Host 部署分别记录。
