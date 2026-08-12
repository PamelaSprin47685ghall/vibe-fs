# coverage-theater — Main

## 现在该做什么
停止优化数字，先写清楚：**什么行为必须变得难以被破坏。**

对每个重要 test，把“遍历到了代码”的 assertion 改成现实 defect 能够反驳的 proposition：具体 identity、authorization、error semantics、ordering、durable state、idempotence、cancellation、boundary translation，或者 caller 真正依赖的其他 invariant。

Coverage 可以继续当地图。不要再让地图冒充领土。

## 为什么重要
Coverage theater 最危险的地方，是它让团队在 test 变得越来越不具攻击性时，反而越来越安心。

好 test 应当制造阻力：有些 implementation 即使把每一行都跑过，也必须被拒绝。如果成功主要按“执行了多少代码”衡量，作者就会逐渐写出陪伴 implementation 的 test，而不是挑战 implementation 的 test。

最后得到的是一套非常昂贵、但 defect-detection power 很低的 suite：refactor 因为 test 多而变慢，regression 却仍能逃出去，因为真正保护 meaning 的 assertion 太少。

## 修复策略
从 contract 向内走：

1. 命名 caller-visible promise / invariant；
2. 命名一个会破坏它的 plausible defect；
3. 选择能暴露这个区别的最小 input；
4. 断言 observable consequence；
5. 最后才看 coverage，寻找相邻的 unvisited risk。

如果 test 使用 mock，先问 mock interaction 本身是不是 contract。如果不是，就断言这个 interaction 理应造成的 outcome。只有当“恰好调用一次”本身就是业务/系统保证时，call count 才是有效 evidence。

Snapshot 只有在 review 能指出其中哪些字段具有 semantic significance 时，才算一种压缩 assertion。每次变化都产生巨大 opaque diff、大家只会重新生成的 snapshot，应拆小或替换。

## 决策分支
- **只有 truthiness / non-null / no-throw：**加强到 caller 真正需要的具体 result 或 invariant。
- **Mock choreography 占主导：**除非 interaction 自身就是 public guarantee，否则向外移动到 stable contract。
- **Coverage threshold 正在制造垃圾 test：**先让 meaningful tests 拥有重要 behavior，再决定 metric 是否保留或调整。不要为了百分比发明 test。
- **Uncovered code 实际不可达/已死：**删掉它，不要写 ceremonial test 给 dead code 上色。
- **High-risk branch 未覆盖：**把报告当线索，然后写一个可证伪的 behavioral test。

## 常见假修复
- 新增只 instantiate class、调用 method、检查 value defined 的 test。
- 把所有 private helper call 都 assert 一遍，让 suite 变成当前 implementation 的镜像。
- 大规模生成/更新 snapshot，却没人能陈述它保护的 contract。
- 单纯降低 threshold 然后宣布问题解决。坏 metric 当然可以删，但删 metric 不会自动创造缺失 evidence。
- 在 trivial glue 上追求 100% branch coverage，却让一个真正 causal boundary 几乎没测试。
- 庆祝 mutation score、coverage%、test count，却说不出这些数字到底保护哪些 system invariant。

## 验证
针对 test 声称保护的 property，做一个 deliberate semantic mutation，例如：

- 对调两个 domain ID；
- 吞掉应该暴露的 error；
- persistence 之前就 publish；
- 接受 unauthorized caller；
- 返回 stale state；
- 丢掉 cancellation；
- 反转 required order。

相关 test 必须因为正确理由变红。

Invariant：

> 重要 test 会拒绝现实可发生的错误行为；coverage 只是这些问题被认真提出后的副产品。

## 完成条件
团队可以不用引用任何百分比，就解释为什么相信相关 behavior。

Coverage 可以继续有用，但再也不需要靠它假装 execution 本身就是 verification。
