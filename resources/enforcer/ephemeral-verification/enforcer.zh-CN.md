# ephemeral-verification — Enforcer

Ephemeral verification 的病，是团队已经做过一个能区分对错的 experiment，却让它只存在于 terminal history、scratch script、manual click、临时 debug output 或某个人脑子里。

这类 evidence 可以让一个人今天相信，但不能让 repository 明天变红。Session 结束后，stimulus、setup、expected observation、failure mode 一起蒸发；同一个 defect 回来，项目重新从零付 investigation cost。

以下情形触发：

- “我手工 curl 过，没问题”是唯一 proof；
- 临时 Node/Python/shell probe 找到真实 bug，修完后脚本丢掉；
- 只靠浏览器点一遍或手动 inspect output；
- debug log 证明过某 invariant，但没有测试/contract/canary 保留；
- 一次 race reproduction 成功，却没有 deterministic regression；
- completion 引用本地某次 command output，标准 check path 无法重放。

不要误杀 exploration。临时 probe 本来就是调查工具；只要它发现的 reusable knowledge 最终进入 maintained test/gate/canary，probe 可以消失。一次性 production forensics 若现实上无法复现，也不必强行伪造 test；应保存可复用的 invariant/lesson，而不是原始事故噪声。

与 `missing-regression-test` 区分：那条专门要求 concrete bug fix 留 executable memory；本规则更广，任何重要 verification 若只存在一瞬间都可能触发。与 `unverified-completion-claim` 区分：这里**曾经有 proof**，只是没有 durable；后者可能根本没有足够 proof。

最直接的问题：**另一个 maintainer 只 clone repository，能不能重跑同一个 falsifiable check？** 不能，就说明 evidence 仍属于个人 session，不属于工程系统。

> 调试真正完成，不是在你亲眼看见它对，而是在项目学会以后自己检查它对不对。