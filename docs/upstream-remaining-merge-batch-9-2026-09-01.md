# Upstream Remaining Merge — Batch 9

## 范围

第 9 次累计在第 8 次最终节点之上，不回退 M0–M7E。范围仅含 M8 requirement trace AST 与 M9 Surface Manifest AST；未引入可选 fast-check pilot。

施工起点为 `upstream/master@fcd5ab11b`；PR 前已再次刷新并语义合入 `upstream/master@d76a4a8b5`，合并节点为 `5c7ad47cf`。此前累计 PR #20–#26 尚未由 upstream owner 合并；当前账号 `dyx13` 无 merge/admin 权限，因此本批只能继续提交累计 PR，不能伪造 merge SHA。owner 应只合并最新累计 PR，旧 PR 随后关闭，避免重复应用同一提交链。

## Git 节点与因果

1. `37ece7962 test(verification): expose lexical binding false greens`
   - RED 固定伪造 `test`、shadow、间接注册、动态状态、缺 callback、dead helper、非 terminal alias、错误 WHAT callback。
   - 旧 lexical/regex gate 对其中四类返回假绿。
2. `f69f4d480 feat(verification): add shared JavaScript syntax core`
   - 直接依赖 `acorn@^8.18.0`；`package-lock.json` audit 为 0 vulnerability。
   - `scripts/lib/js-syntax.mjs` 是 M8/M9 共用的唯一 parser/walker/pattern owner。
3. `ff0cda20f feat(verification): bind requirement trace to node test AST`
   - 只接受真实 `node:test` binding 的直接注册；invalid declaration 不再取得 graph edge。
   - 保留 HOW exact anchor、proof level、duplicate owner、symlink、inactive 与 portfolio 规则。
   - 15 处真实动态/间接注册改为静态 test declaration；case 数据循环仍留在 callback 内。
   - 删除旧 lexical tokenizer/scanner，不保留平行解释器。
4. `024684299 feat(verification): bind surface evidence to test callbacks`
   - exact import binding provenance 取代文本 regex；shadow、assignment、dead alias、静态不可达路径不算 use。
   - use 必须位于 active、single-primary WHAT 的直接 callback；模块 helper、嵌套 callback 与其他 law decoy 不取得 proof authority。
   - 14 个旧 proof 改为在命题 callback 中直接调用 production Surface。资源测试用 `context.after` 保留 deterministic cleanup。
5. `5c7ad47cf merge(upstream): adopt release closure structure`
   - 接受 upstream 71-node ReleaseClosure、coverage backlog 0、临时 migration ledger 删除及永久 owner gates；未复活旧 ledger。
6. `c6db87a44 fix(release): retain owner dependency lane after upstream merge`
   - 深度验证使旧 proof 精确变红：upstream 的 direct release command 绕过 `owner-dep` FCS lane。恢复 `format → text check → owner-dep → build`，不降低 oracle。
   - 同一正式格式入口修正 upstream `Interaction/Authority/Child.fs` 的一处 Fantomas 漂移。
7. `e1ec11045 fix(ownership): close cumulative surface contracts`
   - upstream 扩展后的严格 contract graph 暴露 16 条批次 7/8 Surface 跨 owner 使用；补最窄 PTY/session-port symbols。
   - Satellite 测试端口删除伪造 `Fatal` 结果；意外 SendPrompt 直接失败，避免虚构 dispatch dependency。

## 对 upstream 原内容的修改

