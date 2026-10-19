# ephemeral-verification — Lesson

## 评价

### 定义

Ephemeral verification 的病，是团队已经做过一个能区分对错的 experiment，却让它只存在于 terminal history、scratch script、manual click、临时 debug output 或某个人脑子里。

这类 evidence 可以让一个人今天相信，但不能让 repository 明天变红。Session 结束后，stimulus、setup、expected observation、failure mode 一起蒸发；同一个 defect 回来，项目重新从零付 investigation cost。

### 支配原则

无法重放的发现不属于工程系统。它可以说服一个人一次，但不能在明天守护 invariant、教会下一个 maintainer、或在 defect 回来时变红。Durable verification 把私人信心变成共享的 executable memory。

### 何时触发

以下情形触发：

- “我手工 curl 过，没问题”是唯一 proof；
- 临时 Node/Python/shell probe 找到真实 bug，修完后脚本丢掉；
- 只靠浏览器点一遍或手动 inspect output；
- debug log 证明过某 invariant，但没有测试/contract/canary 保留；
- 一次 race reproduction 成功，却没有 deterministic regression；
- completion 引用本地某次 command output，标准 check path 无法重放。

### 不应触发

不要误杀 exploration。临时 probe 本来就是调查工具；只要它发现的 reusable knowledge 最终进入 maintained test/gate/canary，probe 可以消失。一次性 production forensics 若现实上无法复现，也不必强行伪造 test；应保存可复用的 invariant/lesson，而不是原始事故噪声。

### 与相邻规则区分

与 `missing-regression-test` 区分：那条专门要求 concrete bug fix 留 executable memory；本规则更广，任何重要 verification 若只存在一瞬间都可能触发。与 `unverified-completion-claim` 区分：这里**曾经有 proof**，只是没有 durable；后者可能根本没有足够 proof。

### 判定程序

最直接的问题：**另一个 maintainer 只 clone repository，能不能重跑同一个 falsifiable check？** 不能，就说明 evidence 仍属于个人 session，不属于工程系统。

### 例子

- positive：一个 fix 被 terminal 里一次性 curl “验证”过，repo 里没有任何东西能重放这个 check。
- near-miss：同一条 curl 用来发现 bug，然后加了一个编码 stimulus 与 result 的 contract test。
- counterexample：把 probe 转成标准入口点上最窄的 durable automated check。

## 建议

### 现在该做什么

把临时 experiment 中真正有区分力的部分提炼成 repository-owned proof。

先抽掉调查噪声，只保留：stimulus、关键 setup、observable、以及什么 defect 会让它失败。然后把它放到最合适的永久层：unit/property、boundary contract、replay/recovery、integration、或真实 canary。

不是所有 scratch script 都值得保存。保存的是**发现的 invariant**，不是当时敲过的每一条命令。

### 为什么重要

Manual verification 证明的是一个瞬间，不是一个系统。它的 setup、命令、observation 都会消失，下一次 regression 从无知开始。只有当调试知识被转换成未来 change 必须通过的东西，repository 才在进步。

### 修复策略

从 probe 中提取 essential stimulus 与 observable result，按涉及的 boundary 放进 unit、contract、replay、integration 或 canary test。保持 deterministic，并成为正常 check path 的一部分。

### 决策分支

- 如果 probe 发现了 repo 必须守住的 invariant，把它编码成 maintained check。
- 如果 session 没有产出可复用的 stimulus/result，不要声称验证；用别的方式获得 durable proof。
- 如果 bug fix 缺任何 regression，同时适用 `missing-regression-test`；本规则专管会蒸发的 proof。

### 常见假修复

常见假修复：

- 把 scratch 脚本原样丢进 `scripts/`，没人知道何时运行、expected output 是什么；
- 把 terminal output 粘进 comment/issue；
- README 写 “验证步骤”，却不接 standard check path；
- 保存一个 flaky stress reproduction，不固定真正 causal schedule；
- manual canary 只有作者知道 secret/setup；
- 为了自动化把一条小 contract probe 扩成巨大、昂贵、不稳定 E2E。

### 验证

选择最窄 faithful boundary。纯 law 放 unit/property；producer/consumer agreement 放 contract；restart fact 放 replay；外部 undocumented behavior 放 canary。Evidence 层级应由 claim owner 决定。

验证 durable proof 自己：临时恢复 old defect 或破坏该 invariant，标准入口必须红；fix 后 green。另一个 clean checkout 应能按照 repository 自己拥有的 setup 复现，不依赖你的 terminal history。

若 probe 只是 investigation stepping stone、并没有形成长期 invariant，可以让它消失；RuleBook 不要求保存所有探索过程。

### 完成条件

完成时“我亲眼验证过”被升级成“项目以后会自己验证”。下一次 regression 不需要某个人还记得当年怎么敲命令。

> Session confidence 会过期；executable memory 才会留下来。

### Nudge

> 调试真正完成，不是在你亲眼看见它对，而是在项目学会以后自己检查它对不对。
