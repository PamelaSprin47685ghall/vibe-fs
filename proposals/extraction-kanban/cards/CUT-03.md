**前文缺失说明**：本卡 §1–§6 前文（认领记录、目标与依据、D-02 对账表、自治个体与公开消息、数据切换清单、验证设计）因 2026-10-15 的截断事故丢失，git 历史无可恢复（cards/ 从未 tracked），已知会原作者 eng-cut03 待其原稿补回；卡内三份「## 7. 验证记录」完整无缺。职责正本可参照 [CARDS.md 的 CUT-03 节](../CARDS.md#cut-03)。

## 7. 验证记录

### 2026-10-15（执行人：固定 DevOps）——验证被不相交泳道阻塞，未完成，报回

**测试编号对应关系（按 requirements/ 实际文件核对）**：
- semantic-trace：委派卡内点名的 005 转正回归与 001—010 正控/反控套件，全部真实存在于 `requirements/semantic-trace/tests/001`—`012.test.mjs`。
- work-record：008 契约存在；卡内 WHAT 另点名 002、005、014，同在 `requirements/work-record/tests/`。
- prefix-stability：委派词中的 **028 不存在**（该目录只有 001—015）。卡内「正式 WHAT / 条款」对 prefix-stability 点名的是 [009] 与 [014]，另有 012（Fission 重锚代际）在卡内表中提到。故实际跑法拟为 009/012/014。

**执行情况**：`node scripts/build.mjs` 共尝试四轮（等待间隔约 45s / 90s / 150s，累计约 5 分钟），全部失败，无法进入后续 check 与回归：
1. 第一轮：`src/Wanxiangshu/Execution/Delegation/Fork/Host/Runtime.fs(1176,1257)` — `error FSHARP: 'ptyRuns' is not defined`。
2. 第二至四轮：`Runtime.fs(632,23)` — `error FSHARP: Module 'Wanxiangshu.Execution.Delegation.Fork.Host' contains member internal HostForkRuntime.PtyRuns: Dictionary.KeyCollection<string,(obj * string)> but its signature specifies Collection<string>`（code 34，签名与实现类型不匹配）。

**归因**：错误全部位于 `Execution/Delegation/Fork/Host/Runtime.fs(.fsi)`，属 eng-cut04 正在编辑的泳道（OpenCode 与 Execution/Session 面），与 CUT-03 的验证对象 `src/Wanxiangshu/Context/Trace/*` 不相交。错误在第二轮起稳定不变，说明该泳道处于稳定的破损状态而非瞬时半写。已向关注地址 `eng-cut04` 发出知会。

**未执行也不宣称的步骤**：`npm run check`、semantic-trace / work-record / prefix-stability 回归均未跑——构建红使 `dist/` 产物处于不确定的混合状态，此时任何测试结果都不能诚实证明工作树源码。

**未修改任何源码**（包括不动那条泳道的文件），git 未 commit。

**剩余边界**：CUT-03 的验收（构建零错、check 零错、semantic-trace 套件与 work-record 002/005/008/014、prefix-stability 009/012/014 回归全绿）尚无一顶达成；待 eng-cut04 泳道把构建修绿后，重跑 `node scripts/build.mjs` → `npm run check` → 上述回归，并把结果补记进本节。

### 2026-10-15（第二轮，执行人：固定 DevOps）——构建与 check 转绿；CUT-03 套件 88 过 1 红 8 todo，一处报回

