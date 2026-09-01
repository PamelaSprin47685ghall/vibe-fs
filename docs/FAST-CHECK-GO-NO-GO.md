# Fast-check GO / NO-GO — upstream replay 2026-09-01

本文件记录 property-testing 的工具选择证据，不定义产品语义。语义仅来自对应 `requirements/<package>/WHAT.md`。复核基线为 `upstream/master@ff85615e9a8dc0c94447eb55960a72deb46ed9db`。

## 判定规则

只有同时满足下列条件才采用 fast-check：

1. 风险空间不能以小型有限表完整枚举；
2. generator 只生成输入，不复制 production decision 或状态机；
3. oracle 直接观察注册 Surface 的 production 行为；
4. 至少一个精确错误世界能被 assertion 杀死；
5. seed、run budget 与 shrink path 可重放。

有限代数优先穷举；exact identity 优先逐字段 decoy；因果排列可完整枚举时不降级为随机抽样。文件名含 `property` 不等于使用 fast-check，也不等于 comprehensive。

## P6B-2 — execution-failure-policy

结论：`NO_GO`。最新 upstream 已包含完整 proof，未发现需要新增 fast-check 的剩余空间。

### Owner 与现有覆盖

- 唯一 decision：`ExecutionFailurePolicy.decide`；正式边界：`Execution/Failure/Surface.decide`。
- `policy.test.mjs` 穷举 closed failure、phase、retry/fallback budget、breaker、persistence 与六维输出。
- `cancel-retry-fallback-stream.property.test.mjs` 是 216 组手写有限积，不使用 fast-check；它跨正式 policy、recovery interpreter 与 Hook Promise 边界。
- EXECFAIL-006 定向杀死三个错误世界：cancel 获 retry、LocalInvariant 丢 fatality、exact fence 被错误保留。
- focused suite：21/21，约 0.59s。

### 不采用 fast-check 的原因

- 核心输入是小型封闭代数，确定性穷举强于 sampling。
- 合法输出由 production decision 决定；测试若生成 expected retry/fallback 表，会形成第二 oracle。
- 跨调用的 licence consumption 属于 recovery owner；在 policy generator 内建 operation stream 会复制另一套状态机。
- identity delimiter/injectivity 若成为独立 law，应先写最小固定 collision counterexample，而非用随机字符串掩盖 owner 缺失。

### 已知 proof 债务

216 组组合含不影响 `policy.decide` 输入的 persistence、Host evidence 与 Hook mode；`expectedRecoveryEffect` 复制了一小段解释映射。该 proof 仍 production-bound 且 mutation-sensitive，但组合数不能被表述为 216 个独立 policy world。后续若修改此 owner，应删除无因果维度，并由 recovery Surface 直接返回可断言的封闭结果；本次不为工具统一而改写已绿 proof。

## P6B-3 — durable writer-tail truncation

结论：`GO`。实现节点：`2dee337b9`。

### 缺口与输入域

DURABLE-EVENTS-004/007 要求 retained writer 内任何 incomplete NDJSON line 都 fail-closed，禁止丢弃损坏 suffix 后继续读取。固定 malformed JSON 例子没有覆盖：

```text
任意 canonical payload × 最后一条 canonical line 的任意非空 UTF-8 字节尾部截断
```

generator 产生 1..12 个 payload string；事件 identity 与 parent chain 合法。正式 `EventCodecSurface.encode` 生成 canonical bytes。cut seed 选择最后一行 1..N-1 个尾部字节，既覆盖多字节字符中间截断，又排除删除整行后形成合法 prefix 的世界。

### Production path 与 oracle

- 物理输入写入 `.git/wanxiang/events/<WriterId>.ndjson`。
- 观察路径：`RetentionSurface.retainedWriterIdsAt → ProcessEventLog.readStreamsAt → decodeWriterText`。
- oracle 只要求 production reader 报 `incomplete trailing line`；测试不解析 JSON、不折叠事件、不复制 newline detector。
- retention cutoff 固定为 `0`，不依赖 wall clock。
- 正常 property：seed `0x44555241`，300 runs。

### Mutation sensitivity

定向 mutant 在读取前删除最后一个 LF 后的损坏字节，精确模拟“skip corrupt tail”。`fc.check` 使用 seed `0x44555242` / 100 runs；测试要求 mutant 失败、产生 counterexample/path，并以同 seed/path 单次重放失败。

验证：focused 2/2，约 0.18s。fast-check 已锁定为仓库依赖；未新增 production Surface、decoder helper 或依赖。

## P6B-4 — capacity fence interleaving

结论：`NO_GO`。最新 upstream 的 lifecycle、soak、restart 与 causal proofs 仍比新增随机 operation stream 更强。

### Owner 与现有覆盖

- 正式边界：`OpenCode/Host/ModelRoutingSurface`；相关 laws：EMR-003/010/011/012/014 与 VERIFICATION-SYSTEM-007。
- lifecycle 表覆盖 Pending/Committed/Released 合法边、非法反向边、duplicate、same physical retry 与 newer generation stale。
- exact identity proof 逐字段拒绝错误 physical message、agent、target、generation、runtime fence。
- admission soak：固定 seed，32 rounds × 32 waiters，3,808 个逐操作 snapshot audit。
- lineage soak：64 cycles，832 个逐操作 audit。
- restart soak：16 个新进程，96 个操作。
- 因果 proof 完整枚举 6 个操作的 720 个排列：12 个合法排列执行，708 个非法排列在 effect 前拒绝。
- focused bundle：20/20，约 3.28s。

### 不采用 fast-check 的原因

- 生命周期、exact identity 与六操作排列均有完整有限覆盖；随机 sampling 只能取其子集。
- 若 generator 维护 acquire/settle/cancel 的合法前置条件，就会复制 queue/fence owner 状态机。
- opaque lease 与异步 waiter 被 shrink 后容易只剩非法编排，而非更小业务反例。
- 现有每步 production snapshot oracle 已覆盖长期 retained-state bound、fairness、stale release、duplicate settlement 与跨进程边界；没有识别出新增 property 能独占杀死的错误世界。

### 范围说明

`identity-capacity-interleaving.property.test.mjs` 的 32 个 deterministic families 并非所有维度的完整笛卡尔积；它不能单独声称 comprehensive。完整性由相邻的 720 排列穷举与 owner-specific lifecycle/identity/soak proofs共同承担。`cancel/terminal first durable settlement wins` 跨 durable chat、failure policy 与 capacity owner；除非出现可直接消费 event stream 的唯一 production decision，不得在 capacity 测试内镜像该跨 owner 状态机。

## 复核结论

fast-check 只增加在结构/字节位置空间大、shrinking 有诊断价值、且 oracle 可完全依赖 production 的 P6B-3。P6B-2/P6B-4 保留 deterministic exhaustive + exact counterexample。测试数量不是充分性指标；每个 proof 必须说明其独占消灭的错误世界。
