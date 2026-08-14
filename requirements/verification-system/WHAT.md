# WHAT：verification-system 必须成立的规则

本文件是 `verification-system` 的**唯一 normative 合同**。WHY/HOW/PROOF 非 normative。

命题编号 `VERIFICATION-SYSTEM-NNN`；每条命题 = 当前世界必须同时成立的事实。证据指针 →
`PROOF.md` 行号。引用别的包一律用包名，不复制其它包命题。

---

## VERIFICATION-SYSTEM-001：五层证据金字塔

**规范陈述**：requirement acceptance 由分层证据定义，层序固定且被机器 pin：

```text
0. Static architecture/proof gates（纯文本/文件系统检查，不依赖编译产物）
1. Pure laws（无 Host / clock / process / network）
2. Temporal（deterministic temporal workflow proof：virtual ports + 显式 traces）
3. Adapter（single physical adapter contract：恰好一个物理边界）
4. Long Stroke（恰好 1 个真实 OpenCode E2E）
5. Release（一次确定性 full proof + build/package/packing）
```

层序的机器载体是 `package.json` 的 `format-build-test` 与 `scripts/check.mjs` 的 wired
gate 清单；两者被 `tests/proof-ladder.test.mjs` 钉住，层序重排即红。

**含义/动机**：越接近纯逻辑，case 越多、证明越彻底、运行越快；越接近物理世界，数量越少、
自由度越少。第 0 层永远可运行（不依赖编译产物），因此任何阶段都可跑。

**边界**：本命题管「分层原则与层序 pin」，不管任一产品事实内容；具体产品断言回各产品包。

**证据指针**：→ PROOF.md L8。

## VERIFICATION-SYSTEM-002：One World——恰一个 Long Stroke

**规范陈述**：第 4 层恰好一个真实 E2E 入口（`requirements/verification-system/tests/e2e/entry.test.mjs`）、全程恰好一次
OpenCode spawn/lifetime；E2E case 天花板（`E2E_CASE_CEILING = 0`）只降不升；禁止并行
multi-canary / worker pool / 每 scenario 一个 world 冒充覆盖面。

**含义/动机**：证明世界至多一个。语义命题在 Pure/Temporal 层证明；物理世界只承担不可
模拟的组合契约。并行 canary 曾让「证明覆盖」退化成「跑得多」。

**边界**：`format-build-test` 中 `tests/e2e/` 引用恰一次；g4r-freeze 迁移期 ratchet 已于
2026-08-14 退休，由永久 One World 门 `e2e-watchdog-feed`（sole top-level entry、无 cases/ 通道）
与 proof-ladder 层序 pin 承接。

**证据指针**：→ PROOF.md L9。

## VERIFICATION-SYSTEM-003：晋级阶梯，禁止跨级

**规范陈述**：语义命题按 1 Pure → 2 Temporal → 3 Adapter → 4 Long Stroke → 5 Release
逐级证明，不允许跨级；禁止把 semantic branch 无理由直接升级为昂贵 E2E；禁止 repeat-
until-pass。若声称「必须由 OpenCode 才能验证」，必须先回答「它到底依赖哪个不可模拟的
physical contract」，答不出则降回 Pure/Temporal/Adapter。

**含义/动机**：E2E 是稀缺物理资源不是覆盖捷径。把语义命题「晋级」成并行 fan-out 或
「重跑直到通过」= 用运气代替证明。

**边界**：禁止跨级的机器可红面 = case 天花板 0 + 唯一入口 + 精确 event 天花板（由
e2e-watchdog-feed / e2e-event-ceiling 承接；g4r-freeze 已退休）+ 唯一 Long Stroke 入口必须
声明不可模拟 physical contract（`tests/physical-contract.test.mjs`；删声明即红）。针对“某个 tool
正在执行”才有意义的外部刺激（例如 EXEC-017 user-message join wake），provider expectation 只证明
模型已返回 tool-call，**不证明 Host 已开始执行 tool**；注入前必须先观察真实 Host ToolPart 的 running
状态，禁止用调度运气或 fixed sleep 代替该物理 barrier。答不出则不得留在 e2e。

**证据指针**：→ PROOF.md L10。

## VERIFICATION-SYSTEM-004：verifier 必须可红

**规范陈述**：每个 verifier / gate / canary 必须真正可红：每个静态门有永久回归测试，
且用受控反例证明仓库入口会判红；反例不得进入最终提交。断言不得为绿而削弱。

