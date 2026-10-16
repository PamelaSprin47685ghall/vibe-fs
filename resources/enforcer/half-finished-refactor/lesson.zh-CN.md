# half-finished-refactor — Lesson

## 评价

### 定义

当新的 ownership model 已经被引入，但旧 model 仍然 live 到需要 routing rule、compatibility adapter、duplicated state 或团队 convention 来决定“今天哪个世界才 authoritative”，refactor 就烂尾了。

Code 搬了。Authority 没搬完。

### 支配原则

Structural refactor 只有在 post-refactor model 成为 repository 的普通真相时才算结束。

引入新 service/module/type，却“为了安全”保留 old writer，会制造 dual constitution。每个 caller 又必须回答一个原本 refactor 就是为了消灭的问题：走 old path 还是 new path？答案随后泄漏进 flag、adapter、`if legacy`、call-site folklore、mirrored tests 与 reconciliation code。

Transition state 合法。永久 transition 是 architecture failure。

### 何时触发

当一个本来要替换 ownership 的 refactor 完成后，old/new internal model 仍同时 authoritative 时触发。常见形式：

- repository-owned callers 一半 legacy、一半 new，没有 real external compatibility requirement；
- old/new modules 都能 mutate 同一个 semantic fact；
- adapter 每次 call 都在 old/new representation 间翻译，而 migration 永不结束；
- rollout uncertainty 已结束很久，feature flag 仍用来选择两套 architecture；
- legacy/new paths 各自一套 tests，并被预期无限期同时 green；
- 新 code 从一个 source of truth 读，但 old callback/job 仍写另一个；
- alias 永久 re-export old names，vocabulary 永不 converge；
- refactor 停在“以后新 code 都用 X”，旧 path 仍是 first-class route，没人负责删除。

### 不应触发

- Bounded migration window 确实需要同时支持 old/new external consumers，而且有真实 exit condition。
- Blue/green / rolling deployment 暂时要求 process boundaries 两个 version 共存。
- Historical durable decode 为 recovery 保留，但 current writes/ownership 已完全 converge。
- 最初以为 old/new 重复，后来证明二者实际拥有 distinct responsibilities；此时 coexistence 不是 transition。
- Refactor 本来就只 scope 一个 subsystem，scope 外 untouched owner 仍然正确。

### 与相邻规则区分

`compatibility-cruft` 可以发生在 internal ownership 已收敛之后，只是 old external shape 仍被接受。`half-finished-refactor` 的关键是 internal authority 本身没收敛。

`facade-hides-mess` 经常把这种状态藏在 clean API 后，用 facade 在 old/new path 之间 routing。`duplicated-truth` 可能是症状——两边存同一 fact；本规则命名导致它的 unfinished ownership transfer。

### 判定程序

用一句话写出 intended post-refactor ownership：

> Refactor 完成后，X 单独拥有 decision/state Y。

然后搜索所有 repository-controlled read/write/call path。

任何 ordinary path 仍需要 old owner 时，问它是否由 external contract 或 bounded migration condition 真正要求。不是，就说明 refactor 未完成。

特别检查 background job、callback、test、alias、recovery path、generated binding、temporary flag；mainline caller 迁完以后，旧 authority 最爱藏在这些角落。

### 例子

- positive：引入 `NewSessionStore`，但 retry path 仍写 `LegacySessionCache`，于是又加 synchronizer 保持两边一致。
- positive：所有新 caller 都用 `newExecute`，old `execute` 仍 export，一半 tests 继续跑它“for compatibility”，实际没有 external consumer。
- positive：rollout 已结束几个月，feature flag 仍选择 old/new persistence，两套 schema 继续收 write。
- near-miss：rolling deployment 短期 dual-read，直到所有 old nodes drain；fleet convergence 就是 removal condition。
- counterexample：internal callers/writers 全迁到 one owner，只在 recovery ingress 保留 historical v1 decode。

## 建议

### 现在该做什么

把 authority transfer 做完。

