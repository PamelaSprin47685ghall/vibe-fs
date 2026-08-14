# prefix-stability — WHY

## 1. 不可替代的存在理由

provider KV-cache 与认知连续性都依赖一件事：**已经呈现给 provider 的过去不会无故重排**。
同一 semantic epoch 内，如果历史字节不断搬家——synthetic pair 换位置、frozen prefix 换内容、
system prompt 被重写——那么即使语义相同，模型看到的也是「新的世界」：缓存失效、seal 破、
身份/语言/guidance 的连续性全部断裂。

**prefix-stability 保证：同一 semantic epoch 内已提交前缀 byte-stable；冷边界只能由
已提交事实驱动，不能由 token 估算或临时状态驱动。**

## 2. 独立存在测试

完全替换当前 HOST-013 gap anchoring / wire representation，或完全替换
`ProviderProjection.isAppendOnlyPrefix` 的调用方——只要 append-only prefix law 与合法
cold-boundary semantics 不变，context-compression / provider-projection / provider-language
的 WHAT 一行都不用改。反过来，若允许「无业务语义变化时重排历史字节」，所有依赖 prefix
的机制（KV-cache、ReviewSeal、Pair Hint 重放）同时失真——独立失败域。

## 3. 失败意义（FAILURE MEANING）

RED = 满足下列任一：

1. 无业务语义变化时历史被重排 / 改字节（同 epoch 前缀不 stable）；
2. 未提交 candidate 被当成 stable prefix（candidate ≠ committed 被打破）；
3. 冷边界由容量/token 估算或临时状态触发（而非已提交事实）。

## 4. 历史考古

### 4.1 `cache.md`（HOST-013 anchored prefix，历史 completed change）

P0 缺陷：HOST-013 auto-injected 的当前实现把历史 synthetic pair 重组、搬家，破坏了已经发给
provider 的字节前缀。正确设计：

```text
PREFIX LAW
same PrefixEpoch:
ProviderWire(n) is an exact prefix of ProviderWire(n+1)
```

仓库已有 `ProviderProjection.isAppendOnlyPrefix`（比较 provider/model/variant/tools/system 及
完整 message prefix），因此本 Change 直接复用它作为 PREFIX LAW 的权威判定，**不得再写第二套
「差不多是前缀」的 helper**。

被拒方案（cache.md §4 考古）：

- 每次 transform 删除历史 marker、把单条 completed tool-result 挪到新位置 → 前次 wire 不再是
  后次的字节前缀；
- 删除历史 synthetic 后把全部历史 pair 压缩成 `historyBlock` 放到当前 call/result 批前，或按
  当前 trailing user / tool batch 重定位 → 历史字节随 transcript 形态搬家；
- 把 FakeReq 写成独立 Host `pending`/`running` tool part → 模型看到伪中断；
- 每次 transform 无条件 `ordinal+1` append 新 pair，以 `history.Length + 1` 判断新 round →
  Host retry / 测试重放 / 同请求重入凭空多出 pair。

### 4.2 `cursor-pair-hint.md` §12（prefix/idempotence scope）

同一 provider projection family + 固定 Cursor role strategy 内：same occurrence history +
same semantic transcript + same strategy → 重复 transform byte-identical。deliberate
provider-family transition 可以改变物理字节（FakeToolPair bytes ≠ CursorText bytes）——
**那不是 prefix corruption，是另一种 provider projection**；durable semantic occurrence
identity 必须稳定。

### 4.3 历史 why/host 决策 9–13（HOST-013）

- durable gap anchor 原位 replay vs 移动/重定位 marker：pair 一经加入即不可变永久历史；
  位置由 durable gap anchor 唯一决定；
- `isAppendOnlyPrefix` 是 PREFIX LAW 唯一权威判定：只检查 pair 数量 / callID 相同 / markerText
  正确 / FakeReq 在 Req 后——这些在 Prefix Cache 已坏（历史被搬家）的实现上全部通过。

### 4.4 COMPANION-009 / ARCH-004：epoch 切换三证据源

同一 PrefixEpoch 内 `request[n+1]` 历史前缀必须**逐字节**等于 `request[n]` 的 sealed prefix。
Epoch 切换仅三个证据源，且必须 `EpochId+=1`：成功 prefix probe 提升、Host compaction 重锚、
TodoCheckpoint lag-1 rebase。禁止按容量/token 主动切换 epoch。

## 5. 与相邻包的边界

| 看似相邻 | 为什么不归本包 |
|---|---|
| 为什么需要压缩/rebase | context-compression（何时/哪些可替换） |
| provider language/identity/cognition 内容 | provider-language / participant-identity；本包只要求「若属于 prefix identity 则稳定」 |
| renderer 实现 | provider-projection |
| 当前 gap-anchor / fake-tool / Cursor suffix HOW | 可整体替换（INDEPENDENT CHANGE） |
| 候选的选择 policy | context-compression（CTX-011 判定） |
| fold/落盘机制 | durable-events |

## 6. 源材料

- 历史 HOST-005/006/013、历史 why/host（决策 9–13）
- 历史 COMPANION-009/010/011/013、历史 shape/companion
- 历史 CTX-010/011/012/015、历史 why/context（ActivePrefixEpoch 理由）
- 历史 PROMPT-014、历史 TODO-009、历史 ARCH-004
- 历史 change（cache）、历史 change（cursor-pair-hint §12）、历史 change（pair-parallel-tools，prefix 相关）
- 历史 requirements-design card（13-context-continuity，prefix-stability card）
