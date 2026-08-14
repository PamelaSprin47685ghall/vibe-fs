# work-record — WHY

## 1. 不可替代的存在理由

一段 work 会在多个边界之间传递：

```text
parent ──fork──→ child         （父→子：子需要任务全文）
child  ──join──→ parent        （子→父：布置者已知任务，只回结果）
process review / Finality      （第三方 judge 需要完整证据，但不需要 Opening）
SyncDelegate caller            （reusable session，只许看见本次 invocation）
```

每个 receiver 需要**同一个事实**：这段 work 做了什么、边界在哪。但它们的投影不同
（includeOpening 有 true/false）。如果每种 receiver 各造一份摘要，同一个 work 就有多份
「官方说法」，review 与 finality 无法互证。

**work-record 保证：一段 work 只有一个 canonical bounded statement（LifecycleWorkRecord /
LWR），receiver 只能选择投影，不能改变事实。**

## 2. 独立存在测试

把 LWR 的 renderer / 段表示 / 标题字面整体重写——只要 work-boundedness、preserved Opening、
coverage 分型、prose-claim 与 projection semantics 不变，delegation / review / finality 的
WHAT 一行都不用改。反过来，若允许「session head summary 冒充某次 work 的 record」，delegation
与 review 会读到别的 invocation 的历史——独立失败域。

## 3. 失败意义（FAILURE MEANING）

RED = 满足下列任一：

1. consumer 收到的工作记录混入其它 invocation / session 历史（取 session head）；
2. 记录丢失 constitutive Opening（从 Assignment/requirements 文本重建）；
3. 记录因 receiver 不同而改变事实（不是只改投影）；
4. 要求 participant 填固定 DTO（`### Summary` / Files/Tests/Risks）才算完成。

## 4. 历史考古

### 4.1 为什么从 Companion/Review 中抽出（HANDOFF §6.10）

`LifecycleWorkRecord` 被 delegation（EXEC-006/008/028/031）、process review（REVIEW-016）、
Finality（GLORY-004/050）同时复用，且有独立 WHY：跨边界传递的 bounded canonical statement。
继续藏在 Companion/Review 下，会让「Comp 的 record」与「Review 的 record」看起来像两个概念。

### 4.2 旧标题与 Closing report（COMPANION-003/015 考古）

旧三段标题 `Opening task / Work log / Uncompressed tail / Final output` 与独立 `Closing report`
段**已删除、无 alias**。原因：

- 「Uncompressed tail」把表示边界说成「未压缩尾巴」，诱导实现者再造固定报告 DTO；
- 独立 Closing 是第二通道——正式陈述已经是 Recent work 最后一条助手文本；
  另开一个「Final output」段 = 同一次 invocation 有两个答案，review 无法判断信哪个。

### 4.3 父 LWR 作 child Seed 被拒（COMPANION-003）

父 LWR 是 child 的**输入 context**，不是 child 的 Opening 复制。若父 LWR 当 Seed，
多代 fork 指数嵌套：孙子的 Opening 里套着父的 Opening 里套着爷的 Opening。

### 4.4 固定 schema 被拒（ARCH-015 / COMPANION-015 ⑫）

`### Summary` / `### Files Changed` / 逐角色 DTO 约束骨架而非诚实。散文 claim 约束诚实：
角色可以自然提及事实，但不得把「必须提到 files/tests/risks」写成格式。machine-semantic
结构只留协议真需处（`exit_code`、`verdict`）。

### 4.5 coverage 分型的历史教训（COMPANION-003 / TODO-008）

RecordCoverage 与 PrefixCoverage 曾可能被混用。「Y 还没覆盖完就声称可替换 X 前缀」会让
prefix replacement 建立在半 turn 证据上。分型的现实失败模式：用 LWR RawGap 填 prefix
Y bundle，或反过来用 PrefixCoverage 填 LWR gap——两种都是「用一种证明量纲冒充另一种」。

## 5. 与相邻包的边界

| 看似相邻 | 为什么不归本包 |
|---|---|
| XTrace 原始历史 | record 是 trace 的**物化**；事实源在 semantic-trace |
| Chronicle 怎么生成 | Y 的压缩能力归 context-compression |
| review 是否消费 record | 消费时机/资格归 review-assurance；record 本身是本包 |
| 何时发起 delegation/finality | delegation / finality 的触发语义 |
| Terminal 完成标记 | 私有，不是 LWR 段（归 semantic-trace 的 terminal 事实 + 本包的「不是段」边界） |

## 6. 源材料

- 历史 what companion（COMPANION-003/005/007/014/015）
- 历史 what todo（TODO-001/008/009/015）、历史 what review（REVIEW-016）
- 历史 what execution（EXEC-004/028/031）、历史 what glory（GLORY-004/006/072/074）
- 历史 why/shape/how companion
- 历史 requirements-design card（21-work-record、13-context-continuity work-record 部分）
- 历史 COVERAGE（COMPANION-003/014/015、REVIEW-016、TODO-001/008、GLORY-004 行）
