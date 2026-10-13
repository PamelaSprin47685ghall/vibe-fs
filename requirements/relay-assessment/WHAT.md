# relay-assessment — WHAT

评审结果只有 Promoted 与 Stayed。主合同在 requirements/work/ 的 WHAT[work-003]。本文件保留 surface 仍引用的编号。

## [001] 三问与 pairs

review 必填 findings，它是 [(acceptance_criteria, work_plan), …] 的数组。空数组表示缺口为空，结果是 Promoted。非空表示 Stayed。不得缺项，不得使用数值或总评分。

## [006] 禁止同任自证

本任已经 Reviewed 之后，不得再 review。第二评审、reverify 或 challenge 不得写入。

## [007] 拒绝不占名额

schema、范围、narrative 或精确 binding 校验失败时，不得写入 Reviewed，也不得消耗本任名额。同 idempotency key 的异载荷须拒绝。