**含义/动机**：可红性是证明资格的地板。canary 迎合错误生产（历史 change（canary-
unbend.md`）与「删掉回归没有任何测试变红」都是同一件事：门没有失败价值。

**边界**：本命题管「可红」；每个具体断言的可红 fixture 归该断言 owner（每 assertion 恰
一个 owner，见 `requirement-system`）。本包只拥有 layer-0 门禁机制自身的回归。

**证据指针**：→ PROOF.md L11。

## VERIFICATION-SYSTEM-005：fail-closed

**规范陈述**：门禁与运行器遇数据损坏、版本失配、边界失配或未知状态时安全失败（传播
非零退出码），不崩溃吞上下文、不假装通过。`scripts/check.mjs` 的
`process.exit(result.status ?? 1)` 把单个 gate 的失败码传播到仓库入口；spawn 失败
（status 为 null）也必须判失败。

**含义/动机**：「没跑起来」「跑挂了」「跑红了」三者都不得转绿。陈旧 dist、损坏
envelope、未绑定 ID、互斥 intent 组合都属于 fail-closed 义务（VERIFY-005 破坏性回归
指南）。

**边界**：各产品包的 fail-closed 行为断言（envelope 损坏等）归对应包；本命题管传播
机制与通用义务。

**证据指针**：→ PROOF.md L12。

## VERIFICATION-SYSTEM-006：因果推进门禁

**规范陈述**：挂死判据 = 距上次**因果进展**的静默时长，不是套件总时长。watchdog 只由
语义事件投喂（被消费的剧本步、显式检查点、waitFact 声明的目标事实增长）；原始 SSE /
provider 流量 / 生命周期噪声不算进展；背景车道进展只记录不续期（`advance(blocking=false)`）。
禁止固定 sleep、timeout padding、以「有字节在动」续期。超时先转储诊断（最后一次进展是
什么）再退出非零。

**含义/动机**：慢与死必须可分。把总时长当唯一判据 = 卡住几分钟后诊断信息已腐烂；
让传输层动静续期 watchdog = 一个反复重连的读者能永久续期一个错误的 watchdog。

**边界**：waitFact 的 `renewOn` 是 proof 剧本声明，不进生产事实/Journal/运行时配置。
e2e top-level 测试不得直接调用 `watchdog.advance`（只经 support 因果原语）。

**证据指针**：→ PROOF.md L13。

### 禁止退化清单

以下任一出现即为门禁退化，等同于发布 No-Go（VERIFICATION-SYSTEM-010 验收判据不可放宽）。

机器解析锚点：下列 ```text 块内条目文本不得无故改写；解析器按整行绑定 id（`e2e/support/degradation-list.mjs`）。

```text
把 wall-clock 总超时当作唯一挂死判据
让原始 SSE 或 provider 流量续期 watchdog
让背景车道进展续期 watchdog
删除 watchdog 的诊断转储，只保留退出码
让 watchdog 计时器持有事件循环，使干净结束也要等满静默窗口
存在只有总超时保护的时间窗
声明了断言心跳但未接线
用固定 sleep 代替因果 bark 交错启动
就绪超时或就绪前退出被当作通过
Release gate 变成「最多 N 轮」或「重跑直到通过」
数量常量与清单各自维护
静态门禁的路径判据指向不存在的目录
延长静默窗口或测试超时以掩盖竞态
```

最后一条是最隐蔽的：调大超时永远能让红灯变绿，而它消灭的是发现问题的能力，不是问题。

One World 目标态下，下列形状同样视为退化（与上文各目标节同义；待静态 ratchet 落地后可并入机器锚点，现以条款正文为准）：

```text
并行 multi-canary / worker pool 冒充证明覆盖面（违反 One Physical World）
Long Stroke 内二次 spawn OpenCode（违反 Spawn Exactly Once）
用真实 scheduler 碰巧顺序证明 semantic race（缺少 deterministic trace）
Temporal 测试依赖真实墙钟推进语义（违反 Virtual Time）
用 wall-clock / sleep 断言语义成立（违反 No Wall-Clock Semantic Assertion）
Long Stroke 断言 production 私有状态或内部 helper 时序
```

## VERIFICATION-SYSTEM-007：时间确定性

**规范陈述**：proof 不依赖真实 wall-clock 偶然性。Temporal 层用虚拟时间/注入时钟
（virtual ports + 有界交错）；wall-clock 只可作挂死兜底，不可作语义判据。禁止
「等够 N 秒」断言语义成立，禁止用真实 scheduler 碰巧顺序证明 race。

**含义/动机**：时间若从 ambient 偷渡，同一证明在不同运行时刻给不同答案——不可重放。
race 是代数（可枚举的交错），不是调度彩票。

**边界**：时钟/定时 capability 本身归 `time-capability`；本命题管「证明不得依赖墙钟」。
facade 层的时区无关性（`requirements/verification-system/tests/domain.meta.test.mjs`）是本命题在契约面的落点之一。

**证据指针**：→ PROOF.md L14。

## VERIFICATION-SYSTEM-008：契约面语言边界

