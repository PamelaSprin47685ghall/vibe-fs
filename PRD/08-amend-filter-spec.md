# 万象术自动纠偏（Amend）机制产品需求文档 (PRD)

---

## 1. 需求背景与痛点 (Problem Statement)
在大语言模型驱动的多代理插件运行时中，
由于 LLM 存在概率性幻觉，
工具调用时常遭遇参数非法、断路保护或逻辑走错等不可逆失败。
当失败已成为既定事实，
现有的历史对话累加模式不仅增加了上下文窗口开销，
更会迫使 LLM 沿着错误的逻辑链条继续推进，
丧失了“后悔药”机制。

为解决这一根源痛点，
系统必须在工具调用前置与历史投影折叠两个维度引入
**amend (纠偏/撤销)** 机制：
1. **工具执行前置参数拦截**：
   防止宿主及下游工具因为识别到元参数 `amend`
   而产生协议解析异常。
2. **历史消息投影流剪枝**：
   自动在消息投影中回溯并剪除已被 amend 纠偏的错误工具调用链，
   为 LLM 注入具备自愈能力的纯净历史视图。

---

## 2. 第一性原理设计方案 (First Principles Architecture)

### 2.1 物理沙盒隔离与双边防线
- **Before-Hook 拦截防线**：
  在宿主工具执行的最外侧边界（Before Hook）
  同步拦截并删除 `amend` 字段，
  确保 downstream 工具执行纯净度。
- **Pipeline 投影剪枝防线**：
  在 pipeline 消息清洗层（Cleaned）与
  Backlog 积分投影（Backlog Projection）之间注入 `AmendFilter`。
  先裁切错误历史，再折叠 progress，消灭幽灵状态。

```
LLM Output (with amend=N)
   │
   ├──► [Host Before Hook] ──► filterAmendFromArgs ──► Execute Tool
   │
   └──► [MessageTransformPipeline]
              │
              ▼
       [AmendFilter] ──► (O(N) Stack Pruning) ──► Cleaned History
              │
              ▼
       [Backlog Projection] ──► folds durable backlog (No Ghost States)
```

---

## 3. 功能规约与算法描述 (Functional Specifications)

### 3.1 工具参数过滤规约
- **输入**：动态类型参数 `args: obj`。
- **字段检测**：提取 `amend` 字段，
  支持类型包括：`int`、`float`、`string`
  （利用 `Int32.TryParse` 进行文化无关转换）。
- **拦截行为**：若 `amend` 存在且其整数值 $N > 0$：
  - 从 `args` 物理删除 `amend` 键值（
    调用 `Dyn.deleteKey`），
    确保下游工具不可见。
  - 返回 `Some N` 作为纠偏命令。
- **作用域**：全宿主覆盖，包括 OpenCode, Mux 以及 oh-my-pi (Omp)。

### 3.2 消息栈剪枝过滤规约
- **输入**：`messages: Message list`。
- **输出**：`filteredMessages: Message list`。
- **折叠累加器（Stack Scanner）**：
  从左到右以尾递归折叠消息列表：
  - **普通消息**：以 $O(1)$ 头部插入追加到已处理反序栈 `acc`。
  - **纠偏消息 (amend=N)**：
    - 将 `acc` 反转为正序：`tempNormal = List.rev acc`。
    - 循环 $N$ 次调用 `popOneToolCall`，
      对 `tempNormal` 进行局域工具链剪枝。
    - 将剪枝后的列表反转回反序，
      追加当前 `amend` 消息本身（
      保持事实可还原）。

### 3.3 工具链（Tool Call Chain）界定与弹出边界
一个完整的工具调用链被定义为一个紧凑的原子交互闭环。
其弹出边界通过以下步骤进行数学上的闭合界定：
1. **寻找 Assistant 发起锚点**：
   从列表尾部逆向扫描，
   以最新的 `Assistant` 消息（
   包含至少一个 `ToolPart`）为锚点。
2. **提取 CallIDs**：
   从该 `Assistant` 消息的 `parts` 中提取所有 `callID` 集合：
   $$\text{CallIDs} = \{ c \mid \text{ToolPart}(\_, c, \_, \_) \in \text{parts} \}$$
3. **定位匹配 of ToolResult**：
   - 逆向扫描匹配的 `ToolResult` 消息。
     其条件为：消息角色为 `ToolResult`，
     且满足其 callID 属于 $\text{CallIDs}$。
   - 若测试场景下 `ToolResult` 消息不包含 `parts`
     （即 $\text{CallIDs} = \emptyset$），
     则 fallback 自动匹配首个扫描到的无
     callID 约束的 `ToolResult`。
4. **确定前导隔离墙 (startIdx)**：
   - 工具链回溯前导边界 `startIdx`
     受限于该助理消息之前的最近一个 `ToolResult` 索引加 1
     （即 `prevToolResultIdx + 1`）。
   - 若此前没有已结案的 `ToolResult`，
     则回溯界限截止于最近的 `User` 消息索引（`prevUserIdx`），
     以防跨交互误杀历史普通消息。
5. **原子弹出**：将 `[startIdx ... endIdx]`
   闭区间内的所有消息连同 `Assistant`
   和 `ToolResult` 一同彻底抹除。

---

## 4. 状态机迁移规约 (Nested & Parallel Scenarios)

### 4.1 嵌套纠偏 (Nested Amend)
- **规则**：若消息 A 携带 `amend`，
  折叠时迈不过去自身，也绝不自杀。
- **嵌套求值**：纠偏消息进入 `acc` 后，
  随后的 `amend` 消息在正向折叠中，
  依然可以通过 pop 操作将其剪除。
- **示例**：
  - 输入：
    `[User, ToolChain1, User(amend=1), ToolChain2, User(amend=1)]`
  - 折叠 1（处理第 3 条消息）：
    弹出 `ToolChain1`，保留 `User(amend=1)` 作为状态事实。
  - 折叠 2（处理第 5 条消息）：
    弹出 `ToolChain2`，保留 `User(amend=1)`。
  - 最终投影输出：`[User, User(amend=1), User(amend=1)]`。

### 4.2 并行多工具调用 (Parallel Tool Calls)
- **原子性**：一个 `Assistant` 发起多个 `ToolPart`
  （并行调用）时，
  所有关联 of `ToolResult` 会被 `getCallIDs`
  组成的并集精确锁定。
- **剪枝结果**：整组并行调用及所有响应作为一个原子单元整体同归于尽，
  防止残留悬空 ToolResult。

---

## 5. 架构守护契约 (Architecture Constraints)
为阻止偶然复杂度漂移，系统设计建立了严苛的编译期与运行期强契约：
1. **零 empty-string 默认**：
   禁止在 Kernel 层使用任何形式的 `Option.defaultValue ""`
   或隐式空字面量降级，
   类型缺失必须以 Option DU 分支强迫上游处理。
2. **零二次方追加**：
   禁止在递归或折叠中使用 `@`
   操作符进行向后追加，
   列表操作一律限制为首部 `::` 插入配合单次 `List.rev`，
   时空复杂度恒定为 $O(N)$。
3. **安全切片保护**：
   切片操作（如 `messages.[..idx]`）前置增加边界安全防线，
   杜绝任何 `IndexOutOfBoundsException`。
4. **宿主隔离边界**：
   `src/Omp/` 仅限引用 Kernel 与 Shell 基础能力，
   禁止直接或隐式交叉导入 `Wanxiangshu.Opencode` 或
   `Wanxiangshu.Mux` 的任何特化资产。
