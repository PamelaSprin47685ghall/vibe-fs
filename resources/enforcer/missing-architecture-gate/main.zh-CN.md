# missing-architecture-gate — Main 中文版

## 现在该做什么
把 rule 写成最小 deterministic check，接入 CI/标准 `check` 入口，并用 known-bad fixture 证明它真的能红。若 property 无法低噪机械判断，就不要造假 gate；清楚记录 invariant，留给 human judgment。

## 为什么这很重要
纯 review enforcement 把成本放错时间：违规提交时很便宜，几个月后 dependency graph 腐烂再治理非常贵。机械 gate 把成本前移到第一条 forbidden edge 出现时。

## 常见假修复
- 增加文档/checklist，机器仍可接受违规。
- gate 扫空目录或 fail-open；这会变成 `false-gate`。
- 只放一个没人运行的 local script。
- 为不可机械判断的语义原则写粗糙 regex，制造大量误杀。

## 验证
同一条 standard command 对代表性 known-bad fixture 必须 red，对合法结构保持 low-noise green。

## 完成条件
可机械决定的关键 architecture invariant 不再依赖 reviewer 记忆；不可机械决定的规则也不假装已经自动化。
