# behavior-diagnosis — 为什么必须独立存在

## 1. 一个不可替代的存在理由

工程监督系统最容易退化成「关键词匹配器 + 评分器」：看到 `bool` 就说
boolean-blindness，看到 `catch` 就说 catch-all，把分数加起来再凭阈值决定要不要
「提醒」。这套东西的失败方式不是某一个 bug，而是**整个诊断语义被架空**：

- 模糊关键词让诊断在错误输入上成立（false positive 无边界）；
- 评分向量把「诊断是否成立」变成「分数够不够高」，于是 diagnosis 没有独立身份，
  只是控制流里的一个数值；
- 单次表象（一次失败、一个词）就能升级成病理标签，普通现象被永久污名化。

本包的存在理由：**diagnosis 成立必须由满足 trigger / negative / distinction 的
evidence 支撑，且每次成立的 diagnosis 是一次有独立 semantic identity 的语义事件，
不是评分、不是重复计数、不是历史改写**。规则是数据（目录 Markdown），Host 只证明
cycle 身份与原子提交，不重新解释严重度、不对工程判断做数值积分。

## 2. 历史上为什么 RED（归档 changes 考古）

### 2.1 旧 `15`：120 维 score-vector 才是原罪

历史 change（enforcer）（旧 `SSOT/15` rebase）记录：旧设计是「120 个
optional 0..9 字段 → score vector → leaky-evidence throttle → NudgeAnchored /
NudgeConsumed → Main fake-user overlay」。它把「给出一个工程意见」升级成了第二个
解释器：聚合、衰减、时间、阈值、reset、overlay 锚定、消费确认和恢复窗口全都要。

由此派生出一整串必然后果，每一条都是本包反对的：

- 缺失 score 默认 0（缺失被伪装成「没问题」）；
- 数字字符串 / clamp / score parser（wire 层引入数值语义）；
- Damerau–Levenshtein typo repair（未知字段被强行解释成某条规则 → 诊断在模糊
  匹配上成立）；
- 同规则 score 取 max（多调用时「最严重」赢，而不是确定性选择）；
- `EnforcementReport` 评分向量、`EnforcementObservationOrdinal`、leaky integrator
  / tau / pressure threshold（诊断退化成数值积分）。

### 2.2 修复方向：tip 取代 score，目录即身份

rebase 裁决（已落地）：删除全部 score/throttle/Main-overlay 机制，不给他们找同名
新壳。每个 tip 目录只保留单一 `tip` 字段；Host 只证明 tip 来自 catalog、cycle 身份
成立、text 非空、commit 与 coverage 原子、replay/recovery 不重复产生业务事实。

### 2.3 规则载体三连拒（历史 why/enforcer 条款）

- **拒生成代码**：规范生成 F# 让变更绑编译、多份清单漂移；规则是数据、按 tip
  目录打包、运行期扫描校验。
- **拒 `catalog.json` 第二真相**：目录扫描已给出 TipName 与正文，JSON 只会变成
  第二个会漂的 ordinal/field 表。
- **拒代码内 fallback catalog / dist 双副本**：掩盖打包错误，让坏包静默成功。

### 2.4 观察配对（历史 change（rulebook））

旧实现把 tip 历史与 work-log frame 建模成两套独立列表；squash 后 frame 变一个而
tips 仍独立存活，模型必须自己猜「tip 2 属于 frame 1/2/3 哪个」。Rulebook v2 把二者
压成同一个不可拆 Observation：共同产生、共同持久化、共同 squash、共同恢复。

## 3. 边界：什么**不**归本包

- 诊断如何/何时展示给 Main —— `guidance-delivery`（何时/如何再次告知是独立的
  delivery/coverage 问题）。
- feedback dedupe/coverage —— `guidance-delivery`。
- `chronicle` 工具名与 Blogger 工具权限（ENFORCER-010/011）—— `capability-enforcement`。
- tip 目录物理格式/文件名 —— 本包只消费目录身份，物理布局是资源实现细节（且
  `guidance-delivery` 的 INDEPENDENT CHANGE 恰好就是「目录格式换成 typed catalog
  而 diagnosis 不动」）。
- score vector / ordinal —— 已 clean break（GARBAGE，见 `HOW.md` 弃权）。

## 4. FAILURE MEANING

RED = 模糊关键词、评分或单一表象就能制造 diagnosis；或历史 rewrite（squash /
compaction）被误当成新问题发生；或一个 tip occurrence 没有独立身份、无法在
durable history 中被引用。
