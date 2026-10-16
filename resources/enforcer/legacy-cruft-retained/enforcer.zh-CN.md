# legacy-cruft-retained — Enforcer 中文版

## 定义

Legacy cruft retained 只在一个更强前提成立时触发：**项目已经明确决定 clean break，旧世界不再受支持，但旧 alias/parser/name/branch 仍然活着。**

这不是一般 compatibility debate。债已经被正式免除，代码却继续替一个没有 contract 的 ghost consumer 工作，等于把 clean break 的 state-space reduction 全部抵消。

## 支配原则

Clean break 的价值在于坍缩 state space：边界之后，旧世界不再是系统必须解释的世界。保留 legacy paths 会抵消这个好处。项目假装迁移已经完成，却永远同时为两段历史付账，未来开发者也无法知道旧 path 是被禁止、被容忍还是被秘密需要。

## 何时触发

- 已声明 v2-only，v1 parser 仍 reachable；
- 旧 provider name/field/status 仍可被新请求接受；
- clean-break 后仍有 fallback/alias“保险”；
- tests 继续保护旧 surface，即使 proposal 明确要求 non-advertisement/non-acceptance；
- internal code 仍同时说两套 vocabulary。

## 不应触发

- 明确外部 consumer 仍处 bounded migration window；
- changelog/docs 仅描述历史旧名；
- on-disk residue 已被 current reader 明确拒绝；
- 没有 clean-break decision、只是迁移未完成时，更接近 `half-finished-refactor/compatibility-cruft`。

## 与相邻规则区分

`compatibility-cruft` 缺少有正当理由的外部要求；`half-finished-refactor` 留下未完成的迁移。本规则更尖锐：一个决定已经说过旧世界应该停止存在。Tie-break：如果没有 clean break 被决定、双 owners 仍在，用 `half-finished-refactor`；如果 break 已决定而旧 surface 仍活着，用本规则。

## 判定程序

找到 clean-break decision，然后问每个 old-world occurrence：谁授权它在 break 之后继续 live？没有具体例外，它就是违约的幽灵接口。

## 例子

- positive：项目宣布 v2 是唯一受支持格式后，v1 parser 与 alias 仍 reachable。
- near-miss：外部 client 在带日期的 retirement 前仍需要 v1；那个 adapter 是唯一存活的旧 surface。
- counterexample：旧 surface 被删除；git history 记得它。

## Nudge

Clean break 的价值就是让 supported worlds 从两个变回一个。决定已经做了，就不要在代码里重新投票。