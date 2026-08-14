# Casebook — 所有权与边界

## 所有权

| Owner | 拥有 | 不拥有 |
|---|---|---|
| Domain/Kernel | Case / Observation / ObservationIdentity / ObservationReplayResult / CasebookProjection fold / prune / classifyReplay / normalizeObservations | Git I/O、`GitObjectId`、feature ref API |
| Application | archiveInspectorResult（Append Captured）/ fetchCase / refreshCase（Append Refreshed）/ touchCaseAccess（Append Accessed）/ evictCases（Append Evicted） | 自有 sync；第二状态机 |
| Infrastructure | IEventStore / StoreSnapshot（Persist）；filesystem evidence reads；Host tool observation adapter；SyntheticToml renderer；`js-bookkeeper` surface | feature tree authority、Casebook ref、hook、LWW；旧名 `edit-qa` |
| Session/Process | Inspector epoch CasebookIndexSnapshot freeze（进程内）；same-worktree fetch single-flight；Bookkeeper child lifetime（`fast-bookkeeper`/`deep-bookkeeper`） | 长期领域状态机、pin refs、hook recursion guard；Inspector self-model |
| Persist + GitGateway | 物理 CAS / converge / dumb remote | 领域语义 |

## Bookkeeper 身份边界（CASE-006）

| 面 | Owner | 禁止 |
|----|-------|------|
| 机器身份 | `fast-bookkeeper` / `deep-bookkeeper`（AGENT-002 强制内部 pair） | 用 `fast-inspector`/`deep-inspector` 创建 Bookkeeper session |
| Persona | Clerk / Curator（AGENT-028；session 创建冻结） | 复用 Scout/Investigator self-model / Inspector system 身份字节 |
| 模型绑定 | 可复用 inspector **model config** | 复用 Inspector Role Law / 工具矩阵 / 自称 |
| 唯一工具 | `js-bookkeeper(program)` | `edit-qa`、filesystem capability、第三文件写入 |
| 可见性 | InternalLeaf + Attached；AGENT-008 不可见 | 进 Manager fork 面 / public Role DU / provider enum |

## 责任区

```text
Domain/Kernel    Case 纯类型 + fold/prune/classify/normalize（零 Host I/O）
Application      结构化 workflow（Append Captured/Refreshed/Accessed/Evicted）
Infrastructure   EventStore adapter + evidence reads + observation adapter + TOML + js-bookkeeper
Session/Process  index freeze / single-flight / Bookkeeper lifetime + Persona bind
```

## 硬边界

```text
Casebook 不拥有 sync / hooks / refspecs（Persist 拥有）
Case 动态数据只经 refs/wanxiang/store，不进入 worktree
不建立第二运行时 / 第二状态机
Domain 不得出现 GitObjectId / RootOid / StoreSnapshot / AppendCandidate
不得创建 refs/wanxiang/inspector-casebook 或 pin refs
不得用 revision / wall_clock 排序或 merge
capture 来自 typed execution，不从 transcript 推断
fetch 不写 subject worktree（replay 只读）
provider index 不泄漏 session/status/freshness 机器字段（ARCH-014）
js-bookkeeper：一次程序原子 staged 变换；setQuestion/setAnswer 各至多一次；zero mutation 合法
```

## 禁止端口（proposal §59 SUPERSEDED）

```text
compareAndSwapCasebookRoot / fetchRemoteCasebook / pushRemoteCasebookWithLease
pinCasebookRoot / releaseCasebookPin / readCasebookRoot（as feature-ref authority）
```

## 文档与门禁

正式主题 `docs/{why,what,shape,how,proof}/casebook.md`，Clause 前缀 `CASE-`；spec.mjs 注册前缀；`unified-store-gate` 继续禁止 feature-owned storage。
