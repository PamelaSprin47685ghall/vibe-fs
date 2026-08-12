# guess-based-fix — Main

## 现在该做什么
把 lucky patch 重新还原成一个可以学习的 experiment。

保留 failing case，命名本应阻止它的 invariant。撤回或拆开 speculative changes，直到你可以一次验证一个 causal hypothesis。最终只留下那个能够同时解释 original failure 与 repaired behavior 的 change。

然后用一个能在旧 mechanism 回来时真正失败的 regression，把这个 explanation 编码进 repository。

## 为什么重要
Guess-based fix 会积累一种特别有毒的 technical debt：**unknown necessity**。

每一行“当时也许有用”的 speculative code，在 symptom 消失后都会变成未来 maintainer 不敢删除的东西：“万一这也是 fix 的一部分呢？”于是系统里堆满没人能解释的 retry、没人敢缩小的 lock、没人敢删的 cache flush、没人理解的 exception handler，以及唯一 provenance 是某次古老 red build 的 timeout。

眼前 symptom 可能消失了，但 codebase 变得更不可知。下一次 incident 会从比第一次更差的 epistemic state 开始。

## 修复策略
先缩小，再扩展：

1. 把 failure 复现到足以观察；
2. 写出互相竞争的 causal hypotheses；
3. 设计最便宜、能区分它们的 observation；
4. 找到 violated invariant 的 owner；
5. 在真正 owner 处实施最小 coherent repair；
6. 删除 causal contribution 未被证明的 speculative edits；
7. 增加在旧 mechanism 下会失败的 regression evidence。

如果无法完美复现，也可以提高 causality：instrument suspected transition、约束 hidden variables、根据最强 evidence 选择 repair。不要假装 uncertainty 已经消失；保留仍未知的部分。

当 root-cause repair 现在无法安全交付时，mitigation 可以合法。明确称它为 mitigation，保留 underlying issue，不要把 operational containment 写成永恒 architecture doctrine。

## 决策分支
- **多个 speculative edits 一起 landed：**拆分/回退，直到每个 retained change 的 causal contribution 都清楚。
- **某个 knob 只是隐藏 timing：**安全情况下恢复旧 policy，调查 missing signal/race/resource cause。
- **Broad lock 修掉 race：**找 exact shared invariant；如果 global exclusion 没有 semantic necessity，就缩小 ownership/lock scope。
- **Catch exception 让 failure 消失：**证明 recovery/swallow 是否仍满足 caller contract；否则恢复 error visibility。
- **Generated patch green 但没人解释：**把 diff 当一组 hypotheses 审，而不是 oracle answer。
- **当前只能 mitigation：**记录 causal uncertainty 与 containment boundary，不要宣称 root-cause closure。

## 常见假修复
- 所有 speculative changes 都留着，因为“删哪个都可能把 bug 带回来”。这正是必须拆 experiment 的理由。
- 写一个 regression，只断言当前 implementation 的整个 output。好 regression 应隔离 violated invariant。
- 事后编一个很自信的 explanation，却没有 discriminating observation 支撑。
- 把 green suite 当 mechanism proof。Suite 只证明那些 tests 真正有能力区分的东西。
- 把 workaround 改名“architecture improvement”，让以后没人再问它到底有没有必要。
- 邻接 symptom 出现时继续叠第二层 workaround，而不是回头找 cause。

## 验证
Causal repair 应该能做预测。

它要解释：

- original failure 为什么发生；
- retained change 为什么阻止它；
- 为什么至少一个 plausible alternative hypothesis 与 evidence 更不一致；
- 哪个 regression / invariant 会在 recurrence 时报警。

条件允许时，恢复 old mechanism 应让 regression red，应用 repair 后 green。

Invariant：

> Repository 中保留的 mechanism，都有可以解释的理由，而不是因为它们恰好与一次 successful run 同时存在。

## 完成条件
Patch 本身成为可执行 knowledge。

未来 maintainer 可以无迷信地删除无关 changes，因为真正 repair 拥有一个 named invariant，也有一条 test 记得它为什么存在。
