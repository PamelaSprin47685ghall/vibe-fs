# half-finished-refactor — Enforcer

## 定义
当新的 ownership model 已经被引入，但旧 model 仍然 live 到需要 routing rule、compatibility adapter、duplicated state 或团队 convention 来决定“今天哪个世界才 authoritative”，refactor 就烂尾了。

Code 搬了。Authority 没搬完。

## 支配原则
Structural refactor 只有在 post-refactor model 成为 repository 的普通真相时才算结束。

引入新 service/module/type，却“为了安全”保留 old writer，会制造 dual constitution。每个 caller 又必须回答一个原本 refactor 就是为了消灭的问题：走 old path 还是 new path？答案随后泄漏进 flag、adapter、`if legacy`、call-site folklore、mirrored tests 与 reconciliation code。

Transition state 合法。永久 transition 是 architecture failure。

## 何时触发
当一个本来要替换 ownership 的 refactor 完成后，old/new internal model 仍同时 authoritative 时触发。常见形式：

- repository-owned callers 一半 legacy、一半 new，没有 real external compatibility requirement；
- old/new modules 都能 mutate 同一个 semantic fact；
- adapter 每次 call 都在 old/new representation 间翻译，而 migration 永不结束；
- rollout uncertainty 已结束很久，feature flag 仍用来选择两套 architecture；
- legacy/new paths 各自一套 tests，并被预期无限期同时 green；
- 新 code 从一个 source of truth 读，但 old callback/job 仍写另一个；
- alias 永久 re-export old names，vocabulary 永不 converge；
- refactor 停在“以后新 code 都用 X”，旧 path 仍是 first-class route，没人负责删除。

## 不应触发
- Bounded migration window 确实需要同时支持 old/new external consumers，而且有真实 exit condition。
- Blue/green / rolling deployment 暂时要求 process boundaries 两个 version 共存。
- Historical durable decode 为 recovery 保留，但 current writes/ownership 已完全 converge。
- 最初以为 old/new 重复，后来证明二者实际拥有 distinct responsibilities；此时 coexistence 不是 transition。
- Refactor 本来就只 scope 一个 subsystem，scope 外 untouched owner 仍然正确。

## 与相邻规则区分
`compatibility-cruft` 可以发生在 internal ownership 已收敛之后，只是 old external shape 仍被接受。`half-finished-refactor` 的关键是 internal authority 本身没收敛。

`facade-hides-mess` 经常把这种状态藏在 clean API 后，用 facade 在 old/new path 之间 routing。`duplicated-truth` 可能是症状——两边存同一 fact；本规则命名导致它的 unfinished ownership transfer。

## 判定程序
用一句话写出 intended post-refactor ownership：

> Refactor 完成后，X 单独拥有 decision/state Y。

然后搜索所有 repository-controlled read/write/call path。

任何 ordinary path 仍需要 old owner 时，问它是否由 external contract 或 bounded migration condition 真正要求。不是，就说明 refactor 未完成。

特别检查 background job、callback、test、alias、recovery path、generated binding、temporary flag；mainline caller 迁完以后，旧 authority 最爱藏在这些角落。

## 例子
- positive：引入 `NewSessionStore`，但 retry path 仍写 `LegacySessionCache`，于是又加 synchronizer 保持两边一致。
- positive：所有新 caller 都用 `newExecute`，old `execute` 仍 export，一半 tests 继续跑它“for compatibility”，实际没有 external consumer。
- positive：rollout 已结束几个月，feature flag 仍选择 old/new persistence，两套 schema 继续收 write。
- near-miss：rolling deployment 短期 dual-read，直到所有 old nodes drain；fleet convergence 就是 removal condition。
- counterexample：internal callers/writers 全迁到 one owner，只在 recovery ingress 保留 historical v1 decode。

## Nudge
Refactor 不是“新世界已经存在”就结束。

它结束于 repository 不再需要记得怎样生活在旧世界。
