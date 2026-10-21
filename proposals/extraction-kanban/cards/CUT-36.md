# CUT-36：第二宿主真实 canary（DOING，不挡主线）

返回 [看板](../BOARD.md) / [职责卡](../CARDS.md#cut-36) / [范围对账](../COVERAGE.md) / [模板](../TEMPLATES.md#contract)。

## 1. 当前状态与 D-07 处置边界

- **当前列状态**：`DOING`（2026-10-21 仓主授权后由 `OPTIONAL` 转入；`CUT-38` 终局交账当时不等它，但本卡自有后续交账）
- **所属泳道 / 波次**：可选 / W5（授权已具备：仓主确认以 **omp 18.12.4** 为第二宿主（含锁定版本），并同意发起 canary 所需的**真实模型调用**）
- **切换前置**：`CUT-34`（OpenCode 适配边界）、`CUT-35`（顶层组装与逐域恢复）
- **关联决策护栏（`D-07`）**：
  - 历史记载（2026-10-21 之前）：独立库仓虽存在 `wanxiangli/src/host-omp` 草稿目录，但当时尚未核实该草稿对应的实际外部宿主产品、锁定版本号、公开入口、Hook/SDK 协议及子会话能力；
  - 严禁根据目录名 `host-omp` 凭空编造宿主 API、写模拟回调冒充物理 canary，或宣布“已获全面实现授权”；
  - 更不得把“领域库采用了可移植的宿主无关设计”偷换表述为“已经支持第二宿主”。
  - **2026-10-21 更新（取代上面前两条）**：第二宿主已由仓主指认为 omp 18.12.4，且真实模型调用已获授权；上两条约束中“尚未核实宿主身份/版本/入口”一条已解除，“不得凭空编造 API / 不得用模拟回调冒充 canary / 不得宣称已支持第二宿主”三条**继续有效**。

## 2. 目标与启动前提

- **本卡存在的唯一目的**：当且仅当未来获得用户明确授权、且提供了可运行的第二宿主真实二进制/运行时环境时，用最小真实物理 canary 检验 `wanxiangli` 独立领域个体（`Work`、`Ownership`、`Trace`、`ProcessSession`、`Resources` 等）在脱离 OpenCode 1.18.29 后，能否无需重写领域内核即可被第二宿主装配驱动。
- **启动前提（缺一不可，未齐备前保持 `OPTIONAL`）**：
  1. 用户或委派人明确指认第二宿主的真实软件包名称、精确锁定版本及可复现安装方式；
  2. `CUT-34` 与 `CUT-35` 已将 OpenCode 专属 SDK 调用与领域组合切开；
  3. 看板协调者登记将本卡从 `OPTIONAL` 转为 `READY` / `DOING`。
  - **2026-10-21 更新**：三条前提均已具备，逐条依据：① 宿主指认——仓主指认 **omp 18.12.4**（本机 `omp --version` = `omp/18.12.4`，可执行文件 `/usr/local/bin/omp` → `/home/hermes/.local/bin/omp`）；② `cards/CUT-34.md` 与 `cards/CUT-35.md` §1 均记 `当前列状态：DONE`；③ 本卡已在看板登记为 `DOING`。下面“明确不做”三条继续有效。
- **明确不做**：
  - 在没有真实第二宿主进程的情况下，不编写纯内存 mock 宿主或假 callback 测试来宣称完成；
  - 不为迁就假想的第二宿主而提前在主仓制造一层“通用万能宿主抽象框架（Universal Host Framework）”；
  - 不继续调用 OpenCode SDK 却改名为第二宿主适配器。

## 3. 若未来获批启动时的最小 Canary 契约

一旦上述前提齐备，首个真实 canary 只验证以下四项最小物理事实：
1. **产物真实加载**：Fable 编译生成的 ESM 产物被第二宿主真实进程加载，不依赖 OpenCode 内部模块或全局注入；
2. **工作区根定位**：在第二宿主生命周期入口准确读取 Git 证明的工作树根目录与 `git-common-dir`，初始化 `wanxiangli` 的 `Work` 与 `Ownership`；
3. **Provider 请求观察与拦截**：观察第二宿主真实发出的一次模型请求，验证系统提示词与工具描述经 `Resources` 成对双语渲染注入；
4. **子会话或受管执行边界**：如实探测第二宿主是否支持创建/查询子会话；若该宿主物理上不支持子会话，则如实报告能力边界并拒绝对应委派工具，绝不伪造内存假子会话冒充宿主能力。

## 4. 必须能红的反例护栏

1. **反例 1（文档/类型有字段，真实宿主快照无该字段）**：适配器对宿主传入对象执行与 `host-boundary-030` 相同级别的精确 JS 运行时类型谓词校验；缺失身份或字段类型不符时立即 fail-closed，不靠默认值猜测。
2. **反例 2（模拟回调冒充物理 canary）**：canary 必须启动真实第二宿主子进程并通过公开接口驱动，任何仅在 `node:test` 内手写 JS 对象模拟宿主 Hook 回调的测试均不得计为本卡的 canary 证明。
3. **反例 3（切换宿主迫使重写领域库）**：若接入第二宿主需要修改 `wanxiangli/work`、`wanxiangli/ownership`、`wanxiangli/trace` 的领域规则，说明领域库仍泄漏了 OpenCode 假设，必须先回退修复对应领域卡，而非在领域库里塞入 `if (host === 'omp')` 分支。

## 5. 写集合与退出口径

- **当前写集合（2026-10-21 更新）**：本卡说明文档 `proposals/extraction-kanban/cards/CUT-36.md` 与相关看板/交接文档；实现侧的第二宿主产品装配写在主仓 `src/Wanxiangshu/Host/SecondHost/HostPlugin.fs`（见 §7 范围说明），omp 侧适配仍在 `proposals/host-neutral-interface/adapters/omp/`。库仓 `wanxiangli/src/host-omp` 草稿保持零改动。
- **对 `CUT-38` 终局交账的口径**：
  - `CUT-38` 当时已按 OPTIONAL 交账，本卡不追溯改写那一笔；本卡的后续交账以本卡 §7 与看板为准；
  - `wanxiangli/src/host-omp` 草稿在发布打包白名单（`package.json` `files` / `exports`）中保持不导出、不夹带，不影响主线净改进验收。

## 6. 2026-10-21 可行性核对（eng-cut34b-goose 核对，eng-cut34c-goose 复核确认）〔本节结论已被 §7 取代，保留为历史〕

- **找到了什么**：本机 `/home/box/.local/bin/omp` 是真实的 oh-my-pi CLI，`omp --version` 输出 `omp/18.12.4`。它和库仓草稿 `src/host-omp` 的名字对得上，公开入口也有：`-e/--extension`、`--hook`、`--plugin-dir`，以及 `--mode rpc|json` 和 `-p` 非交互模式。所以“有没有真实第二宿主二进制”这一条，在本机上可以答“有”。
- **为什么仍保持 `OPTIONAL`（不启动）**：
  1. 前置 `CUT-35` 还没有 DONE。按 §2 第 2 条，领域组合尚未从 OpenCode 组合根切开。现在做 canary，只能证明“又写了一份组合根”。
  2. §2 第 1 条要求用户或委派人明确指认第二宿主及锁定版本。本次授权是执行抽取看板，没有指认 omp 作为第二宿主，也没有授权改动它的安装或配置。
  3. §3 第 3 项要观察一次真实 provider 请求，就得用本机 omp 配置的真实模型凭据发起一次真实模型调用，会花用户的额度。这超出本次授权。只读加载、不发请求的半截 canary 达不到 §3 的四项，按 §4 反例 2 的精神也不能算数。
- **本次没有做的事**：没有读取或使用 `~/.omp` 下的凭据或 token，没有启动 omp 会话，没有写 `src/host-omp`。
- **解除条件 / 下一检查人**：CUT-35 DONE，且用户确认以 `omp 18.12.4` 作为第二宿主并同意发起 canary 所需的真实模型调用后，由协调者把本卡转为 READY。CUT-38 交账时本卡按 §5 诚实保留为 OPTIONAL。

## 7. 2026-10-21 授权确认与当前实现范围（supersede §6 的“不启动”结论）

- **授权事实（仓主，本轮明确指示）**：
  1. 第二宿主确认为 **omp 18.12.4**（锁定版本）；
  2. canary 所需的**真实模型调用已获授权**（会消耗用户额度），凭据按 omp 自身配置使用；
  3. 据此本卡由 `OPTIONAL` 转为 **`DOING`**。
- **§6 的历史结论如何处置**：§6 记录的是 2026-10-21 可行性核对当时“保持 `OPTIONAL`、不启动”的判断与理由（当时 `CUT-35` 未 DONE、宿主未指认、真实调用未授权）。该判断**已被本节取代**，其段落保留作为审计痕迹，不再是当前状态依据。
- **当前实现范围（诚实边界）**：
  - **产品侧**：主仓新增 `src/Wanxiangshu/Host/SecondHost/HostPlugin.fs`（`IHostPlugin` 的真实实例，端口族 P-11）：激活时用 `GitSubject.revParseShowToplevel` 定位工作树根，用 `RuntimePath.stateRoot` 解析机器状态目录（产品内部 `gitCommonDir` 语义即该状态目录），打开 durable Work 与 durable Ownership scope，并提供产品工具 `wxs_deliver`（描述取自产品资源树，成对双语）。该文件当前**尚未纳入编译分片清单**（`src/Wanxiangshu/*.fsproj` 未收录），因此尚无对应构建产物。
  - **宿主侧**：`proposals/host-neutral-interface/adapters/omp/` 仍是 **HNI-03 的类型检查 + 离线 fixture 通过的骨架**（`ports.ts` 由 `gen-ports.mjs` 机械生成并由 `mirror-check.mjs` 逐字节比对），`activateOnOmp` 尚未与产品产物绑定；O-3（真实绑定需要 Fable 编译出的 JS 产物并绑定到 `activateOnOmp`）**仍然开放**。
  - **smoke 侧**：`proposals/host-neutral-interface/smoke/` 已备好 opt-in 真实 smoke（`CUT36_OMP_REAL_SMOKE=1` 才启动真实 omp 进程；断言只认宿主观察事件与磁盘事实，产品不写自述报告）。**真实 smoke 尚未通过**：最近一次运行为 FAIL（宿主侧扩展加载失败），不得据此宣称 canary 成立。
- **因此当前不能说的话**：不说“smoke 已通过”，不说“已支持第二宿主”，不说端口族已 WIRED-OMP。当前只能说：授权已具备，canary 在做。
- **下一检查人 / 转 DONE 条件**：待真实 smoke 跑通 §3 四项（产物真实加载、工作区根与状态目录定位、一次真实 provider 请求且双语资源注入、真实子会话/受管执行边界）并留下 `proposals/extraction-kanban/evidence/CUT-36/` 下的证据后，由协调者把本卡与看板改为 `DONE`；在那之前保持 `DOING`。
