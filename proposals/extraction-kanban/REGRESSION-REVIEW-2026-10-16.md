# 四模块修复的独立回归复审

日期：2026-10-16。依据：[原报告](SOURCE-AUDIT-2026-10-15.md)、[施工看板](BOARD.md#source-audit-repairs)及 AGENTS.md。状态和交付证据见[第五批记录](REPAIR-PROGRESS-2026-10-15.md#regression-review-r5)。

**此前修复确实引入过问题。本轮仍能复现的新增回退是 O-13、O-14，已经修正；O-12、P-04 在修复前就存在，是此前漏修、漏验。** 不能把后来发现的问题一律归给原拆分，也不能把原有问题都算成我的回退。T-01、O-01 仍有明确未完成范围。

## 比较范围和方法

| 对象 | 修复前 | 本轮复审起点 | 全部修改范围 |
|---|---|---|---|
| 主仓 | `7d8feff030adfdeae511bdc0d8bb77f3c00b2a1f` | `86c0c9b8c14a75bfdf505f560033b0f520b46f40` | 14 提交、92 文件、2946 增 / 558 删 |
| 独立库 | `46d9c072af50389d069709e847d5772af3ec160a` / 0.5.2 | `eef482506f4ea65cad0a88d3a8c5769a8ca9adbb` / 0.6.2 | 10 提交、48 文件；包括实现、测试、构建和分发 |

三名未参与本轮实现的审查者分别复核 Process/Capture/Supervision、Ownership/Host、Trace/Journal/分发，只读共享仓库并编写隔离探针；root 串行修正，再由非作者复验。比较的是实际实现，不是旧报告中的说法。

历史主仓在 `/private/tmp/vibe-fs-before-repairs-20261016.rECMOc` 独立检出，使用原 lock、vendor 0.5.2 和独立 node_modules，实际执行 `node scripts/build.mjs` 成功：1530 项、173 surfaces / 835 modules。首次离线 npm ci 因缺缓存失败，随后按原 lock 正常安装；没有改历史输入或借用当前 dist。独立库两个精确包分别隔离安装，在 Node/Bun 执行同一探针。

本轮没有重建每一个中间提交。以下区分双版实跑、中间源码归因及此前运行记录，不把源码判断写成已执行反例。

## 一、修复前已有的问题

| 项目 | 双版对照或源码依据 | 归因 / 当前状态 |
|---|---|---|
| P-01 完整输出 | 同一 128 MiB 双路输出，baseline Node 只保存 92,930,048 字节、Bun 67,108,864；当前均保存 134,217,728 字节、摘要一致 | 原接线缺陷已修。默认 Process 的有界留尾本身没有错 |
| P-02 Draining 取消；P-03 相对 PATH | 原库在直接子进程退出后拒绝向旧 PGID 发信号是公开契约，主仓承诺更强却未补足；相对 PATH 按宿主 cwd 查找在 baseline 已存在 | 原接线缺陷。显式 Supervision、目标 cwd 查找已承接；默认 Process 规则未变 |
| T-02 / T-03 | 630,050 字节 Opening，baseline 的派生 raw Trace 仅保留 524,288，等长异尾 cut 相同；旧 openingEvidence / 持久原文仍完整。晚 Opening 改写 raw 前缀、旧 cut 拒绝、原区间改变。当前全字节及三个稳定性断言均通过 | 原 CUT-03 `042ae616a1` 引入，`e62f2a036` 修复 |
| O-02 / O-03 | baseline 无退出见证便成功清 lease，port 仍 live；同步退出留下幽灵 lease、零 completion。当前分别拒绝清账、一次 completion 且无 lease | 原 Host 接线缺陷已修。同步反例是受控端口，不冒称 OS 同步事件 |
| O-04 / O-06 / O-10 / O-11 | blocker 错误当空、共享 port 全量清理、无法退订、取消后仍可新开均在 baseline 源码存在。实跑还证实 baseline 空 runtime 取消会误杀 sibling，完全取消后仍 Spawn；当前保全 sibling、拒绝新 Spawn | 原有问题已修。O-11 不是 R3 新引入 |
| J-01 / J-02 | 旧 Bun COMMIT 入口故障后重开仍 locked；close 首次失败后仍允许 read，Unknown 后显式 close 不重试。当前封存业务、finalize、允许显式重试物理 close，并可立即追加 | 原首刀 `06488abf` 已存在，分别由 `93a7173` / `eef4825` 修复；J-02 不是 0.6.1 首次引入 |
| **O-12 / P1：真实 DevOps 终结订阅漏清理** | 两版 installRun 均绕过 Runtime.Complete 的 drain。canonical journal + 保存的真实订阅回调得到 Finished=true、drain=0、PTY/blocker 仍在；真实 Bun 两版均留下活进程 | 既有缺陷，早于修复基线。本轮修正真实接线；冷恢复无本地绑定时也拒绝假完成 |
| **P-04 / P2：Spool 首次创建后失败残留** | 两版均残留。baseline 还返回假成功；起点已返回 ExecutionFailed，但 collector 尚未取得路径。blame 追溯至拆分前 `c3c357562a` / `746b55907b` | 不是拆分新造，也不是我的修复新造；此前修复不完整。本轮创建者在异常时删除文件、重抛原错 |
| T-01 / O-01 剩余范围 | 旧原件权威、描述符桥、无本地绑定的持久 lease，以及 PTY leader 退出后后代仍存活，两版都有 | 仍未完成。完整后代监管已有失败实证，不能只写“尚未测试” |

## 二、我的修改引入的问题

| 项目 | 首次引入 / 证据种类 | 修复与状态 |
|---|---|---|
| **O-13 / P1：未知启动阻断后续批量清理** | `4b363f858` 增加 exitObservationFailures 后，CloseAll 在首项 Unknown 直接抛错。真实 Bun 耗尽 fd，恢复后开另一 ID、忽略 TERM 的 shell：baseline 能杀掉，起点留下活 shell | 本轮逐项尝试，最后仍报告首错。非作者确认健康 shell 已死、未知 ID 仍 active、无虚假 completion |
| **O-14 / P1：同 ID 覆盖继承旧未知记录** | 同一 `4b363f858`。Fork 仍可覆盖 active，但新增失败记录属于旧实例。真实 fd 故障后起点启动第二个同 ID shell，却无法升级 KILL；baseline 不受该新增记录干扰 | 本轮在 Spawn 前拒绝覆盖未结束 ID，不删除未知义务；见证退出后可复用。旧“活动 ID 覆盖成功”测试已纠正，并明确 process-execution-003 约束 |
| O-09 / P1：Cancel Unknown 阻止物理清理 | `4b363f858` 新 Cancel 回执分支先抛错；`bb9a811cf` 将 detach 改为本地 ClosePtys 后，原本独立的物理关闭阶段也受影响。中间源码归因；R3 有真实 COMMIT 前后反例 | `7d12e3462` 已修，保留错误并继续物理清理。本轮未重建中间提交 |
| O-08 / P2：Drain Unknown 跳过退订 | `4b363f858` 把 durable Drain 放在 DetachPendingObservers 的退订之前，新失败支路跳过 Dispose | `7d12e3462` 已分离收尾；属于我新增路径的缺陷，不是原 Ownership 状态机错误 |
| B-01 / P2：遗漏直接引用 | `4b363f858` 新增 DurableWorkHost 调用，未补 Mission/Lifecycle ProjectReference，真实 focused 构建不成立 | `bb9a811cf` 已补；本轮复核源码，没有重演中间构建 |
| O-05：新增持久确认路径不完整 | `4b363f858` 加入持久 lease 后，真实退出的 Ended Unknown 确认未完整接好 | `bb9a811cf` 已修。属于新增能力缺口；原版无同等持久确认承诺，不能编造“原版在该契约下本来正确” |
| Capture 缓冲区读空后的 EOF 推进 | 新能力 `490e1c0` 在 count=0 直接返回，缓冲区读空后需要 read(0) 推进 EOF；实际反例先转发双路 8 MiB 并读空，再终止转发进程 | `d6b45b0` 在 0.6.0 交付前修正并补 worker 反例。属新能力中间缺陷，不是默认旧 Process 的回退 |

本轮未发现当前 Trace、Journal、默认 Process 或 Ownership 纯状态机新增 P1/P2。此结论限于本文实际比较范围，不等于任意平台、任意驱动或完整宿主均已证明。

## 三、review 误判和证据缺口

| 项目 | 正确区分与纠正 |
|---|---|
| Trace“两个竞争的持久 owner” | 我的早期结论过强。实际是旧原件权威 + 新描述符索引，抽取不完整；未证明两个独立持久 writer 竞争。原报告已撤回 |
| E-01 重入 | accessor 拒绝不证明重入不可达。两版本 × Node/Bun 均实际到达 Proxy trap 并拒绝重入；新增有效证明，没有修复本来正确的库 guard |
| E-02 / **E-05 真实终结接线** | 端口模型不是 OS 证据，手调 Runtime.Complete 也不是生产订阅。我此前接受了过宽证明，漏掉 O-12。025 现捕获真实注册回调，补有效失败、无效/foreign、重复和晚到事件，另用真实 Bun 验证退出 |
| E-04 Bun 关闭 oracle | 我在 `93a7173` 用 statement.run 的 closed 错误证明 finalized，证据不足。原生反例证明 close 后 run 拒绝而 get 仍可执行；`eef4825` 改保存原生 get，R3 已做去 finalize 突变，本轮重做原生反例及双版 probe。不等于 0.6.1 业务代码没调用 finalize |
| R3 取消复用正控 | 混淆了完全取消 mailbox 和保留固定 DevOps 的可复用取消，掩盖原 O-11。R4 已分开；本轮双版证明 O-11 在原基线存在 |
| 平台和并发夹具 | `/var` 路径、Homebrew Node 注入 env、Journal 初始化/关闭同步，不能直接判为库参数转发或 append CAS 错误。已比 native spawn，worker ready 后仍并发 append |

本轮施工自身也有失败记录：首个 Fable 候选遗漏 PendingRun.fsi 字段且回调类型推断错误；check 拒绝新增嵌套控制；新测试曾误用关闭准入的 DrainOwnedWork、直接取不存在的静态导出。均已修正，失败候选未交付。晚到正控改用 AwaitObservedWork，确保回调真正进入，而非被 shutdown 入口提前丢弃。首次 fd 探针的 finally 遇 EMFILE，补齐探针释放后串行复验。Bun Capture 原生错误注入另有 child.stdout 取值失败，只取得 6 项业务结果，未取得第 7 项产品结果。

## 本轮修复边界与验收

O-12 统一为：检查有效 terminal 和 child/root → 认领当前 run 的一次 preparation → DevOps PTY drain → WorkRecord 物化 → 原有 JoinableCompletion / durable completion。失败释放临时认领，保留订阅和未完成 Source；父取消或 token 替换后，旧异步尾部不再交付。未向独立 Ownership/Work 注入 Host，未另造业务状态账。重投虽可完成业务，原 ownedWorkFailure 仍显式保留，后续等待 / detach 报告首次失败，不吞错。

冷恢复无 child runtime 不证明没有 PTY：现在读取同一持久 registry/root，非空则拒绝结算并保留 blocker，查询错误照常传播。这只阻止假完成，没有取得杀死未知旧进程的权限，也没有完成跨重启接管。

| 独立比较 | 实际结果 |
|---|---|
| 默认 Process，旧/新精确包，Node/Bun 各 10 场景 | 空输出、信号、exit 23、坏 cwd/命令、显式 env、双路 tail、二进制 stdin、独立 wait/KILL 观察一致 |
| 主仓进程，旧/新 Node/Bun | 普通输出、33 MiB stdin/EOF、启动失败、超时保持；TERM/INT 的错误 137 改为实际 143/130；128 MiB 完整输出修复见上表 |
| 真正历史 Trace journal | baseline 写入，当前冷开追加大 Opening 后再冷开；原 10 条前缀、旧 cut、原文保持，新 15 条 records 一致，Opening 重放零追加 |
| 库及独立消费者 | Trace/Journal 85/85；Ownership 57/57；Process/Capture/Supervision 69/69；安装包 Supervision/拆除增强 5/5；主 Trace 消费者 integration 1/1 |
| O-13 / O-14 修复后真实 fd 反例 | 两路均保留未知义务；不同 ID 健康 shell 清理成功；同 ID 第二次启动被拒且无 pid/ready 文件，受控端口额外断言第二次 Spawn=0 |
| O-12 独立矩阵 | held exit 前未物化/未交付；重入只 drain 一次；失败可重投；父取消等待退出；等待前后替换 token 均不误清新 PTY；foreign Failed 不消耗有效资格 |
| O-12 真实 Bun | PID 43786 就绪存活 → 注册回调后退出，lease/blocker 空。原生 handler 未计数，不用 physicalSignals=0 推断无信号；PID 已复核无残留 |
| 精确库分发 | 0.6.2 的 22 文件与库目录、隔离安装及主仓依赖逐字节一致；vendor SHA-256 `d8909bebb92b01e0cd0cfa28cfdc14d0d1d89f467b7afe5e94a99efd96b0038e`；本轮库及包未变 |

新增反例已进入编号 requirement：Process 001/003/009、managed-session-lifecycle 025。最终主仓分组、构建、check、打包和非作者冷恢复复验统一记在[第五批进度](REPAIR-PROGRESS-2026-10-15.md#regression-review-r5)，不能用上表局部结果冒充完整门禁。

历史 journal 兼容实证限于先有 Part/Terminal、后由新版追加 Opening。旧坏版本已经外存的大 / 晚 Opening raw records 和旧 cut 不保证自动升级；不能从本轮对照推导已经截掉的 raw 字节或被改写的前缀会自动恢复。

隔离探针在本机 `/private/tmp/r5-process-baseline.mDnXHt`、`/private/tmp/trace-journal-r5-20261016`、`/private/tmp/ownership-r5-compare.mjs`、`ownership-r5-o12-{matrix,cold-scope}.mjs`。关键观察保存在本文；临时目录可能清理，它们是独立补证，不替代仓库编号测试。

下一包仍按看板处理 T-01 原件迁移、O-01 完整后代监管、跨重启接管及跨进程每轮准入。真实 leader-exit TODO 仍失败，GAP-133 保持 PARTIAL。Linux、完整 RSS、脱组后代、断电及运行中宿主部署不在本轮已证范围。
