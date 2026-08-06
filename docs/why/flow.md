# FLOW — 理由

曾把 DSL 做成「封闭 AST + 唯一 Interpreter」，与 ARCH-001 冲突：用业务代码重做动态调用协议，Reply DU 与 Trace 解释器把复杂度乘在每一业务步上。

撤回后，语言 CE 即流程栈；规则 DSL 只管「是否允许」，不管「程序下一步」。恢复从事实重入普通 workflow，不恢复协程指针——后者假装崩溃可透明续跑，实际绑定不可序列化的调用栈。

## 备选与被拒

**流程表达：语言 CE 直执 vs 封闭 AST + 唯一 Interpreter。** 拒 AST：与 ARCH-001 冲突；Reply DU + Trace 解释器把复杂度乘以每一业务步（FLOW-001 直执）。DSL 退化为「规则是否允许」的判断面。

**恢复：Journal 事实重入 workflow vs 恢复协程指针。** 拒指针：调用栈不可序列化，假装透明续跑实为不可恢复。从事实重新进入普通 workflow（Projection 复用，PERSIST-010）。

**规则 DSL 职权：只判允许 vs 兼管程序下一步。** 拒兼管：规则面长第二运行时（program-counter 反模式）；职责收窄到决策，控制流仍归语言 CE。
