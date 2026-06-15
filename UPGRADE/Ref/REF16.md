# REF16: Deepthink 模式——策略生成与子策略

## 1. 策略生成 (generateStrategies)

### 初始策略生成
- 使用 `buildInitialStrategyPrompt(challengeText, count)` 构造提示
- 系统提示词：`sys_deepthink_initialStrategy`
- JSON 输出模式
- 返回 `{ strategies: ["Strategy 1: ...", ...] }`
- 解析为 `DeepthinkMainStrategyData` 数组
- 关键：策略必须独立、自包含、不代表最终答案

### 子策略生成 (generateSubStrategies)
- EDFS 模式下跳过（直接使用主策略文本）
- 非 EDFS 模式：对每个主策略并行调用
- 系统提示词：`sys_deepthink_subStrategy`
- 返回 `{ sub_strategies: ["Sub-strategy 1: ...", ...] }`
- 跳过模式（skip mode）：subStrategyCount=0 时直接用一个 `{id}-direct` 子策略

## 2. 提示词构建细节

### 策略生成提示
- 包含 Core Challenge、策略数量
- 要求生成真正新颖、基础不同的高级解释
- 明确禁止：最终答案、代码、详细执行步骤、JSON 以外内容

### 子策略生成提示
- 包含 Core Challenge、当前主策略、其他主策略
- 要求生成子策略在**当前主策略内部**的细化方向
- 明确禁止：脱离主策略、包含执行细节、最终答案

## 3. 策略的核心质量要求

- 80% 的策略应强调真正新颖或非显而易见的探索方向
- 最多 20% 可以采用传统/保守方法
- 策略不能是检查清单步骤、通用指令、主挑战的复述
- 每个策略必须独立可执行
