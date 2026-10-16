# order-dependent-test — Lesson

## 评价

### 定义

Order-dependent test 的病，是这条 test 的 premise 有一部分不是自己声明的，而是由**suite 在它之前碰巧跑过什么**提供。

一条 test 本来应该是局部 proposition：

```text
given 这份 setup
when 发生这个 action
then 这个 observable 必须成立
```

Order dependence 会偷偷把它改成：

```text
given 这份 setup
再加上前面 tests 留下的 globals/files/rows/env/caches/ports/mocks
when 发生这个 action
then 也许成立
```

到这一步，suite 已经不是一组 independent proof，而是一台巨大、没有文档的 state machine；test runner 的 schedule 恰好成了它的 transition order。

### 支配原则


### 何时触发

以下情形触发：

- full suite 里 pass，单独跑 fail；
- first run fail，但另一 case warm cache / create data 后就 pass；
- tests 共用 mutable DB row、temp dir、process registry、singleton、mock cursor、clock、env、cwd、port、file；
- test A 的 cleanup 实际由 test B 或最终 global teardown 完成；
- `beforeAll` 建一份 mutable state，各 case 按顺序消费/修改；
- 必须强制固定 order 才稳定；
- 一开 parallelization 就暴露所谓 independent cases 其实共享 premise；
- 某 test 依赖 neighboring test 已推进 ID counter / random seed / provider preference。

### 不应触发

不要误杀**顺序本身就是 scenario**的测试。“create → approve → ship” 若就是一个业务 lifecycle，就应该作为一个显式 ordered scenario，由同一个 test 拥有完整 setup/teardown。问题在于把这些步骤拆成几条假装 independent 的 test，再让 runner order 偷偷承担 lifecycle。

Shared fixture 也不天然有罪。Immutable package data、只读 constant、昂贵但能给每个 test 独立 namespace 的 service、每 case 独立 transaction/schema 的 DB，都可以共享 infrastructure 而不共享 semantic premise。

### 与相邻规则区分

与 `flaky-test-tolerated` 区分：order dependence 是 nondeterminism 的一个具体 mechanism；后者是组织接受不稳定 verdict 的 policy failure。`mock-hidden-state` 更专门管 hidden premise 藏在 stateful mock；`resource-not-scoped` 可能解释 residue 为什么残留，但本规则关注 residue 改变了另一条 test 的真值。

### 判定程序

决定性实验不是“shuffle 两次没事就算了”，而是让 case：单独跑、第一条跑、最后跑、跟在可疑 neighbor 后、以及在合法 parallel/random order 下运行。它自己的 explicit input 不变而 verdict 改变，就说明 suite history 是 undeclared input。

修法只有两条诚实道路：让 test 自己拥有并清理所有 mutable premise；或者承认这些 operation 就是一条 lifecycle，把它们合成一个显式 scenario。不要用 filename order、数字前缀、`--runInBand`、或“这个目录别并行”去编码隐藏因果。

### 例子


## 建议

### 现在该做什么

让每条 test 自己拥有 premise。

对 case 依赖的每份 mutable input，只选三种诚实模型之一：

1. **local ownership**：test 自己 create，自己 cleanup；
2. **isolated lease/namespace**：昂贵 infrastructure 可共享，但每条 test 拿独立 key/schema/transaction/state；
3. **one explicit scenario**：步骤确实属于同一 lifecycle，就合成一个 ordered test，不再伪装成几条 independent case。

### 为什么重要

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

### 修复策略

然后在 test boundary scope/reset。能用 fresh identity 时优先 fresh identity：unique session ID、per-test temp dir、每 case rollback transaction、namespaced key、fresh runtime instance，通常比“全局 resetEverything()”更可靠。

Global 必须 mutate 时，用 `try/finally` / scoped helper 保存并恢复，让 test 自己失败也不能把 mutation 泄漏给下一条。

### 决策分支

Serial execution 可以是合法 resource choice，但不是 isolation proof。只要 case semantics 仍要求另一 case 先跑，proposition 就不是 local；runner 只是把问题稳定地藏起来。

如果两条 case 真的是一条 causal story，就把它们合并，把 order 写进 scenario。本来就有依赖时，一个较长但诚实的 test，比两条靠 invisible residue 串起来的“短 test”更好。

### 常见假修复

常见假修复：

- 强制 alphabetical/numeric order；
- 把 suite 改成 serial / `--runInBand`，hidden shared state 完全不动；
- 因 reorder 偶尔坏就加 retry；
- 把更多 mutable setup 塞进 `beforeAll`；
- 用一个巨大 `afterAll` 收尾，导致中途 fail 会污染后面所有 case；
- 只 reset DB，env/cwd/cache/static registry/fake clock 继续漏；
- 把 order dependence 叫“integration realism”，但真实产品根本不要求这些 test 共用 lifecycle。

### 验证

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

还要检查 failure isolation：某 case 中途 throw，也必须 release/restore 自己拥有的东西，否则一条真实红会制造后面一串误导性红。

### 完成条件

完成时，每条 test 都能在解释 premise 时完全不说“假设 test X 已经跑过”；suite scheduling 只影响 throughput，绝不影响 semantics。

> Test order 可以决定 evidence 什么时候被收集，不能决定 evidence 是什么意思。

### Nudge

> Test 应只记得 scenario 明确交给它的东西。Suite history 不是合法 fixture。
