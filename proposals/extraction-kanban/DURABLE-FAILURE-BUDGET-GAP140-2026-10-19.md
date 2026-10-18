# GAP-140 持久失败预算

状态：G140-A 已按下列范围验收；[G140-B1](SINGLE-JOURNAL-RECOVERY-G140-B1-2026-10-19.md)已验同一live Journal的non-idle恢复发送及观察者接管。GAP-140整卡继续施工，B2领域原子命令、跨Journal/OS的durable发送权和缺Host-stop的普通冷Workflow仍未闭合。R12-U1继续暂缓。本报告不代表GAP-139已闭合。

## 已冻结合同

- 首个 FailureRecorded 一次提交失败及必需的 BudgetSpec `{ Version: 1, Limit: 正整数 }`。同一 LogicalRun 后续失败必须保持此依据；成功重置连续计数也不更换限额。
- 相同失败身份和完全相同的 count、reason、BudgetSpec 幂等；同身份冲突内容拒绝。身份保留到该 LogicalRun 结束，成功不清去重集合。重复旧成功不能清掉后续失败。
- 达到 Limit 的 FailureRecorded 本身已派生耗尽。RetryExhausted 只确认 `FinalCount = currentCount = Limit`，不能独立建立或更改限额。耗尽不能被迟到成功重新打开。
- 仅首次新失败的 writer 可选择产品默认限额；重放、重复观察、retry preflight 和后续入账只读持久依据。预算证据不产生 Host-stop permit。
- 缺少 BudgetSpec 的旧 FailureRecorded 及旧 Fallback 系列明确拒绝，不猜测旧限额，不在线迁移。现行 SuccessRecorded 仅按当前合法投影折叠；现行 RetryExhausted 必须有先前有效预算。
- writer 失败或 COMMIT 未知不给 Redispatch 权限。此点须保留真实 writer、独立冷读和物理发送的证据；纯 Retry port 替身不替代验收。

## 基线与施工步骤

基线 `0a7a53418`。直接运行现行 007，两个可执行 TODO 均出现预期断言失败；日志 `/private/tmp/gap140-baseline-007.log`。第一个旧夹具在 count=1 即标记耗尽，没有建立 limit，不能单独证明真实预算耗尽后的发送事故。

1. 更新 WHAT 与必需事实合同，统一 projection/fold、writer/retry 与 codec。
2. 以显式小限额验证边界；补同身份冲突、成功后的迟到失败/成功、耗尽标记缺失和伪造标记。
3. 跑相关 requirement；固定构建产物后做非作者复验、冷 OS 和真实 writer/消费者取证。
4. 据证据更新状态；未完成项继续列明，不以局部绿色关闭整卡。

## 分包与当前证据

G140-A 处理持久预算模型、顺序 writer、成功身份和冷重放，交付构建697。首条预算、完全相同事实幂等、冲突拒绝、成功后的旧身份保留、达到限额无标记即耗尽均已接回生产。新增成功命令显式携带来源 AuthorityRoot：ordinary 从 turn 取，Wire 从已冻结 plan 取，ledger 匹配后才追加。合法关闭 A、接受 B 后，695 的无来源 root 旧命令实际把 B 的 count1 清零；697 同一 F# 消费者拒绝 A 且字节不变，B 合法成功仍清零。此证据止于真实 ledger 命令，不声称旧 root 的真实 Host 成功已经送达。

- 构建 692 的正式 41 文件：163 pass / 0 ordinary fail / 29 TODO；构建 695 的相邻 75 文件：437 pass / 0 ordinary fail / 4 skip / 51 TODO，75/75 完成和排空，自有进程组回收通过。四个 skip 是未启用的 Host/旧布局 integration；TODO 与 skip 均不算通过，正式入口因此 exit 1。
- 695 固定包 SHA256 `80d2392e5ea301f984a36d64c9162bbc4948e9bfe11d460d0a3b5ccf831c4418`。非作者 Node/Bun 显式 limit1/3/15、40 组失败-成功、零计数成功、同 attempt 成败互斥、原字节 codec 已通过；小/大限额真实 Journal 冷 OS 与类型消费者继续验。
- 非作者 692 的 Node/Bun 真 delegate→workflow→ledger→Journal：关闭 writer、原生 append 前抛错、真写入/fsync/owned lock release 后丢回执，新增 send 均为0；冷 OS 分别读0/0/1。健康正控实际 send1。相同 runtime 重复失败无新增发送；该负控在已终止 call 的边界成立，不代表活 call 内的原子仲裁。
- 两 Host 真12轮：前11次确认失败各重发1次，第12次不再发；FailureRecorded12、RetryExhausted1，冷读保持耗尽。仅证明所测 delegate 路径，不关闭普通/Blogger/全恢复 GAP-139。
- 永久005已手工接入四个物理 writer/冷 OS 探针。首次迁入把私有 `/private/tmp` 限制复制到了 macOS TMPDIR，四个探针在产品动作前失败；现只验绝对 owned 目录，复验8 pass / 0 fail / 1既存TODO。此红归夹具前提错误，不归产品回退。
- 最终697的101个相关文件：**693 pass / 0 ordinary fail / 7 skip / 62 TODO**，101/101完成排空、自有组清理通过。新增并发事实 TODO 执行得到2而非1，原红保留，不算通过；其余 skip/TODO 均列在正式原件中。构建、已改F#格式检查与静态门禁通过；report-only提示不计关闭。
- 最终包 SHA256 `21a3409fe09a626976189b47349122c2e94b405b941d4536a5ad7e837817346f`；非作者核2076包文件逐字节一致。C组双Host9场景、真实Journal limit3/15及四个新OS冷读通过；limit15确实在12之后继续至15才耗尽。冷读未创建writer文件，原件字节/SHA不变。
- 真F#合同：24源码/12fsi闭包正编1、负编3；新增Ledger 28源码/14fsi正编1、缺root负编1。删除生产生成JS后，同一发射消费者仅消费697固定包，在Node/Bun通过。以上是内部F#合同证明，不冒充npm声明文件。
- 非作者697的两Host真实writer三故障、健康正控及12轮，共10个producer和10个独立cold全部通过。最终包与692仅12个JS差异；Workflow、Retry、Host及库字节相同。没有发布registry或部署Host。

