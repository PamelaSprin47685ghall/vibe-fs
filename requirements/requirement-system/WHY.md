# WHY：为什么必须有一个包管「谁拥有什么」

## 不可替代的存在理由

万象术的知识同时活在五个地方：正式 docs（条款）、静态 gate（脚本）、单元测试（断言）、
completed change（历史裁决）。只要没有一条规则说清「某个产品事实由谁拥有、谁能定义、谁能
证明」，这五个地方就会**各自长出自己的法域**：

```text
docs 说 A，gate 说 B，test 证明 C，change 记录 D —— 四者同时为真，互相覆盖
```

历史已经发生过这种失败。历史 change（fix.md）审计发现的「验收口径事后缩水」之所以
可能，正是因为**没有单一权威能裁决「谁有权宣布 close 判据」**；旧 `ce.md` 的 DSL 门禁
「threshold=0」只扫 136/245 个生产文件却宣称全量清零，正是因为**门的边界没有 owner**——
一个没人拥有的门禁，扫不扫、扫多少、谁负责，全凭实现者当天的心情。

`requirement-system` 存在的理由就是：**把「谁拥有什么」本身变成一条被拥有的规则。**

## 为什么是元合同（META）而不是又一个产品包

产品包（如 `durable-events`、`review-assurance`）拥有领域事实：journal 怎么 append、
review 何时可消费。它们回答「世界是什么」。

`requirement-system` 不回答任何领域问题，它回答：

```text
这条命题归谁？
这个证明归谁？
这个包依赖什么？
哪个文件是 normative，哪个只是导航？
```

这些问题的答案不能散落在 docs 散文里（那会变成第七个平行法域），必须由**一个包**集中拥有，
并且这个包自身也遵守「无裸规范权威」——连「治理规则」都要有明确的归属文件（WHAT.md）。

## 为什么不能并入 verification-system

`verification-system` 回答「**怎么证明**」（分层证据、可红性、fail-closed）。
`requirement-system` 回答「**谁拥有什么**」（唯一 owner、显式依赖、唯一 proof ownership）。

两者的独立变化测试：把 manifest 从 TOML 改成其它机器格式、重排包目录物理布局——所有包的
WHAT 都不变，只有 requirement-system 的 HOW 变；把真实 Host canary 换成另一物理 adapter——
verification-system 变而 requirement-system 不变。合在一起，一次重构会同时牵动两种失败
意义，无法独立验收。

## RED 是什么样

```text
仓库中存在无 owner、双 owner、互相矛盾或无法独立验收的 normative authority
```

具体形态：两个包同时声称拥有同一命题；某条命题只在散文里存在、没有编号没有落点；
requirements/ 出现 INDEX 之外的神秘包目录；某个包声明了骨架里不存在的依赖边；某条
WHAT 命题在 HOW.md 里找不到行——「绿」无法被检查，只能靠口头相信。

## 考古：本包的条款来源

| 来源 | 吸收为什么 |
|---|---|
| 历史 boundary card（01-meta-programming） | 唯一 owner、同时为真、proof 唯一 owner、无裸权威、包 verifier |
| 历史 GOV 条款（GOV-002/005/006/007/008/009/011/012，2026-08-14 归档） | 分域权威、条款 ID、单文件生命周期、用户所有权、Active/Completed、blocker、层归属、直接闭环 |
| 历史 document-governance 证明 | 所有权与边界、执行程序、机器检查/人工评审义务 |
| 历史 changes README + `AGENTS.md` 文档生命周期节 | 变更工作协议的执行文本 |
| 历史 COVERAGE GOV 行 | GOV-001/003/004/010 判 HOW/GARBAGE（旧 5 层载体 + clean break 历史），不迁入永久 WHAT |
| 历史 PROOF-MAP Meta 行 | spec.mjs + spec-rules.mjs 机制归本包；meta-verifier 已落地 |

## 被拒方向（为什么不是别的样子）

- **每个包自管自己的所有权**：所有权规则若每包一份，跨包矛盾（双 owner）没有裁决者，等于没有规则。
- **把治理规则放回 AGENTS.md / docs 散文**：散文不可执行、不可红，正是本包要消灭的形态。
- **建中央 manifest / 注册表**：schema 未裁决（见 HOW.md 历史与弃权）；当前用「目录即状态 +
  每包 5 份文档」的零同步债务形态。
