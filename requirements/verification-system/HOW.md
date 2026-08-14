# HOW：verification-system 的实现模型与约束

非 normative。本文件解释机制怎么工作、历史怎么来的、什么被弃权。

## 实现模型

无 runtime 源码（META 包正确形态，见 `requirements-design/EVIDENCE.md` §1）。证据面
分布在三个机制层：

### 1. proof ladder（`tests/proof-ladder.test.mjs`）

`node --test requirements/verification-system/tests/proof-ladder.test.mjs`

三组断言（Oracle 3，HANDOFF §29 调查结论直接执行）：

```text
1. format-build-test 层序（fantomas → check.mjs(L0) → build.mjs →
   unit/run.mjs → integration/run.mjs → integration/package/run.mjs →
   warmup-opencode.mjs → e2e/entry.test.mjs(L4，恰一个) → npm pack --dry-run(L5)）
2. check.mjs wired gate 清单：每个 wired 路径存在；
   scripts/checks/*.mjs == wired ∪ {spec-rules.mjs(lib), semantic-anchors.mjs(catalog),
   enforcer-rulebook-gate.mjs(retired stub)}
3. check.mjs fail-closed：process.exit(result.status ?? 1) 传播非零；
   行为面：必败 gate 退出码传播、不可 spawn 的 gate 判 exit 1
```

「可红」由现有 per-gate red fixture 交叉证明，不在本测试重造。`e2e-watchdog-feed.mjs`
已由 lead 接入 check.mjs（test-boundary 之后）。

### 2. layer-0 gate 回归（`tests/e2e-watchdog-feed.test.mjs`）

VERIFY-004 因果 watchdog feed 门禁的永久回归：top-level e2e 测试不得直接
`watchdog.advance(`；唯一入口 `requirements/verification-system/tests/e2e/entry.test.mjs` 必须在扫描范围内。
（自 `tests/unit/verify/` 迁移，import 深度不变。）

### 3. 行数 advisory（`tests/kolmogorov-size-advisory.test.mjs`）

Kolmogorov size 是 advisory：超过基线只给 suggestion，0 blocking finding——行数不是
门禁（VERIFY-005 不设行数门禁）。（自 `tests/unit/verify/` 迁移，ROOT 深度不变。）

### 4. 运行器机制（lead 集成时执行，本包 REUSE 登记）

```text
node scripts/check.mjs              # 22 个 wired layer-0 gate（proof-ladder pin 清单）
node requirements/verification-system/tests/run.mjs             # L1–3 入口：staleness gate + verdict-silence 监督
node requirements/verification-system/tests/run.mjs --coverage  # VERIFY-009 覆盖门禁（run-inner 判阈值）
tests/e2e/support/*                 # watchdog / readiness / 因果原语（VERIFY-004）
```

## 依赖与理由

- INDEX 骨架：`verification-system → requirement-system`。理由：本包命题（每 assertion
  一个 owner、WHAT 是唯一合同、依赖闭包验收）建立在 requirement-system 的元合同之上；
  没有「谁拥有什么」就无法定义「谁的 Satisfied(P) 需要什么证据」。

## 运行与验证

```text
node --test requirements/verification-system/tests/proof-ladder.test.mjs
node --test requirements/verification-system/tests/e2e-watchdog-feed.test.mjs
node --test requirements/verification-system/tests/kolmogorov-size-advisory.test.mjs
```

proof-ladder 现在必须绿。全量命令由 lead 在集成时执行（不跑 `node requirements/verification-system/tests/run.mjs` /
`node scripts/check.mjs` 于本支线）。

## 历史与弃权

| 来源 | 裁决 | 记录在哪 |
|---|---|---|
| multi-canary / parallel pool / shuffle / 三轮 repeat（test.md G4R 之前形态） | GARBAGE（target-delete）：One World 取代；只作反例不成为目标 | WHAT-002/003；本 HOW |
| `tests/e2e/cases/**`（31 cases） | GARBAGE：已删除；E2E_CASE_CEILING=0 只降不升 | WHAT-002 |
| `enforcer-rulebook-gate.mjs` | retired stub（2026-08-12）：RuleBook 散文质量属编辑/判断关切，不设机械门；空壳保留在 proof-ladder allowlist | proof-ladder allowlist |
| g4r-freeze / student-teacher-absence | 迁移期 ratchet，已删除（2026-08-14 Wave 2b）：由 `e2e-watchdog-feed`（One World 门）与 unified-store `student-qa-revival` scanner 承接 | PROOF SPLIT@cutover |
| 旧 symbol blacklist（dsl-ownership / provider-leak） | 迁移期 ratchet（PROOF-MAP 标 DELETE）：基线稳定后弱化；不进入永久 verifier | PROOF SPLIT@cutover |
| canary-unbend / orchestrator-e2e-timeout 的具体场景修复 | 历史证据：证明「断言不可弯曲」「先可解释再修根因」有现实失败模式 | WHY 考古；WHAT-004/005 |
| waitfact-causal-renewal 的 `renewOn` 记法 | 并入 VERIFY-004 因果续期语义（WHAT-006）；具体 schema 是当前 HOW | WHAT-006 |
| fix.md 的 DSL 门禁盲区（136/245 文件） | 教训并入 WHAT-009（静态门禁命中真实路径）+ WHAT-010（验收判据不可放宽） | WHAT-009/010 |
| PROOF-MAP 顶层 3 文件归属（verdict-feed→review-judgement、domain.meta→requirement-system） | 按断言内容改判 verification-system；显式记录差异，cutover 复核 | PROOF SPLIT@cutover |
| `tests/unit|integration|e2e` 顶级目录分类 | HOW/GARBAGE：当前物理载体；cutover 后按包重组 | WHAT 边界；本 HOW |
| 当前 One Long Stroke 的 OpenCode 脚本名（warmup-opencode.mjs 等） | HOW：具体脚本名是当前载体；「恰一个 Long Stroke」原则是 WHAT-002 | WHAT-002 |

## 遗留风险 / cutover 待办

- **SPLIT@cutover**：g4r-freeze 迁移 ratchet → 永久 One World 门（已执行：`e2e-watchdog-feed`）；
  覆盖门禁 → 独立 oracle 或包内测试；PROOF-MAP 归属分歧按 assertion 复核后回写协调文件。
- **GAP@cutover**：「禁止跨级」的人工裁决面（物理契约论证）暂无机器落点，若需机器化再补。
- 本包测试均为文本/文件系统级，不依赖 dist；proof-ladder 对 package.json / check.mjs 的
  格式假设（`&&` 拼接、`const checks = [...]` 形状）若未来改格式需同步适配（属本包独立
  变化）。
