# implicit-control-flow — Main

把 happens-before 关系从 folklore 变成显式 composition。

先列出 correctness 真正依赖的 causal edges：A 必须完成后 B 才有意义；transform X 必须在 seal 前；subscription 必须在 event 可能发生前；cleanup 必须等 child teardown。然后让一个明确 owner 组合这些 phase/operation，而不是让各 module 靠 import/register 顺序自行“碰上正确位置”。

健康做法可以是：

- 一个显式 workflow 顺序调用；
- typed phase/pipeline builder；
- 有稳定语义的 hook phase enum + completeness/order validation；
- event protocol 中显式 predecessor/identity；
- startup composition root 负责 construction order，并让 component 本身不依赖全局副作用；
- adapter 把 framework lifecycle 翻译成 application 能读懂的 causal contract。

常见假修复：

- 在数组旁写“DO NOT REORDER”；
- 给 hook 名加 `01_`, `02_` 前缀；
- 依赖文件 import order/lexical filename order；
- 把顺序常量挪到 config，却没有 semantic validation；
- 通过 sleeps “确保前一个应该跑完了”；
- 每个 callback 自己检查 global flag 猜前置 phase 是否完成；
- 发生错序后靠 retry 把流程拉回正常，而不修 causal ownership。

验证应主动打乱**不相关**注册顺序，并故意交换一条真正 required edge。前者不应影响 behavior；后者必须被 structure/gate 明确拒绝或 test 打红。这样才能证明系统保护的是 semantic order，而不是偶然 total order。

对 extensible hook/pipeline，新增 participant 时应强迫作者声明它属于哪个 phase/前后关系；不能让“append 到列表末尾”自动获得业务语义。

如果 framework 本身有正式 lifecycle contract，就把它封装成一个 adapter-level fact，并在 boundary 测真实 order，不要让 domain/application 到处背诵 framework phase 名。

完成时，阅读 composition 即可回答关键 happens-before；移文件、改 import、换 registry implementation 不会静默改业务因果。

> 顺序一旦影响 correctness，它就已经是 protocol。Protocol 应有 owner，不应藏在加载顺序里。