# waitFact 续期因果归因

未裁决候选。不是当前规范，不得直接据此修改生产代码。

## Problem

`tests/e2e/support/scenario-driver.mjs` 的 `awaitFactBarrier` 当前把两类观察都当成阻塞车道进展：

1. 被等待事实的计数增长；
2. journal 任意事实增长。

第二类通过未指定 `blocking` 的 `watchdog.advance` 调用继承默认值 `true`。同一 Blogger 信号在其它入口却明确属于背景车道：provider expectation 使用 `blocking=false`，`VERIFY-004` 也规定「背景进展（非阻塞车道，例如 blogger sidecar）必须被记录但不得续期」。因此 waitFact 将背景 journal 事实重新分类为阻塞进展，与现行门禁语义冲突。

本候选不把该冲突解释为当前 release 用时过长的根因。已有失败证据显示：

- `manager-full-loop` 两次均在最后一次 `manager.4` 后静默 5000ms 被 watchdog 收割；Blogger 被记录为 `none of them renewals`；
- `fallback-aabb-trace` 在 `FallbackCursorAdvanced=3` 后静默 5000ms，被已修复的 Fable `TrySetCanceled` 运行时错误阻断；
- 90–127 秒是并行套件总墙钟，不是单个 waitFact 的失败延迟。

所以这是独立的因果归因缺口，不是调低超时的性能提案。

## Current baseline

`awaitFactBarrier` 每 500ms 读取一次 journal：

```text
目标事实计数增长  → blocking advance
任意其它事实增长  → blocking advance
无事实增长        → 不 advance
```

`tests/integration/harness/timeout-cases.mjs` 当前第二个 waitFact 子用例持续追加 `UnrelatedProgressFact`，并要求它让 barrier 跨过多个静默窗口。该用例把「durable append」直接等同于「被等待因果链前进」，保护了上述冲突。

同时，`tests/e2e/support/watchdog.js` 与 `docs/proof/verify.md` 的 VERIFY-004 已明确把 Blogger sidecar 定义为背景车道：记录但不得重置静默计时器。

## Goal

让 waitFact 的续期依据由剧本显式表达，不从「journal 有任何写入」反推因果：

```toml
{ waitFact = { name = "Published", eq = 1, renewOn = ["CandidateReady", "RebaseAccepted"] } }
```

规则：

1. `name` 的计数增长始终属于阻塞进展；目标尚未达到时，部分增长仍证明本链前进。
2. `renewOn` 中任一事实计数增长属于阻塞进展。
3. 其它 journal 增长只调用 `advance(blocking=false)`，保留诊断但不续期。
4. `renewOn` 缺省为空；简单 barrier 只依赖目标事实。
5. `WAIT_FACT_WINDOW_MS` 保留为持续进展但不收敛时的总兜底；本候选不修改任何时间值。
6. `renewOn` 是 proof 剧本声明，不进入生产事实、Journal envelope 或运行时配置。

## Non-goals

- 不静默、限流或重写 Blogger。
- 不把 Blogger commit、provider 流量或 Session 类型硬编码成全局黑名单。
- 不通过 session、fact payload 或命名启发式自动猜测因果关系。
- 不调整 watchdog、readiness、canary 或 waitFact 的时间常量。
- 不宣称缩短当前 `check:release` 的正常执行时间。

## Impact map

- what: 无产品可观察行为变化。
- shape: 无生产所有权变化；proof 剧本拥有中间事实归因声明。
- how: 无生产算法变化。
- proof: `docs/proof/verify.md` 的 VERIFY-003/004；裁决后补充 waitFact 显式归因记法。
- code/resources: `tests/e2e/support/scenario-driver.mjs`、`journal-observer.js`、`scenario-schema.js`、`tests/integration/harness/timeout-cases.mjs`、确需跨静默窗口的 scenario TOML。

## Alternatives

1. **维持任意 journal append 续期**：拒绝。它与 VERIFY-004 的背景车道定义直接冲突。
2. **只允许目标事实续期**：过窄。publish/rebase/recovery 链可能在目标事实出现前经历超过一个静默窗口的真实中间进展。
3. **按 Blogger fact kind 或 Companion session 自动过滤**：拒绝。事实种类与 session 身份不是「是否推动当前判据」的充分条件；自动推断会把 proof 语义藏进启发式分类器。
4. **剧本显式 `renewOn`**：推荐。最小改动；因果知识落在声明 barrier 的同一位置；载入期可校验；失败诊断能直接列出被承认的中间事实。

## Migration / cutover

1. scenario schema 接受 `waitFact.renewOn` 字符串数组，拒绝空字符串、重复项及与 `name` 重复的项。
2. journal observer 在一次扫描中返回目标、`renewOn` 与总计数，避免每个事实名重复扫描文件。
3. `awaitFactBarrier` 按 Goal 中三类信号分别调用 blocking/background advance。
4. 删除「`UnrelatedProgressFact` 必须续期」旧门禁；以背景持续写入仍被 watchdog 杀死的反例替换。
5. 只给实测会跨过静默窗口的 barrier 声明最小 `renewOn` 集；不预填 speculative 列表。
6. clean break：不保留「缺少 `renewOn` 时任意 append 续期」兼容分支。

## Compatibility disposition

CleanBreak

## Proof plan

1. journal 完全静默：barrier 在一个注入静默窗口内由 watchdog 结束。
2. 背景事实每 250ms 追加：barrier 仍在一个静默窗口内结束，且诊断记录 background advance。
3. `renewOn` 事实每 250ms 追加、目标在两个静默窗口后出现：barrier 存活并只在目标满足后返回。
4. 目标事实从 0 增长到未满足的中间计数：续期；达到 `eq`/`gte` 后返回。
5. 载入期拒绝非法 `renewOn`；序列化相同剧本得到相同归因集合。
6. 故意恢复当前「任意 append 续期」分支，背景持续写入用例必须红。
7. 运行 `npm run lint`、integration harness 与三轮 e2e release gate；不放宽任何时间值。

## Decision owner

Wanxiangshu 项目 Owner。
