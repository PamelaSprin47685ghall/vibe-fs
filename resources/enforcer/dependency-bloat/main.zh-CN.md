# dependency-bloat — Main 中文版

## 现在该做什么
列出 dependency 实际替项目拥有的 capability，再把 transitive graph、升级频率、安全面、runtime/build requirement、未来 migration 一起计价。若 platform 或一个小直接实现已经足够，删除依赖；若外部复杂度确实值得外包，就保留并缩窄使用边界。

## 为什么这很重要
一个 dependency 很少只是一组函数。它会进入 lockfile、CVE 流程、bundling、runtime compatibility、license、upgrade cadence 和 incident surface。短期少写 30 行，可能换来几年维护一个你并不需要理解的 ecosystem。

## 常见假修复
- 因为“依赖坏”而手写 crypto/parser/protocol；这是把真正困难复杂度抢回自己。
- 只看 direct dependency count，不看 transitives 与 runtime obligations。
- 已决定保留后让 package types 穿透整个 domain，扩大退出成本。
- fork dependency 后不再升级；债务只是从外部变成本地。

## 验证
对每个新增/保留 dependency 能清楚回答：它替我们拥有的复杂度是什么？如果删除它，必须重新承担哪些真正困难的 invariant？答案若只是“少写一些 wrapper”，应重新评估。

## 完成条件
依赖关系与问题复杂度成比例；外部包承担真正值得外包的困难，而不是为了避免几行直接代码引进一整个未来。
