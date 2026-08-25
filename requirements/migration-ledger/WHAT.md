# migration-ledger — WHAT

本文件是 `migration-ledger` 的**唯一 normative 合同**。WHY 与 HOW 非 normative。

---

## MIGRATION-LEDGER-001: DAG 完整性与覆盖闭合

`scripts/checks/migration-ledger.json` 的每条 `depends_on` 必须指向已存在节点且带合法 kind，图必须无环（Kahn），且 `nodes.files ∪ coverage_backlog` 必须与 `semantic-owners.json` 完全一致。多位置重复、缺失、额外文件均判红。

## MIGRATION-LEDGER-002: PENDING 证据纯度

`state=PENDING` 的节点证据（`evidence`/`closure_evidence`）不得包含大小写不敏感的成功标记 `verified`/`complete`/`green`。含则门禁红。

## MIGRATION-LEDGER-003: READY 准入

`state=READY` 的节点必须同时满足：至少一项 `publishes`/`consumes`/`depends_on`/`production_callers_to_migrate` 非空（owner 图），且至少一项 `proofs`/`architecture_gates` 非空；且所有依赖必须为 DONE。否则红。

## MIGRATION-LEDGER-004: DONE 闭环

`state=DONE` 的节点必须满足：`result != PENDING`，分类与结果兼容（KEEP→PROVEN-KEEP、DELETE→DELETED、MOVE/SPLIT/ADAPTER→CUTOVER、COMPOSITION-ROOT→CUTOVER|PROVEN-KEEP），含合法 `implementation_commit`（40 位哈希且为 HEAD 祖先），`touched_paths` 非空且含生产路径，`proofs` 与 `architecture_gates` 各自非空。任一缺失则红。

## MIGRATION-LEDGER-005: 闭合与覆盖归属

`kind=closure` 的依赖目标必须为 DONE；仅 `coverage_tags` 无 owner 图的 DONE 节点判红。违反则红。

## MIGRATION-LEDGER-006: 基线/抑制冻结

`scripts/checks/deadcode-baseline.json` 与 `scripts/checks/provider-prose-ownership-baseline.json` 不得无显式 admission 而增长。增长则红。

## MIGRATION-LEDGER-007: 机械门禁与可红性

`scripts/checks/migration-ledger.mjs` 必须提供 `validateLedger(ledger, owners)` 纯函数与 `--self-test` 自检，且 `requirements/migration-ledger/tests/gate-rejection.test.mjs` 必须以独立变异覆盖全部 11 类可红非法态。自检与变异任一不可红则红。