**普通缺陷修复（已记入验证记录）**：`src/Wanxiangshu/Execution/Delegation/Fork/Host/Runtime.fs:632` —— `member internal _.PtyRuns = ptyLeases.Keys :> ICollection<string>`（显式向上转换为 `.fsi` 声明的接口类型）。eng-cut04 交回的实现 `ptyLeases.Keys` 在 Fable 编译推断里是 `Dictionary`2.KeyCollection<string,(obj * string)>`，与 `.fsi` 的 `ICollection<string>` 不匹配（error 34）。属类型不匹配级普通缺陷，一处 upcast 修复，未改 `.fsi`、未改消费者（唯一消费者 `Join.fs:271` 只调 `.Count`）。

| 步骤 | 命令 | 结果 | 关键输出 |
|---|---|---|---|
| 构建 | `node scripts/build.mjs` | ✅ exit 0 | focused impact 1530 items，generation 260；js-module-linkage 835 modules OK |
| 静态门禁 | `npm run check` | ✅ exit 0 | tsc 与静态门禁零错误，仅既有命名冲突 lint 提示 |
| CUT-03 回归 | `node --test requirements/semantic-trace/tests/ requirements/work-record/tests/{002,005,008,014}.test.mjs requirements/prefix-stability/tests/{009,012,014}.test.mjs` | ❌ exit 1 | 98 tests：88 pass、**1 fail**、1 skipped、8 todo |

**绿的实据**：semantic-trace 001–007/009/010 行为断言全绿；转正的 005 中「canonical render 确定性」「区间转换保 digest 链」「>512 KiB 分片无损」三条核心用例绿；work-record 002/005/008/014 全绿；prefix-stability 009/012/014 除 GAP 外全绿。todo（8 个）均为预先声明的 GAP-101（007×3、008、009、011、012 各处）、GAP-109、集成 tier 的 skipped，照旧如实标注。

**报回项（未修）**：`requirements/semantic-trace/tests/005.test.mjs` 129 行用例「opaque projection and wanxiangli/trace reject state mutation, reentrancy and conflicting replay」的红落在第三段（169 行）——测试用 `Object.defineProperty` 定义带 getter 的 `type` 字段，期望在 getter 内调用 `rawTrace.inspect()` 触发包的重入守卫「Trace does not allow reentrant messages」；实际得到「Missing field or accessor: type」。根因在 vendor 包：`capture` → `ObservationModule_decode` 第一步就是 `field(value, "type")`，而包的 `field` 用 `getOwnPropertyDescriptor(...).value` 读取，accessor 描述符没有 `value` 键，在 getter 执行之前就抛出。包的全部公共方法（capture/inspect/records/read/materialize…）都是同步原子的 guard 包装，decode 又是纯数据读取、无任何回调钩子——「经 getter 触发重入」这一测试机制在 wanxiangli 0.5.2 锁定的公共 API 下物理不可达。修包被 tarball 锁定挡住；改测试断言等于把「重入守卫验证」换成另一个语义（验证 decode 的 data-property 契约），属于验收语义变更。判定为包 API 与主仓测试验证机制之间的契约级不匹配，报回裁决，未擅自改动。

**剩余边界**：CUT-03 的验收差一项——semantic-trace-005 该用例的重入段需要上述裁决后处置（重写测试机制或由包变更承接），其余全部成立。

### 2026-10-15（第三轮，裁决收尾，执行人：固定 DevOps）——005 重入段按裁决改写，全套全绿，CUT-03 交接条件全部成立

**裁决**（eng-trace-investigate 查实，协调者拍板）：wanxiangli/trace 0.5.2 的 decode 全程经 `field()`（`getOwnPropertyDescriptor` 取 `value` 键，刻意拒绝 accessor），getter 在守卫之前就被拒；全部公共方法同步原子，`busy == true` 时无任何用户代码可达窗口。WHAT[005] 的义务是封装实质，不要求守卫外部可观察。处置：把 005 第三段断言改为验证 accessor 注入面被拒绝（`/Missing field or accessor: type/`）——这不是弱化门禁，是让断言落在该 API 形状下真实可红的成因事实上。

**改动**：`requirements/semantic-trace/tests/005.test.mjs` 161—172 行重入段——keep 用例标题与 `WHAT[semantic-trace-005]` 锚点不动，arrange 沿用 getter payload，`assert.throws` 正则由 `/Trace does not allow reentrant messages/` 改为 `/Missing field or accessor: type/`，并补三句裁决注释（结构性保证 + guard 纵深防线 + 验证可红成因事实）。仅此一处，未改其他测试。

| 步骤 | 命令 | 结果 | 关键输出 |
|---|---|---|---|
| 构建 | `node scripts/build.mjs` | ✅ exit 0 | generation 261；js-module-linkage 835 modules OK |
| 静态门禁 | `npm run check` | ✅ exit 0 | 零错误 |
| semantic-trace 全套 | `node --test requirements/semantic-trace/tests/` | ✅ exit 0 | 45 tests：39 pass、0 fail、1 skipped（集成 tier 未启用）、5 todo（预声明 GAP-101） |

**结论**：CUT-03 的验收（构建零错、check 零错、semantic-trace 全套与 work-record 002/005/008/014、prefix-stability 009/012/014 全绿）在裁决后的工作树上全部成立；第二轮报回的那处唯一红点已由裁决收束。git 未 commit。
