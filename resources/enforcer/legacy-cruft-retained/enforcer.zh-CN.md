# legacy-cruft-retained — Enforcer 中文版

## 定义
Legacy cruft retained 只在一个更强前提成立时触发：**项目已经明确决定 clean break，旧世界不再受支持，但旧 alias/parser/name/branch 仍然活着。**

这不是一般 compatibility debate。债已经被正式免除，代码却继续替一个没有 contract 的 ghost consumer 工作，等于把 clean break 的 state-space reduction 全部抵消。

## 何时触发
- 已声明 v2-only，v1 parser 仍 reachable；
- 旧 provider name/field/status 仍可被新请求接受；
- clean-break 后仍有 fallback/alias“保险”；
- tests 继续保护旧 surface，即使 proposal 明确要求 non-advertisement/non-acceptance；
- internal code 仍同时说两套 vocabulary。

## 不要误判
- 明确外部 consumer 仍处 bounded migration window；
- changelog/docs 仅描述历史旧名；
- on-disk residue 已被 current reader 明确拒绝；
- 没有 clean-break decision、只是迁移未完成时，更接近 `half-finished-refactor/compatibility-cruft`。

## 刀口
找到 clean-break decision，然后问每个 old-world occurrence：**谁授权它在 break 之后继续 live？** 没有具体例外，它就是违约的幽灵接口。

## 提醒
Clean break 的价值就是让 supported worlds 从两个变回一个。决定已经做了，就不要在代码里重新投票。
