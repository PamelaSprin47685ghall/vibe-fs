# WHY：为什么「绿」本身需要被治理

## 不可替代的存在理由

「测试通过」是最容易被伪造的信号。它可以通过十种方式变绿而世界仍然坏着：

```text
门禁扫的是不存在的目录（恒为通过）
测试没跑（入口指错、脚本不存在、产物陈旧）
断言只看真值（字段改名后 undefined 静默通过）
watchdog 被原始 SSE 流量续期（挂死永远不触发）
超时调大（红灯变绿，消灭的是发现问题的能力）
canary 迎合错误生产（断言为绿而削弱）
repeat-until-pass（跑 N 遍直到碰巧通过）
验收口径事后缩水（close 判据自己降级）
mock 按已观察次数改变响应（退化成队列，失去确定性）
覆盖率分母缩水（没加载的模块从账本上消失）
```

其中每一件都真实发生过。`archive/changes/completed/fix.md` 的审计发现 DSL 门禁只扫 136/245 个
生产文件却宣称「threshold=0 全量清零」——**门禁绿了，门根本没装在关键房间门口**；
`archive/changes/completed/test.md`（G4R）记录了 31 个 E2E 里单 case 90 秒超时、flake 当作测试
分类而不是架构错误的漫长历史。

`verification-system` 存在的理由：**把「什么算证明」本身变成被证明的规则。** 证据要
分层（纯逻辑证明用纯逻辑、物理契约才用物理世界）、要可红（没有失败价值的门不是门）、
要可重放（不依赖墙钟运气）、要 fail-closed（损坏时安全失败而不是假装通过）。

## 为什么是元合同（META）而不是产品包

产品包拥有领域事实：`durable-events` 拥有 journal 语义，`review-assurance` 拥有
judgement 消费资格。它们各自有「这条产品规则怎么证明」的义务。

`verification-system` 不拥有任何领域断言，它拥有的是**证明义务的通用规则**：什么证据
技术够格、什么时候必须用便宜证据、什么情况下升级到昂贵 E2E、门禁怎么保证可红。同一个
规则服务于所有产品包——「一个 physical world 可为多个 package-local semantic oracle
提供证据」这种共享事实只有 META 包能拥有。

## 为什么不能并入 requirement-system

`requirement-system` 回答「谁拥有什么」（归属与合同），`verification-system` 回答
「怎么证明」（证据资格）。独立变化测试：把真实 Host canary 从当前 harness 换成另一
物理 adapter——verification-system 的 HOW 变，requirement-system 与所有产品包 WHAT 不
变；把 manifest 从 TOML 改成其它格式——requirement-system 的 HOW 变，verification-
system 不变。合在一起，一次改动会同时牵动两种失败意义。

本包依赖 `requirement-system`：证据资格建立在「每 assertion 一个 owner」「WHAT 是唯一
合同」之上——不知道谁是 owner，就无法定义「谁的 Satisfied(P) 需要什么证据」。

## RED 是什么样

```text
repository 无法可信地区分「requirement 已满足」与「测试/门禁没有覆盖或没有失败能力」
```

具体形态：proof ladder 层序在 package.json 里被悄悄重排；check.mjs 接线到不存在的
gate；gate 失败码被吞掉照样 exit 0；watchdog 靠总超时而不是因果进展；temporal 测试
依赖真实墙钟；门禁扫不存在的目录；删掉一个 gate 的回归测试没有任何测试变红。

## 考古：本包的条款来源

| 来源 | 吸收为什么 |
|---|---|
| 历史 boundary card（01-meta-programming） | proof ladder、禁止语义分支直跳 E2E、verifier 必须可红、dependency closure 验证、One World 共享、确定性证明原则 |
| 历史 verify 条款（VERIFY-001..009，2026-08-14 归档） | 五层金字塔、晋级阶梯、canary mock 剧本、因果推进门禁、Architecture Gates、No-Go、三种投影、语言边界、覆盖门禁 → WHAT VERIFICATION-SYSTEM-001..012 |
| 历史 G4R change（test.md） | One World / Pure Time：恰一个 Long Stroke、race 是代数不是调度彩票、watchdog 因果续期 |
| 历史 change（canary-unbend） | canary 不可弯曲迎合生产；断言不得为绿削弱 |
| 历史 change（orchestrator-e2e-timeout） | 先可解释再修根因；超时放大不是修复 |
| `archive/changes/completed/waitfact-causal-renewal.md` | waitFact 续期因果归因；背景进展只记录不续期 |
| `archive/changes/completed/fix.md` | 验收口径不缩水；静态门禁必须命中真实路径（伪门禁教训） |
| `archive/requirements-design/PROOF-MAP.md` Phase D | gate/test family 的 MECHANISM 归属；missing oracle 3（proof ladder 可红） |
| `archive/requirements-design/EVIDENCE.md` §1 | 两 META 包正确无 runtime 源码 |

## 被拒方向

- **每个产品包自管证明技术**：证据分层与晋级规则若每包一份，跨包证据资格没有统一判据，
  同一个物理世界被各包重复证明。
- **把「绿」的定义留在 CI 散文 / README 里**：散文不可红；layer 0 门禁 + 每门回归 + red
  fixture 才是可执行的证据资格。
- **恢复 multi-canary / 三轮 shuffle**：G4R 已裁决为 target-delete（One World）；历史形态
  只作反例，不成为目标（见 HOW.md 历史与弃权）。
