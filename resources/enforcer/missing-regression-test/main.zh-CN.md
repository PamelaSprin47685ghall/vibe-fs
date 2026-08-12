# missing-regression-test — Main

把 defect 变成 executable memory。

重现仍包含 original failure causal ingredient 的最小 scenario，通过真正 owning behavioral boundary 进入，并断言 caller-visible wrong/right outcome。

不要从 patch 开始，要从 incident 开始：

```text
当时有哪些条件？
本来应该发生什么？
实际上发生了什么？
哪个 observable 能区分二者？
```

然后证明 test 真能抓 old behavior。Production 已修好时，可以临时 revert/mutate 对应 mechanism，或在 controlled branch 构造旧 outcome。直到 old defect 能把 test 打红，regression 才算成立。

保留 cause，不保留 noise。真实 incident 可能有巨大 log、多 service、timing、无关 retry、历史 residue。只有识别出哪些 fact load-bearing 后才能删 noise。一个很小但已经不再重现 cause 的 test，比稍大但 faithful 的 test 更差。

常见假修复：

- 新 test 只 call fix 新增的 helper，old code 根本没法跑这条 test；
- 断言新 field/type 存在，而不是用户丢失的 behavior；
- snapshot repaired internal structure，不看 bug 的 public consequence；
- input 看起来类似，却漏掉 stale/cancelled/duplicate/versioned 等真正 trigger；
- 用 comment / issue link 代替 executable memory；
- ticket 里留 manual repro command，suite 不接管；
- 写 stress loop 偶尔碰 old race，而不是 deterministic control schedule。

Boundary defect 就把 regression 放在失败的 boundary；serializer/protocol bug 把 incompatible bytes/identity 作为 fixture；recovery bug 在真实 transition fault/crash；concurrency bug 用 barrier 固定 causal order；timezone bug freeze exact instant/zone；duplicate-delivery bug 用同 identity replay。

如果 defect 暴露更广的 law，concrete regression 与 strengthened property/contract 都要留。Concrete counterexample 记录人类真正付过成本学到的事实，property 保护周围 input space。

验证很直接：old defect red，repair green；再做 semantics-preserving refactor，确保 test 不依赖 patch decomposition。

完成时，同一个 material failure 只要被重新引入，就必定在 delivery 前打红 test。

> Fix 修今天的代码；regression test 修项目的记忆。