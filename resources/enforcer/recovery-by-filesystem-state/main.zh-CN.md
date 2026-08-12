# recovery-by-filesystem-state — Main

把 recovery truth 移进显式 durable protocol。

对每个当前依赖 path shape 的 restart decision，先说清它真正想推断哪个 semantic fact，再在真实 commit point 把这个 fact 写进具备正式 atomicity/durability semantics 的 store。

坏推断：

```text
worktree exists → job 一定 started
.done file exists → publish 一定 committed
temp file absent → cleanup 一定 finished
```

更诚实的 protocol：

```text
JobAccepted(jobId, ...)
PublishCommitted(publicationId, ...)
CleanupCompleted(resourceId, ...)
```

或者等价的 versioned state record / transaction。

Artifact 仍然可以存在。Durable fact 可以引用 worktree、blob、temp directory、branch、file generation。但 restart 时“发生过什么”由 record 回答；artifact 只回答更窄的问题，例如 bytes 在哪里、cleanup 是否还有资源要做。

如果某个 file 本来就想当 store，就把它做成真正 store：

- versioned schema；
- 明确 commit protocol，例如平台 contract 支持时的 write-temp + fsync + atomic rename；
- corruption/substitution 重要时有 checksum/digest；
- generation/owner identity；
- absent / old-version / corrupt 的定义行为；
- sibling filename 不再偷偷携带 lifecycle semantics。

常见假修复：

- 再发明更多 filename prefix：`pending-`、`done-`、`failed-`；
- 比 mtime 猜哪个 phase 后发生；
- 造 sentinel file 却没有 atomic commit semantics，就叫“durable event”；
- journal 也写了，但 recovery 因 migration 麻烦继续读 filesystem heuristic；
- 更积极清 stale artifact，却不移除它们的 semantic authority；
- 把 lifecycle status 编进 path name，让 rename order 变成隐藏 state machine；
- PID/lock file 里有进程号，就认定那个进程现在仍是合法 owner。

验证要故意制造 misleading residue：

- artifact 已创建，但 semantic commit 未到；
- semantic commit 已到，但 cleanup artifact 还在；
- previous generation/session 留下旧 artifact；
- partially initialized directory；
- implementation path 被 rename/reorganize；
- crash 后 stale lock/PID file；
- lifecycle fact 已 commit，但 cache artifact 缺失。

Recovery 必须跟 explicit durable fact 走，再按这些 fact 决定 ignore/validate/reuse/cleanup artifact。纯粹 rename implementation directory 不应改变 lifecycle meaning。

还要测反方向：真实 durable lifecycle record corrupt/missing 时，recovery 应按 store contract fail/reconcile，不能偷偷从 residue “恢复 truth”，那会掩盖真正 authority 已丢失。

完成时每个 restart decision 都能引用一个 typed/versioned durable fact；filesystem topology 退回 data/resource evidence，除非那个 file 本身就是正式设计的 store。

> Recovery 应读取 commitment，不应做 archaeology。