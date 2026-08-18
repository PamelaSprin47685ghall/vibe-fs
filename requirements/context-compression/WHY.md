# context-compression — WHY

## 1. 不可替代的存在理由

provider history 可能超过可用上下文。压缩若依赖预测窗口、把候选先提交再回滚、
或按错误文字分类失败，会把**模型容量猜测**与**未发生世界**写进产品事实。

**context-compression 保证：何时、哪些历史有资格被语义替换，只由真实失败与证据边界决定。**

## 2. 独立存在测试

把当前 failure-driven X/Y 恢复管线（probe + squash）换成另一个 semantic compressor——
只要「不观察容量、失败驱动、候选未提交不是事实、只替换可证明 covered 区域、Opening 有
floor」这些合同不变，trace / prefix laws 一律不动。反过来，若允许「请求前按长度预判溢出」，
KV-cache 与前缀稳定（prefix-stability）同时被破坏——独立失败域。

## 3. 失败意义（FAILURE MEANING）

RED = 满足下列任一：

1. 未提交候选污染真实历史（probe 先写事实再回滚、Y squash 失败仍改 frames）；
2. 压缩覆盖不完整证据（半 turn 冒充完整 prefix 证明、Opening 被吞）；
3. 系统靠猜模型窗口主动改写用户世界（CTX-001/002 违例）。

## 4. 历史考古

### 4.1 失败驱动 vs 预测式（历史 why/context）

拒预测：估计容量把错误阈值固化成产品行为，与 KV-cache / 前缀稳定性冲突（CTX-001/002）。
接受「第一次溢出必失败」的代价，换取协议零窗口表依赖。

### 4.2 probe：失败不写事实 vs 提交再回滚

拒回滚：未提交候选从未成为世界一部分，无需 `PrefixProbeRolledBack` 神话（CTX-010）。
`A′` 失败不禁止 `B′` 用等价候选重试；候选只进不可变 `AttemptExecutionProfile.ProjectionChoice`。

### 4.3 失败语义：不分类 vs 按错误文字分叉

拒文字分叉：换 provider 即整体失效，且制造永不执行分支。只看 snapshot `Outcome`
（Completed / Failed / Aborted）（CTX-005）。

### 4.4 squash 依据：内容/比例触发 vs 仅失败槽

拒比例触发：压缩点由失败堆栈决定，不看 token/配额（CTX-004）。`isValidTerminal`
（非空 ∧ 非 XML-only）是唯一内容校验。

### 4.5 X9 容量估算器被删（ctx014 tombstone）

估算器曾在 X9 删除。`ctx014.test.mjs` 的注释明确：禁止字段一旦出现在 production source
就会被 tombstone 测试拦下。「日志里出现 context_ratio 式字段」= 把模型窗口猜测写进产品事实。

### 4.6 200 KiB 是输入合同不是窗口估算（CTX-003）

`BloggerDeltaLimitBytes = 200 * 1024` 只约束单次 delta 渲染字节，不比较、不触发、不动态调参。

### 4.7 catch-up 是连续追平，不冻结 frontier，不把 quiet 当完成

一次 main-material wake 可能需要多个 ≤200 KiB cycle。Y 每次提交后都从当前 canonical
coverage + 当前 XTrace Current 重新取下一块；**不得**把 wake 时、首次 cycle 时或任一中间时刻的
XTrace head 冻成 drain frontier。否则 drain 期间新到的真实 material 会被人为推迟到下一次 wake，
吞吐与时序都发生用户可见退化。

当前瞬间无 material 只表示暂时 caught-up，不表示该连续 drain 已完成。**在同一存活物理执行内**，
只要 main 仍允许继续，Blogger 必须保持悬挂等待；未来 material 到达后唤醒同一 continuation，再从
最新 Current 继续追平。把 quiet/caught-up 改成自然 stop，会把持续追平退化成离散批处理，也破坏
COMPANION-005 / ENFORCER-047/050 的既有行为。process death 是另一条边界：按 CRASH-017/018，旧
invocation 已中断，restart 不得把这里的“持续等待”解释成跨进程自动恢复。

### 4.8 park/seal 不能撤销已经起飞的 Blogger 请求

park waiter、drain window 与 physical flight 是三个正交物理事实。取消 waiter 或关闭 drain 只影响
“还要不要等待/还准不准起新请求”，不能把已经交给 provider、可能马上返回 `chronicle` tool call 的
flight 从注册表删除。否则会制造确定的竞态：provider 已生成合法 tool call → 本地先清 flight → tool
execute 重查 `HasFlight=false` → 把合法在途调用误判成 stale no-live-cycle。flight 只由 cycle terminal
commit、显式 abandon/fail 或 session disposal 释放；seal 只阻止新 work，并让当前 flight 自然收尾。

## 5. 与相邻包的边界

| 看似相邻 | 为什么不归本包 |
|---|---|
| XTrace 原始历史 | 事实源在 semantic-trace；本包只消费 |
| 已提交前缀的字节稳定 | 那是 prefix-stability（epoch/reanchor 合同） |
| prefix 候选的「新于已提交」证明 | 是 candidate coverage 证明 → 本包拥有选择 policy（CTX-011），epoch 提升归 prefix-stability |
| RecoverySlot 的 armed/primed | armed/primed 由 fallback 提供（provider-attempt-recovery）；本包拥有 hasMaterial 与动作选择 |
| Blogger/Y 的 summarization Persona | Companion 拓扑归 session-ontology；压缩输入合同归本包 |
| TOML 渲染器 | 布局/转义归 provider-projection（CTX-013 的 delta 合同归本包，渲染归 provider-projection） |

## 6. 源材料

- 历史 why/what context（CTX-001..016）
- 历史 how/context、历史 shape/context
- 历史 why/what companion（COMPANION-005/006/007/008/013）
- 历史 HOST-006（containment 层）
- 历史 requirements-design card（13-context-continuity，context-compression card）
- 历史 COVERAGE（CTX-*、COMPANION-005/006/007/008 行）
