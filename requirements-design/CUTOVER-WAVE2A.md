# Cutover Wave 2a — tests/unit 归包操作细则（共享契约）

> Wave 2a 目标：把 `tests/unit/**` 剩余的 146 个测试文件全部迁移为包自有测试（MOVE / SPLIT / DELETE），
> 使旧 `tests/unit/` 最终可删。Wave 2b 再处理 harness（support/、run.mjs、eval/、integration/、e2e/）。
> 本文件只规定机械规则；owner 判定依据各包 PROOF.md / WHAT.md 与 `requirements-design/PROOF-MAP.md`。

## 0. 环境状态（已发生）

- `docs/`、`changes/` 已归档至 `archive/`（散文引用已统一改写；Clause 原文在 `archive/docs/**`）。
- 45 包 × 5 文档 + 包测试已在 `requirements/`；meta-verifier 全绿。
- `tests/unit/run.mjs` 自动发现 `tests/unit/**` + `tests/eval/**` + `requirements/*/tests/**/*.test.mjs`。

## 1. 每文件的处置类型

- **MOVE**：文件全部断言归一个包 → 物理移入 `requirements/<pkg>/tests/`（git mv），import 深度修正，删原文件。
- **SPLIT**：多 owner → 按断言分组，为每个 owner 在 `requirements/<owner>/tests/` 新建文件，删原文件。
  新建文件头注释：`// Split from tests/unit/<family>/<file> (cutover Wave 2a); owner: <pkg>`。
  **断言不得丢失、不得重复**：SPLIT 前后测试总数守恒（可合并同语义断言，但必须报告）。
- **DELETE**：迁移 ratchet / retired stub / 纯迁移期证明（如 `enforcer-rulebook-gate.test.mjs`、absence ratchet 测试）→ 删除并报告理由。
- **未认领（PROOF 无引用）**：读文件判定真实 owner（按断言语义对照 owner 包 WHAT/PROOF；PROOF-MAP family 归属起步），
  再按 MOVE/SPLIT 处置；判定依据写入报告。**禁止猜测后不改即留**——每个文件都必须有终态。

## 2. 铁律

1. **禁止**把直接 `import ... dist/fable_modules/...` 带入 `requirements/` scope（test-boundary 门红）。
   此类 import 改写为经 `tests/unit/support/domain.mjs`（或 `support/` 下对应 adapter）的同语义调用；
   若无法改写，该文件保持 REUSE 留在原处并报告（Wave 2b 处理 harness 时一并裁决）。
2. 新文件命名：沿用原测试名（或按 owner 语义命名），必须是 `*.test.mjs`；helper 不得命名 `*.test.mjs`。
3. 每个产出的文件必须 `node --test <file>` 单独跑绿；SPLIT 的文件逐 owner 文件跑绿。
4. **禁止编辑** `requirements/<pkg>/{README,WHY,WHAT,HOW,PROOF}.md` 与 `requirements/README.md`（lead 收尾统一改写落点路径）。
5. **禁止触碰**：`tests/unit/support/**`、`tests/unit/run.mjs`、`tests/unit/` 顶层测试（domain.meta/guide-contract/verdict-feed）、
   `tests/eval/**`、`tests/integration/**`、`tests/e2e/**`、`archive/**`、`src/**`、`scripts/**`（Wave 2b 范围）。
6. 移动/分裂后旧文件必须删除（不留空壳、不注释保留）。
7. 环境变量：单跑测试如遇英文文案断言失败，加 `WANXIANGSHU_PROVIDER_LANGUAGE=en`（与 run.mjs 一致）。

## 3. git

- 提交：`git add <你的源删除路径> <你产出的包测试路径>` → commit `cutover(wave2a-<family>): ...`。
- 只提交你自己的路径；不 push；index 竞态下的归属不完美可接受（内容正确优先）。

## 4. 报告（终报必答）

```text
1. 逐文件处置表：源文件 → MOVE/SPLIT/DELETE → 目标文件（每个 owner 一个）/理由。
2. SPLIT 断言守恒：原文件 test 数 → 各 owner 新文件 test 数（合计）。
3. 未认领文件判定：每个的 owner + 依据（断言 → 哪条 WHAT/PROOF）。
4. 保留 REUSE 的文件（fable 无法改写等）+ 理由 + cutover 后续计划。
5. 验证摘要：每个产出文件 node --test 结果；受影响门禁单跑结果。
6. 旧→新路径映射表（lead 用来统一改写全部 PROOF.md 落点）。
```
