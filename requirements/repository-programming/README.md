# repository-programming

## 一句话 WHY

> repository 变换需要能力投影、可组合、sandboxed、all-or-nothing 的编程 surface；若拆成多套独立 RPC，path boundary、sandbox、transaction、result rendering 与 capability gate 会重复实现并漂移，模型看到的 surface 就不再与真实能力同构。

## 阅读顺序

1. `WHY.md` — 为什么这个包必须独立存在；RED 长什么样。
2. `WHAT.md` — 唯一 normative 合同：21 条编号命题（`REPOSITORY-PROGRAMMING-001..021`）。
3. `HOW.md` — 实现模型（JsSurface / JsSandbox / JsTransaction / JsToolWorkflow / FileMutationTools）与约束；含历史与弃权。
4. `PROOF.md` — 每条命题的可执行落点表；SPLIT@cutover 计划；semantic anchor 归属。

## WHAT 概览

| ID | 命题（压缩） |
|---|---|
| `REPOSITORY-PROGRAMMING-001` | js-ROLE surface 从唯一权威 `ToolCapabilitySet` 机械投影；无第二权限矩阵。 |
| `REPOSITORY-PROGRAMMING-002` | 四层同构应用到编程面：capability→基类方法→description→example→runtime gate 完全一致。 |
| `REPOSITORY-PROGRAMMING-003` | 确定性生成：同一 profile 同一字节；tier 不进工具名。 |
| `REPOSITORY-PROGRAMMING-004` | generated-name gate：只执行当前 Attempt surface 生成的名字；forged/stale fail closed。 |
| `REPOSITORY-PROGRAMMING-005` | 编程面诚实：base class 只含可执行成员；hook 只推荐 provider-visible 工具。 |
| `REPOSITORY-PROGRAMMING-006` | sandbox 无 ambient OS authority；runner 只拿数据不拿文件；deadline/memory/output bounded。 |
| `REPOSITORY-PROGRAMMING-007` | `file()`/FileView：immutable UTF-8 快照；strict UTF-8 fail closed；有序 anchor；`name±N` 是字符串下标。 |
| `REPOSITORY-PROGRAMMING-008` | `glob()`：gitignore/wildmatch 确定性枚举；永不进 `.git`；全量返回，超限由 Host 留尾收敛。 |
| `REPOSITORY-PROGRAMMING-009` | `grep()`：Grep capability 投影为 Host member；gitignore 选文件；非 UTF-8 跳过；全量返回，超限由 Host 留尾收敛。 |
| `REPOSITORY-PROGRAMMING-010` | `rewrite()`/`write()` 分离（Edit≠Write）；`FILE_NOT_FOUND`/`FILE_ALREADY_EXISTS`；same-path-once。 |
| `REPOSITORY-PROGRAMMING-011` | return 必须 JSON-compatible；commit 前校验；`INVALID_RETURN_VALUE`。 |
| `REPOSITORY-PROGRAMMING-012` | mutation 先入 ephemeral staging；durable prepare 只经统一 EventStore；禁 feature store。 |
| `REPOSITORY-PROGRAMMING-013` | multi-file all-or-nothing：任一失败全部零提交；成功结果 commit 后才暴露。 |
| `REPOSITORY-PROGRAMMING-014` | conflict detection：快照后外部修改 → `FILE_CHANGED` fail closed；不隐式 retry。 |
| `REPOSITORY-PROGRAMMING-015` | rollback 按 preimage 恢复；crash recovery 只从 EventStore facts 重建。 |
| `REPOSITORY-PROGRAMMING-016` | 结果面是 Synthetic TOML 两份文档（`#ok`/`#failed`）；无 `status` discriminator；`[data]`/`[fs]` 分离。 |
| `REPOSITORY-PROGRAMMING-017` | 并行调用绝对安全：Host 确定性串行；同文件顺序叠加无 lost update。 |
| `REPOSITORY-PROGRAMMING-018` | failure algebra：稳定失败码；可预见失败不伪装异常；不从 exception message 反推业务错误。 |
| `REPOSITORY-PROGRAMMING-019` | program return 在 commit 前诚实编码；return 与 commit 耦合；纯查询零 mutation。 |
| `REPOSITORY-PROGRAMMING-020` | `mv`/`rm` 文件变换的 POSIX 语义（编程面的变换成员）。 |
| `REPOSITORY-PROGRAMMING-021` | 静态门禁禁手写 per-role js-* 变体；生成面与 runtime gate 同源（应用 capability-enforcement 同构律）。 |

