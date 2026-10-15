# CUT-00：两仓准备、差异裁决与并行开工

返回 [看板](../BOARD.md) / [职责卡](../CARDS.md#cut-00) / [操作指南](../README.md)。

## 1. 认领记录

```text
卡号：CUT-00
这一棒只完成：两仓主线、锁包与工具链实测核对；D-01—D-07 初判与执行护栏落盘；首批并行卡（CUT-02、CUT-03、CUT-04）认领与公共编辑面预约，以及 W1 独立卡（CUT-05、CUT-07、CUT-18、CUT-21、CUT-24、CUT-26）READY 转化
Owner：eng-cut00
Reviewer（非作者）：manager
主仓 base SHA / branch / worktree：b322c180be8048597199be91daaaa9391f78c4f9 / master / /home/kunweiz/Desktop/vibe/wanxiangshu
库仓 base SHA / branch / worktree：46d9c072af50389d069709e847d5772af3ec160a / main / /home/kunweiz/Desktop/vibe/wanxiangshu-lifecycle-cutover/wanxiangli
已有相关在制工作/原 T-WP-GAP 编号：盘点建板基线 8f7f9be77（已合入主线 b322c180b）；CUT-01 已合入（参见 ../lifecycle-cutover-closeout.md）
准备修改的精确文件（含 .fsi、工程、测试）：proposals/extraction-kanban/cards/CUT-00.md、proposals/extraction-kanban/BOARD.md
需要预约的公共文件：proposals/extraction-kanban/BOARD.md（当轮协调者登记）
我不会修改：src/**、tests/**、package.json、package-lock.json、vendor/** 及库仓源码
前置卡/协议版本与已取得证据：wanxiangli@0.5.2（vendor/wanxiangli-0.5.2.tgz，SHA-256: 53f21944e86a314035d82e40e395aa785730e95110a95fb74177791460422656）
最可能推翻边界的一个反例：两个工作树指向同一 SQLite/common-dir，或同一公共编辑面被两张并行卡同时无预约修改，或以新库草稿直接删除主仓仍生效的业务语义
预计交接条件（不是口头时间承诺）：两仓基线、D-01—D-07 执行护栏、W1 首批卡认领与公共编辑面预约落盘至 CUT-00.md 与 BOARD.md
当前已启动的验证/物理资源及其 owner：无
```

## 2. 两仓基线实测事实

### 2.1 主仓与库仓版本状态

| 仓库 | 物理路径 | 当前分支 | 起始 HEAD | 工作树状态 (`git status --short --branch`) | 备注 |
|---|---|---|---|---|---|
| 主仓 `wanxiangshu` | `/home/kunweiz/Desktop/vibe/wanxiangshu` | `master` | `b322c180be8048597199be91daaaa9391f78c4f9` | `## master...origin/master`（干净） | 盘点建板基线 `8f7f9be77` 已合入主线 |
| 库仓 `wanxiangli` | `/home/kunweiz/Desktop/vibe/wanxiangshu-lifecycle-cutover/wanxiangli` | `main` | `46d9c072af50389d069709e847d5772af3ec160a` | `## main...origin/main`（干净） | 当前版本 `0.5.2` |

### 2.2 依赖与锁包状态

- `package.json` 中声明 `"wanxiangli": "file:vendor/wanxiangli-0.5.2.tgz"`，且 `"wanxiangli"` 位于 `bundleDependencies` 中。
- `vendor/wanxiangli-0.5.2.tgz` 的 SHA-256 摘要为 `53f21944e86a314035d82e40e395aa785730e95110a95fb74177791460422656`，与 `node_modules/wanxiangli` 安装内容一致。
- `wanxiangli@0.5.2` 已导出六个子路径入口：
  1. `wanxiangli/work`
  2. `wanxiangli/eventstore`
  3. `wanxiangli/durable-work`
  4. `wanxiangli/process`
  5. `wanxiangli/trace`
  6. `wanxiangli/ownership`

### 2.3 工具链与构建验证命令

- **工具链实测版本**：
  - Node：`v26.8.1`
  - npm：`12.2.0`
  - Bun：`1.4.0`
  - Fantomas：`v6.3.0`
- **主仓构建与门禁命令**（严禁使用 `dotnet build`，本仓只有 Fable 编译目标）：
  - 构建：`node scripts/build.mjs`（或 `npm run build`）
  - 格式化：`npm run format`（`dotnet tool run fantomas src/Wanxiangshu`）
  - 格式检查：`npm run format:check`（`wireit` -> `dotnet tool run fantomas --check src/Wanxiangshu`）
  - 静态检查：`npm run check`（`wireit` -> `node scripts/check.mjs`）
  - 完整格式化+构建+测试验证：`npm run format-build-test`（`node scripts/verify.mjs`）
  - 发布级验证：`npm run verify:release`（`node scripts/verify.mjs --release`）

## 3. `D-01`—`D-07` 初判与执行护栏

各并行卡在接线与抽取过程中必须遵守以下裁决护栏，不得以草稿简化为由擅自弱化主仓现有契约：

| 决策号 | 影响卡 | 初判结论与执行护栏 |
|---|---|---|
| **D-01** | 07、09、11 | **保留现行 `RejectUnknownOrigin` 拒绝语义，禁止回退为 `human`。** 权威来源无法证实时必须明确拒绝或保持 typed unknown，不得按草稿回退成 `human` 身份。 |
| **D-02** | 03、06、15、16、28 | **在 CUT-03 先建立 `Trace` 与主仓 `XTrace` 差异对账表。** 先切换已对齐的单流确认轨迹、哈希链、窗口切片与 Opening/Originals/Statement 投影；保留多流并发、Reasoning、`ProviderRetryAttempt` 排除与 Fission 合流在显式组合层 C。 |
| **D-03** | 02、20、21 | **在 CUT-02 接入 `wanxiangli/process` (`ProcessSession`) 承担非 PTY 进程启动、进程组信号与尾缓冲。** 同时在主仓组合层保留硬期限强杀确认、全量 `Spool` 外存与取消联动；PTY 留待 CUT-20，显式时钟留待 CUT-21。 |
| **D-04** | 18、19 | **`wanxiangli/eventstore` (`SqliteJournal`) 单流 SQLite CAS 与主仓多 writer JSONL 合法分叉 `DomainConflict` 分属不同承诺。** 在 CUT-18/19 中分别建模与演进，禁止用单流 CAS 抹杀多 writer 分叉。 |
| **D-05** | 27、29、30、31 | **Casebook、Strength/投机调查、Rulebook/退化观察、Sphinx 默认暂留并维护。** 未经逐卡裁决不得借口删库。 |
| **D-06** | 32、33、38 | **主干合并采用相对基线净改进准入，验证结果如实报告，不虚报 Long Stroke。** |
| **D-07** | 00、34、36 | **按当前授权推进 `wanxiangshu` 与 `wanxiangli` 重构。** OpenCode 宿主适配归 CUT-34，第二宿主 `CUT-36` 保持 `OPTIONAL`。 |

## 4. 首批并行卡认领与公共编辑面预约安排

### 4.1 首批进入 `INTEGRATING` 的接线卡（已在 `wanxiangli@0.5.2` 具备 `LIB_READY` 基础包）

1. **CUT-02（非 PTY 进程执行）**
   - **当前列**：`INTEGRATING`
   - **Owner / Reviewer**：`eng-cut02` / `manager`
   - **预约公共编辑面**：`src/Wanxiangshu/Process/{ProcessRunner,NodeProcessHost,NodeProcessWait,Surface}.fs(.fsi)`
   - **起始 base**：`b322c180b`
   - **释放条件**：`CUT-02 完成进程执行接线与 process-execution 测试回归`
2. **CUT-03（原始轨迹与固定区间）**
   - **当前列**：`INTEGRATING`
   - **Owner / Reviewer**：`eng-cut03` / `manager`
   - **预约公共编辑面**：`src/Wanxiangshu/Context/Trace/*.fs(.fsi)`
   - **起始 base**：`b322c180b`
   - **释放条件**：`CUT-03 完成确认轨迹子范围接线与 semantic-trace 测试回归`
3. **CUT-04（唯一资源归属与关闭）**
   - **当前列**：`INTEGRATING`
   - **Owner / Reviewer**：`eng-cut04` / `manager`
   - **预约公共编辑面**：`src/Wanxiangshu/Execution/Session/Ownership.fs(.fsi), src/Wanxiangshu/Mission/Lifecycle/DurableWorkHost.fs(.fsi)`
   - **起始 base**：`b322c180b`
   - **释放条件**：`CUT-04 完成资源归属接线与 session/work 测试回归`

### 4.2 已核清契约准备、转入 `READY` 的 W1 独立卡

以下 W1 独立卡的前置条件（仅依赖 `CUT-00`）与编辑面边界已核清，互不与 `CUT-02`/`CUT-03`/`CUT-04` 的预约编辑面冲突，在看板中由 `BACKLOG` 更新为 `READY`：

- **CUT-05**（双语资源与包内定位，泳道：表达）
- **CUT-07**（参与者身份与执行绑定分离，泳道：权威，受 D-01 护栏约束）
- **CUT-18**（规范事实编码与校验，泳道：存储，受 D-04 护栏约束）
- **CUT-21**（显式时间与因果等待，泳道：物理，受 D-03 护栏约束）
- **CUT-24**（文件读取、锚点与原子编辑，泳道：仓库）
- **CUT-26**（需求发现与材料溯源，泳道：仓库）

## 5. 卡完成检查（CUT-00）

- [x] 两仓主线分支、起始 HEAD、工作树干净状态已实测核对并记录。
- [x] `package.json` 依赖声明、`bundleDependencies`、`vendor/wanxiangli-0.5.2.tgz` SHA-256 及六个导出子路径已核实。
- [x] 工具链版本（Node / npm / Bun / Fantomas）与主仓 Fable 构建验证命令已记录。
- [x] `D-01`—`D-07` 初判与执行护栏已落盘，明确禁止未知来源回退 `human`、禁止单流 CAS 替代多 writer 分叉、禁止未经裁决删库。
- [x] `CUT-02`、`CUT-03`、`CUT-04` 已认领进入 `INTEGRATING` 并完成公共编辑面预约登记。
- [x] W1 独立卡 `CUT-05`、`CUT-07`、`CUT-18`、`CUT-21`、`CUT-24`、`CUT-26` 已由 `BACKLOG` 更新为 `READY`。
