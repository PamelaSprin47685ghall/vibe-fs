# legacy-cruft-retained — Main 中文版

## 现在该做什么

按 clean-break decision 做机械清扫：删除旧 writer/parser/alias/branch/schema vocabulary 与保护它们的 tests；只保留 decision 明确允许的历史说明或 bounded external exception。

## 为什么重要

Clean break 本来是在购买更小的 state space。旧 surface 若继续 live，项目实际上仍付双份设计、测试、support 与推理成本，却失去了“我们仍兼容”的诚实声明。

## 修复策略

以 clean-break decision 为权威，迁移剩余仓库自有的 callers，然后彻底移除旧 representation。只记录带 retirement condition 的显式 external exceptions。

## 决策分支

- 若 clean break 没有授权例外，删除每个 reachable old surface。
- 若外部 contract 仍需要旧 path，只保留那个带 retirement condition 的例外。

## 常见假修复

- 旧 parser 不 advertise，但仍 silently accept。
- 旧 field 在 internal object 里继续 mirror。
- tests 说“legacy still works, just in case”。
- 把 old vocabulary 改成 comment/compat helper，却仍影响 provider/runtime behavior。
- 因为删除令人紧张，就重新引入一个没有 consumer 的 migration window。

## 验证

对 retired vocabulary / wire shape / tool name / branch 做 repo scan 与 boundary tests：provider 不 advertise、不 accept，production 不 emit，只有明确允许的 historical text 可出现。

## 完成条件

clean-break 后只有一个 live world；旧世界只能被历史提及，不能再被程序解释或生成。