# recovery-by-filesystem-state — Lesson

## 评价

### 定义

Recovery by filesystem state 的问题，不是“用了 filesystem”，而是 restart logic 根据**偶然 path residue**推断 business/lifecycle progress，而这些 artifact 从来没被设计成承载那条事实的 durable protocol。

File 完全可以就是 authoritative store。真正缺陷更具体：**presence、absence、filename、directory shape、temp residue、worktree layout 被要求表达超出 storage contract 的语义。**

### 支配原则

最常见根因是**accidental commit point**。某个 implementation artifact 恰好在 semantic transition 附近创建，于是 recovery 把这种时间上的邻近误当 protocol。后来 refactor 一改 creation/cleanup order，recovery semantics 就静默改变，domain code 却完全没变。

### 何时触发

以下判断一出现就值得触发：

- “directory exists，所以 job 已 create/commit”；
- “temp file 消失，所以 publish 完成”；
- “worktree branch 在，所以 integration 成功”；
- “`.done` file 在，所以 external effect 发生”；
- “lock file 还在，所以 owner 还活着”；
- “filename 以 `failed-` 开头，所以 workflow failed”；
- “目录里有 N 个文件，所以 phase N 已结束”。

### 不应触发

不要误杀真正 file-backed store。`state.json` 如果通过 atomic rename commit，拥有 version/schema/checksum 和正式 recovery semantics，它当然可以 own lifecycle truth。SQLite 物理上也是 file，但 transaction contract 显然不等于 incidental directory residue。区别只在：contents/commit protocol 是否**按设计就是 authority**。

Path existence 若只用于 discovery 也没问题：找到 file 后，真正 decision 来自解析后的 versioned durable record，而不是“文件在那里”本身。

### 与相邻规则区分

邻近规则：

- `log-as-recovery-protocol`：diagnostic prose 被抬成 restart authority；
- `snapshot-as-truth`：derived projection 高于 source facts；
- `leftover-scaffolding`：artifact 残留，但 recovery 不一定依赖；
- `resource-not-scoped`：lifetime leak 留下 residue。

### 判定程序

只有当最准确的描述是这句时才用本规则：**incidental artifact topology 正在决定 workflow 相信发生过什么。**

最强 crash exercise：在每次 artifact create/remove 前后都停进程。如果同一种 path shape 可以对应两种不同 semantic reality，这种 shape 就不配做 recovery fact。

健康 protocol 应直接命名 milestone：`JobAccepted`、`PublishCommitted`、`IntegrationCompleted`、`OwnerLeaseExpiresAt`、versioned state row、journal event、transaction status。Artifact 可以被这些 fact 引用，但不能替代 fact。

### 例子

Temp dir 可能 validation 前就创建；lock file crash 后可能残留；worktree 在 integration fail 后可能还在；rename 是否发生可能只是 cleanup 顺序。它们记录 execution topology 的碎片，不天然等于 semantic milestone。

## 建议

### 现在该做什么

把 recovery truth 移进显式 durable protocol。

对每个当前依赖 path shape 的 restart decision，先说清它真正想推断哪个 semantic fact，再在真实 commit point 把这个 fact 写进具备正式 atomicity/durability semantics 的 store。

### 为什么重要

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

### 修复策略

如果某个 file 本来就想当 store，就把它做成真正 store：

- versioned schema；
- 明确 commit protocol，例如平台 contract 支持时的 write-temp + fsync + atomic rename；
- corruption/substitution 重要时有 checksum/digest；
- generation/owner identity；
- absent / old-version / corrupt 的定义行为；
- sibling filename 不再偷偷携带 lifecycle semantics。

### 决策分支

Recovery 必须跟 explicit durable fact 走，再按这些 fact 决定 ignore/validate/reuse/cleanup artifact。纯粹 rename implementation directory 不应改变 lifecycle meaning。

还要测反方向：真实 durable lifecycle record corrupt/missing 时，recovery 应按 store contract fail/reconcile，不能偷偷从 residue “恢复 truth”，那会掩盖真正 authority 已丢失。

### 常见假修复

常见假修复：

- 再发明更多 filename prefix：`pending-`、`done-`、`failed-`；
- 比 mtime 猜哪个 phase 后发生；
- 造 sentinel file 却没有 atomic commit semantics，就叫“durable event”；
- journal 也写了，但 recovery 因 migration 麻烦继续读 filesystem heuristic；
- 更积极清 stale artifact，却不移除它们的 semantic authority；
- 把 lifecycle status 编进 path name，让 rename order 变成隐藏 state machine；
- PID/lock file 里有进程号，就认定那个进程现在仍是合法 owner。

### 验证

验证要故意制造 misleading residue：

- artifact 已创建，但 semantic commit 未到；
- semantic commit 已到，但 cleanup artifact 还在；
- previous generation/session 留下旧 artifact；
- partially initialized directory；
- implementation path 被 rename/reorganize；
- crash 后 stale lock/PID file；
- lifecycle fact 已 commit，但 cache artifact 缺失。

### 完成条件

完成时每个 restart decision 都能引用一个 typed/versioned durable fact；filesystem topology 退回 data/resource evidence，除非那个 file 本身就是正式设计的 store。

> Recovery 应读取 commitment，不应做 archaeology。

### Nudge

> Path 最多能证明 path 存在。除非 protocol 明确把“存在”定义成 commit，否则它证明不了 business transition。