选定 post-refactor owner，把每一个 repository-controlled caller/writer 迁过去，删除 obsolete adapter/alias/flag/duplicate state，并让 old internal path 无法再被“顺手”使用。

不要把 transition 稳定下来。结束它。

### 为什么重要

Half-finished refactor 往往比它要替换的旧 architecture 更糟。

旧系统即使难看，至少只有一套 rule。Transitional system 有两套 rule，再加 routing logic、synchronization、migration convention，以及“到底哪边 canonical”的 uncertainty。以后每个 bug 都多一个维度：它来自 old world、new world，还是 seam？

团队经常在 new path 工作正常、mainline callers 已迁以后停止。但 background job、recovery code、alias、test、rare callback 会让 old authority 再活很多年。最后 transition 成了没人真正设计过的 permanent architecture。

Session boundary 不会改变这个 completion criterion。如果你还能说出一个要留给“next session”的 repository-controlled migration step，而且自己仍有 authority 去做，那么你刚刚提供了 refactor 仍然活着的 evidence。productive checkpoint 或 clean handoff 是有价值的 progress，不是 refactor closure。

### 修复策略

把 refactor 当 ownership migration，并明确 closure checklist：

1. 写出 target ownership rule；
2. 枚举触碰 old owner 的 readers、writers、callers、exports、callbacks、jobs、recovery paths、tests、generated surfaces；
3. 区分 repository-controlled 与真实 external compatibility obligation；
4. repository-controlled paths 全部迁完；
5. 合法 compatibility 只留在 boundary；
6. migration condition 达成后停止 dual write/duplicated state；
7. 删除 obsolete aliases/flags/adapters/tests；
8. 如果 old path 很容易重新被引入，加 architecture/test gate 防回流。

在 closed repository 内，优先 deletion，而不是永久 `deprecated`。没有 external consumer 的 deprecation，往往只是把 ownership 决定推迟。

### 决策分支

- **Old path 没 external consumer：**迁完 internal caller 后直接删。
- **Real external consumer 仍存在：**保留 narrow compatibility adapter + exit condition；internal model 仍保持 singular。
- **Rolling deployment 需要 dual behavior：**编码 fleet/version convergence，之后删 transition machinery。
- **Historical recovery 需要 old shape：**保留 decode-only boundary，不保留 old writer/owner。
- **Old/new 实际 responsibility 不同：**明确 rename/reframe 成两个 distinct owners，不要继续假装一个在替换另一个。
- **一次迁移风险太高：**允许 staged migration，但每一 stage 都必须减少 remaining old authority，并保留 concrete completion criterion。

### 常见假修复

- 加 synchronizer，让 old/new sources 永远都能 write。
- 用 facade 藏 routing，然后说 migration complete。
- 所有 callers 都迁了还保留 aliases “方便发现”，让 old vocabulary 永远活着。
- Rollout 完成后因“删 flag 有风险”永久保留 feature flag。
- 没 supported compatibility contract，却把每个 new test 复制一份跑 old implementation。
- 给 old code 标 deprecated，却不给 owner 或 deletion condition。
- Documentation 全改成 new path，但 compiler/runtime 仍完全接受 old path。

### 验证

用 structure 证明 convergence，不要靠意图：

- repository search 找不到 uncontrolled old-owner caller/writer；
- old exports/names 除 explicit compatibility boundary 外已消失；
- current state 只写一个 canonical representation；
- transition flags/adapters 已删，或确实绑定仍 live 的 bounded migration condition；
- tests 只保护 one internal truth，legacy cases 只存在于 external/historical contract boundary；
- 条件允许时，重新引入 old-path call 会被 type/module/architecture constraint 抓住。

Invariant：

> Current system 内，一个 semantic fact 只有一个 post-refactor owner。

### 完成条件

Migration machinery 已经没有任何东西需要仲裁。

New architecture 不再只是“recommended”。它就是 architecture。

### Nudge

Refactor 不是“新世界已经存在”就结束。

它结束于 repository 不再需要记得怎样生活在旧世界。
