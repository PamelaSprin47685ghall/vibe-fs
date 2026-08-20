# provider-language — WHY

## 核心动机与不可替代性

一个 participant life 必须生活在单一且稳定的自然语言世界中，而机器协议标识符（protocol identifiers）则必须保持全局同一，严禁被翻译。

语言绑定是 session 级别的不可变事实：
- **不隶属于 `participant-identity`**：切换执行绑定（如 fast 到 deep 的 fallback 或 Strength 副本）不更换参与者身份，也不应切换会话语言。
- **不隶属于 `participant-horizon`**：Horizon 决定“什么信息有资格进入感知界面”；Language 决定“这些信息以何种自然语言呈现”。
- **不隶属于 `provider-projection`**：Projection 负责将语义意图转化为确定性的字节布局；Language 确定会话所使用的语言。
- **不隶属于认知引导**：语言是承载散文文本的会话基础设施，而非散文本身的业务语义。

## 失败模式（RED）

- **混合语言世界**：同一会话内出现多种自然语言混合（例如中文 System Prompt 配对英文工具契约），破坏认知一致性。
- **子会话语言漂移**：在委托、重试、错误恢复或计划分支后，子会话或后续轮次突然切换语言，导致前缀缓存失效与上下文断裂。
- **机器标识误翻译**：将工具名、字段名或参数标识符（如 `exit_code`）翻译为自然语言，破坏协议执行的确定性。
- **散落的条件判断**：业务代码中充斥 `if lang == ...` 或硬编码的多语言字面量，缺乏集中的所有权管控。

## 独立变化能力

新增支持的 Locale 或调整资源组织方式，无需修改身份、感知范围或投影代数的任何命题；反之亦然。

## DEPENDS ON

- `session-ontology`
