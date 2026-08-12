# recovery-by-filesystem-state — Enforcer

Recovery by filesystem state 的问题，不是“用了 filesystem”，而是 restart logic 根据**偶然 path residue**推断 business/lifecycle progress，而这些 artifact 从来没被设计成承载那条事实的 durable protocol。

File 完全可以就是 authoritative store。真正缺陷更具体：**presence、absence、filename、directory shape、temp residue、worktree layout 被要求表达超出 storage contract 的语义。**

Temp dir 可能 validation 前就创建；lock file crash 后可能残留；worktree 在 integration fail 后可能还在；rename 是否发生可能只是 cleanup 顺序。它们记录 execution topology 的碎片，不天然等于 semantic milestone。

以下判断一出现就值得触发：

- “directory exists，所以 job 已 create/commit”；
- “temp file 消失，所以 publish 完成”；
- “worktree branch 在，所以 integration 成功”；
- “`.done` file 在，所以 external effect 发生”；
- “lock file 还在，所以 owner 还活着”；
- “filename 以 `failed-` 开头，所以 workflow failed”；
- “目录里有 N 个文件，所以 phase N 已结束”。

最常见根因是**accidental commit point**。某个 implementation artifact 恰好在 semantic transition 附近创建，于是 recovery 把这种时间上的邻近误当 protocol。后来 refactor 一改 creation/cleanup order，recovery semantics 就静默改变，domain code 却完全没变。

不要误杀真正 file-backed store。`state.json` 如果通过 atomic rename commit，拥有 version/schema/checksum 和正式 recovery semantics，它当然可以 own lifecycle truth。SQLite 物理上也是 file，但 transaction contract 显然不等于 incidental directory residue。区别只在：contents/commit protocol 是否**按设计就是 authority**。

Path existence 若只用于 discovery 也没问题：找到 file 后，真正 decision 来自解析后的 versioned durable record，而不是“文件在那里”本身。

邻近规则：

- `log-as-recovery-protocol`：diagnostic prose 被抬成 restart authority；
- `snapshot-as-truth`：derived projection 高于 source facts；
- `leftover-scaffolding`：artifact 残留，但 recovery 不一定依赖；
- `resource-not-scoped`：lifetime leak 留下 residue。

只有当最准确的描述是这句时才用本规则：**incidental artifact topology 正在决定 workflow 相信发生过什么。**

最强 crash exercise：在每次 artifact create/remove 前后都停进程。如果同一种 path shape 可以对应两种不同 semantic reality，这种 shape 就不配做 recovery fact。

健康 protocol 应直接命名 milestone：`JobAccepted`、`PublishCommitted`、`IntegrationCompleted`、`OwnerLeaseExpiresAt`、versioned state row、journal event、transaction status。Artifact 可以被这些 fact 引用，但不能替代 fact。

> Path 最多能证明 path 存在。除非 protocol 明确把“存在”定义成 commit，否则它证明不了 business transition。