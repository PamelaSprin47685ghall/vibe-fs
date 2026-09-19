# action-affordance — HOW

## 架构与实现机制

1. **描述资源作为契约载体**：
   - 动作契约文本统一定义于 `resources/provider/tool/<name>/description/{en,zh-CN}.md`。
   - `ToolRegistry` 与 OpenCode `Tool.Def` 仅负责加载已本地化的描述文本，`ToolHostCodec` 负责布局与转义，不拥有散文语义。

2. **双语语义锚点与防退化门禁**：
   - 高风险动词的核心约束通过双语认知锚点进行机械化保护（如 `TOOL_DESCRIPTION_ANCHORS`）。
   - 静态检查门禁保证多语言描述中语义锚点严格成对、无遗漏。

3. **边界镜像机制**：
   - Canonical 权限后果由 `office-capability` 唯一定义；本包在调用边界（如 `fork`、`inspect` 描述）中镜像其负边界与职能分工，防止跨角色误用。

4. **`assume` 的 jq 画板实现**：
   - `AssumeTool` 只公开两个必填 jq program：`update` 与 `query`。当前进程持有一个唯一 JSON 根值；不建立第二套 workspace/record/revision DSL。
   - jq 语义由 `jq-wasm` 提供的 jq 1.8.2 实现直接承担，避免另外引入 Process 子系统或依赖宿主机安装 jq 二进制。
   - 每次调用串行执行：旧根值 → `update`。`update` 必须恰好得到一个 JSON output，随后替换根值；再以新根值 → `query`，返回其零/一/多 outputs。`query` 失败不回滚已成功的 `update`。纯查询使用 `update = "."`。
    - 长篇 jq 画板使用指南继续由 `resources/provider/tool/assume/description/{en,zh-CN}.md` 拥有；F# 只实现机器语义，不复制散文规则。

## GAP

- `action-affordance-002` ~ `005` / `009` / `011` / `012`（CLOSED）：高风险动词最低契约与认知锚点约束（002）、inspect 负边界（003）、repair-behavior mechanical 语义（004）、establish-behavior 源码与执行证据分离（005）、能力选择非裸枚举（009）、关键边界镜像（011）以及易混淆相邻动作镜像（012）均已闭合，落点分别为 `tests/002.test.mjs`、`tests/003.test.mjs`、`tests/004.test.mjs`、`tests/005.test.mjs`、`tests/009.test.mjs`、`tests/011.test.mjs` 与 `tests/012.test.mjs`。

