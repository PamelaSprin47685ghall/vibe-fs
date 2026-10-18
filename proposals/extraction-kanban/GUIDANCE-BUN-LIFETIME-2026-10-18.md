# CUT-17-R3：Bun 指导测试的启动环境与收尾

输入：主仓 `511a86d3e`，库 `10bbb32 / 0.6.4`。本包不改生产实现、库、vendor、持久格式或用户配置。施工窗口从 2026-10-18 15:03:36 至 18:03:36（Asia/Singapore）；本报告只记录其中 R3 的闭环。

## 原因与归属

| 记录 | 归属 | 证据及处理 |
|---|---|---|
| R3-01 | 前轮修复新增的测试夹具缺陷；不是原拆分的产品缺陷 | `guidance-host.mjs` 自 `7c50ed590` 创建后，模拟了正在执行的 assistant，却没有报告其确切终态。dispose 将未终结的 provider step 留在 Retiring，不能凭插件关闭当作退出。连续 16 个夹具后容量用尽，后五项等在首次 `chat.message`，尚未进入呈现或冷读。现通过同一注册 Host event 交付匹配 parent 的 completed/stop，并断言本夹具没有残留 owner 或 waiter。 |
| R3-02 | 前轮验证环境缺口 | Bun 1.3.14 的 `os.homedir()` 保留启动时 HOME；Node 26.6.0 响应后来修改的 HOME。夹具在启动后才改 HOME，因此裸 Bun 可能加载外层配置或推荐模板。Bun原生探针先创建独立HOME和测试路由再启动；Node runner、插件夹具和探针共用同一测试配置定义。实际模型投影也须等于夹具模型，防止用错配置仍报绿。 |
| R3-03 | 不成立的产品归因，未登记为确认缺陷 | 深测时只有顺序相关超时，不能据此认定 Pair、Trace、原件 owner 或上一轮生产修改回退。本轮确切等待点、容量快照及不变生产包上的夹具对照解释了原因；没有修改这些生产 owner。 |
| R3-04 | 已有路由测试的相同环境前提错误 | EMR-020 共享配置例在同进程修改 HOME 并假定能选择自己的 module；Bun 启动 home 和已初始化 singleton 都使此前提不成立。现在真正启动新 OS 进程，启动前指定该例的自有配置，再验证两个相反的 unrelated global 不能改变其配置事实。原断言未放宽。 |

诊断日志只用于确定到达点和占用。所有临时阶段打印已删除；没有延长 5 秒默认超时、拆分 014、加 skip 或删除字节/冷恢复断言。生产的“确切物理终态才释放”规则保持原样。夹具终态仅属于其模拟的 Host 执行，不是对真实外部进程补造退出证据。

## 修改与反证

- `guidance-host.mjs` 以 try/finally 结束最后一个已准入的模拟 provider run，包含测试断言失败；最后一轮引用的物理 parent 与 provider run 同步跟踪。注册事件之后检查本 session 的 owner/waiter 均已消失。
- 014 增加单槽反例：第一夹具在实际 transform 后故意抛出原错误，第二夹具须能正常准入并完成新 OS 进程冷读。修前 helper 在同一生产包上 0 pass / 1 timeout；修后必须保留原异常并完成第二次调用。
- `routing-home.mjs` 统一此前两份测试路由。曾新增的`run-bun.mjs`经独立复审发现导入阶段无监督，已删除；启动HOME和完整文件的物理对照仍保留为原生探针，不再规范为正式门禁。

## 验收证据

以下日志位于 `/private/tmp`，原失败保留。TODO 和 integration skip 不算通过，重复执行不重复加总。

