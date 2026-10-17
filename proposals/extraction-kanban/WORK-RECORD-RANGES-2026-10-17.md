# GAP-111：跨 invocation 的 WorkRecord 取材

输入：主仓 `53cba28de`，库 `10bbb32 / 0.6.4`；工作区干净。按[当前计划](REMAINING-REPAIRS-2026-10-16.md#gap111-next)认领 CUT-06-R1，状态只在[看板](BOARD.md#source-audit-repairs)维护。R12-U1 按用户要求继续暂缓。

## 原因与契约

`Materialize.fs` 的 bounded 路径采用所有相交 frame，再用全会话 RecordCoverage 决定 raw 起点。跨起点 frame 会带入旧工作；只过滤摘要仍沿用其 coverage，又会遗漏本轮原文。既有 work-record-004 TODO 已在 `c3689035c` 的真实 journal 反例中失败，本包不是 T-01 新引入的问题。

依据 work-record-002/003/004/005/007/010/014/016 和 context-compression-029：frame 作为不可分割的已确认摘要保留。只有完整落在本次工作范围内、且没有跳过本轮未表示原件的连续摘要前缀，才可进入 bounded Chronicle。遇到跨界材料后，从已采用前缀的末端读取同一 Trace 的 bounded 原件，沿用既有 WorkRecord 工具材料过滤和明确 Terminal 绑定。没有采用该 frame 就没有获得它的展示覆盖证明；durable RecordCoverage、PrefixCoverage 和历史 frame 不回写。

生产同步消费者是 SyncDelegate 的实际完成路径，异步消费者是 Fork 的实际完成及 Join。旧 Inspector 工具和角色已退役，本包不重新开放；验证内部 Engineer 同步调用及异步 fork/resume/join，不能把历史名称当成仍有效的入口。

## 施工与验收

1. 固定原 TODO，补完整内含、跨起点、跨终点、跨两端、多 frame、工具材料、正式终结与空区间的真实 journal 矩阵，同时检查旧内容隔离和当前内容完整。
2. 用现有 opaque harness 的 scoped JournalHandle 在实际 writer 中提交观察，回调返回或抛错后立即使借用失效，不释放消费者持有的 writer。同步和异步运行时仍调用生产物化器；不在 JS 复制取材公式，也不导出内部 journal 或 projection。
3. 修正 bounded 的 frame 选择及实际采用材料的覆盖，删除旧 overlap/全局 clamp 路径。现有全会话读取和 prefix 物化各自保留原契约。
4. 验证冷重开、真实消费者、后续追加及坏 blob 的拒绝；定向跑 WorkRecord/Trace/Compression/Prefix/Delegation/Horizon 与静态门禁，最终固定候选后非作者复验。
5. 更新确切测试计数、原始红绿日志、包身份、归因和剩余范围；本包不关闭 GAP-101/108/109、CUT-06 整卡或 O-01。

## 进度与证据

### 生产修正与归因

| 项目 | 归因、修正和证据 |
|---|---|
| G111-01：相交摘要越界与排除后漏原文 | 本包输入已有缺陷。`framePrefixInRange` 跳过完全早于本轮的 frame，只接纳完整内含的连续前缀；首个跨界 frame 之后不能借后续摘要跳过 raw。suffix 起点由实际采用的最后 frame 证明，不改写任何持久 coverage |
| G111-02：尾部工具使正式陈述消失 | 本包输入已有缺陷，非作者指出后补两个正式反例，原562均失败。bounded raw 起点同时保留本范围最后助手 text；Terminal 去重只看实际 raw，而非未展示的整个 workRange。无/有匹配 Terminal 的原反例均转绿 |
| 验收接缝 | JournalHandle 的 internal borrowed constructor 只创建 scoped capability，release 为 no-op；两类已有 harness 在 finally 使其失效，不暴露 AgentJournal/projection。数据仍经过实际 capture、ContextFact、fold、BlobWriter 和生产物化器。新增 squash 入口只是 plain payload 到已有正式 fact 的编码，无第二套选择算法 |
| 后续表示冻结 | 真实 squash 合并旧/本/后轮 frame 后，固定范围当前派生视图安全回到原件，冷恢复一致；表示可从 Chronicle+Recent 改为纯 Recent。已完成WorkRecord的持久冻结与实际消费仍需另验，不把当前派生视图变化当作已发布完成记录被改写的证据，也不关闭整个GAP-111/109 |

### 先红与夹具纠正

- `gap111-bounded-red-561.log`：原生产算法的004为 **14 / 6 pass / 8 fail**；跨起点、跨终点、跨两端、跨界后内含和后续追加均有实际失败。更早 `gap111-bounded-red-53cba28de.log` 有9失败，其中1条把 reasoning 的既有裸文本渲染错认成 assistant 行；只修断言后保留真正的8条红例。
- `gap111-real-consumers-red-562.log`：实际 SyncDelegate 完成和 Fork resume/Join 均混入 `PRIOR_DETAIL`，**1 pass / 2 fail / 1 TODO**。`gap111-statement-red-562.log`：已完全覆盖正文后又有工具时，无/有匹配Terminal均只剩Chronicle，**0/2**。
- 早期另起writer再Refresh的夹具出现暖冷Terminal前沿不同，`gap111-sync-fixture-diagnostic.log`。它扩大成了未建立所需因果顺序的跨writer实验，未作为本包验收依据，也未据此修改durable协议；最终改为生产实际writer提交，冷读才另起实例。该观察不证明任意跨writer捕获/终结排序已受本包承接。
- 早期fork resume错误地传入了新calling而非空调用字段，导致未生成预期prompt的等待；原 `gap111-real-consumers-red-561.log` 被中断保留。Join按既有TOML指令注释编码换行，不直接承诺wire含原始CRLF；最终验证canonical冷读保留原值，Join对应其实际注释投影。
- 原004复用终态、011 Terminal正控的frame同时覆盖caller charge和assistant，而ForRun按本轮前已有WHAT-007只允许assistant范围。分别拆为caller/current两条实际frame，继续保留Chronicle+Terminal正控；非作者确认未通过放宽实现迁就测试。
- 最初损坏夹具使用不存在的零digest，在存储层先被 `MissingPayload` 拒绝，`gap111-core-candidate-563.log` 的两条失败未触达物化器。修为两个真实已验证payload之间的错绑：selected frame拒绝，excluded crossing frame不阻止有确切原件的bounded视图。另一次Bun不支持Node的嵌套 `context.test`，`gap111-core-final-bun-563.log`；已改回顶层TODO，不改生产语义。
- 复审表述校正：squash独立探针只有固定range和真实frame/squash，没有写Terminal或完成WorkRecord冻结事实。最初把它称为“completed range”的字节冻结缺陷过强；非作者已确认校正。当前证据证明固定range的派生表示变化，完成记录的持久冻结与真实消费另留证据项，不新增一个未经证明的产品错误。

### 验收证据

生产构建 `node scripts/build.mjs`：generation **563**，174 surfaces / 838 modules，`gap111-build-candidate.log`。格式 `gap111-format-check.log`、静态 `gap111-check-candidate.log` 均exit0；未增加门禁例外。以下日志均在 `/private/tmp/`。

| 不重叠默认组 | total / pass / 普通fail / skip / TODO | 日志 |
|---|---|---|
| WorkRecord、semantic-trace、prefix-stability | 318 / 300 / 0 / 1 / 17 | `gap111-trace-record-prefix-delivery-564.log`；最终顶层TODO与011夹具已复核 |
| context-compression、delegation、participant-horizon | 598 / 549 / 0 / 6 / 43 | `gap111-consumer-compression-563.log` |
| durable-events、obligation-ledger | 283 / 247 / 0 / 27 / 9 | `gap111-durable-coverage-563.log` |
| 合计 | **1199 / 1096 / 0 / 34 / 69** | TODO/skip不计通过 |

扩大组第一次 `gap111-trace-record-prefix-563.log` 有1条011正控失败，按上方范围前提修正后转绿，保留原失败日志。核心004/010/011最终在Node、Bun均 **30 / 28 pass / 0 fail / 2 TODO**：`gap111-core-delivery-{node,bun}.log`；这是上述组的子集，不再相加。

非作者 `gap111_review` 未修改仓库或重建产物。在固定563做 source review、自有Node/Bun探针及独立新进程冷恢复，`gap111-independent-{node,bun}.log`；正式004/010当时各 **24 / 23 pass / 0 fail / 1 TODO**，`gap111-independent-requirements-{node,bun}.log`。随后只读复查004/011正控的范围拆分与新增历史冻结TODO，确认不构成产品回退。两个运行时的真实squash表示变化另记 `gap111-squash-boundary-{node,bun}.log`，不把未承接的字节冻结记为通过。

两项单独启用 integration tier：semantic-trace-005 独立 F# 消费者正反编译 **1/1**，`gap111-independent-compile.log`；obligation-ledger-004 非空 Trace/Terminal checkpoint 保留 **1/1**，`gap111-checkpoint-integration.log`。不与默认组重复相加。

文档/测试元数据刷新至564，生产行为与563相同，`gap111-build-delivery.log`。实际 `npm_config_cache=/private/tmp/p1-npm-cache node scripts/verify-package.mjs` 通过，`gap111-package-delivery.log`：**2075 members / 13,323,845 bytes**，SHA-256 **`ce6e084c2f0f6038edd5ad483bdbc62cd86e8ae0f5dd552caa3676335a495be1`**。真实 pack、完整闭包核对、隔离安装与默认入口消费均通过；库仍为原0.6.4，未改版本、锁或vendor。

同一确切tarball由非作者另在隔离目录按精确lock离线 `npm ci` 安装117依赖；删除顶层wanxiangli，实际使用包内bundled0.6.4。安装文件与tarball逐字节一致，依赖及链接均在隔离目录，没有借主仓/兄弟仓runtime。Node/Bun包外探针均exit0，覆盖borrowed-owner、跨界范围、正式陈述和独立新进程冷恢复。`gap111-package-review-{ci,integrity,isolation,node,bun,cleanup}.log`；首次直接离线install缺packument的 `gap111-package-review-install.log` 保留，不记产品失败。安装及临时Git目录已清理。

CUT-06-R1按上述限定范围完成，随本原子提交正常推送master；不强推，不部署用户Host。`gap111-check-delivery.log`、`gap111-format-delivery.log`与diff检查通过。最后仅刷新文档/测试元数据并核对同一包身份，不冒称又一轮全套行为验收。

<a id="remaining-boundaries"></a>
## 剩余边界

- 本包只关闭跨界摘要越界、排除后漏原文、正式陈述被尾部工具遮蔽的已确认反例。完整且连续的摘要仍可复用；跨界后原件来自同一Trace，EventStore继续是唯一持久载体，durable RecordCoverage/PrefixCoverage不回写。
- GAP-111保持PARTIAL。后轮squash使固定range的当前派生表示从摘要变为原文，内容范围正确；这尚不能证明已发布的完成记录被改写。CUT-06-R2先核对已有完成WorkRecord的持久表示及实际下游读取，再补冻结证据。不为此向Trace添加Blogger状态，不创建任意range的第二份写入权威。
- 实际同步Engineer和Fork resume/Join已验证；退役Inspector不开放，真实provider、完整Opening/Fission及所有消费者的生命周期证明仍归GAP-101/109。scope借用是已登记验收边界，不是新的产品业务入口或持久owner。
- 独立writer未建立因果顺序的捕获/终结实验保留原日志，不据此宣称跨writer排序已验收。旧坏历史也不自动改写。R12-U1继续按用户要求暂缓，本包未调查物理Unknown、未部署用户Host。
