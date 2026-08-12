# repeated-known-mistake — Main

把现有 lesson 当作 active premise 重新读一遍，然后做两种诚实选择之一：遵守，或正式 supersede。

先定位 prior record 真正保护的 invariant/failure mechanism，而不是只看标题。确认它当时依赖哪些环境条件、external version、architecture assumptions。若这些 premises 仍成立，当前实现必须尊重 lesson；若已变化，收集能区分新旧世界的 evidence，并在 owning documentation/test/contract 中更新结论。

常见假修复：

- 复制 old workaround “因为以前这样修过”，却没看它后来是否已经被 supersede；
- 反过来，完全忽略 old lesson “因为那是历史”；
- 新文档说旧规则不再适用，但 test/gate/runtime contract 仍按旧事实运行；
- 只改当前 code，不把新 evidence 写回知识 owner，下一次又重复争论；
- 把近似 symptom 当同一 root cause，机械套旧修法。

验证要比较 mechanism，而不是表面形状。能重现 old counterexample 时，新设计仍应保护对应 invariant；若声称环境已变，让新的 contract/canary 明确证明旧 failure 不再可能或语义已变化。

如果 prior lesson 本身 stale，就更新它，不要要求 contributor 一边遵守“当前事实”，一边被历史文档反向审判。Repository memory 需要可修订，但修订必须有 provenance。

理想情况下，最重要的 known mistake 不只存在 prose：能做 regression/architecture gate/contract test 的地方，把 lesson 变 executable memory；只能靠 judgment 的部分才留成 RuleBook/decision note。

完成时当前 choice 能清楚回答：过去发生过什么、那条 lesson 今天是否仍有效、如果无效是什么新 evidence 让它失效。

> 学费已经交过，就别因为换了工程师又交一次；但也别把收据当永恒自然法。