| 修改 | 原因 | executable evidence |
|---|---|---|
| 15 个循环生成/间接 test registration → 静态 declaration | 动态标题与间接调用无法静态证明 node:test binding 与 primary WHAT | requirement trace contract 19/19；全树 3901 tests closure |
| 14 个 helper-hidden surface use → primary callback direct use | import/use 存在不等于断言因果绑定 production | surface charter 19/19；受影响行为 121/121 |
| `ReconcileSurface` law `STRUCTURED-WORKFLOW-007` → `004` | Surface 实际公开 Evidence→Decision/publish 行为；007 是 semantic-vocabulary ownership gate，不是该行为 | `reconcile-program.test.mjs` 的 004 production calls |
| `ReviewTodoSurface` owner `review-assurance` → `review-judgement`；law → `EFFECT-ACCOUNTING-011` | `semantic-owners.json` 与源码实际 owner 为 review-judgement；Surface 的 fold/view 行为证明 exact Prepared→Accepted 绑定 | `todo-accepted-precise-ref.test.mjs` 三个 production-bound counterexample |
| custom manifest fixture 不检查全局 consumer stale rows | 局部 fixture 的输入 manifest 不拥有全局 registry，交叉污染会遮蔽目标诊断；正式 manifest 仍执行 stale check | unauthorized fixture 精确得到一个 rogue consumer failure；正式 stale fixture 仍绿 |
| `format-build-test` direct command → Wireit 四步入口 | upstream direct command 未运行 `node scripts/check.mjs --lane=owner-dep`，与 ReleaseClosure 的永久 owner gate 要求冲突 | 旧 `VERIFICATION-SYSTEM-001` 与 `DISTRIBUTION-007` proof 在合并后稳定 2 RED；修复后 18/18，真实 owner lane 全绿 |
| `published-contracts.json` 增加 PTY/session-port exact symbols | upstream ReleaseClosure 把 owner graph 从 185 扩到严格的 777 contracts 后，批次 7/8 新 Surface 的边未进入新图 | 首次 owner lane 16 RED；修复后 27,226 strict uses / 624 edges / 778 contracts 全闭合 |
| Satellite fake port 的未使用 SendPrompt | 原 Surface 返回伪造 `Fatal "unused"`，制造无行为价值的 dispatch-protocol 依赖 | 改为意外调用即异常失败；Fable build、Satellite/session recovery 与 PTY 22/22 |
| `Interaction/Authority/Child.fs` 排版 | upstream 最新提交未满足仓库 Fantomas 输出 | 正式 `format-build-test` 首次报告 1 formatted；修正后 700 unchanged |

production F# 仅有 Satellite 测试 Surface 的 fail-closed fake port 修正与 upstream 文件的 Fantomas 排版；未修改业务语义、公开运行时 API、baseline、suppression、allowlist 或门禁阈值。

## 验证

- `node --test requirements/requirement-system/tests/requirement-trace.test.mjs`：19/19。
- `node scripts/checks/requirement-trace.mjs`：772 WHAT / 3902 tests，closure complete。
- `node --test requirements/js-semantic-surface/tests/surface-charter.test.mjs`：19/19。
- `node scripts/checks/js-surface-manifest.mjs`：165 registered surfaces closed。
- 15 个受影响行为文件：121/121。
- `node scripts/build.mjs`：738 F# sources / 165 surfaces。
- `node scripts/check.mjs`：全绿；700 production files；71 release-closure nodes DONE、coverage backlog 0；0 control-pyramid/deadcode/JS boundary debt。
- `node scripts/check.mjs --lane=owner-dep`：27,226 FCS strict uses / 624 owner edges / 778 contracts；authority、composition、DSL 与 decorator gates 全绿。
- `WIREIT_CACHE=none npm run format-build-test`：在最终累计树完整通过；Fantomas 700 unchanged；text/FCS gates 全绿；Fable 738 sources / 165 surfaces；authoritative unit 3854/3854；全部 integration/package 与 273/273 harness 全绿；OpenCode 1.18.18 Long Stroke 57 步 / 5.8s；`npm pack --dry-run` 2019 files、2.2 MB packed / 10.5 MB unpacked。

## 剩余边界

- GitHub CI 与 upstream merge 是外部事实，不能由本地绿色替代。
- M10 fast-check pilot 仍是可选独立批次；没有负责人对依赖、property、CI budget、seed/path 形式的裁决时，不混入本 PR。
