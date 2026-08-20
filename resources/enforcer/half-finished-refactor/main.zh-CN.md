# half-finished-refactor — Main

## 现在该做什么
把 authority transfer 做完。

选定 post-refactor owner，把每一个 repository-controlled caller/writer 迁过去，删除 obsolete adapter/alias/flag/duplicate state，并让 old internal path 无法再被“顺手”使用。

不要把 transition 稳定下来。结束它。

## 为什么重要
Half-finished refactor 往往比它要替换的旧 architecture 更糟。

旧系统即使难看，至少只有一套 rule。Transitional system 有两套 rule，再加 routing logic、synchronization、migration convention，以及“到底哪边 canonical”的 uncertainty。以后每个 bug 都多一个维度：它来自 old world、new world，还是 seam？

团队经常在 new path 工作正常、mainline callers 已迁以后停止。但 background job、recovery code、alias、test、rare callback 会让 old authority 再活很多年。最后 transition 成了没人真正设计过的 permanent architecture。

Session boundary 不会改变这个 completion criterion。如果你还能说出一个要留给“next session”的 repository-controlled migration step，而且自己仍有 authority 去做，那么你刚刚提供了 refactor 仍然活着的 evidence。productive checkpoint 或 clean handoff 是有价值的 progress，不是 refactor closure。

## 修复策略
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

## 决策分支
- **Old path 没 external consumer：**迁完 internal caller 后直接删。
- **Real external consumer 仍存在：**保留 narrow compatibility adapter + exit condition；internal model 仍保持 singular。
- **Rolling deployment 需要 dual behavior：**编码 fleet/version convergence，之后删 transition machinery。
- **Historical recovery 需要 old shape：**保留 decode-only boundary，不保留 old writer/owner。
- **Old/new 实际 responsibility 不同：**明确 rename/reframe 成两个 distinct owners，不要继续假装一个在替换另一个。
- **一次迁移风险太高：**允许 staged migration，但每一 stage 都必须减少 remaining old authority，并保留 concrete completion criterion。

## 常见假修复
- 加 synchronizer，让 old/new sources 永远都能 write。
- 用 facade 藏 routing，然后说 migration complete。
- 所有 callers 都迁了还保留 aliases “方便发现”，让 old vocabulary 永远活着。
- Rollout 完成后因“删 flag 有风险”永久保留 feature flag。
- 没 supported compatibility contract，却把每个 new test 复制一份跑 old implementation。
- 给 old code 标 deprecated，却不给 owner 或 deletion condition。
- Documentation 全改成 new path，但 compiler/runtime 仍完全接受 old path。

## 验证
用 structure 证明 convergence，不要靠意图：

- repository search 找不到 uncontrolled old-owner caller/writer；
- old exports/names 除 explicit compatibility boundary 外已消失；
- current state 只写一个 canonical representation；
- transition flags/adapters 已删，或确实绑定仍 live 的 bounded migration condition；
- tests 只保护 one internal truth，legacy cases 只存在于 external/historical contract boundary；
- 条件允许时，重新引入 old-path call 会被 type/module/architecture constraint 抓住。

Invariant：

> Current system 内，一个 semantic fact 只有一个 post-refactor owner。

## 完成条件
Migration machinery 已经没有任何东西需要仲裁。

New architecture 不再只是“recommended”。它就是 architecture。
