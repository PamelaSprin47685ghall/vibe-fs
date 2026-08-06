# Proposal 代码隔离缺口（GOV-003）

目标：
- 生产代码与正式测试不得把未裁决 proposal 当现行合同；proposal 只能经 GOV-006 裁决并原子分发到正式层后进入实现。

当前：
- `Domain/StrengthTypes.fs`、`StrengthPolicy.fs`、`StrengthPredictor.fs`、`StrengthController.fs`、`StrengthValue.fs` 已进入生产编译图。
- `Session/EnforcerHost.fs` 直接引用 `docs/proposal/strength.md` 的 `STRENGTH-079` 常量。
- `tests/unit/strength/` 直接以该 proposal 为行为依据。
- `Domain/StudentTeacher.fs` 已进入生产编译图；`ProviderRequestKind.StudentLearn/StudentCompile` 已进入现行 Prompt/恢复类型。
- `tests/unit/student-teacher/` 与 unit facade 直接以 `docs/proposal/student-teacher.md` 的 `LEARN-` 候选编号为行为依据。

缺口：
- 当前编译图存在 `proposal → code/tests` 依赖，违反 GOV-003；这不表示 Strength 或 Student-Teacher proposal 已按 GOV-006 被正式接受。
- 在 proposal 裁决前，应把 Enforcer 共用常量迁回现行 ENFORCER owner，并从生产编译图、共享类型与正式测试入口隔离仅服务 proposal 的 Strength / Student-Teacher 模块；若先完成裁决，则须按 GOV-006 原子分发正式规范并以新的实现差距 status 替换本文件。

阻塞：
- Strength / Student-Teacher 的产品取舍仍需人的 proposal 裁决；清除现有 proposal 直连不依赖该取舍。
