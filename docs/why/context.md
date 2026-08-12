# 上下文恢复 — 理由

预测式压缩把错误阈值固化成产品行为，并与 KV-cache / 前缀稳定性冲突。失败驱动接受「第一次溢出必失败」的代价，换取协议不依赖任何模型窗口表。

X probe 失败不写事实：未提交的候选从未成为世界的一部分，无需回滚神话。失败不分类：按错误文字分叉会在换 provider 时整体失效，并制造永不执行的分支。

200 KiB 是输入合同不是窗口估算：它只约束单次 delta 大小，不触发主动 squash。

## 备选与被拒

**恢复触发：失败驱动 vs 预测式。** 拒预测：估计容量把错误阈值固化成产品行为，与 KV-cache / 前缀稳定性冲突（CTX-001/002）。接受「第一次溢出必失败」的代价，换取协议零窗口表依赖。

**probe：失败不写事实 vs 提交再回滚。** 拒回滚：未提交候选从未成为世界一部分，无需 `PrefixProbeRolledBack` 神话（CTX-010）。

**失败语义：不分类 vs 按错误文字分叉。** 拒文字分叉：换 provider 即整体失效，且制造永不执行分支。只看 snapshot `Outcome`（CTX-005）。

**squash 依据：内容/比例触发 vs 仅失败槽。** 拒比例：压缩点由失败堆栈决定，不看 token/配额（CTX-004）。  
**上界：200 KiB 合同 vs 窗口估算。** 拒估算：合同只约束单次 delta 渲染字节，不比较、不触发、不动态调参（CTX-003）。

## ActivePrefixEpoch 与 TodoCheckpoint（理由）

**一条 PrefixEpoch SSOT vs todo-only 平行 epoch。** 拒第二真相源：若 todo 事实说已 rebase 而 `ActivePrefixEpoch` 未变，崩溃恢复与 seal 绑定会分叉。TodoCheckpoint 只是既有 `PrefixRebaseCommitted` 的 `EvidenceKind`，进入同一 ActivePrefixEpoch（CTX-015；义务见 TODO-009；禁平行 owner 见 TODO-012）。

**desired cutoff ≠ committed epoch。** 拒 Accepted 后立刻写 committed、拒 `NeedRebase`/`Requested` Stage：Accepted 链只导出下一轮 policy；epoch 证明在下一 provider attempt seal/绑定前原子提交。provider 成败不是 commit 条件，失败不得回滚已 seal epoch（CTX-015；TODO-009、TODO-012）。

**PrefixCoverage-only Y vs RecordCoverage/LWR。** 拒用 LWR RawGap 冒充可替换前缀：过程评审要看 frontier 上的完整证据（可含 gap）；Manager lag-1 前缀只许 proven complete-turn Y。二者不得互推（CTX-015；coverage TODO-008；rebase TODO-009）。

**Opening floor = WorkRecordStart，不是 Activation。** 拒把 Opening 保护绑回 `WorkActivated`/planning floor：删除两阶段后仍须 byte-stable Opening；Blogger/Y 的结构性起点是 Opening exclusive end（CTX-016；TODO-001）。

## Strength 与前缀世界（理由）

**Replica 继承 owner 的 Persona / language。** Strength 是同一人的廉价只读分身，不是新办公室。若 Replica 自造 Persona 或另绑 `ProviderLanguage`，投机轨迹进入的「自我模型 / 世界语」与 primary 不同质，Promotion 后会把异质前缀补丁进主历史。继承 owner → 投机与主链共享身份与语言常量；只换 fast 模型绑定。

**未 Promote 的 Candidate ≠ 历史。** 与 X probe 失败不写事实同构：未提交候选从未成为世界一部分。提前写入 XTrace / Companion / provider-visible 历史，等于用未发生的干预污染后续请求；primary 消费并 Promote 之后才是真实因果。拒把 source label（「来自 Strength」）写进 Main reasoning——那是机器溯源，不是经验层该看见的状态机。
