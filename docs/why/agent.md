# Agent — 理由

Role 与 Tier 分离：权限矩阵若随「换模型」分叉，fast/deep 会演化成两套产品，Peer Fallback 也失去对称前提。

双层权限（Host schema + ToolRegistry）是因为 Host 配置可漂：只信一层会在配置异常时漏工具或越权执行。

`external_directory` 固定 allow 是对 Host 默认 ask 的显式覆盖，不是把路径逃逸塞进角色工具矩阵——后者会污染 AGENT-006 的语义边界，并诱导「工具白名单 = 一切权限」的错误心智。

内部 Agent 从 enum 消失，是防止模型调用本应仅由运行时合成的 Blogger/Executor 路径。

## Meditator：inspector-only + epistemic style（G3）

**工具面：只留 SyncDelegate `inspector`，删除 Meditator 的 read/glob/grep。**  
若保留 filesystem 直读，产品无法回答「何时自己读、何时叫 Inspector」，必然退化成「便宜证据自己看、复杂再委派」——重复扫库、Inspector 上下文无法积累、角色边界靠 prompt 自觉。正确分层是 `Meditator = reasoning`，`Inspector = evidence`（AGENT-025；边见 AGENT-024）。

**认知姿态：吸收 Student epistemic style，删除 Student workflow protocol。**  
保留：形成理解、主动反例、委派取证、证据/推论/不确定性区分、收敛前不草率终止。拒绝：LearningState、QA、Compile、MeditatorLearn/Compile、Student 式 final `return`。学习不再是特殊 Agent 程序，而是普通推理 + 同步证据委派。

**Student/Teacher：G3 已删除（AGENT-020…022 空缺）。**  
Catalog / Role DU 基线 20；旧名 fail closed、无 alias。不得再写「pending deletion / 仍存在于生产」。

## 备选与被拒

**权限维度：Role 与 Tier 分离 vs 随模型分叉。** 拒分叉：fast/deep 随「换模型」演化成两套产品，Peer Fallback 丢对称前提（AGENT-001 不变角色，CanonicalRole 与 fast/deep 无关）。

**权限载体：双层（Host schema + ToolRegistry）vs 单层可信。** 拒单层：Host 配置可漂；只信一层会在配置异常时漏工具或越权执行（AGENT-006 语义边界）。

**external_directory：固定 allow 元权限 vs 塞进角色工具矩阵。** 拒塞矩阵：污染 AGENT-006 边并诱导「工具白名单=一切权限」的错心智（AGENT-019）。显式覆盖 Host 默认 ask 即可。

**内部路径暴露：Agent enum 留内部 vs 隐去。** 拒保留：模型须无法合成 Blogger/Executor 等运行时专用路径（AGENT-008/ENFORCER-010），只余外部可寻 agent。

**Meditator 工具：inspector-only vs 保留 read/glob/grep + inspector。** 拒双持：会重新变成「小 Inspector + 推理」，破坏证据获取与推理分层（AGENT-025）。

**Student 价值迁移：epistemic style → Meditator prompt vs 整套 Learn/Compile/QA/return 改名留存。** 拒改名留存：那只是换皮的 Student 状态机；G3 删除 workflow、保留认知纪律（AGENT-020 空缺 → AGENT-025）。

**Student/Teacher 兼容 alias vs clean-break。** 拒 alias：`student`→Meditator / `teacher`→Inspector 会永久污染身份边界（AGENT-004）。
