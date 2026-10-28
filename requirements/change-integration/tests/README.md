# change-integration 测试范围

规格在 `../WHAT.md`；本文件只说明各用例实际做什么、边界在哪，不新增规则。用例文件按 WHAT 编号对应。

## 夹具

- `observeRelayProgram` 执行真实 Change Program，注入受控 Git、Relay、持久化与资源端口，记录实际调用顺序。它能覆盖程序对这些端口结果的裁决，不覆盖外部端口自己的落盘、真实模型的独立评估，也不覆盖 Orchestrator 的完整接线。
- `recordFact` / `fold` 类用例验证事实投影与恢复裁决。重复折叠、重建 projection 或注入已有 claim 都不是真的进程崩溃与重启。
- Git adapter 用例调用实际适配器，以命令端口注入正常与失败结果。002 还通过实际 Host 的 `ForkManagerJob` 调用实际 Git adapter：cleanliness 检查失败或命令被拒时，新 Job 不追加 worktree 请求、不创建 worktree 或 Manager；clean 正向对照抵达 worktree 请求与创建边界。该夹具从空的 Orchestrator 历史开始，不覆盖启动期恢复既有义务，也不覆盖清理旧义务的副作用。另有真实临时 Git 仓库用例覆盖 status 失败返回错误、dirty target 不被推进。
- IntegrationGate 用例实际创建并释放锁；ManagerLoop 的 10,000 次 Continue 用例执行真实循环，只覆盖这些信号下的有限推进与资源回收，不是 10,000 次发布。

## 条款对应

| 条款 | 现有用例覆盖 | 尚未覆盖 |
| --- | --- | --- |
| [001] | 真实 Program 从候选到发布的调用顺序与继续循环；候选与已承认变基 pin 不符时拒绝 | 真实模型独立评估的全链闭合 |
| [002] | dirty/clean/error 分别返回；实际 Host 在 cleanliness 错误后拒绝新 Job，保持其持久请求、worktree、Manager 与目标 HEAD；ff 发布遇 dirty 或 status 失败时拒绝 | 用户请求入口的全部受理条件；启动期恢复与清理的无副作用 |
| [003] | 各事实独立投影；完整 claim 的恢复裁决；缺失字段拒绝；Published 追加失败不伪装成功 | 任意真实中断点都有足够材料可恢复 |
| [004] | rebase 与冲突处置不持锁；ff 受锁保护；发布完成后先释放锁再清理一次；实际锁与循环 | 锁内只重读 ref 与 CAS 的口径；durable claim/Published 写入当前仍在锁内执行（WHAT [004] 已标【程序待改】） |
| [005] | 发布不再携带生命周期评审资格；冲突事实留存、门外继续循环、worktree 清理错误传播 | 下一真实 incumbency 的端到端因果链；重复折叠不是重启 |
| [006] | 事实投影保留道路、终态与 worktree 身份；受控资源创建/采用/清理 | 新进程从中断产物恢复并沿用原 worktree |
| [007] | claim 三分支优先级；受控已发布重入不再 ff | 真实进程故障后自动完成这些分支 |
| [008] | 冻结符号引用、head 读取拒绝、错误分支与 expected head、严格快进判定 | 多进程共享同一入口的完整发布原子性 |
| [009] | Job/worktree 身份在事实投影中稳定 | 追加 charge、retirement、AuthorityRevision 与真实物理 session 的完整接线 |
| [011] | 无有效用例，只有 TODO | 真实 fork/resume/join 发给 provider 的内容遵守 horizon |
| [013] | target 前移或 CAS miss 时真实 Program 释放锁、rebase 并继续；新事实替代旧绑定 | 这些裁决在真实进程崩溃后的重放 |
| [014] | 真实 Program 在 target 与已承认变基证据不符时拒绝发布、拒绝 unmerged files | 同类检查贯穿全部生产路径 |
| [015] | 真实 Program 在观察到的 target 与已承认证据不符时 rebase 并继续到下一 tenure，含 fresh 正向控制 | 真实 Engineer/DevOps 修改后旧测试证据失效并重跑受影响验证 |
| [017] | 无有效用例，只有 TODO | 两条真实道路合流后对最终工作树独立验证 |

`PublishClaimed` 的 Surface 只接受明确给出的完整绑定材料，不从 Job 或候选记录猜字段；相关用例覆盖这条接缝不补造事实，不覆盖生产事件解码兼容。

VerdictMailbox 的 FIFO、空闲 sentinel、中断不消费最终结果、已就绪结果先于中断这四类用例在 delegation 包的 tests/014、tests/015，覆盖委托结果接收，不覆盖发布。

## 运行

局部诊断：`WXS_TIER_INTEGRATION=1 node --test requirements/change-integration/tests/*.test.mjs`。正式执行走 requirement runner，保留 TODO 与失败。本目录文件全部跑过不等于全部合同已满足。