**规范陈述**：生产代码是 `.fs`；第 1–3 层测试全部是 `.mjs`，直接消费 `dist` 发布产物
（生产入口与测试入口同一份字节）。Fable 输出形状（`Module_` 前缀、DU tag、FSharpMap）
隔离在唯一 facade `requirements/verification-system/tests/support/domain.mjs`；测试只经契约面进入（序列化文本、纯
函数、公开 Port、Host hook 对象、发布产物 export）。断言必须比对完整结构或完整序列化
文本，不得只断言真值。`.mjs` 消费的 `dist` 早于 `.fs` 源时运行器拒绝运行（陈旧产物
fail closed）。

**含义/动机**：语言边界物理性阻止测试触碰实现内部；Fable 约定是编译器产物不是领域
概念。字段改名后 undefined 静默通过——`.mjs` 没有编译期重命名保护，完整结构断言是它的
对价。

**边界**：facade 文件本身是 MECHANISM（VERIFY-008 文本规定 facade 需要元测试）；具体
产品契约面断言归各产品包。

**证据指针**：→ PROOF.md L15。

## VERIFICATION-SYSTEM-009：静态门禁命中真实路径

**规范陈述**：静态检查的路径判据必须与实际目录一致；指向不存在目录的检查恒为通过，是
伪门禁，等同于没有检查。层序/ladder 中每个被声明的入口（check.mjs、build.mjs、各
run.mjs、entry.test.mjs、wired gate）必须是真实存在的文件。

**含义/动机**：历史 change（fix）教训：DSL 门禁只扫 136/245 个文件仍宣称全量
清零——门没装在房间门口。proof-ladder 的「每个 wired 路径存在」断言把这条变成机器事实。

**边界**：本命题管「门禁自身指向真实路径」；具体扫描范围（扫哪些源码）归各语义门禁的
owner。

**证据指针**：→ PROOF.md L16。

## VERIFICATION-SYSTEM-010：验收判据不可放宽

**规范陈述**：已冻结的验收判据（case 天花板、timeout 预算、ratchet 基线、断言强度）只能
收紧不能放宽：case 天花板只降不升（g4r-freeze ratchet 已退休 2026-08-14，sole-entry scope
由 `e2e-watchdog-feed` 承接）、timeout 预算拒绝膨胀。
执行者不得自降 close 判据（Deferred 不阻塞 close 需用户 Amendment——过程面归
`requirement-system` 的 blocker 协议）。

**含义/动机**：历史 change（fix）审计的「验收口径事后缩水」：frozen scope 明说
是 close 条件，执行者自己宣布 Deferred 不阻塞 close。机器 ratchet 只降不升 + 过程
Amendment 协议双保险。

**边界**：机器可红面 = 只降不升断言；人类过程面（谁有权宣布 Deferred）归
`requirement-system` 013/014。

**证据指针**：→ PROOF.md L17。

## VERIFICATION-SYSTEM-011：覆盖率门禁分母完整

**规范陈述**：单元覆盖率的门槛（整体行覆盖率 ≥ 80%）作用在**全部 dist 生产模块**（排除
`fable_modules`）：覆盖率运行先预导入全部生产模块，未加载模块以 0% 计入分母；排除项固定
为 `node_modules` / `fable_modules` / `tests`；低于阈值 exit 1，不允许豁免通道。
覆盖率只许通过增加测试提升，禁止为测试新增 export、放宽可见性或改写生产结构压缩行数。

**含义/动机**：node:test 的 V8 覆盖率只统计被加载文件；不预导入，分母缩水让百分比虚高。
豁免通道 = 伪门（没锁的门不是门）。

**边界**：覆盖门禁的机器载体是 `requirements/verification-system/tests/run.mjs --coverage`（MECHANISM，lead 集成时
执行）；本命题的落点当前为 REUSE + cutover 拆分计划（SPLIT@cutover）。

**证据指针**：→ PROOF.md L18。

## VERIFICATION-SYSTEM-012：行数不是门禁

**规范陈述**：Gate 只阻断语义违规，不阻断尺寸；文件长度既不硬阻断也不告警。行数是
症状不是病因；用行数代理会产生反向激励（机械拆分 `*Helpers.fs` / `*Fields.fs` /
`*Core.fs`）。机械后缀命名仍需显式 allowlist——这才是防拆分逃逸的真门禁。Kolmogorov
size 是 advisory（建议信号），超过基线只提示不判红。

**含义/动机**：真正要禁的是样板、框架礼仪、错误抽象、重复知识——由语义门禁直接命中。
advisory 与 gate 的区分本身是证明资格问题：没有失败价值的「门」会让人以为有保护。

**边界**：本命题管「行数不得成为门」；各语义门禁的 allowlist 内容归各包。

**证据指针**：→ PROOF.md L19。