## HOW 概览

- **投影**：`src/Wanxiangshu/Domain/{JsCapability,JsSurface,JsDescription,JsFailure,JsAnchor,JsTransaction}.fs`；`JsToolGenerator.generate(roleName, ToolCapabilitySet, prose)` 是唯一 surface 来源。
- **执行**：`src/Wanxiangshu/Process/JsSandbox.fs`（deadline/kill/framed result）+ `src/Wanxiangshu/Infrastructure/JsToolsBindings.fs`（read/glob/grep/rewrite/write 注入）。
- **事务**：`src/Wanxiangshu/Infrastructure/{JsGlobFs,JsMutationFs,JsUtf8Fs,JsAnchorFs,JsToolsTransactionStore}.fs`；durable prepare/commit 是统一 EventStore 上的 `JsTransactionPrepared`/`JsTransactionCommitted` facts（`Domain/JsTransaction.fs`）。
- **编排**：`src/Wanxiangshu/Infrastructure/OpenCode/Tools/{JsToolWorkflow,JsToolHost}.fs`（workflow + 生成 description/spec）。
- **变换成员**：`src/Wanxiangshu/Infrastructure/OpenCode/Tools/FileMutationTools.fs`（mv/rm）。
- **门禁**：`scripts/checks/js-surface-gate.mjs`（禁手写 per-role js-* 变体）。
- 细节见 `HOW.md`。

## proof 概览

- 本包测试（MOVE 自 `tests/unit/js-tools/`、`tests/unit/tools/`、`tests/unit/verify/`）：`js-surface`、`js-bindings`、`js-sandbox`、`js-anchors`、`js-tools-fs`、`js-transaction`、`js-tools-transaction-store`、`js-workflow`、`js-tool-host`、`file-mutation-tools`、`js-surface-gate` + NEW `js-parallel-contract`。
- 复用：`scripts/checks/js-surface-gate.mjs`（static gate）、`requirements/repository-programming/tests/integration/plugin/file-mutation-tools.test.mjs`（plugin 级 mv/rm + 角色门禁，REUSE 不移动）。
- 单跑：`node --test requirements/repository-programming/tests/<file>`（与 runner 一致设 `WANXIANGSHU_PROVIDER_LANGUAGE=en`）。全套：`node requirements/verification-system/tests/run.mjs`。

## 边界（DOES NOT OWN）

- office capability canonical authority → `office-capability`（本包只消费 `ToolCapabilitySet`，不裁决权限）。
- capability 同构/同源律本身 → `capability-enforcement`；本包只把它应用到编程面。
- builtin 文件系统工具是否长期 coexist、`js-*` 具体工具名、JS 语言与 base class 的具体形态 → HOW（当前实现形态，非永久合同）。
- Synthetic TOML 的一般 representation law（值编码/引号/布局）→ `provider-projection`；本包只消费 `SyntheticToml` 渲染结果面。
- durable prepare/commit 的 EventStore substrate → `durable-events`；transaction 的 Requested/Prepared/Committed 效果分型 → `effect-accounting`。
- Git shared-ref integration → `change-integration`。

## DEPENDS ON

`office-capability`、`capability-enforcement`、`effect-accounting`、`durable-events`、`participant-horizon`（逐条理由见 `HOW.md` §依赖）。
