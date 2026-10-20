# CUT-11 证据（子范围①：派发认领账规则）

返回 [卡片](../../cards/CUT-11.md)。环境：Linux，node v22.21.1，共享 8 核机器。原始日志在 box 的 `/workspace/vfs-cut/wt/logs-cut09e/`（不进仓）。

| 项 | 值 |
|---|---|
| 库实现 / 发布 | wanxiangli `c06f8c6` / `202a41a` **0.40.0**（`wanxiangli/dispatch`） |
| 主仓 base | master `c109341d7` 的干净构建（`Interaction/`、`Foundation/` 与当前 master 无差异：`git diff --stat c109341d7 origin/master -- src/Wanxiangshu/Interaction src/Wanxiangshu/Foundation` 为空，CUT-09-a 只改 `OpenCode/Host/ChatAdmission`） |
| 主仓 candidate | 当前 master + Model.fs/Run.fs/Recovery.fs 接线 + gitlink `202a41a`，build exit 0 |

- 投影差分 `node differential.mjs <base> <cand> 3000 40` → [differential.out](differential.out)：3,000 条随机序列 × 40 步（register/submit/accept/abandon 混合，10 种来源含无逻辑运行的根认领、4 种载荷含 gate nudge 与 repair 载荷），每步比较整份投影及 claimScope/nextClaimSequence/derivePromptKey(sha256)/gateNudgeAlreadyAdmitted/gateNudgeAcceptedPhysical/repairAlreadyClaimed：**1,200,000 次比较，0 处不同**。
- 恢复差分 `node recovery-diff.mjs <base> <cand>` → [recovery-diff.out](recovery-diff.out)：base 私有 `findPhysical`（给 base dist 模块的临时拷贝追加一行 export，跑完即删）对 candidate `DispatchLaw.recover`，消息数含 0/49/50/51/80 等窗口边界、5 种 role、近似 key：**200,000 次，0 处不同**（45,515 Proven / 154,485 StillPending）。
- 变异 → [mutants.out](mutants.out)：对 candidate 实际加载的库产物做 11 个语义变异（放弃不清 occasion、接受不留精确证据、回执移出待决、occasion 不看来源、缺席标记变空串、序号起点、未待决也结算、任意 role、窗口失效、丢回执、取最后一个匹配），差分全部报出不同。
- 库测试：`src/dispatch/tests/001.test.mjs` 5/5（独立 oracle）；对库产物 9 个变异全部报红；build-001 dispatch 隔离构建通过；scripts 测试 60/61，唯一失败是既有的 build-001 sandbox 编译警告（与本卡无关）。
- 套件（candidate 与 base 计数相同，失败/取消用例名集合逐行 diff 为空）：interaction-authority 104 pass/13 todo；dispatch-protocol 99 pass/1 cancel/1 skip/7 todo；crash-reconciliation 52/2 skip/15 todo；managed-chat-execution 82/2 skip/4 todo；effect-accounting 34 pass/5 cancel/3 skip/1 todo。
- 如实记录：主仓现有需求测试没有任何一条覆盖 `PromptRecovery` 的窗口与 role 规则（把这两条变异放进 candidate，dispatch-protocol 与 crash-reconciliation 计数不变），所以恢复等价只由上面的恢复差分和库测试证明。

## 子范围②：Host 发送回答（wanxiangli `c73eb2f` / `d0318dc` **0.42.0**）

- 差分 `node send-diff.mjs <base> <cand>` → [send-diff.out](send-diff.out)：base 与 candidate 的私有 `awaitPhysicalAwareSend`、`continuationAttemptOutcome`（各自给 Send.js 临时拷贝追加一行 export，导入后即删）在 5 种 SendOutcome × persist/accept/abandon 成败 × 有无确认等待者上比较端口调用序列、参数和返回值，另比较 attempt 分类：**90 次，0 处不同**。
- 变异 → [send-mutants.out](send-mutants.out)：对库产物 6 个语义变异（未知结局被放弃、未知结局算确定未发送、拒绝不算确定未发送、物理 id 跳过 PhysicalAccepted、回执不记 Submitted、Detached 拒绝当成未知）全部被抓住；前 5 个由 send-diff 抓住，Detached 那个由 dispatch-protocol 套件抓住（Detached 分类是成员内闭包，差分够不到）。
- 套件（rebase 前后 dist 相同，上游只改 `requirements/work/tests`）：interaction-authority 104/13 todo；dispatch-protocol 99/1 cancel/1 skip/7 todo；crash-reconciliation 52/2/15；managed-chat-execution 82/2/4；effect-accounting 34/5 cancel/3/1；degeneration-guard 55/6 todo，与 base 相同。
