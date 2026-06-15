# REF61: Deepthink 配置面板——策略执行配置

## 1. 策略数滑块

EDFS 模式：1-5（最大）
非 EDFS 模式：1-10

颜色：红色（`#e86b6b`）

## 2. 子策略数点阵滑块

离散值：0, 2, 3, 4, 5

EDFS 模式强制 0（禁用）。

点阵滑块特点：
- 下方有点阵刻度
- 选中点高亮显示
- 值为 0 时标记"(Disabled)"

## 3. PQF 设置

仅 EDFS 模式启用，两种模式：
- **Balanced**: 默认，仅在战略失败证据确凿时更新
- **Very Aggressive**: 更激进地替换

UI 显示 PQF 流程图：
```
Branch(5轮) → [PQF] → Keep(继续) / Update(新分支)
```

## 4. 信息包窗口

类 macOS 窗口风格的容器，包含：
- 红/黄/绿控制按钮（装饰性）
- 标题 "Information Packet"
- 假设注入模式可视化
- 假设生成和执行的流程连线图

## 5. 精炼选项

主开关：Enable Refinements（ToggleSwitch）
子选项：
- Critique Synthesis（批判合成）
- Full Solution Context（完整方案上下文）
- Evolving Depth First Search（EDFS）
  - Search Depth 滑块（1-10）
  - Token Volume 实时估算图表
