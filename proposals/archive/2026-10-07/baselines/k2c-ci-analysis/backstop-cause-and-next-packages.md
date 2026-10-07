# K2C 官方 300 秒截断：有限因果分析与后续工作包

此稿只分析 run37631505588 / job112826675909、source `898e3f7ae31c16b130cc922b0a34128a509119aa`。未修改仓库、运行项目测试、重跑 CI 或改变预算。原收据与八份原日志在 `/private/tmp/vibe-fs-ci898e3f7ae-37631505588/`；本目录只有可重放分析脚本、派生数据和建议。

## 结论先说

现有证据指向两槽几乎持续饱和，最后仍在做实际 fixture 工作。没有发现长时间调度空洞；也没有证据证明最后一个测试已经挂死。但仅凭已完成文件，不能证明固定两个文件并发在任何重新调度下都必然超过 300 秒。必须保留这两个边界，不能把“容量很紧”写成已证不可能，也不能把一次截断写成某个 leaf hang。

最小后续步骤是先给原 016 文件一次明确冻结、相同 tier 和原预算的局部终态观察，随后优先消除其完整工具 archive 的大 Buffer 汇集/复制。这个优化有明确源码和成本依据，但尚不能承诺足以使全部 824 文件在原 300 秒内结束。

## 计算口径与来源

1. ZIP 内八份日志是四个逻辑阶段 `format-check/check/build/unit` 的 timestamp/latest 两套逐字节相同副本。`phase-log-dedup.json` 已核四对相等。只分析一份 unit，未重复累计 latest，也未把 format/build 时间加进 unit。
2. official job 明确两个并发文件、每文件 `run(...concurrency:1)`；release pipeline 处于 unit stage，未进入 integration/e2e/package。单位均为 ms 或明确换算的秒，不使用 CPU 计数。
3. 外层 `file-start/file-drained.elapsedMs` 全由同一个 supervisor 的 `performance.now()-startedAt` 产生（原源码 supervise-node-test.mjs 421–433 附近）。这一个时钟可相减、积分。
4. worker `interval.wallMs` 是各自 PID 的 pre-import→exit 单调时钟差。只有已验证 complete 的 809 行可相加；它与外层 wall 是两套测量口径，不能相加，更不能加入 `cpuUserMicros/cpuSystemMicros`。
5. 两个 invalid cost 文件仍有正常外层 drained 生命周期，因此 811 个完成文件的外层 wall 可完整计算。invalid 成本没有被升级为有效 worker wall 或行为 verdict。
6. 最后实际 file-start 位于 `299988.812635ms`。把它作为完全有原始记录的积分截止 H，不假定定时器回调恰在整 `300000ms` 被执行。新的 work-record/006 在 H 尚无可观察持续时间，记作 0 下界，不当作实际耗时为 0。
7. source 的 Git tree 与官方 actual merge tree 完全相等，均为 `a0dbd1f0b6cbfdb4df00ce88c186e8aaa8c462f6`；本次源码分析不是借用另一个 head 的实现。

## 已完成工作、峰值与条件下界

| 测量 | 结果 | 可证明的范围 |
| --- | ---: | --- |
| 完成文件数 | 811 | 外层 stream 已 drained；不是 leaf pass 数 |
| 811 文件外层 wall 累计 | 580370.664ms = 580.371s | 当前实际调度下的文件槽位占用总量 |
| 两槽条件下界 `sum/2` | 290185.332ms = 290.185s | 若保持这些已测文件服务时间，任何两槽排程至少需要此时间；小于 300s |
| 最长完成文件外层 wall | 26690.331ms | requirement-grounding/007，已完成而非挂死 |
| 有效 complete worker 数 | 809 | 另两个 invalid 不纳入 worker wall |
| 809 worker wall 累计 | 543403.794ms | pre-import→exit；不是 CPU 时间 |
| 有效 worker 两槽条件下界 | 271701.897ms | 不是完整全量工作下界 |
| 同 809 文件外层减 worker wall | 34183.652ms，平均 42.254ms/文件 | 混合进程/包装/消息/排空观察开销，不能直接宣称是 CPU 或单一根因 |
| 完成文件 median/p90/p95 | 230.046 / 1754.041 / 2522.310ms | 文件 wall 分布；不是测试叶子分布 |
| 最长完成 worker wall | 26621.572ms | 同一 requirement-grounding/007 |

前十五个较长完成文件的外层累计为 180469.462ms。前五个为：

- requirement-grounding/007：26690.331ms；
- verification-system/006：24434.123ms；
- delegation/025：20133.869ms；
- context-compression/024：15966.388ms；
- host-boundary/032：14334.880ms。

