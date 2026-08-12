# order-dependent-test — Enforcer

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

以下情形触发：

- full suite 里 pass，单独跑 fail；
- first run fail，但另一 case warm cache / create data 后就 pass；
- tests 共用 mutable DB row、temp dir、process registry、singleton、mock cursor、clock、env、cwd、port、file；
- test A 的 cleanup 实际由 test B 或最终 global teardown 完成；
- `beforeAll` 建一份 mutable state，各 case 按顺序消费/修改；
- 必须强制固定 order 才稳定；
- 一开 parallelization 就暴露所谓 independent cases 其实共享 premise；
- 某 test 依赖 neighboring test 已推进 ID counter / random seed / provider preference。

不要误杀**顺序本身就是 scenario**的测试。“create → approve → ship” 若就是一个业务 lifecycle，就应该作为一个显式 ordered scenario，由同一个 test 拥有完整 setup/teardown。问题在于把这些步骤拆成几条假装 independent 的 test，再让 runner order 偷偷承担 lifecycle。

Shared fixture 也不天然有罪。Immutable package data、只读 constant、昂贵但能给每个 test 独立 namespace 的 service、每 case 独立 transaction/schema 的 DB，都可以共享 infrastructure 而不共享 semantic premise。

与 `flaky-test-tolerated` 区分：order dependence 是 nondeterminism 的一个具体 mechanism；后者是组织接受不稳定 verdict 的 policy failure。`mock-hidden-state` 更专门管 hidden premise 藏在 stateful mock；`resource-not-scoped` 可能解释 residue 为什么残留，但本规则关注 residue 改变了另一条 test 的真值。

决定性实验不是“shuffle 两次没事就算了”，而是让 case：单独跑、第一条跑、最后跑、跟在可疑 neighbor 后、以及在合法 parallel/random order 下运行。它自己的 explicit input 不变而 verdict 改变，就说明 suite history 是 undeclared input。

修法只有两条诚实道路：让 test 自己拥有并清理所有 mutable premise；或者承认这些 operation 就是一条 lifecycle，把它们合成一个显式 scenario。不要用 filename order、数字前缀、`--runInBand`、或“这个目录别并行”去编码隐藏因果。

> Test 应只记得 scenario 明确交给它的东西。Suite history 不是合法 fixture。