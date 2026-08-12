# duplicated-control-flow — Main

给 protocol 一个 owner，然后让所有 entry point 表达 intent，而不是各自重演 sequence。

修复时先抽出真正共享的**控制知识**：transition、ordering、failure/cancel/retry、cleanup、idempotency。不要只把几行相同 syntax 抽成 helper；如果 caller 仍自己决定何时调、失败后下一步是什么，protocol authority 仍然分散。

Healthy shape 常见为：

```text
entry A ─┐
entry B ─┼→ one workflow / transition owner → effects
entry C ─┘
```

不同 entry 可以负责 decode/auth/presentation，但进入共享 protocol 后，不再各自维护同一时序法则。

常见假修复：

- 把每个重复 step 抽成 helper，三条 workflow 仍分别手写 sequencing；
- copy 一份“shared base class”，子类继续 override failure semantics；
- 配置化所有 step 顺序，最后得到一个没人拥有 semantics 的 generic workflow engine；
- 为减少 duplication 把实际上不同的两个 protocol 强行统一，埋掉合法差异；
- production 一个 workflow，test 复制同样 algorithm 算 expected result，双方可能一起错；
- 把 common sequence 放进 facade，但旧 entry 仍可绕过。

验证可以做“单点变更实验”：改变一条真正 protocol rule，例如 persist 必须先于 publish、某 failure 不再 retry、cancel 必须先停 child。实现应该只需要改一个 semantic owner；所有 entry 的行为一起改变，且各自 boundary tests 仍证明 caller contract。

再检查 escape path：repo search 不应发现第二套手写同 sequence 的 branch。若某一处确实不同，给差异命名并让它成为参数/独立 protocol，而不是 quietly fork implementation。

不要追求 DRY 数字。两段相似代码若独立演化，保留 duplication 反而更诚实。只有“一个规则必须同步更新多处”才值得集中。

完成时，workflow 的 ordering/failure law 有单一 authority；caller 可以很多，presentation 可以很多，但同一 protocol 不再有多份各自会漂移的解释。

> 真正的 DRY 不是少写几行，而是同一决策只需要改一次。