长期证据见 [目录与哈希](evidence/GAP-140/README.md)，包括全部首红、夹具误判、源码/精确包、正式监督原件、独立复跑脚本和归因对照。

## G140-B：并发命令和单次发送，仍为 P1

真实并发同身份 ledger 命令会写两条 FailureRecorded，投影仍为1。非作者用同一实际 SyncDelegate HandleTurn 消费者又复现两次恢复 SendPrompt；该调用刻意绕过普通按 session 调度器。旧680精确包、同脚本的实测也为 facts2 / send2 / count1，因此是本轮前既存问题，不是本轮修改引入；普通公开调度入口是否可达仍未证明。

003新增可执行 TODO 保留“事实必须一条”的失败断言和 NDJSON。计数去重不能替代命令仲裁，正常顺序重复绿色也不能覆盖这个红。GAP-140 整卡继续 DOING。

以下为B拆包前计划；B1的原型、生产候选700、观察者接管与剩余边界见新报告，B2按其freshness前置继续施工。原并发facts1强断言仍红，不用B1单发关闭本节。后续真实writer原型与生产接线要求：

1. 在现有 writer 的串行接纳点定义一次领域决策：读取该次新快照，同步返回 NoWrite 或待提交事实及结果。budget owner 继续独占判断，Journal 不重写一份去重键；不加全局锁或第二套预算账。
2. 同时确认 store gate 内跨 writer/进程的新快照与提交边界。当前 IEventStore.Append 只收预造 events，现有 NoNewWrite 不能替代领域动态决策；单 writer 队列的证明不能冒充跨 writer 原子性。
3. 并发同身份必须只有一条物理失败事实；并发不同身份须合法排序；成功/新 root 交错重新核对归属。持久失败结果仍不等于发送权。
4. 单次恢复发送交给原有 durable prompt claim owner 仲裁，补并发和迟到重复实际发送反例。当前 RunGateNudgeOnce 的 flight 属于各 Runtime 实例，`forPrompts` 每次新建实例；要核实共享 owner 与持久 claim 的完整合同，不能仅缓存一个 Task 当终身发送证据。
5. 原型通过后再迁 ledger/retry/消费者；验证 COMMIT 前后丢回执、未知重入、独立冷 OS 没有 Host-stop permit 时零发送。最后固定包、非作者复审，再判断整卡是否闭合。

冷 OS 的预算投影已验，但实际普通恢复 Workflow 在缺当前进程 Host-stop 观察时零发送仍是验收缺口，不能用“冷进程没有活 delegate call”提前拒绝代证。R12-U1 继续暂缓。

## 归因纪律

原预算缺依据、成功清去重集合、重复旧成功与并发重复事实/发送属于修前边界；并发发送有旧680消费者实测。旧 root 输入缺口的修前依据是0a7源码对照；动态旧端为本轮695，不冒充修前实测。施工中曾漏 direct FactCodec 的非法 Spec 校验，现已用同一 isValid 补齐，未提交带缺口版本。Envelope 早已拒绝，不能谎称当时三入口均严格。

两项 review 假设不记成缺陷：缺 Spec 的 Reason 含 legacy 关键词会被兼容过滤吞掉，实际三入口及 Journal 均拒绝，原件没写；数字字符串按既有 Thoth int 归一化不违反本包已冻结的 typed 整数合同。另一次未关闭 A 就接 B 的迟到成功探针先被合法 root 规则拒绝，不能作为跨 root 事故证据。
