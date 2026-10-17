# T-01 原件接管与消费者切换

输入：主仓 `84ba23b2d`，库 `b6984d9 / 0.6.2`，两仓起点干净。依据 [剩余计划](REMAINING-REPAIRS-2026-10-16.md#4-cut-03-r6分四个闭环迁移原件)、semantic-trace WHAT-001/002/003/005/006/007/008/009/010 及 D-02 对账。状态以看板为准。R12-U1 按用户决定暂存，不扩大调查。

## 已确认的缺口

生产 fold 只把 blob 引用和摘要装进库；原文仍由 Materialization/WorkRecord 重新读 blob，临时创建另一个 Trace 再捕获一遍。`xrun/xmsg` 由语义游标生成，每个 part 都被补成 Completed，不能代表实际 Host 的运行和完成。现有事实中 ProviderRun 可以缺失，也没有统一的 MessageFinished，因此不能硬塞进旧库的完整消息协议。

## 本包契约

- 库增加独立的已确认材料观察：身份、原角色、语义种类、可缺的真实 ProviderRun、工具名、连续字节分片。材料完整只表示声明的分片齐备，不表示 Host 消息或运行完成；不合成 run/message 或 Completed。正文、推理、工具调用/结果、明确省略材料分别保留。旧事实未记录的 origin/物理地址不补造，typed retry 继续在提交前排除。
- Terminal 由显式 ProviderRun 绑定完整材料与已验证的固定前沿；不拿最后助手散文替换本次正式终结。相同事实重放幂等，身份或字节冲突零追加。库不认识 Host、Journal 或主仓。
- 统一 EventStore 继续保存既有事实与 payload。JournalIntegration 将当前 envelope 的已校验 payload 交给 fold；缺失、摘要错误或冲突拒绝，不推进公开 trace。库作为唯一原件内存 owner，冷 replay 从同一载体还原；不新增旁挂数据库或长期双写。
- 保留已有语义游标、generation、Opening、覆盖范围和 Terminal 前沿。新的原件 trace 使用明确的新 identity；旧描述符 raw cut 不冒充新原件 cut。既有 journal 事实的词汇和含义不变；不得添加旧事实没有的来源/执行断言。真正的事实协议迁移仍须遵守 durable-events-026。

## 施工顺序与验收

1. 固定库协议与生产原文反例，先观察失败；增加真实材料、恢复、切片、冲突和 Terminal 证明。
2. 接通 payload-aware fold，完成实际 Host 捕获、并发、typed retry 与冷 journal 恢复；不用描述符满足原件断言。
3. 将 Materialization、WorkRecord 等原件读取消费切到同一 owner；删除描述符追加与临时重捕获。保留实际需要的兼容游标索引，不保留第二份正文权威。
4. 跑对应 semantic-trace、WorkRecord、prefix、持久化及 Host 链；库新版本、精确包隔离消费、主仓 Fable 与独立复审通过后记验收。Fission/Invocation 的独立未实现产品能力不借存储迁移关闭。

## 进度与证据

1. **库协议已完成**：`a636cbc / 0.6.3` 增加真实材料与明确 Terminal 绑定，Node/Bun 各 67/67、拆除构建 2/2、安装包 5/5。已正常推送既有修复分支；主仓锁定 `vendor/wanxiangli-0.6.3.tgz`，SHA-256 `42eb5ca5aeb9b613ea5400ab43b4de835605e7b74d232f31db4f8e21b04e6162`，旧包由 Git 历史保留。
2. **生产接线已完成**：JournalIntegration 在同一 envelope 中检查 payload 闭包、严格 UTF-8、引用和摘要，再交同步 fold。缺少 ProviderRun 不补造；同 session 保留异步串行队列。新 raw identity 为 `xtrace-material-v2/<session>`，语义游标、generation 和既有范围继续有效。
3. **消费者及旧路径已切换**：Materialization、WorkRecord、Y/prefix 的统一取材入口、Strength 恢复从同一 Trace 原件读取；Casebook/Invocation 通过现有 WorkRecord 路径消费。已删除描述符追加、临时 Trace 重捕获、伪造每 part Completed 和假的 lifecycle→Terminal 便利入口。Main 的元数据/游标索引仍保留，EventStore 仍是唯一持久载体，不保留第二份正文权威。
4. **本包验收完成**：生产编译 generation 547（1536 items、174 surfaces、838 modules），文档/夹具元数据刷新为 548，收尾 549 同包摘要复核通过，格式及静态门禁通过。两个不重叠相关组共 **1290 项 / 1207 pass / 0 普通 fail / 34 skip / 49 TODO**。精确主包隔离安装、Node/Bun 包外捕获→真实 SIGKILL→独立冷恢复通过；看板按本包范围进入 DONE。

## 问题归因与修正

| 编号 | 归因 | 已修正的边界 |
|---|---|---|
| T01-01 / P1 | 原拆分接线缺口 | 库原来只有 ref/digest 描述符，主仓另读原文并临时重捕获。现由库保管已校验的真实材料，消费者共用同一原件；没有运行/消息完成事实时不补 Completed |
| T01-02 / P1 | 既有捕获问题 | 同身份改写被当成重放、批内重复重复分配游标、同 Host 工具部件的调用/结果互相吞掉。现比较角色/种类/运行/调用/工具名/字节，冲突预检先于写入，每步重读已提交索引；调用与结果分别保存 |
| T01-03 / P2 | 既有缓存问题 | Emit 的值表达式每次调用都新建 WeakMap，缓存一直 miss。现为模块级一次初始化；固定五次读取新增 WeakMap 为 0，不用耗时猜性能 |
| T01-N1 / P1 | 本轮候选引入 | bridge 把库返回的 Replayed record 再追加，造成 cursor gap；544 实证失败。现拒绝新语义游标复用已有 provenance，bridge 只接 Appended，旧投影及分支可独立冷建；545 非作者原反例转绿 |
| T01-N2 / P1 | 本轮候选的历史兼容遗漏 | 工具 facet 后缀改变物理路径，旧事实缺 HostToolPartId 时可能重复。现只在精确旧物理路径、同 kind 和完整已知语义上识别重放，保持旧缺失字段，不回填；只补实际缺失的另一 facet。546 非作者真实 journal/cold 用例通过 |
| T01-E1 | 验收夹具及 review 误判 | 纯 fold 夹具没有原文、旧 lifecycle 便利入口伪造 run、placeholder 被当成有效 Terminal；现显式提供材料或使用真实非 Trace 事实，placeholder 只证明闭包过滤并被 Trace 拒绝。Body 被误计入证据视图、null/undefined、SIGKILL/exitCode 的断言均按原契约修正，未改产品迁就测试 |
| T01-E2 | 验收环境 | npm 默认缓存和 ps 被沙箱拒绝，不记产品失败；临时缓存及进程核验权限下实际重跑。旧 context-compression-025 使用真实用户配置路径，现复用已有临时插件夹具。新 union 漏配分支、控制嵌套及格式失败均在交付前收口 |

已确认的 Host 身份冲突在捕获预检处零写入；库单条观察冲突零追加。**坏 durable fact 的拒绝不是物理日志零追加**：既有 Journal 契约会保存坏事实和 cut-tail，报告 `journal-semantic-cut` 并实际 SIGKILL；冷恢复的已接受 Trace 前缀不变。缺 ProviderRun 的 JS 输入则在调用写入前拒绝。这三个边界分别验收。

## 已执行证据

- 库：`t01-library-protocol-red.log`（新协议旧版失败），`t01-library-build-all.log`（Fable），`t01-library-trace-{node,bun}-final.log`（各 67/67），`t01-library-isolated-build.log`（2/2），`t01-library-package-delivery.log`（5/5，Node/Bun SQLite SIGKILL、恢复、拆除、strict TypeScript）。其余六入口只是 esbuild 注释路径/PID 不同，Acorn token 一致，见 `t01-library-unchanged-modules.log`。
- 正式原文反例：`t01-main-original-red.log` → `t01-main-original-first.log`。真实 Host 的 BOM/CRLF/NUL/大 Unicode 原文、缺 run 推理和冷 replay 记录一致；完整工具大正文、明确省略种类由 005/007 实际 owner 断言。
- 捕获缺口：`t01-host-conflict-red.log`、`t01-host-facets-red.log`；修后 002/003 覆盖真实工具 running→completed、批内重复、同身份异字节/角色、工具别名批内冲突及同 session 并发。
- 非作者：`r6c_protocol_review`、`r6c_observation_review` 未编辑或构建。重复 identity 的 `t01-duplicate-identity-red.log` → `t01-duplicate-identity-green-545.log`；缓存 `t01-cache-observer-green-545.log`（旧版仅静态证据）；旧物理路径 `t01-independent-legacy-physical-546-green.log`；547 缺 run、摘要/缺 payload 和旧伪造入口断言为 `t01-review-{007,013}-547.log`，4/4。null 的旧失败属于 oracle 误判，不归给生产。
- 扩大组：`TMPDIR=/private/tmp node --test requirements/durable-events/tests/*.test.mjs requirements/obligation-ledger/tests/*.test.mjs requirements/speculative-investigation/tests/*.test.mjs requirements/context-compression/tests/*.test.mjs requirements/knowledge-reuse/tests/*.test.mjs requirements/host-boundary/tests/029.test.mjs`，1066 / 1001 pass / 0 普通 fail / 33 skip / 32 TODO，`t01-main-durable-consumers-delivery.log`。前两次的 ps/旧 placeholder 失败原样保留在 `t01-main-{durable,consumers}-expanded.log`、`t01-main-durable-consumers-final.log`，不覆盖成通过。
- 两项单独启用 integration tier：005 独立 F# 消费者正反编译 1/1，`t01-main-independent-consumer.log`；obligation-ledger-004 真实非空 Trace/Terminal checkpoint 保留 2/2，`t01-main-checkpoint-integration.log`。不与默认组重复相加。
- Trace/WorkRecord/prefix 完整组：`TMPDIR=/private/tmp node --test requirements/semantic-trace/tests/*.test.mjs requirements/work-record/tests/*.test.mjs requirements/prefix-stability/tests/*.test.mjs`，**224 / 206 pass / 0 普通 fail / 1 skip / 17 TODO**，`t01-main-trace-delivery.log`。其余广组为上述 1066 项；TODO 的预期失败输出没有计入 pass，也没有转成 skip。最后仅整理缩进的 context-025 临时夹具再验 2 pass / 1 TODO，`t01-main-config-fixture-delivery.log`。
- 主仓 `node scripts/build.mjs`、`npm run format:check`、`node scripts/check.mjs`、`git diff --check` 通过，日志 `t01-main-build-{fifth,delivery}.log`、`t01-main-format-check-delivery.log`、`t01-main-check-delivery.log`。shard-edge-audit 原有 report-only 提示不冒称新错误。一次包检查正确拒绝了测试输入更新后未刷新的 manifest，见 `t01-main-package.log`；随后重新构建，未放宽 freshness 门禁。
- `npm_config_cache=/private/tmp/p1-npm-cache node scripts/verify-package.mjs`：**2075 members / 13,291,249 bytes**，SHA-256 `918cf931babe942b2d9f1306d1e1cbc99c5ee98c6add32d8cc0ae1dcb19e0c7b`，generation 548，`t01-main-package-delivery.log`。实际 npm pack、闭包检查、隔离 npm ci、默认入口通过。
- 收尾文档与测试缩进刷新后，generation 549 再执行同一验证，字节数、成员数与摘要完全相同，`t01-main-package-final-recheck.log`；没有以新元数据冒充新的行为测试或替换已验证包。
- 同一主包的额外仓外证明：`npm_config_cache=/private/tmp/p1-npm-cache node /private/tmp/t01-packed-originals-proof.mjs <该 tgz>`，`t01-main-packed-originals.log`。不借主仓/兄弟源码或主仓 node_modules；Node、Bun 分别从已安装包走实际捕获入口，确认大 Unicode/BOM/CRLF/NUL、缺 run 推理、工具两个 facet、typed retry 排除、晚 Opening 和明确 Terminal，提交屏障后 SIGKILL 本测试进程。另一新进程恢复同样的 11 条 records、head、语义视图与渲染，并精确核对 Terminal 的原 part 前沿；两种运行时均通过。这是包外真实载体证明，不是在线 provider 或用户 Host 部署证明。

以上日志均在 `/private/tmp/`。库已提交 `a636cbc` 并正常推送 `codex/repair-four-modules-20261015`；主仓随本次原子提交正常推送 master，不强推。自动审批曾拒绝库的组合推送，随后只读核实 gh 账号、远端 owner 与 admin/push 权限，才获准按原分支正常快进推送；未绕过拒绝或改到另一目的地。已验证、已提交/推送、已部署分别报告。

## 保留边界

旧 journal 仍按原事实形状与内嵌 payload 重放；没有离线改写或第二条长期写入权威。只保存描述符而没有可验证原文的 raw history 不升级成原件；旧 `xtrace-projection` raw cut 不能混入新的 `xtrace-material-v2`，外部私存的坏/截短材料也不能凭空恢复。旧事实若已经把同一 provenance 重复写成不同语义游标，派生 owner 明确拒绝，不生成不可恢复的 raw 前缀。

Opening 领域字段、语义坐标和 BlogFrame 仍是主仓各自的不可变事实投影；它们不回写原件。已确认 Host part 与 Terminal 的正文均由同一个 Trace 对象读取，库没有接收 Host/Journal 服务，也没有接管业务范围裁决。

T-01 只关闭已确认原件归属、实际取材和旧路径退出。semantic-trace 008/009/011/012 的整体产品证明、GAP-101/108/109/111 等原有 TODO、Process 的 GAP-091/092/Bun 边界、O-01 脱组进程仍分别保留。R12-U1 按用户决定保持 BACKLOG，未调查、未声称修复。本包没有部署用户正在运行的 Host。
