# provider-projection — WHY

## 核心动机与不可替代性

已获准进入参与者感知的类型化语义意图（typed semantic intent），必须经由唯一、确定性的代数投影管线转换为 Provider 可消费的表示形式（representation）。同时，表示层产物严禁反向解析为领域权威或状态。

投影管线在系统架构中具有不可替代的分工：
- **区别于 `participant-horizon`**：Horizon 决定哪些事实有资格进入感知范围；Projection 负责将已准入的意图确定性地渲染为字节形式。
- **区别于 `provider-language`**：Language 决定会话所使用的自然语言；Projection 负责结构布局、转义与格式编排。
- **区别于具体功能领域**：功能模块决定具体意图（如 Repair、Review、Companion 等）是否存在；Projection 负责意图的排序、合并律、冲突仲裁与确定性渲染。
- **区别于 `prefix-stability`**：Prefix Stability 关注跨请求的前缀字节连续性；Projection 提供底层的意图代数与无状态渲染器。

## 失败模式（RED）

- **装配顺序非确定性**：同一组语义意图因注册或装配顺序不同而渲染出不同的 Provider 上下文或前缀。
- **意图冲突静默选边**：多个意图修改同一锚点且缺乏明确合并律时，系统静默选择先注册者生效而非显式报错（fail-closed）。
- **表示层反向解析为权威**：将渲染后的 Wire/TOML 文本反向解析为控制流、领域状态或权限凭证。
- **混淆语义相等与传输相等**：将包含易失元数据（时间戳、耗时、调用 ID）的传输层视图用于计算规范语义摘要。
- **把来源误当成分面依据**：看到“record/evidence/result”就机械放入字段，忽略它对当前 Agent 实际承担的是责任交接或行动约束；child → parent LWR 因而被错误降格成“参考数据”。
- **多 renderer 漂移**：feature owner 各自拼 `#`、Markdown/XML envelope、TOML table 或空行，单处看似可读，组合后却出现第二 instruction block、字段被 table 吸收、或同一概念在不同路径使用不同形状。
- **render 后再组合**：多个各自合法的 document 在字符串层拼接，导致整体不再满足 instruction-first/data-second 的唯一语法与认知结构。

## 独立变化能力

更换 TOML/Wire 渲染器或优化意图规划算法，只要语义意图契约与相等性定义保持不变，本包即可独立演进。

## DEPENDS ON

- `participant-horizon`
- `provider-language`
