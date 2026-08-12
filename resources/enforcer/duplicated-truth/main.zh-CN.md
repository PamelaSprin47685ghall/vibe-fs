# duplicated-truth — Main

选一个 authority，把其他 representation 降级成 projection、cache、index、compatibility decode 或明确 bounded replica。

修复的核心不是“同步得更勤”，而是**把 write direction 变成单向**。

常见健康形状：

```text
commands → one authority → durable/current fact
                        ↓
              projections / caches / views
```

Secondary representation 可以有自己的 refresh/rebuild lifecycle，但不能自行创造同一事实的新 truth。需要改变事实时，它应发送 command/intention 回 authority，而不是直接改自己再反向同步。

若处于 migration，写清楚 creditor 与 exit：谁仍必须读旧 shape、哪边唯一接收新 writes、dual-write 是否只是机械 projection、何时删除。两套 system 都能继续写的“过渡期”如果没有截止条件，通常就是永久 multi-master architecture。

常见假修复：

- 加 bidirectional sync job；
- timestamp/mtime 更新的那份赢；
- startup 比较两边后“尽量 merge”；
- 每个 reader 自己选择更可信 source；
- 用 distributed lock 保证一次只写一边，但仍允许不同 operation 分别写不同 owner；
- 给两边都加 version，却没有一个 owner 决定合法 transition；
- 文档写“DB 是 source of truth”，代码仍从 file/cache 反向 overwrite DB。

验证要故意破坏 projection。删除、清空、写入错误值，然后从 authority rebuild；最终语义必须恢复且 source 不受污染。再尝试通过 secondary surface 修改事实：要么被拒绝，要么转化为发给 authority 的明确 command。

若真的需要 multi-master（例如某些 distributed CRDT domain），就必须有正式 merge algebra、identity、causal semantics；此时不是“两个 truth”，而是一个分布式 authority protocol。不要拿“系统分布式”当允许任意双写的借口。

完成时，每个 fact 都能回答：谁能原生写我？谁只是 derived copy？disagreement 发生时谁必须被丢弃/rebuild？答案不依赖 timestamp、启动顺序或团队口头约定。

> Synchronization 可以复制状态，不能复制 sovereignty。