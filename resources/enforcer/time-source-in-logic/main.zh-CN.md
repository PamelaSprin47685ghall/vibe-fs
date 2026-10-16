# time-source-in-logic — Main

## 现在该做什么

把 clock observation 移到 shell，把 temporal judgment 留给 policy。

最常见修法是在 operation owning boundary 读取一次 time，明确 normalise timezone/precision，然后把这个 instant 当普通 input 传入 domain：

```text
asOf = clock.now()
result = decide(state, command, asOf)
```

这样相同 declared inputs 会产生相同 decision；event/replay 也能记录当时真正依据的 temporal fact。

如果一个长期 operation 确实需要多次观察时间，不要为了“pure”硬塞一个初始 instant。注入窄 clock port，让 capability 显式存在，并规定哪些 transition 有资格读取它。显式 effect 比假纯洁更诚实。

## 为什么重要

隐藏的时间让相同的 visible inputs 在现实中不再相同。这会破坏 deterministic tests、event replay、incident reconstruction，以及“为什么这次 deadline/eligibility decision 与上次不同”的解释。

## 修复策略

在 owning boundary 读一次 clock，在那里 normalise zone/precision，把 instant 穿过 pure policy。只有当一个 operation 内多次观察在语义上必要，才用 clock port。

## 决策分支

如果 decision 需要 temporal fact，把它加进函数 inputs，让 core 保持 deterministic。如果 operation 内多次观察在语义上必要，注入 clock port，而不是在 policy 里读 ambient clock。

## 常见假修复

常见假修复：

- 只在 test monkey-patch global `now()`，production architecture 完全不动；
- 一半 call site 传 `asOf`，另一半 deep logic 继续偷读 system clock；
- 把 startup time 存进 hidden singleton，之后所有 domain call 读它，就声称“固定了时间”；
- 每个 layer 各自读一次 now，最后用毫秒差异解释不同 outcome；
- 只传 date string，却把 timezone/precision 继续留给 ambient defaults；
- 为了可测试把整套 Clock service 注入所有函数，连只需要一个 instant 的 pure decision 都背上 capability。

## 验证

验证要证明 replay。固定 state/command/instant，多次运行得到完全相同 core outcome；明确改变 instant，只在 temporal law 的 boundary（expiry 前/到点/后等）改变结果。

事件若需要解释历史，就记录足够的 temporal provenance：是 command observed-at、deadline、lease expiry、还是 business effective date。不要只记“expired=true”，否则以后仍无法回答当时为什么过期。

还要测 timezone/precision normalization 的 owning boundary，防止不同 adapter 给 core 传语义上不一致的 instant。

如果只需要一个 system-clock adapter smoke，单独测它即可，不要让整个 domain suite重新依赖 real clock。

## 完成条件

完成时，每条 time-sensitive decision 都能从 recorded/declared inputs 重放；阅读函数签名即可知道 time 是依赖，而不是靠 grep `Date.now` 才发现。

> 显式时间不是为了让代码更“函数式”，而是为了让过去的 decision 在未来仍有可证明的时间坐标。