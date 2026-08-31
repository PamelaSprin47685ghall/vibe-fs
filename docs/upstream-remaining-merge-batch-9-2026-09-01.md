# Upstream Remaining Merge — Batch 9

## 范围

第 9 次累计在第 8 次最终节点之上，不回退 M0–M7E。范围仅含 M8 requirement trace AST 与 M9 Surface Manifest AST；未引入可选 fast-check pilot。

基线为 `upstream/master@fcd5ab11b`。此前累计 PR #20–#26 尚未由 upstream owner 合并；当前账号 `dyx13` 无 merge/admin 权限，因此本批只能继续提交累计 PR，不能伪造 merge SHA。owner 应只合并最新累计 PR，旧 PR 随后关闭，避免重复应用同一提交链。

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

## 对 upstream 原内容的修改

| 修改 | 原因 | executable evidence |
|---|---|---|
| 15 个循环生成/间接 test registration → 静态 declaration | 动态标题与间接调用无法静态证明 node:test binding 与 primary WHAT | requirement trace contract 19/19；全树 3901 tests closure |
| 14 个 helper-hidden surface use → primary callback direct use | import/use 存在不等于断言因果绑定 production | surface charter 19/19；受影响行为 121/121 |
| `ReconcileSurface` law `STRUCTURED-WORKFLOW-007` → `004` | Surface 实际公开 Evidence→Decision/publish 行为；007 是 semantic-vocabulary ownership gate，不是该行为 | `reconcile-program.test.mjs` 的 004 production calls |
| `ReviewTodoSurface` owner `review-assurance` → `review-judgement`；law → `EFFECT-ACCOUNTING-011` | `semantic-owners.json` 与源码实际 owner 为 review-judgement；Surface 的 fold/view 行为证明 exact Prepared→Accepted 绑定 | `todo-accepted-precise-ref.test.mjs` 三个 production-bound counterexample |
| custom manifest fixture 不检查全局 consumer stale rows | 局部 fixture 的输入 manifest 不拥有全局 registry，交叉污染会遮蔽目标诊断；正式 manifest 仍执行 stale check | unauthorized fixture 精确得到一个 rogue consumer failure；正式 stale fixture 仍绿 |

未修改 production F#、业务语义、公开运行时 API、baseline、suppression、allowlist 或门禁阈值。

## 验证

- `node --test requirements/requirement-system/tests/requirement-trace.test.mjs`：19/19。
- `node scripts/checks/requirement-trace.mjs`：772 WHAT / 3901 tests，closure complete。
- `node --test requirements/js-semantic-surface/tests/surface-charter.test.mjs`：19/19。
- `node scripts/checks/js-surface-manifest.mjs`：165 registered surfaces closed。
- 15 个受影响行为文件：121/121。
- `node scripts/build.mjs`：738 F# sources / 165 surfaces。
- `node scripts/check.mjs`：全绿；700 production files；36 migration nodes DONE；0 control-pyramid/deadcode/JS boundary debt。
- `WIREIT_CACHE=none npm run format-build-test`：完整通过。Fantomas 700 unchanged；text gates 全绿；owner lane 27,218 FCS uses / 333 edges / 185 contracts；Fable 738 sources / 165 surfaces；authoritative unit 全绿；全部 integration/package 与 273/273 harness 全绿；OpenCode 1.18.18 Long Stroke 57 步 / 5.8s，journal 579/700、SSE 2399/3450；`npm pack --dry-run` 2019 files，2.2 MB packed / 10.5 MB unpacked。

## 剩余边界

- GitHub CI 与 upstream merge 是外部事实，不能由本地绿色替代。
- M10 fast-check pilot 仍是可选独立批次；没有负责人对依赖、property、CI budget、seed/path 形式的裁决时，不混入本 PR。
