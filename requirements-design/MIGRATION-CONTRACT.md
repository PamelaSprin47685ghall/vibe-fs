# Requirement Packages 迁移契约（MIGRATION-CONTRACT）

> 本文是 `requirements/` normative cutover 的**唯一共享契约**。所有迁移 agent 必须先读本文件，再读自己的 boundary card 与源材料。
> 主交接背景见 `HANDOFF.md`；本契约只规定迁移执行，不复述 ontology 设计。

---

## 0. 使命

把 `docs/`（why/what/shape/how/proof 五层）、`changes/`、`src/`、`tests/` 中混杂的产品语义，
按已完成的 45 包 ontology 迁移为 `requirements/<package>/` 下的**保姆级文档 + 每包自有测试**。

三条硬约束（用户明确指令）：

1. **每个包拥有测试**：`requirements/<package>/tests/` 是包的可执行 proof 的家；旧的 `tests/` 目录最终删除。
2. **最终必须绿**：迁移中途允许套件暂时红；结束时 `scripts/check.mjs` + 单元套件必须全绿。这是铁的。
3. **不丢失信息**：`docs/` 与 `changes/` 迁移后将被删除并存档。你消费的每一份源材料的信息都必须落进本包的文档/测试/显式记录；GARBAGE 判断也要留下「为何弃权」的记录。

---

## 1. 目标形态

```text
requirements/
  <package>/
    README.md     # 包入口页：一句话 WHY、WHAT 概览、HOW 概览、proof 概览、阅读顺序（保姆级导航）
    WHY.md        # 不可替代的存在理由（保姆级）
    WHAT.md       # 唯一 normative 合同：编号命题（保姆级）
    HOW.md        # 实现模型与约束；非 normative；含「历史与弃权」节
    PROOF.md      # 测试落点表：每个 WHAT 命题 → 具体测试（文件 + 断言锚点）
    tests/        # 本包拥有的可执行 proof（*.test.mjs）
```

- 包名 = INDEX.md 中的包名（`durable-events` 等）。包目录名 = 包名。
- 文档语言：中文为主，保留英文术语/代码标识符（与现有仓库风格一致）。
- 不创建 PACKAGE.toml / manifest（schema 未裁决，避免投机）。

## 2. 保姆级标准（每份文档必须达到）

读者是**零上下文的新工程师**：没读过 docs/、changes/、HANDOFF。他读完你的包文档后必须能：

1. 说出这个包保证什么、为什么必须独立存在、什么情况下世界就 RED 了；
2. 知道每条要求的边界（哪些看似邻近的事实**不归我**）；
3. 找到实现代码（精确到 `src/...fs` 文件与类型/函数名）并理解 HOW；
4. 对每条要求找到测试落点并知道怎么跑、红了说明什么。

因此每份文档必须：
- **自包含**：首次出现的术语给定义或指向本包内定义；不依赖读者读过其它包。
- **引用精确**：`src/Wanxiangshu/Kernel/Temporal.fs`、`tests/unit/temporal/...test.mjs` 这种级别，不许写泛称。
- **给真实例子**：从仓库现状取材（类型名、失败场景、测试断言）。
- **讲失败模式**：RED 是什么样、历史上为什么发生过（用 changes/ 考古）。

## 3. WHAT.md 命题规范

- 每个命题一个 ID：`<PACKAGE>-<NNN>`（如 `DURABLE-EVENTS-001`），标题 + 规范陈述 + 含义/动机 + 边界 + 证据指针（→ PROOF.md 行号）。
- 命题 = **当前世界必须同时成立的事实**。历史断言、迁移沉积、被拒方案不得写成命题。
- 数量不限；由你消费的源材料密度决定。宁可多而精确，不可丢信息。
- 反向覆盖：你消费的每个 OWNED clause（COVERAGE.md 单 owner 行）必须出现在本包 WHAT 或被显式驳斥；NEEDS-SPLIT clause 中本包的部分必须出现。

## 4. 测试规则（用户：可以复用现有测试，也可以自己写，不受拘束）

**每一条 WHAT 命题都必须有测试落点**，在 PROOF.md 表格中恰好一行：

```text
| 命题 | 落点测试（文件 + test/describe 锚点） | 类型 | 运行命令 |
```

落点类型：
- `MOVE`：现有单-owner 且边界干净的测试文件，**物理移入** `requirements/<package>/tests/`（适配 import 深度，删除原文件，`node --test` 验证绿）。
- `REUSE`：多-owner（SPLIT）或不宜移动的现有测试，留在原处；PROOF.md 记精确文件 + 断言锚点 + cutover 拆分计划（`SPLIT@cutover`）。
- `NEW`：没有现有落点的命题，**写新测试**进 `requirements/<package>/tests/`。