| 范围 | 结果 | 日志 |
|---|---|---|
| 原完整 Bun 014 | 20 pass / 5 timeout / 1 integration skip | `r3-20261018-baseline-bun.log` |
| 等待点及完整容量快照 | 每夹具累积一枚 Retiring；第 17 次 chat.message 前 16 枚，随后排队 | `r3-20261018-stages-authority.log`、`r3-20261018-capacity-bun.log` |
| 同一个 2e0f 生产包，16 槽启动配置，旧 helper | 20 pass / 5 timeout / 1 skip；只作归因，不作产品验收 | `r3-20261018-package-before-fixture-repeat.log` |
| 同包同配置，修后 helper，原完整 014 | 25 pass / 0 fail / 1 skip；包摘要未变、目录已清理 | `r3-20261018-package-after-fixture-final.log` |
| 同包旧 helper，新增单槽反例 | 0 pass / 1 timeout；所有其他项明确 filtered | `r3-20261018-single-slot-red.log` |
| 完整 Bun 014，启动前独立 HOME | 27 项：26 pass / 0 fail / 1 integration skip | `r3-20261018-final-bun.log` |
| 真实 OpenCode Host 网络/原件/新 OS 冷恢复 | 1 / 1 | `r3-20261018-real-host-allowed.log` |
| Node prefix/grounding/routing，35 文件 | 280 项：265 pass / 0 fail / 1 skip / 14 TODO；35/35 完成、收尾 accepted=true | `r3-20261018-final-node.log` |
| Node runner 的 005/006/021 | 100 pass / 0 fail；3/3 完成、收尾 accepted=true | `r3-20261018-runner-node-allowed.log` |
| Bun routing 001/009/020，同一启动器 | 15 pass / 0 fail；含实际 chat.message 和新 OS 配置验证 | `r3-20261018-routing-bun-fixed.log` |
| Node 最后调整的 020 | 7 pass / 0 fail | `r3-20261018-routing-node-fixed.log` |
| 真实 pack、隔离安装、默认入口 | generation609，2076 members / 13,375,692 bytes，SHA-256 `2e0f994058540b58a1c31c2bef1dc8954ccc1b0abaf6ca4871ac2e61e2da5c30`，与修前产品字节相同 | `r3-20261018-package.log` |

初次真实 Host 在沙箱内因 loopback listen EPERM 未能启动，放行后真实用例通过。初次 runner 组因沙箱禁止 `ps` 出现 23 项失败；放行后的相同三文件为 100 pass / 0 fail、3/3 完成、收尾 accepted=true，`r3-20261018-runner-node-allowed.log`。这些是验收环境失败，不算产品修复或业务通过。

第一次包外复制少了 semantic-trace 的测试支持目录，0 pass / 1 import error；补齐实际输入后才进入业务。第一次完整旧 helper 包外对照另有一次 Git worktree 查询失败，结果 19 pass / 1普通失败 / 5 timeout / 1 skip；未将其当成新的稳定产品问题，原日志 `r3-20261018-package-before-fixture.log` 保留。

额外 Bun 路由组初次为 14 pass / 1 fail，正是上表 R3-04；修后同一组 15/15。最终 Node 大组在 R3-04 改为子进程之前完成，随后对最后变更的 020 单独验收 7/7；不把补验再加进 265。build/check/diff 门禁通过，所有临时阶段打印已删。

## 非作者复审与新增问题

`review_guidance_r3`独立核对exact session/parent/provider-run终态、HOME归属、EMR-020相反配置断言，未发现产品或fixture新增问题。另发现本轮新增P2：`run-bun.mjs`无界spawnSync不能监督模块导入挂起。真实Bun反例输出module-entered后保留interval并无限await，6505ms后仍须外部SIGTERM，日志`/private/tmp/r3-review-20261018-bun-import-hang.log`。已删除入口和WHAT中的正式入口声明，不加总时长超时、不解析pass文本或伪造verdict。正式Bun监督适配单列CUT-00-R2；Node正式监督证据与Bun原生物理证据分别保留。复审时窄测被并发施工freshness拒绝，不计通过。

## 剩余边界

本包只修正指导测试夹具及测试启动配置，非作者复审已完成且发现的新增入口已移除。Bun原生通过没有完整正式监督证明，归CUT-00-R2；其他夹具完整收尾、Process A/C/B、Trace完整产品链、Bootstrap及主动脱组均不因本包通过关闭。R12-U1继续暂缓；正常提交推送master，未部署用户Host。
