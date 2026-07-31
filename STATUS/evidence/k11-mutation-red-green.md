# K11 红/绿证据留档（包 K11：森林变异自检）

日期：2026-07-31 · 分支 refactor/ssot-shock-anneal · 运行器：临时 scratch-k11-runner.mjs（只跑 mutationCases，证据后即删除）

方法：把正确实现暂时改错 → mutationCases 必须红；git checkout 恢复 → 必须全绿。
每类变异对应 design-script-forest.md 第十四节的一种历史绿灯误判。

## class 1：epochCold（前缀在未声明处断裂靠 tools+system 相同放行）

变异手法：scenario-runtime.js select() 中给 seal.broken 增加 `ephemeralColdPass = true` 直通——
broken seal 不再返回 { sealBroken }，正是 epochCold「tools+system 未变即通过」的谓词。

红：exit=1
  ✗ VERIFY-003 a prefix rewrite is refused even when tools and the system message are unchanged:
    select must return { sealBroken }, got delivered mgr.1

绿（恢复后）：exit=0，8 passed, 0 failed

## class 2：specificity（同长度冲突前缀打分取一）

### 2a 载入期
变异手法：scenario-schema.js 删除 `...duplicateDeclarations(entries)` 校验。

红：exit=1
  ✗ VERIFY-003 two declarations for one point are refused at load, never scored:
    the mutated source must not compile

### 2b 运行期
变异手法：runtime-key.js 把 `tied.length > 1 → ambiguousTurn` 改为静默取第一个（打分取一）。

红：exit=1
  ✗ VERIFY-003 two equal-weight prefixes are reported as ambiguous, not resolved by score:
    select must return { ambiguous }, got delivered left.0

绿（恢复后）：exit=0，8 passed, 0 failed

## class 3：requestRoleOf（角色由 wire 反推）

### 3a 声明侧
变异手法：legacy-fields.js 把 `role` 从 RETIRED_FIELDS 摘除（允许剧本声明角色）。

红：exit=1
  ✗ PROMPT-008 a scenario cannot declare a role, and the refusal names the real source:
    role must stay retired, with its replacement named

### 3b 推导侧
变异手法：scenario-runtime.js 末尾重新加入 requestRoleOf 函数体（wire → role 推断）。

红：exit=1
  ✗ PROMPT-008 the ScenarioRuntime selection path contains no role inference:
    role inference must not exist on the selection path: testkit/opencode/scenario-runtime.js

绿（恢复后）：exit=0，8 passed, 0 failed

## class 4：loadScripts（重启后本该命中的边消失；锚定 bind + clearSeals + select）

变异手法：clearSeals() 除清 seals 外，把 `this.scenario` 换为空——loadScripts 的运行期换剧本。

红：exit=1
  ✗ VERIFY-003 a declared edge still resolves after a restart clears the seals:
    Cannot read properties of undefined (reading 'id')

绿（恢复后）：exit=0，8 passed, 0 failed

## 结论

四类变异每一类都被 mutationCases 抓住（红），恢复后全绿（绿）。
门禁令牌：node testkit/opencode/tests/gate-testkit.mjs 中 mutationCases 八条全 ✓。
（gate-testkit 另有 7 条红，全部属 K10/COMPANION-002 对
 orchestrator-restart-publish-conflict.toml 的既有冲突，非本包引入；
 该文件在 HEAD 31d4958b 已红，git status 干净。）