这些完成文件并未把两槽下界推过 300 秒。因此“单看已经完成的 work 就必然无法通过 300 秒”是不成立的推断。上述下界还以本次已测服务时间不变为条件；机器负载、fixture 内部工作或实现改变后，服务时间并非不可变常数。

若再纳入已实际消耗的 active 部分，下界已知槽位工作量为 `580370.664 + 17441.570 = 597812.234ms`。在理想的两槽 600000 slot-ms 总容量中，只有 `2187.766 slot-ms` 余量可供所有未观测的剩余工作，且尚未计实际空洞。剩余 016 内容、刚起的 work-record/006 与另外十一文件都没有终态服务时间，因此这一很紧的余量仍只是阈值，不是剩余耗时已经测出。

## 两槽占用与空洞

到 H 的完整 sweep 结果：

| 同时占用槽数 | 持续时间 |
| --- | ---: |
| 0 | 39.853ms（启动前） |
| 1 | 2085.685ms（累计） |
| 2 | 297863.275ms |

两槽可用容量为 `2H=599977.625ms`；实际可观察占用 `597812.234ms`；累计 idle-slot 时间 `2165.391ms`，利用率 `99.6391%`。除最初 39.853ms 外，最长连续单槽空洞仅 `9.752ms`。

文件区间可合法染成两条不重叠 lane：429/384 个启动文件，占用分别 298858.181/298954.053ms，空洞分别 1130.631/1034.760ms。两个 lane 的标签只是合法区间划分，不是原生 worker PID 身份；原日志并未给两个异步循环发独立身份。本算法保留两处可分配多个空槽的歧义，绝不把任意染色写成真实所有权证据。

这足以排除“driver 空闲很久才起下一文件”作为本次主要容量损失。重新排序可以改善长尾开始时机，不能消除 580 秒的已观察槽位工作量；单靠填空洞理想也只回收约 1.083 秒墙钟。

## 最后 active 016 到底在做什么

原 supervisor 最后可见 test:pass 与 lastStart 仍指向 `node-tool-candidate-tests.mjs:337` 的“actual npm CLI 添加未选择文件后不能发布原 bundle 身份”。但同一个 worker18529 的直接 `beforeEach` 输出在原 unit9508 已记录下一 leaf：

`a held actual npm probe preserves Error cancellation and drains its process group`

`verdict-transport.mjs` 的 beforeEach 先写该记录，再 `setImmediate`，所以这条记录是当前实际 leaf 已开始的证据，不是以文件名猜归因。两个通道的记录时机不同，当前证据没有证明 attribution 错误，也不需要为此修改 reporter。

原 unit9509 的最后阶段是：

`node-tools:archive-enter`，worker18529 本地 `elapsedMs=16790.683637`，目标 `/tmp/verification-node-tools-fixture-SzCrLN/tools.tar`。

到 backstop 没有这一轮的 `archive-written`，也没有之后的 `prepare-enter`。因此本次执行尚未进入 fs.watch marker 等待或取消后的 process-group drain。不能把它写成“held probe barrier 等不到 marker 挂死”。原 job 的相同阶段文字只在错误尾部转储出现，其转储 UTC 时间不是原阶段执行时间，也不能和 worker clock 混算最后等待长度。

源码顺序是：

1. 改写真实复制的 npm CLI，使其起真实子进程并等待子进程 stdout，再原子 rename 写 marker。
2. **先** await `archiveTools(root)`；该函数原 tar.create 读取完整 toolchain，收集 chunks，Buffer.concat，writeFileSync，计算原 archive SHA。
3. archive 完成后才建立 root fs.watch，调用原 `prepareVerificationNodeTools`，等待 `started.promise` 与 preparing 拒绝/误发布分支的 race。
4. 实际 marker 到达后才 `controller.abort(Error)`，等待原 preparing settlement，并验证两个真实 PID 都 ESRCH、没有 fallback、候选根清空。
5. finally 关闭 watcher、再次保持取消语义、清理本 fixture 实际已观察的组并等待 settlement。

这个 barrier 没有新增物理 sleep 或 leaf-timeout；suite 监督负责实际未完成时终止。若将来有限 scope 真正卡到 marker 或 drain，要按该实际因果边界另立回归，不能从本轮截断预先认定。

同 worker 到截止前已完成 12 对 archive-enter/archive-written：五份约 6KB 的受控工具 fixture、七份约 137MB 的真实 Node/npm fixture，总写出 `962810368 bytes`；其同一 worker clock 中打包阶段累计 `5425.182ms`。最后三个真实打包分别约 1014.811、915.128、1000.844ms。最后一轮未完成，耗时不能填 0，也不能补成上一次耗时。

