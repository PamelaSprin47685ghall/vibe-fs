# weakened-test-to-pass — Main

## 现在该做什么
恢复当前 independently owned contract 仍然要求的最强 expectation。

然后修 implementation，不要修 examiner。

如果 contract 的确改变，先把这个事实建立清楚，再带着 provenance 把 test 改成新 promise。Green 应当是 decision 的结果；decision 不能从“当前 implementation 恰好能通过什么”倒推出来。

## 为什么重要
Test suite 是少数几个代码可以被明确告知“no”的地方。

如果 production code 一失败，就能让自己的 witness 降低要求，suite 就不再约束 behavior，而会退化成“当前 implementation 做什么”的自动生成自传。任何 regression 都能被重新命名成新 expectation。Green 变得无限可获得，也因此几乎没有含义。

当同一个 agent 同时修改 implementation 与 tests 时，这个问题尤其尖锐：机械便利会让 separation of powers 消失。修复方式不是官僚式冻结，而是要求“**为什么 contract 改了**”必须来自 failing code 之外的 owner/evidence。

## 修复策略
改任何东西前，先恢复 behavioral proposition：

- 找到 original requirement、protocol、invariant、acceptance criterion 或 caller dependency；
- 判断它现在是否仍属于当前 task/product；
- 如果仍属于，就保留/恢复 test，修 production behavior；
- 如果已经改变，记录 authoritative reason，再精确写出新 proposition；
- 保持 regression power：除非新 contract 明确把旧 behavior 合法化，否则 old defective implementation 仍应因 contract-level reason 被打红。

Snapshot 场景要逐字段审 semantic difference。只接受 intended change。Critical fields 更适合 targeted assertion，不要让“update snapshot”成为橡皮图章。

如果旧 test 过度绑定 internals，删掉 implementation detail 后，应补上 observable contract assertion；不要只删除压力。

## 决策分支
- **Requirement 未变：**恢复 expectation，修 implementation。
- **Requirement 有意改变：**引用/记录新 contract，test 精确迁移到新 promise。
- **旧 test 误解 contract：**用 authoritative source 证明，然后纠正；当前 implementation fail 本身不是 proof。
- **Assertion 只约束 private implementation：**替换为 caller-visible behavior，而不是保留 ceremony。
- **Failure nondeterministic：**修 nondeterminism，不要削弱 behavioral claim。
- **唯一理由是 release pressure：**那工作就还不是 green。保持 failure visible；如果组织存在 waiver authority，应走显式 risk decision，而不是偷偷改 test。

## 常见假修复
- Exact equality 改 truthiness、宽 range、substring、does-not-throw。
- 用“用户大概不会这么做”删除 edge case，却没有任何 product boundary 支撑。
- Skip/xfail/flaky 一条 test，同时继续把它算成 evidence。
- Wholesale regenerate snapshot，利用 review fatigue 隐藏 unintended change。
- 把 fixture 改容易，使 difficult boundary 直接消失。
- 从 production logic import 同一套计算来生成 expected value，让 test 与 implementation 一起错。
- 写很多 comment 解释弱 assertion 为什么“good enough”。Commentary 不会创造 contract authority。

## 验证
证明 green 仍然具有攻击性。

临时恢复那个促使 test 被削弱的 defective behavior。如果 contract **没有**改变，修好的 test 必须 red。

如果 contract 确实改变，为**新 promise**构造一个 defect，并证明重写后的 test 仍会拒绝它。合法 contract change 只是改变“哪些行为可以接受”，不是废除 rejection power。

Invariant：

> Test suite 按 independently chosen contract 约束 implementation；implementation failure 无权单方面重新定义 contract。

## 完成条件
每一个被放宽的 expectation，都能用“contract 如何改变/被纠正”解释，而不是用“build 当时是红的”解释。

Implementation 可以与 test 发生 disagreement。它不能通过编辑 test，把 disagreement 从历史中删除。
