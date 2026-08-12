# order-dependent-test — Main

让每条 test 自己拥有 premise。

对 case 依赖的每份 mutable input，只选三种诚实模型之一：

1. **local ownership**：test 自己 create，自己 cleanup；
2. **isolated lease/namespace**：昂贵 infrastructure 可共享，但每条 test 拿独立 key/schema/transaction/state；
3. **one explicit scenario**：步骤确实属于同一 lifecycle，就合成一个 ordered test，不再伪装成几条 independent case。

先盘点 hidden state：

- module/static mutable value；
- singleton registry/cache；
- environment variable；
- process cwd/locale/provider preference；
- global fake clock/random seed/ID counter；
- reused file/temp dir/worktree；
- database row/schema；
- port/process/subscription；
- stateful mock 与 captured call cursor。

然后在 test boundary scope/reset。能用 fresh identity 时优先 fresh identity：unique session ID、per-test temp dir、每 case rollback transaction、namespaced key、fresh runtime instance，通常比“全局 resetEverything()”更可靠。

Global 必须 mutate 时，用 `try/finally` / scoped helper 保存并恢复，让 test 自己失败也不能把 mutation 泄漏给下一条。

常见假修复：

- 强制 alphabetical/numeric order；
- 把 suite 改成 serial / `--runInBand`，hidden shared state 完全不动；
- 因 reorder 偶尔坏就加 retry；
- 把更多 mutable setup 塞进 `beforeAll`；
- 用一个巨大 `afterAll` 收尾，导致中途 fail 会污染后面所有 case；
- 只 reset DB，env/cwd/cache/static registry/fake clock 继续漏；
- 把 order dependence 叫“integration realism”，但真实产品根本不要求这些 test 共用 lifecycle。

Serial execution 可以是合法 resource choice，但不是 isolation proof。只要 case semantics 仍要求另一 case 先跑，proposition 就不是 local；runner 只是把问题稳定地藏起来。

验证要故意攻击顺序：

```text
run case alone
run it first
run it last
randomize order
parallelize where architecture permits
让 neighbor 在 setup/cleanup 中途失败
```

只要 suite 声称这种 ordering 合法，case 的 meaning 与 verdict 就必须不变。

如果两条 case 真的是一条 causal story，就把它们合并，把 order 写进 scenario。本来就有依赖时，一个较长但诚实的 test，比两条靠 invisible residue 串起来的“短 test”更好。

还要检查 failure isolation：某 case 中途 throw，也必须 release/restore 自己拥有的东西，否则一条真实红会制造后面一串误导性红。

完成时，每条 test 都能在解释 premise 时完全不说“假设 test X 已经跑过”；suite scheduling 只影响 throughput，绝不影响 semantics。

> Test order 可以决定 evidence 什么时候被收集，不能决定 evidence 是什么意思。