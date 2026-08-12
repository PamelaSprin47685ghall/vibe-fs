# canary-skipped — Main

把不可由内部证明的 external premise 写成一条最小 falsifiable canary。

先明确谁拥有事实：Host、provider、deployment runtime、OS、external service。然后设计最小真实 interaction，只观察那条 assumption 本身，避免用巨大 E2E 把十种风险混在一起。

好的 canary 应回答：

- 发了什么真实 stimulus；
- 观察哪个 exact behavior；
- 什么结果会明确证明 assumption false；
- 依赖哪个 external version/environment；
- 是否安全、可重复、成本可控。

常见假修复：

- 把 mock 写得更像自己希望的真实系统；
- 引用“上个月手工看过一次”；
- broad staging suite green，却没验证关键 ordering/framing/identity；
- live canary 只断言“request 成功”，对 assumption 没辨别力；
- canary 太大、依赖太多 unrelated setup，失败后无法知道哪条 premise 被打破；
- external upgrade 后沿用旧 observation，不重新跑 relevant premise。

Canary 不替代 local proof。Pure logic、codec、contract、replay 应先在更窄层证明；live check 只承担**下面各层无法拥有的那一点 empirical uncertainty**。

验证 canary 自己也要 mutation thinking：如果真实 Host 把关键 behavior 改成相反值，这条 canary 会不会可靠变红？若只是仍然得到 200/“completed”，它不够具体。

如果 external side 终于发布稳定 contract，且本地 contract test 能 faithful exercise，可以降低 live canary 频率或移除；证据策略应跟风险所有权走，不是永久 ritual。

完成时 release 不再依赖 “Host 应该这样吧”，而有一条真实 observation 能让这个 premise 随时被否证。

> Canary 的价值不在“接触了真实环境”，而在真实环境终于有机会对我们的假设说不。