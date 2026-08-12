# log-as-recovery-protocol — Main

把 restart authority 移到真正为 commitment 设计的 channel。

对每个 recovery question，找出能带着所需 guarantee 回答它的 source：

- 哪个 operation 被请求；
- 它是否 commit；
- 哪个 logical identity commit；
- durable current state 是什么；
- external effect 是 known success、known failure 还是 unknown。

这个 source 可以是 event journal、transaction table、durable command inbox/outbox、provider status endpoint、authoritative database row，或其他 typed store。关键是它真正拥有 recovery 所需 semantics，而不是刚好在附近打印过一句话。

然后把 diagnostic log 降回本职：explanation、correlation、debugging、operator context。Log 可以带 ID 帮人定位 authoritative fact，但不能**自己冒充 fact**。

常见假修复：

- 冻结 log wording、加 version，就当 protocol，但 durability/atomicity 完全没定义；
- plain text 换 JSON，就觉得 structure = commitment；
- journal 与 log 双写，restart 因 grep 方便反而优先 log；
- transaction commit 前先 emit “committed”，靠“这几行之间不会 crash”的约定维持；
- trace/span completion 被当 underlying effect commit 的证据；
- “real store 丢了时用 log fallback”，结果真实 authority 丢失被静默掩盖；
- child process 明明有 status/result protocol，却解析 stdout 重建 business state。

如果现有 diagnostic channel 真要升级成 recovery store，就正式升级：定义 typed schema、stable identity、commit boundary、durability、ordering、retention、replay、corruption semantics、migration。做到这一步后，它已经不是“just logging”，而是 journal，ownership 名字也应该改，防止未来代码继续把 observability guarantee 当偶然福利。

验证必须证明 diagnostics 与 recovery 解耦：

1. suppress/drop/rotate human log → recovery 不变；
2. duplicate/reorder diagnostic message → recovery 不变；
3. wording/localization 改 → recovery 不变；
4. emit diagnostic 但 business fact 不 commit → recovery 不能相信；
5. business fact commit 但 log 被 suppress → recovery 仍必须相信。

Structured observability 还要测试 sampling。Tracing backend 丢掉 10% span，不能让任何 business fact 因此不可恢复。

Retention 也要看。Recovery truth 不能活在一个会按 observability 运维策略自动删旧记录、而与 business retention 无关的 channel 里。

完成时每个 recovery decision 都能指出一个 typed durable authority，而任何 log line 全部消失也不会改变 machine belief。

> 好 log 告诉人“系统相信什么”；recovery protocol 告诉系统“它有资格相信什么”。