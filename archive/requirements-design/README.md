# Requirement Packages 设计归档

本目录保存 2026-08-14 Requirement Package cutover 的设计期协调文件（boundary cards、
AUDIT/COVERAGE/EVIDENCE/PROOF-MAP/CHANGES-AUDIT、HANDOFF、MIGRATION-CONTRACT、
CUTOVER-WAVE2A）。它们是**历史设计记录，不是现行规范**。

现行规范：

- 45 包 normative 树：`requirements/<package>/{README,WHY,WHAT,HOW,PROOF}.md`
- 包清单与依赖骨架（live manifest）：`requirements/INDEX.md`
- 测试全部包自有：`requirements/<package>/tests/`（共享 harness 在
  `requirements/verification-system/tests/`）

`INDEX.md` 已从本目录迁至 `requirements/INDEX.md` 并成为 meta-verifier 的权威骨架源；
其余文件保留于此供考古（WHY 裁决、失败模式复盘、被拒方案）。