铁律：
1. **禁止移动**直接 `import ... dist/fable_modules/...` 的测试文件（test-boundary 门新增 requirements scope，会红）。
2. 新测试命名 `*.test.mjs`；helper/fixture **不得**命名 `*.test.mjs`（runner 会误发现）。
3. 移动/新写的每个文件必须单独跑绿：`node --test <file>`。
4. 移动的测试 import 路径按新深度修正；`tests/unit/support/**` 仍可用（`../../../tests/unit/support/...` 视深度）。
5. 不要为了绿而削弱断言；可红性是底线（改坏断言 = 掩盖）。
6. 你的新测试只能 import：`node:` 内置、`dist/`（已构建且新鲜）、`tests/unit/support/**`、包内 helper。不得 import 其它包目录。

## 5. 不丢失信息规则

你负责的每个包，消费以下源后必须把信息落进文档（HOW.md 的「历史与弃权」节收纳非 normative 内容）：

```text
docs/{why,what,shape,how,proof}/<相关 topic>.md   → 全部相关 Clause 的信息
changes/<相关 completed>.md                        → WHY 考古 + 被拒方案 + 失败模式
requirements-design/COVERAGE.md 相关小节            → clause → owner 归属（含 GARBAGE/HOW 判断）
requirements-design/EVIDENCE.md 相关行              → source evidence 映射
requirements-design/PROOF-MAP.md 相关行             → 现有 proof 归属
src/Wanxiangshu/** 相关模块                          → 实现模型（HOW）
tests/unit 相关 family                              → 现有 proof（落点或考古）
```

- 「信息不丢失」= 每条消费到的信息在最终交付里**有可定位的家**：WHAT 命题、HOW 说明、或「历史与弃权」记录（含 GARBAGE 裁决理由）。
- 终报给出「源文件 → 覆盖位置」映射表（源路径 → 你哪份文档哪一节吸收了它）。

## 6. 跨包协调

- **只写**：`requirements/<你自己的包>/**`；以及（若分配给你）指定的新 oracle 测试。
- **禁止改**：`docs/`、`changes/`、`src/`、`scripts/checks/`、`scripts/check.mjs`、`tests/unit/run.mjs`、`tests/unit/support/**`、`requirements-design/` 协调文件、以及不属于你的 `tests/unit/**` 文件。
- 包依赖：`DEPENDS ON` 以 `requirements-design/INDEX.md` 依赖骨架为唯一来源，逐条给一句话理由；不得增删 edge。
- 引用别的包用包名（`durable-events`）；**不得复制**别的包的命题。
- `semantic-anchors.mjs` 中你包的 semantic ID 声明 owner（这是 MECHANISM，逐 ID 归包）：在你的 PROOF.md 里列出你包拥有的 anchor id。
- 一个 assertion 只有一个 owner；共享 checker 可以，双 owner 不行。

## 7. 你需要产出的三份交付物（每包）

1. 文档：`README.md` + `WHY.md` + `WHAT.md` + `HOW.md` + `PROOF.md`（全部保姆级）。
2. 测试：`tests/` 下移动/新增的可执行 proof（每文件单独跑绿）。
3. 终报（见 §9）。

## 8. git 纪律

- 完成你的包后：`git add <你的路径>` → `git commit -m "requirements(<package>): migrate <包名> with package-owned tests"`。
- 只提交你自己的路径；不 push；不改共享历史。
- 若并发 commit 撞 `index.lock`，稍等重试；不要 `git add -A`。

## 9. 终报格式（必须逐项回答）

```text
1. 包清单 + 每包文档完成度（README/WHY/WHAT/HOW/PROOF 各自状态）。
2. 命题统计：WHAT 命题数、落点覆盖数（MOVE/REUSE/NEW 计数）、GAP 数（若有，给理由与计划）。
3. 移动文件清单：源 → 目标；每个都跑绿（附命令输出摘要）。
4. 新写测试清单 + 单跑结果。
5. 源覆盖映射：你消费的每个源文件 → 信息落在哪份文档哪一节。
6. 弃权记录：你裁决为 GARBAGE/HOW 的每一条，理由是什么、记录在哪。
7. 遗留风险 / cutover 待办（SPLIT@cutover 清单等）。
8. 你包的 semantic anchor id 清单（semantic-anchors.mjs 中归你的）。
```

## 10. 验证命令（最终绿的定义）

```text
node scripts/check.mjs                          # 全部 static gates（含新增 requirements scope 的 test-boundary）
node tests/unit/run.mjs                         # 单元套件（自动包含 requirements/<package>/tests/**）
node --test requirements/<package>/tests/<file> # 单文件验证
```

中途红可以；结束时这三条必须全绿（e2e 与 integration 由 lead 在 cutover 阶段处理，不在本轮范围）。