## 剩余集合与当前未决项

精确相等的 source/merge tree 中可发现 824 个本 tier 文件，原始外层只启动 813 个。差集恰为 work-record/007–017 共十一文件，见 `unstarted-source-set.json`。不是缺少测试注册，也不能把这些未开始文件默认为通过。

仍未知：016 所有后续真实工具/npm/SDK fixture 的终态总成本、work-record/006–017 的本次实际完整成本、具体机器资源与压力在文件服务时间中的贡献。因此目前能说“容量接近上限且实际 fixture 仍在推进”，不能认定某个 leaf hang 或给出完整全量成本。

## 可执行后续施工建议（不在本调查中执行）

### W0：补齐剩余正式文件的局部终态，先决定下一包

- 冻结明确 source/build；需要构建只能用 `node scripts/build.mjs`。
- 保持 unit tier、原 30s verdict-silence 与原 300s backstop，明确选择原 016 一次完整文件，不重跑全量 CI，不以 repeated runs 选绿。
- 可用既有正式入口：`TESTS_MJS_FILES=requirements/verification-system/tests/016.test.mjs NODE_TEST_CONCURRENCY=2 UNIT_VERDICT_SILENCE_MS=30000 node requirements/verification-system/tests/run.mjs`。此处只是将来的执行建议，本次未执行。
- 记录 authoritative pass/fail/skip/TODO 与 file drained；继续保直接 beforeEach/fixture phase/tool ownership 原件。若失败，就以这一次确切阶段调查，不增预算。
- 需要补齐队尾成本时，另一次显式有限选择 work-record/006–017，不与 016 反复交替试跑，也不把局部绿色当原官方全量已闭合。

### W1：优先去掉 archive fixture 的大 Buffer 汇集，保持真实字节与 owner

最窄可审查范围先只针对 `support/node-tool-candidate-tests.mjs:archiveTools`：保留原 tar.create、原 cwd/portable/noMtime/成员、真实 selected Node 和完整 npm package，用标准 stream pipeline 将原 tar bytes 直接写文件，途中增量 SHA256/计数；pipeline 完整 settle 后才发 archive-written 并返回 capability。当前 chunks+Buffer.concat+同步写盘至少同时保留整份 archive 的多份内存，属于明确的重复物理搬运。

必须保持：完整归档字节与 digest、每次真实 mutation 的独立新 archive、写入失败原样拒绝、fixture-owned 根清理、准备前后 namespace/owner 复核和全部现有负控。禁止改成小 shim 冒充实际 Node/npm，禁止缓存可变候选或直接复用上次成功 capability，禁止把重复路径 tar.update 当合法新 archive。

验证以现有正式 selected-tool/namespace/probe/cancellation 正反例和完整 016 为准；必要时用小而真实的 tar fixture核流式输出与原 tar.create bytes/SHA 相等，不能以不稳定的耗时断言替代行为证据。此包即使通过，也只能登记 fixture 成本优化，不能直接宣布官方 300 秒已解决。观察到的 5.425 秒只是打包阶段总量，不等于优化必然节省同样时间。

同一方式可以在第一包证据成立后，独立应用于 `npm-tool-archive-tests.mjs:toolArchiveFixture` 的相同 chunks/concat 写入；不借机会改生产 candidate 验收合同或建立通用注入框架。

### W2：依据仍剩的真实成本处理长尾，不先改监督或缩范围

- 若 W0/W1 表明 016 可正常结束但完整工作仍超容量，按 `wall-analysis.json` 的实际长文件顺序，有限剖析 requirement-grounding/007、verification-system/006 等的 setup/真实等待；每包先明确哪一重复工作可消除且保持同一正式命题。
- `verification-system/006` 的长 wall可能包含正式监督负控的真实等待，不能直接缩小超时或删断言；grounding/007 的 provider/tool matrix 和四个 production-wiring mutation也不能只因昂贵而降为 scan/mock。
- 先消除已证冗余计算/拷贝，不用增加 300s、吞掉 TODO、重置分片预算、改变 tier 或盲调更大 worker 数求绿。
- 若最终需要改变文件并发策略，必须另有真实可用资源与总工作成本证据，并保持原监督和所有权合同；本日志没有可授予更多 worker 的资源快照，当前不推荐以改 CI 数字作为第一包。

原始 CI 仍是失败。上述建议与派生计算不提供新的 pass/fail verdict，也不关闭全局清理、全 Host 验收或整个施工计划。
