# CUT-41　GAP 台账逐条核实与精确重定向

返回 [看板](../BOARD.md) · 证据 [GAP-audit-2026-10-21](../evidence/GAP-audit-2026-10-21.md)

## 1. 目标

交接文件「未完事项」第 6 条点名的六条台账（`requirements/GAP.md`）逐条核实并给出**精确处置**：
GAP-088、GAP-075、GAP-123、GAP-015、GAP-005、GAP-050。

台账定义（`requirements/GAP.md:5-13`）允许三种处置，**不得豁免**：

- **A 真实测试关闭**：在包 `tests/` 落地独立 oracle（能单独变红），记测试路径与 commit。
- **B 实现缺失见证**：生产代码落 durable 事实，用例只读回。
- **C 精确重定向**：把台账行指向**当前实际管辖**的 requirement 包与条款，并写清剩余未闭合工作。

本轮**只做 C**。GAP-123 的宿主见证（四类 durable source terminal fact + `ExpectedClosureKind` 原子记录）
属**产品运行时工作**，超出本审计 lane 的范围，故不在本切片内实施；该行状态保持 **OPEN**，**不作豁免**。

## 2. 依据

- 规范条款以各包 `WHAT.md` 为准（台账第 3 条纪律）。
- 基线：`origin/master` = `9003c203260cdaade2d1c7d4810e476e68666385`；子模块 `wanxiangli` 按 pin
  `fde10a080e86c80c3db05c73ecdfdbc4ef2c2e37` 同步。
- 逐条诊断（现行条款路径、测试锚点、处置、剩余工作）见
  [evidence/GAP-audit-2026-10-21.md](../evidence/GAP-audit-2026-10-21.md)。

## 3. 本轮改动

| GAP | 现行包 / 条款 | 处置 | 状态（改后） | 剩余工作（登记在台账行内） |
|---|---|---|---|---|
| GAP-005 | `requirement-system` [015] | C | OPEN（不变） | 无 faithful mechanical oracle；人工审阅；机器落点需规范侧先改写为可判定谓词 |
| GAP-050 | `requirement-system` [003] | C | OPEN（不变） | 无 faithful mechanical oracle；人工语义审阅；机器落点需独立于本仓的权威模型 |
| GAP-015 | `feature-ablation` [005] + `capability-enforcement` [005] 用例 | C（命题退役） | PARTIAL（改指） | 治理清理已完成；功能本身不恢复（`584c30c61be54ad08f96263410cf28257e22b096` 删包） |
| GAP-075 | `capability-enforcement` [025]/[026] | C（残余收窄） | PARTIAL（不变） | ①真实并发 overlap 独立 oracle；②跨进程能力隔离（复用 019 两相夹具形态） |
| GAP-088 | `repository-programming` [003]/[006]/[017]/[025]/[026]/[027] | C（残余逐条列名） | PARTIAL（不变） | 006:135、017:21、025:305、025:306 四条 todo 各自需独立 oracle（027 已无 todo，旧描述过时已移除） |
| GAP-123 | `interaction-authority` [018] + `managed-session-lifecycle` [020]/[026] | C（产品缺口） | OPEN（不变） | 四类宿主 durable source terminal 事实；`AuthorityRootAccepted` 原子携带 `ExpectedClosureKind`；writer 不再硬编码 `RootKind="HumanRoot"`；三条 todo 转真测试；第二步收束待本条落地 |

连带修正（同一台账内，指向已删包的历史行）：GAP-183、GAP-184 改指 GAP-015；
`### 上游已关闭记录的本批校正` 段的 GAP-005 / GAP-015 / GAP-050 三条摘要同步。

## 4. 边界（明确不做）

- 不改任何 `CLOSED` 行；不新增豁免；不新增源码文本守卫（上游已否决的 grep-on-source 不得复活）。
- 不手写 durable witness（见证必须由生产落下，用例只读）。
- 不改 `requirements/GAP.md` 之外的规范、WHAT/WHY、`src/` 生产代码。
- 不动 `ruling-void` lane 拥有的裁决文档与其在途行（GAP-218、delegation/crash-reconciliation/
  intra-participant-parallelism 的 WHAT/WHY）。
- 不实施 GAP-123 的宿主运行时改造（属产品缺口，非本切片）。

## 5. 验证

- 台账每行的现行条款路径、测试锚点（文件:行 + 断言标题）、生产锚点逐条比对现行工作树。
- `requirements/GAP.md` 无脚本或测试消费方（仅包 README 的散文链接），改动面限于文档；
  表格列数与 `HEAD` 逐行一致（5 列 ×144、9 列 ×51、8 列 ×7、12 列 ×1），未破坏任何既有行。
- `node scripts/checks/architecture.mjs` → `architecture: OK`（exit 0）。
- `node scripts/check.mjs` → exit 1，**131 pyramid + 1 js-boundary = 132**，与 metrics lane 在
  `9003c203` 上实测的基线**逐项相同**；本切片未改任何 `.fs`，两处红均为既有门禁红，
  非本卡引入。`shard-edge-audit` report-only：`265 finding(s) [high=2 medium=0 low=263]`
  （rebase 后 master 已前进，计数随之上移；pyramid 与 js-boundary 计数不变）。
- `node scripts/checks/subsystems.mjs` → `OK — 25 subsystems, 241 compile shards, 658 sources,
  1956 refs, shard DAG, largest subsystem cycle=10`（exit 0）。
- `package-lock.json` 为合法 JSON（lockfileVersion 3）；`wanxiangli` gitlink =
  `fde10a080e86c80c3db05c73ecdfdbc4ef2c2e37`，与 `origin/master` 同指，且在 `wanxiangli`
  `origin/main` 上可达。
- **未取得**：受影响的包套件（requirement-system、capability-enforcement 025/026、
  interaction-authority 018、managed-session-lifecycle 020、repository-programming 006/017/027）
  在 `dist/` 产物上的 pass/fail/todo 精确计数——本机 `flock /home/hermes/work/vfs-build.lock`
  上排队 40+ 个兄弟 lane 的 `--clean` 构建，本 lane 的构建在本轮内未取得产物，
  故**不报数字**。这些套件不消费 `requirements/GAP.md`（已核），本次改动不改变其行为。

## 6. 状态

`DONE`（范围 = 台账核实与重定向本身；GAP-123 等剩余工作按台账登记在各自行内，不在本卡内闭合）。
