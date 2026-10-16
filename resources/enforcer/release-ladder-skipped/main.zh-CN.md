# release-ladder-skipped — Main

## 现在该做什么

按 change surface 重新画 proof ladder，从最窄 faithful rung 往上清 uncertainty。

典型顺序不是死规则，但思路通常是：

```text
pure/local law
    ↓
boundary contract
    ↓
replay/recovery
    ↓
integration
    ↓
real external canary
```

每层只承担下面无法证明的部分。Local rule 已红就先修，不要拿 staging green 压过去；contract 已证明才让 full integration 去测试 composition；external undocumented premise 最后交给 canary。

## 为什么重要

Broad test 混着很多 cause。它失败时诊断昂贵；它通过时，具体的 local invariant 可能仍然没被 exercise。Narrow proofs 先建立小 truths，后面的 stage 只测试下面证明不了的东西。

## 修复策略

把 change surface 映射到 proof levels，在 promotion 前清掉每个 applicable gate。把发现的每条 regression 留在能 faithful 表达它的最低层。

## 决策分支

- 如果 change 触及 pure logic，在 integration 前清掉 local/unit（与 property）rung。
- 如果 change 触及 boundary，在 full environment 前加或跑 contract tests。
- 如果低层 rung inapplicable，记录为什么，只跑剩余 applicable ladder。

## 常见假修复

常见假修复：

- “为了保险”所有改动都只跑最大 E2E；
- lower rung fail 后不断 rerun upper rung；
- broad suite 里某条相似 scenario green，就当 narrow law 已覆盖；
- ladder 变成固定 checklist，change 明明不触及某层也强跑；
- 为了速度永久跳过 contract/replay，只在 release 时一次性承受巨大 diagnosis cost。

## 验证

先为每个 changed promise 写一行：**哪种 defect 能破坏它，最低哪层能 faithful 区分这个 defect？** 这就是 applicable rung。

验证策略本身也应有 feedback：如果 broad E2E 抓到一个本可在 unit/contract 层表达的 bug，把 regression 下沉到最低 faithful rung，未来不要继续靠昂贵 broad test 独自守它。

反之，若 failure 只在真实 Host/provider 出现，不要强行用越来越复杂的 mock 模拟；保留 local proof，再加小 canary。

## 完成条件

完成时高层测试不再替低层证明，而是在已知 local truths 之上增加 composition/realism evidence。失败也更容易定位，因为每一层只剩自己独有的 uncertainty。

> 证据梯子的价值，不是多跑命令，而是把“哪里可能错”一层层缩小，再把“真实世界是否同意”最后交给真实世界。