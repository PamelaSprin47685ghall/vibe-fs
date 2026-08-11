# 合成 TOML — 目标实现

## Implements

- ARCH-010
- ARCH-011
- ARCH-012

## Ownership

统一字符串 codec、可信 renderer 和 production inventory 的边界见
`shape/synthetic-toml.md`。适用 surface 见 `what/synthetic-toml.md`。

## Algorithm

Renderer 接收强类型 instruction 与 data，不接收已拼接的“半成品 TOML”。输出顺序固定：

```text
render leading instruction comments
→ render data tables/fields/values
→ UTF-8 encode
→ enforce the owning surface's byte/line bound
```

三种输入均直接支持：instruction-only、data-only、instruction+data。不得为统一形状伪造空段。

### Instruction comments

每条可信 instruction 由 renderer 生成顶层 `# ` comment。换行 instruction 被拆成连续
comment 行；一旦开始 data，renderer 不再发出顶层 comment。

### Data values 与 instruction 分类（producer adoption）

Instruction/data 分类由**当前** synthetic projection 的 owner 指派，不由 provenance、
trust 或 historicity 自动决定。原料从不自我提升；采用是显式 producer 决策：

- 该 owner **明确采用**为接收 agent 当前指引的材料 → instruction comment plane；
- 作为引用证据、背景、状态、payload 或 machine-readable context **保留**的材料 → TOML data。

业务模块只提供字段名和 typed value，不得拼接裸表头、comment 或 delimiter。未采用的
用户/assistant/reasoning 副本、tool arguments、stdout/stderr、文件、diff、日志、网络响应
与外部文档仍作为 data value 编码。

### String encoding

统一 codec 按以下顺序选择表示：

1. 可安全逐字承载的多行文本使用 TOML multiline literal string `'''`；
2. 单行文本使用 canonical TOML string；
3. 内容含 literal delimiter 或非法控制字符时，使用 canonical basic-string escape；
4. 禁止另建 `"""` 多行方言。

closing delimiter、换行和 escape 规则只由该 codec 决定。

### Value tree（js-* 结果等结构化 data）

同一 owner 编码 JSON 兼容值树。决定性规则：

- `null` 只允许作对象字段（省略）或根（无 data 块）；数组元素 `null` 拒绝，不在此层发明哨兵。
- boolean → `true` / `false`；安全整数（`Number.isInteger` 且 |n| ≤ 2^53−1）→ TOML integer；其它有限数 → TOML float（无小数点且无指数时补 `.0`）。
- 字符串走上文 String encoding。
- 原始值（及嵌套原始值数组）→ inline array；空数组 → `[]`。
- 全对象数组 → `[[path]]` array of tables；行内标量字段写在该 entry；行内嵌套对象/对象数组作为随后的子表，附着于最近一条 aot。
- 对象 → `[path]`；仅有嵌套子表、无本地标量时省略空表头；空对象发空表头。
- 键：`[A-Za-z0-9_-]+` 裸写，否则 basic quoted key（与字符串同一套 escape）。
- 表路径用 `.` 连接已渲染的键。
- `document` 仍把裸字段排到任何表头之前。

无法按上表编码：renderer 边界 typed failure，不回退 JSON 字符串字段，不回退裸英语。

### Local schemas

每个 surface 只定义完成本地语义所需的最小字段。不得添加全局 `kind/origin/authority`
envelope，也不得从渲染文本反向恢复这些类型。

### Blogger delta

Blogger data body 与可选 instruction header 分开构造，再由同一 renderer 合并。
CTX-013 的大小计量发生在完整渲染后的 UTF-8 字节上。该 surface 的 owner 决策下，
historic frame 中的历史祈使句仍作为 data value，不提升为 comment；此为局部 surface
合同，不是全局「凡历史一律 data」。

### Join / fork

本地语义分类（owner 采用，非 trust 自动提升）：

| 材料 | plane |
|------|-------|
| Fork assignment | instruction |
| Fork parent_work_record | background data |
| Fork original_user_requirement | data（由 instruction header 说明） |
| Join 已完成 child work_record | entry-local guidance comments |
| One-shot sync child work_record | entry-local guidance comments |
| Join status / ordinal / kind / agent / failure / interrupt 元数据 | data |

具体 wire 字段由 EXEC-004（Join）与 EXEC-028（One-shot sync）分别定义；本文件只规定它们使用统一 codec 与上述 plane 指派。

### Tool results

pass-through 与 marker+tail 都先通过同一 data renderer，再应用 ARCH-012 的行数和字节界；
截断不得切断 Unicode scalar/surrogate pair。

## Failure handling

- 无法编码为 canonical TOML：在 renderer 边界返回 typed failure，不回退裸英语。
- 不可信 data 试图提供结构：仍按普通字符串值编码，不能逃逸到顶层。
- surface 未进入 inventory：静态门禁失败。
- 超过领域大小界：使用该领域已裁决的截断/拒绝算法，不在此发明新行为。

## Determinism and constants

本文件不重复 CTX-013 或 ARCH-012 的数值。相同 typed input 必须产生逐字节相同输出；
字段顺序、字符串分支和换行由统一 renderer 固定。

## Implementation mapping

- codec/renderer：生产 `SyntheticToml` 单一入口
- surface inventory 与 golden：integration harness 的 ARCH-010 cases
- 领域调用点：由 inventory 映射，不在规范中冻结文件数量或当前清单快照

证明义务见 `proof/synthetic-toml.md`。
