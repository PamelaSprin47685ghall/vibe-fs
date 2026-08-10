# JavaScript Capability-Projected Tool Surface

## Summary

用一个**能力投影生成器**为万象术新增可编程文件系统工具面：

```text
js-coder
js-inspector
js-reviewer
js-devops
js-browser
js-meditator
js-student   ← Universal G3 rebase debt only（见下）
js-teacher   ← Universal G3 rebase debt only（见下）
```

**Universal G3 rebase debt（不得复活生产）：** 文中残留的 `js-student` / `js-teacher`、StudentLearn / StudentCompile / Teacher 专属表面与迁移项，一律视为尚未从原文 scrub 干净的 **Universal G3 Clean Break rebase debt**。G3 已删除 Student / Teacher / QA / SKILL 生产；激活本 Change 时不得再实现、不得再迁回、不得再以兼容别名复活这些角色。实施输入只认当时仍存活的 Agent catalog + `AttemptExecutionProfile.ToolCapabilitySet`。

真正的 primitive 是：

> **一个由当前 Attempt 实际权限机械生成的、受 capability 约束的 JavaScript SDK。**

**内置文件工具不被替换。**

```text
read
edit
write
glob
grep
```

继续保留各自原有 schema、原有 Host 实现、原有执行语义。

它们与 `js-*` **共存**：

```text
builtin filesystem tools  = 既有 RPC 工具（含 read/edit/write/glob/grep/patch；deprecated，仍可执行）
js-ROLE                   = 新增 capability-projected JS SDK（强烈推荐）
```

引流方式不是 alias，也不是 clean break，而是：

> **Tool Definition 钩子：在内置工具的工具描述中极力、强力鼓吹并推荐当前可见的 `js-ROLE`。**

对于每次 provider request，万象术从唯一权威：

```text
AttemptExecutionProfile.ToolCapabilitySet
```

生成与当前 Agent、当前 RequestKind 实际能力**完全同构**的 `js-*` 主工具。

生成结果同时包含：

```text
工具名称
工具 schema
工具描述
JsProgram 基类
允许出现的成员函数
canonical examples
runtime capability bindings
BuiltinToolDescriptionHook 注入文案（写入仍可见的内置工具 description）
```

模型应优先完成一种任务：

```js
class Js extends JsProgram {
  async run() {
    // model program
  }
}
```

模型不再需要阅读一张权限矩阵，也不再需要记忆五种不同的 RPC 协议来完成复杂工作。

它只对一个准确生成的 SDK 编程。

核心原则：

> **能力不是写进工具说明里的；工具本身就是能力的投影。**

以及：

> **If a method is present, the capability exists.
> If a method is absent, it does not.**

最强不变量（四层同构，针对 `js-*`）：

> 没有某能力 → 生成的基类里没有该方法 → 工具描述里不出现该方法 → canonical examples 里不出现该方法 → 即使伪造底层调用，runtime gate 仍 fail closed。

本 Change 只有三个核心不变量：

1. **LLM-visible SDK 与 executable authority 完全同构**；
2. **一次 JS 工具调用 = 一个 all-or-nothing mutation transaction**（纯查询除外）；
3. **内置文件系统工具不被替换**：`read` / `edit` / `write` / `glob` / `grep` / `patch`（凡存在者）原 schema / 原实现保留；仅通过 Tool Definition 钩子在其 description 中 **deprecated + 极力推荐 `js-ROLE`**。

Compatibility：**Additive coexistence**。不移除内置 `read` / `edit` / `write` / `glob` / `grep` / `patch`；不把它们变成 `js-*` alias；不改它们的顶层 RPC schema。`js-*` 是新增表面。迁移靠描述钩子把模型流量推到 `js-*`（见 # 7 / # 71 / # 72）。

### 模型侧使用合同（必须写进 `js-*` 工具描述；内置工具钩子文案须同向鼓吹）

1. **优先使用当前可见的 `js-ROLE`**，不要继续用 `read` / `edit` / `write` / `glob` / `grep` / `patch` 完成可被 JS SDK 表达的工作；
2. **只要需要就并行调用多个工具**：并行读取、并行编辑、同文件、异文件全部绝对安全（Host 侧合同见 # 55.1）；
3. **强烈鼓励对同文件与异文件提交大量并行编辑**，不要因为担心冲突而串行等待；
4. **强烈鼓励模型写很复杂的 JavaScript 脚本**处理工作：一次 program 内批量读、批量变换、批量 rewrite/write，Host 保证整个 program 是单个事务。

---

# 1. Motivation

传统文件工具：

```text
read
edit
write
glob
grep
```

表面上是五种操作，实际上可以归约为少量基础能力：

```text
读取文件
枚举路径
计算字符串
提交新文件内容
返回计算结果
```

例如：

```text
read
= file() + return

glob
= glob() + return

grep
= glob() + file() + RegExp + return

edit
= file() + anchors + text() + rewrite()

write
= write()
```

更复杂的任务：

```text
glob
→ grep
→ read
→ 修改多个文件
→ 返回修改摘要
```

也没有理由被强制拆成多次模型 round trip。

一个 JavaScript program 可以自然地一次完成。

因此本 Change 不再把 JavaScript 看作“高级 edit 的实现语言”。

JavaScript filesystem program 本身成为 primitive。

### 1.1 为什么需要 anchors

传统实现允许模型自己写：

```js
source.indexOf(...)
source.slice(...)
source.replace(...)
```

anchors 并没有增加 Node.js 的理论表达力。

它增加的是可靠性：

1. **Intent 可验证**：Host 可以在运行 JS 前确认所有定位依据确实存在；
2. **定位与变换解耦**：模型不用一边处理 `indexOf` 的 corner case，一边构造输出；
3. **Fail closed**：anchor 找不到 → 根本不运行程序，而不是 JS 算出一个奇怪位置继续写；
4. **对重复内容天然友好**：ordered matching 是模型最便宜的 disambiguation mechanism；
5. **描述容易教**：模型只需要学会一个惯用法：

```js
text("^", "a") + replacement + text("b", "$")
```

6. **未修改原文被精确复制**：`text(a,b)` 保留原始 bytes，而不是让模型重新打一遍未改动内容 → 减少意外格式变化。

### 1.2 表达力界限

不计 JS 本身的计算能力，anchor-and-splice 小语言已经能表示：

> **任意有限个原文 substring 与任意新字符串的有限拼接。**

绝大多数 source edit 本质上都属于这一类。

---

# 2. Design Principle: Capability-Projected Tool Surface

定义：

> **Capability-Projected Tool Surface** 是一种工具生成模式：模型可见的 programmable SDK 不按角色手写，而是从当前 execution attempt 的 immutable capability set 机械投影。

数据流：

```text
Canonical Authority
       │
       ▼
AttemptExecutionProfile
       │
       ▼
ToolCapabilitySet
       │
       ▼
JsToolGenerator
       │
       ├── primary js-* tool
       ├── exact JsProgram base class
       ├── exact description
       ├── legal examples
       ├── runtime capability bindings
       └── BuiltinToolDescriptionHook payloads
              │
              ▼
       inject into still-visible builtin
       read/edit/write/glob/grep/patch descriptions
```

`js-*` 的全部结果必须来自同一 registry。

禁止：

```text
权限表一份
js tool description 一份
base class 一份
runtime switch 一份
builtin 推荐文案再一份手写
```

这种设计最终一定漂移。

Builtin 文件系统工具（含 `patch`，凡存在者）的 **schema / 执行实现** 继续由既有 ToolRegistry 拥有；Generator 只通过钩子改写它们的 **description 推荐层**，不得改写它们的 schema 或执行入口。

### 2.1 最强不变量：4 层投影（`js-*`）

对于当前 Attempt 的每一个 JS filesystem capability，以下四层必须完全同构：

```text
没有该 capability
→ 1. 生成的基类中没有对应方法
→ 2. js-* 工具描述中不出现该方法
→ 3. canonical examples 中不出现该方法
→ 4. 即使伪造底层调用，runtime gate 仍 fail closed
```

模型对 `js-*` 的认知负担因此为零：不需要记“文档里有但你不能用”。

内置 `read` / `edit` / `write` / `glob` / `grep` / `patch` 是否可见，仍由既有 ToolPermission / Attempt profile 决定；它们**不是** JS capability projection 的第五层，也**不是** alias。

---

# 3. 不新增第二份 Authority

`JsToolGenerator` 不接收 Role 后自己重新计算权限。

禁止：

```fsharp
generateJsSurface(role)
```

然后内部维护另一张 role matrix。

唯一输入必须是已经建立的当前 Attempt profile：

```fsharp
generateJsSurface(attemptExecutionProfile)
```

并只读取：

```text
AttemptExecutionProfile.ToolCapabilitySet
```

Role 和 RequestKind 已经在 profile 构造阶段完成解释。

生成器必须保持纯函数姿态。它不能：

```text
读 Session mutable
查当前注册了哪些工具
猜 Agent name
观察 provider schema 后反推权限
根据 builtin 工具名再推权限
根据钩子推荐文案反推权限
```

输入已经是 canonical profile，输出只是 projection。

这对 **按 RequestKind 分叉的同一 CanonicalRole** 尤其重要（原文 Student 示例已是 Universal G3 rebase debt，勿复活生产）：

```text
# historical illustration only — StudentLearn / StudentCompile deleted in G3
RequestKindA
RequestKindB
```

虽然 CanonicalRole 相同，但能力可以完全不同。

Generator 不能再次发明该 Role 的内部状态判断；只读 AttemptExecutionProfile。

---

# 4. 第一版 Capability Family

第一版只统一 filesystem programming family。

Primitive capabilities：

```text
Read
Glob
Edit
Write
```

其中：

```text
Read
```

允许读取已知 UTF-8 文件。

```text
Glob
```

允许枚举当前 path boundary 内的匹配路径。

```text
Edit
```

允许修改已经存在的 UTF-8 文件。

```text
Write
```

允许创建新的 UTF-8 文件。

第一版明确冻结：

> `rewrite()` 只修改存在的文件。

> `write()` 只创建不存在的文件。

因此：

```text
Edit ≠ Write
```

它们是真正可以分别执行的 capability。

如果目标已存在：

```js
this.write(path, text)
```

失败：

```text
FILE_ALREADY_EXISTS
```

如果目标不存在：

```js
this.rewrite(path, text)
```

失败：

```text
FILE_NOT_FOUND
```

这样不会出现：

```text
Agent 没有 Edit
但可以通过 Write 覆盖旧文件
```

的权限旁路。

---

# 5. Grep 不再是 Primitive Capability

在一个拥有：

```text
Read + Glob + JavaScript RegExp
```

的运行时中，grep 已经天然可表达。

因此：

```text
Grep
```

不应继续被理解成一种独立执行能力。

它是：

> **JS SDK 内的 derived affordance**

也就是：在 `js-*` 程序里，grep 可由 `glob() + file() + RegExp` 自然表达。

**内置 `grep` 工具本身继续独立存在**（原 schema / 原实现），与 derived affordance 无关。

逻辑：

```text
js-* description 可教 grep-style example
⇔ Read && Glob

builtin grep tool visible
⇔ 既有 ToolPermission.Grep（或等价既有规则）
```

第一版若仍保留 `ToolPermission.Grep`，它只管辖内置 `grep` 工具可见性；**不**成为 `js-*` runtime authority。

`js-*` 最终 runtime authority 不依赖 `Grep` bit。

---

# 6. Primary Tool

如果当前 Attempt 的 filesystem primitive capability 集为空：

```text
{}
```

则：

```text
不生成任何 js-* 工具
```

如果非空：

```text
primaryName = "js-" + canonicalRoleName
```

例如：

```text
js-coder
js-inspector
js-reviewer
js-devops
js-browser
js-meditator
# js-student / js-teacher — Universal G3 rebase debt only；勿复活生产
```

Tier 永远不进入工具名。

因此：

```text
fast-coder
deep-coder
```

都使用：

```text
js-coder
```

并必须得到 byte-equivalent：

```text
schema
base class
description
examples
runtime bindings
BuiltinToolDescriptionHook payloads
```

Tier 只改变模型绑定。

---

# 7. Builtin Tools Coexist — Not Replaced

传统名字继续全部保留，并且**继续是真正的内置工具**，不是 `js-*` 的 alias。

例如 Coder provider surface：

```text
js-coder          ← 新增 capability-projected JS SDK
read              ← 原内置工具（deprecated + 钩子强推 js-coder）
glob              ← 原内置工具（deprecated + 钩子强推 js-coder）
grep              ← 原内置工具（deprecated + 钩子强推 js-coder）
edit              ← 原内置工具（deprecated + 钩子强推 js-coder）
write             ← 原内置工具（deprecated + 钩子强推 js-coder）
patch             ← 原内置工具（若可见；deprecated + 钩子强推 js-coder）
```

其中（凡 provider 仍可见者）：

```text
read
glob
grep
edit
write
patch
```

继续满足：

```text
原 schema 不变
原 Host 实现不变
原执行入口不变
原权限规则不变
仍可被模型直接调用并成功执行
```

它们与 `js-coder` **不是同一工具的不同名字**。

禁止：

```text
把 builtin filesystem 工具（含 patch）改成 { program: string }
把它们的执行入口接到 JsProgram
把它们从 provider surface 删除
用 alias 伪装成“同一个工具”
```

本 Change 的迁移策略是：

> **共存 + deprecated + Tool Definition 钩子极力推荐 `js-ROLE`。**

---

# 8. Tool Definition 钩子：在内置工具描述中强力鼓吹新工具

引流不靠替换实现，靠 **Tool Definition 钩子**。

定义：

> **BuiltinToolDescriptionHook** 在组装最终 provider-visible tool specs 时运行：若当前 Attempt 已生成 `js-ROLE`，则对仍可见的内置文件系统工具（`read` / `edit` / `write` / `glob` / `grep` / `patch`）description 注入 deprecated 声明与极力推荐文案；**绝不修改**这些工具的 name / schema / executor。

数据流：

```text
AttemptExecutionProfile
       │
       ▼
JsToolGenerator.generate(profile)
       │
       ├── JsToolSpec(js-ROLE, schema={program}, full description, ...)
       └── BuiltinRecommendationPayload(js-ROLE, targets=[...])
              │
              ▼
ToolSurfaceAssembler
       │
       ├── emit js-ROLE as-is
       └── for each visible builtin in {read,edit,write,glob,grep,patch}:
             description := Hook(originalDescription, payload)
             schema/executor untouched
```

### 8.1 钩子必须做到的事

对每个被命中的内置工具 description：

1. **明确标为 deprecated**（保留可执行，不隐藏工具）；
2. **极力、强力、反复鼓吹**改用当前可见的 `js-ROLE`；
3. 说明 `js-ROLE` 能一次完成批量读/搜/改/写，且是单个事务；
4. 说明并行调用 `js-ROLE` 绝对安全；
5. **不得**把内置工具 schema 字段删掉或改成 `program`；
6. **不得**声称“本工具只是 alias / 已无独立实现”。

### 8.2 推荐完整钩子文案（canonical）

钩子应把以下块注入到内置工具 description 的**最前面**（允许按工具名做极小措辞替换，但语气强度不得削弱）：

```text
DEPRECATED. Prefer js-coder for all filesystem work.

Do not use this legacy tool for new work when js-coder is available.
js-coder is the capability-projected JavaScript filesystem SDK for this
request. It can read, search, transform, rewrite, and create files in one
transactional program — including large parallel batches.

Strongly recommended:
1. Call js-coder instead of read/edit/write/glob/grep/patch whenever possible.
2. Write complex JavaScript in one js-coder program rather than many legacy RPCs.
3. Parallel js-coder calls are absolutely safe for same-file and cross-file edits.

This legacy tool remains executable only for compatibility with old habits.
Its schema and semantics are unchanged. New work should target js-coder.
```

Inspector 等角色把 `js-coder` 换成对应 `js-ROLE`：

```text
DEPRECATED. Prefer js-inspector for all filesystem work.
...
```

### 8.3 钩子不是 security scope

模型即使忽略推荐、继续调用：

```text
edit
```

或：

```text
patch
```

Host 仍按**原内置工具语义**执行。

模型调用：

```text
js-coder
```

才进入 JS SDK / transaction / runtime gate。

因此：

```text
builtin tool name
```

永远不决定 `js-*` 执行权限。

决定 `js-*` 权限的是：

```text
当前 Attempt 生成出来的 capability projection
```

---

# 9. Hook Visibility

机械规则：

```text
js-ROLE 已生成且可见
→ 对当前 surface 中仍可见的 builtin ∈ {read, edit, write, glob, grep, patch}
  全部注入 BuiltinToolDescriptionHook(js-ROLE)

js-ROLE 未生成
→ 不注入任何“Prefer js-*”钩子
→ 内置工具保持原 description（若它们本身可见）
```

并有硬不变量：

```text
钩子文案提到 js-ROLE
→ 同一 provider request 必须同时暴露该 js-ROLE
```

不能出现：

```text
read/edit/... description 都说 Prefer js-inspector
但 provider 没有暴露 js-inspector
```

### 9.1 内置工具可见性 ≠ JS capability advertisement

模型判断“当前 JS SDK 能做什么”，只看：

```text
js-ROLE 生成的基类方法是否存在
```

不要用内置工具是否可见来推断 JS capability。

例如某些 profile 可能仍暴露只读内置工具，但若 `js-*` 因 filesystem primitive set 为空而未生成，则**不得**注入 Prefer 钩子。

反过来：Coder 同时看到 `edit` / `patch` 与 `js-coder.rewrite()` 时，钩子必须把流量导向 `js-coder`；`edit` / `patch` 仍可执行，但 description 把它们标成 deprecated。

`patch` 是**可选**钩子目标：仅当它确实出现在当前 provider surface 时才注入 Prefer 文案；本 Change **不**要求新增 `patch` 实现，也**不**把缺席的 `patch` 凭空暴露出来。

### 9.2 钩子文案由 Generator 拥有

禁止在 `read` / `edit` / `write` / `glob` / `grep` / `patch` 各自定义里手写互不相同的“请改用 js-*”长文。

唯一 owner：

```text
JsToolGenerator / BuiltinToolDescriptionHook renderer
```

输入：

```text
primaryName = js-ROLE
visibleBuiltins = intersection(provider builtins, {read,edit,write,glob,grep,patch})
```

输出：byte-stable recommendation block。

---

# 10. Schema

**只有**生成的 `js-*` 主工具使用：

```ts
type JsToolInput = {
  program: string
}
```

内置文件系统工具**继续使用各自原有 schema**，例如既有：

```text
read(path, ...)
edit(path, oldString, newString, ...)
write(path, content, ...)
glob(pattern, ...)
grep(pattern, path?, ...)
patch(...)          # 若该 builtin 存在于当前正式工具面
```

（具体字段以现有正式 what/how 为准；本 Change 不改写它们。`patch` 只要出现在 provider surface，就纳入钩子目标，不要求本 Change 新建其实现。）

禁止：

```text
把 builtin schema 改成 { program: string }
为 builtin 增加隐藏 dual schema
让同一个工具名同时暴露两套 schema
```

`path` / `oldString` / `newString` / `pattern` / `query` 等顶层 RPC 参数继续属于内置工具。

这些内容在 `js-*` 里由 program 通过生成 SDK 表达。

---

# 11. Model Contract

工具描述固定要求：

```js
class Js extends JsProgram {
  async run() {
    // implementation
  }
}
```

必须恰好定义一个：

```text
class Js extends JsProgram
```

并实现：

```text
async run()
```

Host 执行这个派生类。

模型不得重定义：

```text
JsProgram
```

也不得自造 Host capability。

### 11.1 并行调用绝对安全

模型**只要需要就并行调用多个工具**：并行读取、并行编辑、同文件、异文件全部绝对安全（Host 侧合同见 # 55.1）。

强烈鼓励对**同文件与异文件提交大量并行编辑**。

绝对安全的合同基础：

```text
同一 assistant 消息中的工具调用由 Host 按确定性顺序逐个执行
→ 每个调用是独立 transaction
→ 后一个调用基于前一个调用提交后的 committed state 重新 snapshot
→ 同文件多轮编辑 = 顺序叠加，无 lost update
→ 异文件并行编辑 = 各自独立 all-or-nothing
```

### 11.2 强烈鼓励复杂 JavaScript 脚本

模型应尽量写**很复杂的 JS 脚本**一次完成工作：

```text
一次 program
→ glob 大量路径
→ 循环批量读取
→ 复杂字符串/正则/JSON 变换
→ 批量 rewrite/write
→ return 结构化摘要
```

Host 保证整个 program 属于**单个事务**：全部成功或全部不生效。不需要模型把大任务拆成多次简单调用，也不需要为规避文件冲突而串行化（见 # 55.1）。

---

# 12. Generated Base Class Is the Documentation

生成工具 description 的核心不是 prose。

而是直接展示当前 Attempt 实际拥有的基类。

例如 read-only Agent：

```js
class JsProgram {
  async file(path, matches = []) {
    // canonical implementation shown below
  }

  async glob(pattern) {
    // Host capability
  }

  async run() {
    throw new Error("Js.run() must be implemented.");
  }
}
```

没有：

```js
rewrite()
write()
```

Coder：

```js
class JsProgram {
  async file(path, matches = []) {
    // canonical implementation
  }

  async glob(pattern) {
    // Host capability
  }

  rewrite(path, newText) {
    // Host capability
  }

  write(path, newText) {
    // Host capability
  }

  async run() {
    throw new Error("Js.run() must be implemented.");
  }
}
```

模型不需要读：

```text
You cannot write.
```

因为没有方法就是最清楚的说明。

---

# 13. `file()` — Read + Anchor Algebra

只要当前 Attempt 有 `Read`，基类就生成：

```js
async file(path, matches = [])
```

`file()`：

1. 读取本事务 immutable snapshot 中的 UTF-8 文件；
2. 可选解析 ordered anchors；
3. 返回 immutable `FileView`；
4. FileView 提供：

```js
text(from = "^", to = "$")
```

---

# 14. Anchor Declaration

`matches`：

```ts
Array<
  [
    beginAnchor: string,
    endAnchor: string,
    pattern: string | RegExp
  ]
>
```

例如：

```js
const f = await this.file("src/foo.js", [
  ["functionBegin", "afterFunction", "function foo() {"],
  ["returnBegin", "returnEnd", /return\s+oldValue\s*;/],
]);
```

Anchor 是位置名字。

不是文件中的字符串。

---

# 15. Built-in Anchors

每个 FileView 固定存在：

```text
^ = 文件开头
$ = 文件结尾
```

模型不能声明：

```text
^
$
```

作为自定义 anchor 名。

---

# 16. Ordered Anchor Matching

Canonical algorithm 必须直接显示在工具 description 中。

规范等价实现：

```js
async file(path, matches = []) {
  const source = await HOST_READ_IMMUTABLE_UTF8_SNAPSHOT(path);

  const anchors = new Map([
    ["^", 0],
    ["$", source.length],
  ]);

  let cursor = 0;

  const findNext = pattern => {
    if (typeof pattern === "string") {
      if (pattern.length === 0)
        throw new Error("String anchor patterns must be non-empty.");

      const start = source.indexOf(pattern, cursor);

      if (start < 0)
        return null;

      return {
        start,
        end: start + pattern.length,
      };
    }

    if (pattern instanceof RegExp) {
      // Anchor matching defines its own forward-search semantics.
      // Caller g/y state and lastIndex are ignored.
      const flags =
        [...new Set(
          pattern.flags.replace(/[gy]/g, "") + "g"
        )].join("");

      const regexp = new RegExp(pattern.source, flags);
      regexp.lastIndex = cursor;

      const match = regexp.exec(source);

      if (!match)
        return null;

      return {
        start: match.index,
        end: match.index + match[0].length,
      };
    }

    throw new Error(
      "Anchor pattern must be a string or RegExp."
    );
  };

  for (const [begin, end, pattern] of matches) {
    if (!begin || !end)
      throw new Error("Anchor names must be non-empty.");

    if (
      begin === "^" || begin === "$" ||
      end === "^" || end === "$"
    )
      throw new Error("^ and $ are reserved anchors.");

    if (begin === end)
      throw new Error(
        "Begin and end anchor names must differ."
      );

    if (anchors.has(begin) || anchors.has(end))
      throw new Error("Anchor names must be unique.");

    const match = findNext(pattern);

    if (!match)
      throw new Error(
        "Anchor pattern was not found in declaration order."
      );

    anchors.set(begin, match.start);
    anchors.set(end, match.end);

    cursor = match.end;
  }

  const offset = name => {
    if (!anchors.has(name))
      throw new Error(`Unknown anchor: ${name}`);

    return anchors.get(name);
  };

  return Object.freeze({
    text(from = "^", to = "$") {
      const start = offset(from);
      const end = offset(to);

      if (start > end)
        throw new Error(
          `Invalid slice: ${from} is after ${to}`
        );

      return source.slice(start, end);
    },
  });
}
```

实际 Host 实现不要求字节逐字等于这段 JavaScript。

但可观察语义必须完全等价。

### 16.1 Anchor 声明校验（5 类拒绝）

在读取完原文件、但**运行任何模型 JS 之前**，必须完成全部 anchor 声明校验。至少拒绝：

1. **空名字**：`begin` 或 `end` 为空字符串；
2. **保留名字**：`begin` 或 `end` 为 `^` / `$`；
3. **重复名字**：所有 begin/end 名称共享同一个 namespace，重复即拒绝；
4. **begin == end**：同一声明中两个名字相同；
5. **空字符串 pattern**：字符串 pattern 必须非空；空字符串没有稳定的 source-identification 含义。

注意：正则 pattern 没有“空”概念，零宽正则 `/(?=...)/` 是合法匹配（见 # 19）。

---

# 17. Exact String Anchors

字符串：

```js
["a", "b", "oldString"]
```

从当前 cursor 开始寻找第一个 exact occurrence。

字符串 pattern：

```text
必须非空
```

内容无需在全文件唯一。

---

# 18. RegExp Anchors

正则：

```js
["a", "b", /function\s+foo\s*\([^)]*\)\s*\{/]
```

从当前 cursor 开始寻找下一个 match。

调用方提供的：

```text
g
y
lastIndex
```

不参与 anchor 状态。

Anchor matcher 始终建立自己的 forward search。

其它合法 flags 保留。

---

# 19. Zero-width RegExp

允许：

```js
/(?=function foo)/
```

这样的零宽 match。

因此：

```text
begin offset == end offset
```

是合法的。

这使模型可以直接命名一个 insertion position。

注意：

```text
begin anchor name
```

与：

```text
end anchor name
```

仍必须不同。

只是它们对应的 offset 可以相等。

---

# 20. Ordered Semantics

每次 match 后：

```text
cursor = match.end
```

所以：

```text
match₁
match₂
match₃
```

按声明顺序解析。

普通非零宽 match 不重叠。

重复内容无需全局唯一。

例如：

```text
function first() {
  return "old";
}

function second() {
  return "old";
}
```

声明：

```js
[
  ["second", "afterSecond", "function second() {"],
  ["target", "afterTarget", 'return "old";'],
]
```

第二个 pattern 只从 `function second()` 后继续搜索。

自然定位第二个 return。

---

# 21. `text()`

```js
file.text(from, to)
```

返回：

```text
immutable original snapshot
```

中两个 resolved anchors 之间的精确 JavaScript substring。

默认：

```js
file.text()
```

等价：

```js
file.text("^", "$")
```

要求：

```text
from 存在
to 存在
offset(from) <= offset(to)
```

反向 slice 失败。

重新排列由调用方重新排列多个 `text()` 结果完成。

---

# 22. FileView 永远 Immutable

例如：

```js
const f = await this.file("a.js");
const original = f.text();

this.rewrite("a.js", "new");

const stillOriginal = f.text();
```

必须：

```text
original == stillOriginal
```

`rewrite()` 不修改 FileView。

Js program 中需要多阶段 transformation 时：

```js
let next = f.text();

next = transform1(next);
next = transform2(next);

this.rewrite("a.js", next);
```

不要引入 staged filesystem 的第二读取语义。

---

# 23. `glob()`

如果当前 Attempt 有 `Glob`，基类生成：

```js
async glob(pattern)
```

返回：

```text
当前 path boundary 内匹配的 canonical paths
```

结果必须：

```text
deterministic
stable sorted
```

调用受现有 Host path boundary 约束。

`glob()` 不赋予对结果文件的 Read 权。

有 Glob 无 Read 的 hypothetical profile 可以枚举路径，但不能：

```js
this.file(...)
```

---

# 24. `rewrite()`

如果当前 Attempt 有 `Edit`：

```js
rewrite(path, newText)
```

出现于基类。

要求：

```text
target 在 transaction snapshot 中必须存在
target 必须为合法 UTF-8 文本文件
newText 必须为 string
```

调用不会立即修改文件。

只增加：

```text
StagedRewrite
```

到当前 transaction WriteSet。

模型**不要求**先 `file(path)` 再 `rewrite(path)`。

Host 在首次 staging 时自动为该路径建立 preimage snapshot（见 # 57）。

因此以下程序合法：

```js
this.rewrite("version.txt", "2\n");
```

---

# 25. `write()`

如果当前 Attempt 有 `Write`：

```js
write(path, newText)
```

出现于基类。

第一版 canonical semantics：

```text
target 在 transaction snapshot 中必须不存在
newText 必须为 string
```

它只负责**创建新文件**。

目标已存在：

```text
FILE_ALREADY_EXISTS
```

需要修改存在文件必须使用：

```js
rewrite()
```

这使 `Write` 和 `Edit` 成为真实不同的 capability。

---

# 26. Same Path Only Once

一个 transaction 中，同一 canonical path 只能成为一次 mutation target。

禁止：

```js
this.rewrite("a.js", first);
this.rewrite("a.js", second);
```

禁止：

```js
this.write("a.js", first);
this.rewrite("a.js", second);
```

统一：

```text
DUPLICATE_MUTATION_TARGET
```

模型要做多阶段修改：

```js
let text = ...;
text = phase1(text);
text = phase2(text);

this.rewrite(path, text);
```

---

# 27. `run()` Return Value

`run()` 的返回值作为本次 tool result 的业务值返回给 LLM。

因此纯查询：

```js
class Js extends JsProgram {
  async run() {
    const f = await this.file("README.md");
    return f.text();
  }
}
```

就是 read。

允许返回严格 JSON-compatible value：

```text
null
boolean
finite number
string
array
plain object
```

递归适用。

拒绝：

```text
undefined
BigInt
NaN
Infinity
function
symbol
cyclic object
non-plain exotic object
```

错误：

```text
INVALID_RETURN_VALUE
```

---

# 28. JS Return ≠ Student/Teacher `return` Tool

> **Universal G3 rebase debt 对照。** Student / Teacher 专用 `return` 已随 G3 删除；本节只冻结：`Js.run()` return 从不承担任何已删角色终态语义。勿复活 Student/Teacher 生产。

必须明确区分：

```text
Js.run() return
```

只是当前 JS tool call 的 observation/result。

它不会：

```text
结束已删角色会话
回答 teacher
构造 RunCompletion
```

历史 Student / Teacher 专用：

```text
return
```

工具曾保持完全独立的 workflow 语义；现已不在生产面。

两者只有英语单词相同，没有领域关系。

---

# 29. Result Validation Happens Before Commit

执行顺序：

```text
run()
→ obtain return value
→ validate return value
→ construct canonical bounded tool-result representation
→ validate complete transaction
→ commit transaction
→ only after successful commit expose tool success/result
```

因此：

```js
this.rewrite(...);

return 1n;
```

不能：

```text
先修改文件
再因为 BigInt 无法返回而报错
```

而必须：

```text
INVALID_RETURN_VALUE
→ zero committed writes
```

---

# 30. Synthetic TOML

最终 LLM-visible result 继续进入万象术统一 Synthetic TOML renderer。

Js runtime 不自己拼：

```text
status=...
```

也不直接 dump：

```text
JSON.stringify(result)
```

Provider/tool 原生 binding 仍是结构化 schema。

运行后供 LLM 阅读的 tool result 继续服从现有 Synthetic TOML 与 tool-result bound。

---

# 31. Canonical Examples — Read

当 capability 包含 Read 时，可加入：

```js
class Js extends JsProgram {
  async run() {
    const file = await this.file("README.md");
    return file.text();
  }
}
```

---

# 32. Canonical Examples — Glob

当 capability 包含 Glob：

```js
class Js extends JsProgram {
  async run() {
    return await this.glob("src/**/*.fs");
  }
}
```

---

# 33. Canonical Examples — Grep

仅当：

```text
Read + Glob
```

都存在时加入：

```js
class Js extends JsProgram {
  async run() {
    const paths = await this.glob("src/**/*.js");
    const hits = [];

    for (const path of paths) {
      const file = await this.file(path);
      const text = file.text();

      for (const match of text.matchAll(/TODO:.+/g)) {
        hits.push({
          path,
          index: match.index,
          text: match[0],
        });
      }
    }

    return hits;
  }
}
```

没有独立 grep primitive。

---

# 34. Canonical Examples — Replace

仅当：

```text
Read + Edit
```

存在：

```js
class Js extends JsProgram {
  async run() {
    const file = await this.file("src/foo.js", [
      ["begin", "end", "oldString"],
    ]);

    this.rewrite(
      "src/foo.js",
      file.text("^", "begin")
        + "newString"
        + file.text("end", "$")
    );
  }
}
```

---

# 35. Canonical Examples — Regex Replace

```js
class Js extends JsProgram {
  async run() {
    const file = await this.file("src/foo.js", [
      [
        "begin",
        "end",
        /const\s+version\s*=\s*"[^"]*";/
      ],
    ]);

    this.rewrite(
      "src/foo.js",
      file.text("^", "begin")
        + 'const version = "2.0";'
        + file.text("end", "$")
    );
  }
}
```

---

# 36. Canonical Examples — Insert

```js
class Js extends JsProgram {
  async run() {
    const file = await this.file("src/foo.js", [
      ["at", "afterAt", /(?=function foo)/],
    ]);

    this.rewrite(
      "src/foo.js",
      file.text("^", "at")
        + "// inserted\n"
        + file.text("at", "$")
    );
  }
}
```

---

# 37. Canonical Examples — Delete

```js
class Js extends JsProgram {
  async run() {
    const file = await this.file("src/foo.js", [
      [
        "begin",
        "end",
        "const obsolete = true;\n"
      ],
    ]);

    this.rewrite(
      "src/foo.js",
      file.text("^", "begin")
        + file.text("end", "$")
    );
  }
}
```

---

# 38. Canonical Examples — Move

```js
class Js extends JsProgram {
  async run() {
    const file = await this.file("src/foo.js", [
      ["a", "b", "first block"],
      ["c", "d", "second block"],
    ]);

    this.rewrite(
      "src/foo.js",
      file.text("^", "a")
        + file.text("c", "d")
        + file.text("b", "c")
        + file.text("a", "b")
        + file.text("d", "$")
    );
  }
}
```

没有：

```text
move()
```

移动只是 source slices 的重新排列。

---

# 39. Canonical Examples — Copy

```js
class Js extends JsProgram {
  async run() {
    const file = await this.file("src/foo.js", [
      ["a", "b", "const item = createItem();"],
    ]);

    this.rewrite(
      "src/foo.js",
      file.text("^", "a")
        + file.text("a", "b")
        + "\n"
        + file.text("a", "b")
        + file.text("b", "$")
    );
  }
}
```

---

# 40. Canonical Examples — Multi-region Edit

```js
class Js extends JsProgram {
  async run() {
    const file = await this.file("src/foo.js", [
      ["a", "b", "oldA"],
      ["c", "d", "oldB"],
    ]);

    this.rewrite(
      "src/foo.js",
      file.text("^", "a")
        + "newA"
        + file.text("b", "c")
        + "newB"
        + file.text("d", "$")
    );
  }
}
```

---

# 41. Canonical Examples — Write

仅当有 Write：

```js
class Js extends JsProgram {
  async run() {
    this.write(
      "generated/version.txt",
      "1.2.3\n"
    );

    return {
      created: "generated/version.txt",
    };
  }
}
```

---

# 42. Canonical Examples — Multi-file Transaction

有 Read + Edit：

```js
class Js extends JsProgram {
  async run() {
    const implementation = await this.file("src/foo.js", [
      ["a", "b", "oldValue"],
    ]);

    const test = await this.file("tests/foo.test.js", [
      ["a", "b", '"oldValue"'],
    ]);

    this.rewrite(
      "src/foo.js",
      implementation.text("^", "a")
        + "newValue"
        + implementation.text("b", "$")
    );

    this.rewrite(
      "tests/foo.test.js",
      test.text("^", "a")
        + '"newValue"'
        + test.text("b", "$")
    );

    return {
      changed: [
        "src/foo.js",
        "tests/foo.test.js",
      ],
    };
  }
}
```

两个 rewrite 属于同一个 transaction。

---

# 43. Canonical Examples — Search and Rewrite

有：

```text
Read + Glob + Edit
```

时：

```js
class Js extends JsProgram {
  async run() {
    const paths = await this.glob("src/**/*.js");
    const changed = [];

    for (const path of paths) {
      const file = await this.file(path);
      const oldText = file.text();

      if (!/\boldApi\b/.test(oldText))
        continue;

      this.rewrite(
        path,
        oldText.replaceAll("oldApi", "newApi")
      );

      changed.push(path);
    }

    return { changed };
  }
}
```

一次调用同时完成：

```text
glob
grep
read
edit-many
summary
```

---

# 44. Example Projection

每个 canonical example 声明 requirements：

```fsharp
type JsExample =
    { Requires: Set<JsPrimitiveCapability>
      Source: string }
```

Generator 只有在：

```text
example.Requires ⊆ current capabilities
```

时才把它放进 description。

所以：

```text
js-inspector
```

永远看不到 write/edit examples。

```text
js-coder
```

自动看到。

---

# 45. Capability Fragment Registry

每种 capability 只定义一个 fragment。

概念：

```fsharp
type JsCapabilityFragment =
    { Capability: JsPrimitiveCapability
      Members: JsMemberSpec list
      RuntimeBindings: JsRuntimeBinding list
      BuiltinRecommendationTargets: BuiltinToolName list
      Description: DescriptionFragment list
      Examples: JsExample list }
```

例如 Read fragment：

```text
Capability
= Read

Members
= file()

RuntimeBindings
= immutable UTF-8 snapshot reader

BuiltinRecommendationTargets
= read

Description
= ordered anchors + FileView semantics

Examples
= read / anchor examples
```

Edit fragment：

```text
Capability
= Edit

Members
= rewrite()

RuntimeBindings
= stage replacement of existing file

BuiltinRecommendationTargets
= edit
  patch   # if visible; same Edit-family recommendation
```

Write fragment：

```text
Capability
= Write

Members
= write()

RuntimeBindings
= stage creation of absent file

BuiltinRecommendationTargets
= write
```

Glob fragment：

```text
Capability
= Glob

Members
= glob()

RuntimeBindings
= bounded path matcher

BuiltinRecommendationTargets
= glob
```

Grep 不拥有 runtime fragment。

它是 Read+Glob 的 derived example；若 Read+Glob 都在，钩子对仍可见的内置 `grep` 一并注入 Prefer `js-ROLE`。

---

# 46. No Handwritten Role Variants

禁止维护：

```text
CoderJsProgramTemplate
InspectorJsProgramTemplate
ReviewerJsProgramTemplate
DevOpsJsProgramTemplate
...
```

唯一模板来源是：

```text
capability fragments
```

生成：

```text
capability set
→ select fragments
→ canonical sort
→ render class
```

固定 member 顺序：

```text
file
glob
rewrite
write
run
```

没有 capability 的 member 直接不存在。

---

# 47. Generated Description

Description：

```text
canonical header
+
generated base class
+
capability-specific rules
+
filtered canonical examples
+
canonical footer
```

固定 header：

```text
This is the programmable filesystem tool for the current agent.

The base class below is generated from the capabilities actually available in
this request. If a method is present, you may use it. If a method is absent,
that capability is not available.

Define exactly one class named Js that extends JsProgram and implement
async run().
```

固定 footer：

```text
Use the generated API directly. Do not reimplement Host filesystem,
permission, anchor, snapshot, or transaction logic.

Anchors locate. JavaScript transforms. Mutations are staged and committed by
the Host as one transaction.
```

### 47.1 模型推荐 workflow

工具描述应教授以下使用顺序：

1. 声明定位所需的最小 anchor 集合；
2. 让 Host 解析 begin/end 位置；
3. 用 `text(...)` slices 与新内容构造**完整的新文件**；
4. 只有 anchor-and-splice 形式真正不便时，才使用通用 JS 字符串处理（如 `indexOf` / `replaceAll`）。

简单替换优先写：

```js
f.text("^", "begin") + "newString" + f.text("end", "$")
```

而不是手动计算 offset 或重新实现匹配逻辑。

---

# 48. Generated Role Surfaces

根据当前正式角色矩阵，预期第一版主要形态：

## Coder

filesystem capabilities：

```text
Read
Glob
Edit
Write
```

生成：

```text
js-coder
```

同时保留既有内置工具（若 profile 原本可见）：

```text
read
glob
grep
edit
write
patch
```

并由 BuiltinToolDescriptionHook 在它们的 description 中 **deprecated + 极力推荐 js-coder**。

基类：

```text
file
glob
rewrite
write
run
```

其它：

```text
inspector
mv
rm
```

仍是独立工具。

---

## Inspector

filesystem capabilities：

```text
Read
Glob
```

生成：

```text
js-inspector
```

既有只读内置工具继续保留：

```text
read
glob
grep
```

钩子：Prefer `js-inspector`。

基类：

```text
file
glob
run
```

`executor` 继续独立。

---

## DevOps

filesystem capabilities：

```text
Read
Glob
```

生成：

```text
js-devops
```

既有只读内置工具继续保留：

```text
read
glob
grep
```

钩子：Prefer `js-devops`。

没有：

```text
rewrite
write
```

因此也不对 `edit` / `write` 注入“可用”暗示；若这些内置工具本就不可见，钩子不会凭空制造它们。

PTY、executor、coder、join/list 等继续独立。

DevOps 仍不能直接修改文件。

---

## Browser

生成其 filesystem capability 对应：

```text
js-browser
```

既有只读内置工具继续保留；钩子 Prefer `js-browser`。

网络工具仍独立。

---

## Meditator

生成：

```text
js-meditator
```

既有只读内置工具继续保留；钩子 Prefer `js-meditator`。

其它委派能力独立。

---

## Reviewer

生成：

```text
js-reviewer
```

既有只读内置工具继续保留；钩子 Prefer `js-reviewer`。

基类是只读 SDK。

`verdict` 独立。

---

## StudentLearn

> **Universal G3 rebase debt。** 不复活 Student 生产；本节仅保留原文投影示例，激活时删除/忽略。

filesystem primitive capability 为空。

因此：

```text
不生成 js-student
不注入任何 Prefer js-* 钩子
```

保持：

```text
teacher
```

（StudentLearn 本就不暴露文件系统内置工具时，继续如此。）

---

## StudentCompile

> **Universal G3 rebase debt。** 不复活 Student 生产；本节仅保留原文投影示例，激活时删除/忽略。不得实现 `js-student` 生产路径。

filesystem capabilities：

```text
Read
Glob
Edit
Write
```

生成：

```text
js-student
```

既有文件系统内置工具若在该 profile 可见则继续保留；钩子 Prefer `js-student`。

Student 专用终态：

```text
return
```

继续独立。

---

## Teacher

> **Universal G3 rebase debt。** 不复活 Teacher 生产；本节仅保留原文投影示例，激活时删除/忽略。不得实现 `js-teacher` 生产路径。

不在 Generator 中硬编码。

由 Teacher 当前 AttemptExecutionProfile 实际 filesystem capabilities 投影。

内部 Agent 是否可被其它 Agent 创建，与其自身 provider request 能看到什么工具是两个不同问题。

---

## Manager / Orchestrator / Blogger / Executor

如果 filesystem primitive set 为空：

```text
无 js-* 工具
无 Prefer js-* 钩子注入
```

---

# 49. Student AGENT-022

> **Universal G3 rebase debt。** AGENT-022 / Student artifact boundary 随 Student 删除；不复活生产。以下原文约束仅作历史对照。

`js-student` 不能因为 program 可以调用多次：

```js
file()
rewrite()
write()
```

而绕过 Student artifact boundary。

每一次：

```js
file(path)
```

如果是 Student-specific Read path，应经过现有 Student read policy。

每一次：

```js
rewrite(path, ...)
write(path, ...)
```

在加入 transaction staging 之前必须经过 AGENT-022 target parser。

只接受：

```text
.agent/skills/<skill-name>/SKILL.md
```

非法第二个文件也必须令整个 program/transaction 失败。

例如：

```js
this.rewrite(
  ".agent/skills/demo/SKILL.md",
  good
);

this.write(
  "outside.txt",
  bad
);
```

结果：

```text
PATH_DENIED
→ 第一文件也不修改
```

---

# 50. Runtime Capability Gate

生成的 base class 是 LLM-facing capability representation。

它不是 security boundary 的替代。

每个 Host runtime binding 仍必须检查被冻结的：

```text
JsRuntimeCapabilities
```

例如：

```text
Read=false
```

即使恶意 JavaScript 通过 prototype/reflection 等方式尝试构造内部调用：

```text
Host 仍拒绝
```

因此：

```text
generated SDK
= accurate UX + capability declaration

runtime gate
= actual enforcement
```

两者来自同一个 immutable capability source。

---

# 51. Execution Binding

Provider surface 可能同时包含：

```text
js-coder
read
edit
...
```

其中 `read` / `edit` / ... 走**既有 builtin executor**（钩子只改过 description）。

任一 **`js-*` 生成工具**调用时：

1. 用 `ToolContext.messageID` 找到准确 Attempt；
2. 读取该 Attempt 的 immutable execution profile；
3. 重新得到或取缓存的 `GeneratedJsSurface`；
4. 验证 invoked tool name 属于该 surface 的 `js-*` 主工具名；
5. 建立该 surface 对应的 `JsRuntimeCapabilities`；
6. 执行 program。

手工伪造：

```text
Reviewer → js-coder
```

必须失败。

旧 Attempt 的：

```text
js-student
```

调用到新的 StudentLearn attempt：

```text
必须失败
```

不能只因为名字合法就执行。

---

# 52. No New `ToolPermission.Js`

不新增：

```text
ToolPermission.Js
```

否则会形成：

```text
Js permission
+
Read/Edit/Write/Glob permission
```

两层可漂移 authority。

`js-ROLE` 是否存在完全是：

```text
filesystem primitive set 非空
```

的投影。

执行成员是否合法由 primitive capability 决定。

---

# 53. Sandbox Model

模型拥有：

```text
JavaScript language
+
generated JsProgram capabilities
```

不拥有一个普通 Node OS process。

禁止 ambient authority：

```text
fs
network
child_process
process environment
worker
native addon
WASI
FFI
inspector
repository mount
Host secrets
```

除非未来某项被正式设计成新的 explicit capability fragment。

第一版 filesystem access 必须全部经过：

```text
JsProgram public members
→ Host capability RPC
```

### 53.1 runner 只获得数据，不获得文件

父进程负责：

```text
path → path gate → read file → strict UTF-8 decode → resolve anchors
```

runner 只收到已物化的数据：

```fsharp
type EditProgramRequest =
    { Source: string
      Anchors: Map<string, int>
      Program: string }
```

其中 Anchors map 已包含 `^` / `$`。

runner **不收到可用的 repository capability**：

```text
runner 不负责 read(path)
runner 不负责 write(path)
runner 不负责 resolve path
```

即使模型程序完全失控，它面对的也只是一个已物化的字符串快照。

### 53.2 `new Function` 只是 invocation mechanism

Host 内部可以这样调用 program：

```js
const run = new Function("text", "glob", "rewrite", "write", `"use strict";\n${program}`)
```

但：

> **安全边界永远是外层隔离进程，不是 `new Function`。**

`new Function` 只是 program invocation mechanism；同 `node:vm` 一样，不能单独充当安全证明（# 76）。

### 53.3 stdout/stderr 不是编辑结果

禁止：

```text
模型 console.log 什么 → Host 把 stdout 当成结果
```

编辑结果 / observation 只来自：

```text
run() 的 return value（经固定 bootstrap 的 result envelope）
```

stdout/stderr 只能作为 bounded diagnostics。

不能从普通输出猜“最后一行可能是结果”，也不能因为程序打印了 JSON 就解析成结果。

runner 必须有明确的 framed response protocol；模型程序产生的 console/stdout 内容不得被当成 protocol。

---

# 54. Arbitrary JavaScript, Not Arbitrary Host Authority

允许普通计算：

```text
functions
loops
conditions
arrays
objects
Map
Set
RegExp
JSON
string processing
sorting
parsing
```

但：

```text
language power
≠ ambient OS authority
```

生产 runner 必须是：

```text
独立
可杀死
有 deadline
有 memory bound
有 output bound
无 ambient filesystem
无 network
无 process spawn
```

不能把 in-process JavaScript context 本身当作 security proof。

### 54.1 Deadline 与资源界

模型程序：

```js
while (true) {}
```

不能挂死工具。

要求：

```text
每个 edit program 一个明确 deadline
deadline 到达 → kill runner
kill 后等待 process reap
返回 PROGRAM_TIMEOUT
绝不继续写文件
```

第一版至少限制：

```text
最大 runner memory
最大 program source bytes
最大 input source bytes
最大 returned file bytes
最大 diagnostic bytes
```

这些常量必须有**单一 owner**：

- 如果现有 write/edit 已有文件大小合同 → 复用该合同；
- 如果当前没有 → 在正式 what/how 中新增一个明确常量 owner，再实现；
- 禁止在三个模块各写一个 magic number。

### 54.2 日志

允许 diagnostics：

```text
operation = js-*
tool name
path
result
failure_code
anchor_count
input_bytes
output_bytes
duration
```

不得记录：

```text
完整 source
完整 program
完整 replacement
模型处理后的完整文件
secrets
```

调试 program failure 时只记录 bounded sanitized diagnostic。

---

# 55. Transaction Model

每一次：

```text
js-* tool call
```

对应恰好一个：

```text
JsTransaction
```

**Durability：** prepare / committed / rolled-back / recovery-required 等动态持久状态 **只** 进入统一 EventStore（facts + owned payloads）。禁止 `js-transaction.db`、`transaction-v2.json`、special feature ref、或任何 feature-owned durable store。

程序执行期间：

```text
file()
glob()
```

产生 observations。

```text
rewrite()
write()
```

只产生 staged mutations。

文件系统在：

```text
run()
```

成功结束以前不得被修改。

### 55.1 同一消息内的并行工具调用由 Host 串行化

模型可以在一次 assistant 消息中并行发出任意多个 `js-*` 与/或内置工具调用（同文件、异文件均可）。

Host 对同一消息内的工具调用按**确定性顺序逐个执行**：

```text
调用 1 执行 → 独立 transaction → commit
调用 2 执行 → 独立 transaction → commit（基于调用 1 之后的 committed state）
...
```

因此：

```text
同文件并行编辑 = 顺序叠加，后一个调用重新 snapshot 到最新 committed state → 绝对安全
异文件并行编辑 = 各自独立 all-or-nothing → 绝对安全
并行读取 = 各自独立只读 → 绝对安全
```

“并行”是模型侧的请求形态；执行侧是确定性的串行提交。

不存在 lost update（# 58 / FILE_CHANGED），也不需要模型自己节流。

### 55.2 模型不拥有事务控制权

派生类**没有**：

```js
this.commit()
this.rollback()
this.transaction()
this.snapshot()
```

以下 API 必须不存在于生成的基类：

```text
commit / rollback / snapshot / resolve / filesystem / transaction
```

事务生命周期完全由 Host 持有：

```text
run() 正常返回 → Host 统一 preflight → prepare → commit
run() throw / 任意 file()/glob() 失败 → 所有 staged rewrites 丢弃 → 零提交
```

编辑意图只通过 `rewrite()` / `write()` 表达；`run()` 的 return value 只是 observation result（# 27），不是提交指令。

---

# 56. Transaction Read Snapshot

第一次：

```js
this.file(path)
```

时记录：

```fsharp
type ExistingFileSnapshot =
    { CanonicalPath: string
      Bytes: byte[]
      Digest: Digest
      Text: string }
```

要求 fatal UTF-8 decode。

失败：

```text
INVALID_UTF8
```

不得 replacement-character 修复。

不得：

```text
replacement-character 修复
跳过坏字节
猜 encoding
自动转 Latin-1
以 binary Buffer 继续
```

（历史 Student SKILL 合同已是 Universal G3 rebase debt。）新工具对目标文件仍保持：UTF-8 且不可解码时 fail closed。

---

# 57. Mutation Preimage

`rewrite(path, ...)`：

```text
必须 snapshot Existing
```

`write(path, ...)`：

```text
必须 snapshot Missing
```

概念：

```fsharp
type PathPreimage =
    | Missing
    | Existing of bytes: byte[] * digest: Digest
```

因此 transaction recovery 可以恢复：

```text
旧 bytes
```

或者：

```text
旧的“不存在”
```

---

# 58. Read Dependencies

如果 program：

```text
read A
read B
rewrite C
```

则 C 的结果可能依赖 A/B。

所以 commit 前应重新验证本次 transaction 读取过的所有具体文件 snapshots。

任何已读取文件变化：

```text
FILE_CHANGED
```

整个 transaction 不提交。

第一版 `glob()` 结果视为一次路径观察。

Host 不承诺针对外部进程提供完整数据库级 phantom serializability；但所有实际读取文件和 mutation preimages 必须在提交前重新验证。

### 58.1 失败不隐式 retry

检测到 FILE_CHANGED 后：

```text
不得自动重新读取
不得自动重新 resolve anchors
不得自动重新执行模型 program
```

那会把一次 tool call 变成隐式 retry，并可能改变模型原本定位的对象。

失败就是失败（# 89 测试）。

---

# 59. WriteSet

整个 program 最终形成：

```text
ReadSet
WriteSet
ReturnValue
```

WriteSet 成员：

```fsharp
type StagedMutation =
    | Rewrite of
        path: CanonicalPath *
        original: ExistingFileSnapshot *
        replacement: byte[]

    | Create of
        path: CanonicalPath *
        replacement: byte[]
```

同 canonical path 最多一个成员。

---

# 60. Preflight

`run()` 正常结束后：

```text
validate return value
→ canonical bounded result preparation
→ validate all staged text
→ UTF-8 encode
→ validate output size
→ validate all paths again
→ validate all ReadSet snapshots
→ validate all mutation preimages
→ acquire transaction ownership
→ only then enter prepare
```

任何失败：

```text
zero committed filesystem changes
```

---

# 61. Deterministic Path Ordering

全部 mutation canonical paths：

```text
canonicalize
→ stable sort
```

事务所有 lock/prepare/commit/rollback 都使用同一顺序。

禁止：

```text
transaction A: a then b
transaction B: b then a
```

造成不确定死锁。

---

# 62. Prepare Phase

所有 replacement 先进入 **EventStore 上的 durable prepare facts / payloads**。
workspace 文件系统在 `Prepared` 被 EventStore 证明之前不得被修改。

> **Durable prepare 与 crash-recovery 的唯一 substrate 是统一 EventStore。**
> 内存 staging / 进程内 scratch 可以存在，但**不得**充当崩溃恢复权威。

禁止为 JS transaction 自建任何 feature-owned durability：

```text
js-transaction.db
transaction-v2.json
special feature ref / sidecar journal
feature-owned sqlite / blob / ndjson store
transaction/ 目录式 manifest 作为 durability authority
```

逻辑视图（不是磁盘布局合同；权威在 EventStore）：

```text
JsTransactionPrepared  (EventStore fact)
  transaction id
  canonical targets[]
  preimage kind/digest per target
  replacement digest / EventStore payload ref per target
  transaction state = Prepared
```

每个 replacement：

```text
materialize full bytes as EventStore-owned payload
→ append Prepared fact（同一统一 EventStore）
→ Prepared 对 recovery 可证明 durable
```

Prepared fact durable 后进入：

```text
Prepared
```

---

# 63. Multi-file Atomicity Contract

正式产品承诺必须写：

> **transactionally all-or-nothing**

而不能写：

> 所有不同 filesystem path 在同一 CPU instant 瞬间改变。

普通文件系统没有通用的“多个 rename 共享一个跨路径瞬时原子点”。

万象术承诺：

```text
正常执行成功
→ 全部 new state

正常执行失败
→ 全部 old state

崩溃/重启
→ recovery 收敛到一个可证明的完整终态
```

外部不服从万象术 transaction ownership 的进程理论上可能在底层多次文件替换之间观察到短暂 mixed view。

这不属于本合同隐藏的事实。

---

# 64. Commit

正常 commit：

```text
Prepared（EventStore fact 已 durable）
→ final snapshot validation
→ apply mutations in canonical order（workspace filesystem effect）
→ append Committed fact to EventStore
→ only now expose successful tool result
→ cleanup ephemeral scratch only（不得留下 feature-owned durable store）
```

Create：

```text
Missing → Replacement
```

Rewrite：

```text
Original → Replacement
```

每一步都必须有 compare-before-effect 保护。

Committed / 终态只以 **EventStore facts** 为准；目录 fsync 或临时文件布局不是 durability contract。

---

# 65. Rollback

如果第 k 个 mutation 后发生 commit failure：

```text
restore already-applied mutations
```

Rewrite 恢复：

```text
Replacement → Original
```

Create 恢复：

```text
Replacement → Missing
```

但 rollback 也必须 CAS-style。

若当前文件已经不是本 transaction 写入的 replacement：

```text
不得覆盖第三方内容
```

进入：

```text
TRANSACTION_RECOVERY_REQUIRED
```

并在 **同一 EventStore** 保留 durable evidence（recovery blocker fact + 既有 Prepared/payload refs）。
禁止另写 `js-transaction.db` / feature sidecar 充当 evidence store。

---

# 66. Crash Recovery

最小 transaction state（全部为 EventStore durable facts，不是程序计数器）：

```text
Prepared
Committed
RolledBack
```

不要把程序计数器固化成几十个领域状态。

启动恢复只读取：

```text
EventStore transaction facts（Prepared / Committed / RolledBack / RecoveryRequired）
EventStore-owned preimage / replacement payload digests
current workspace filesystem state
```

禁止：

```text
打开 js-transaction.db
扫描 transaction/ manifest 目录作为权威
读任何 feature-owned journal / blob / json 旁路
```

每个 target 分类：

```text
Original
Replacement
Missing-as-expected
Unknown
```

若本次 tool success 尚未被确认且 transaction incomplete，第一版 policy：

> **优先恢复调用前状态。**

也就是：

```text
unfinished transaction
→ rollback toward original preimages
→ 终态仍 append 到 EventStore
```

无法证明安全恢复：

```text
fail closed
retain EventStore evidence
report recovery blocker
```

不得猜测。

---

# 67. Successful Return Is Coupled to Commit

有 mutation：

```text
run returns value
→ value valid
→ transaction commits
→ expose value
```

commit 失败：

```text
不能把 run() 的业务 return 当作成功结果交给 LLM
```

纯 query：

```text
WriteSet empty
→ no commit necessary
→ expose validated return
```

因此同一个 JS primitive 同时自然支持 query 和 mutation。

### 67.1 No-op

如果某个 staged replacement 与 preimage 字节相同：

```text
该文件不执行无意义 write/rename
整体仍算成功
```

No-op 结果（见 # 78.1）：

```text
status = "ok"
changed = false
```

---

# 68. Tool Result Bound

`run()` 可以返回大字符串，例如整个文件。

但最终 LLM-visible tool result 仍服从万象术统一：

```text
line bound
UTF-8 byte bound
canonical bounded rendering
```

JS tool 不建立第二套 unlimited read channel。

---

# 69. Tool Description DRY

只有：

```text
js-ROLE
```

拥有完整 JS SDK 描述。

内置 `read` / `edit` / `write` / `glob` / `grep` / `patch`（凡存在者）：

```text
保留各自原有简短 schema/语义说明
+
由 BuiltinToolDescriptionHook 注入的 Prefer js-ROLE 推荐块
```

禁止在这些内置工具里复制整份 anchors/transaction/examples 长文。

所以关于：

```text
anchors
regex
transaction
return
examples
```

的任何修改都只改：

```text
Capability Registry / description renderer
```

钩子推荐块的任何修改只改：

```text
BuiltinToolDescriptionHook renderer
```

不存在五份文档同步。

---

# 70. Role Prompt DRY

角色 system prompt 不应再承担 filesystem capability 教学。

Prompt 负责：

```text
你是谁
你的职责
你的协作方式
```

Generated tool description 负责：

```text
你当前这个 attempt 能编程调用什么
```

避免：

```text
system prompt 说只读
但 generated SDK 有 rewrite()
```

或者反过来。

---

# 71. Builtin Tools Remain — Additive `js-*`

这不是 clean break，而是 **additive coexistence**。

最终 provider surface 中：

```text
read
edit
write
glob
grep
patch   # 若该名字已存在于正式工具面
```

名称仍然存在，并且继续是**真正的内置工具**：

```text
原 schema
原 Host/native 实现
原执行语义
```

同时，当 filesystem primitive set 非空时，额外暴露：

```text
js-ROLE
```

旧 Host/native：

```text
read(path)
edit(oldString,newString)
write(path,content)
glob(pattern)
grep(pattern,path)
```

schema **必须继续暴露**（与现有正式合同一致）。

必须证明：

```text
每个 builtin 名字恰好一个 spec
js-ROLE 是另一个独立 spec
builtin schema ≠ { program: string }
```

例如：

```text
edit
```

不能同时存在：

```text
legacy edit
generated edit alias pretending to be edit
```

也不能把 `edit` 偷换成 `js-coder` 的第二个名字。

---

# 72. No Alias / No Dual Schema Takeover

不接受：

```text
把 read/edit/write/glob/grep/patch 改成 Alias of js-ROLE
（patch 只要可见，同样禁止 alias takeover）
```

也不接受：

```text
同一工具名同时接受
  { path, oldString, newString }
与
  { program }
```

作为 dual semantics。

本 Change 的唯一迁移阀门是：

```text
BuiltinToolDescriptionHook
→ deprecated
→ Prefer js-ROLE
→ 极力鼓吹复杂 JS program / 并行调用
```

内置工具继续可执行；新工作靠描述层把模型推到 `js-*`。

如果需要迁移 fixture/canary，优先新增 `js-*` 路径；不要靠删除 builtin schema 强迫迁移。

---

# 73. Dynamic Tool Generation

不要在：

```text
ToolRegistry.baseSpecs
```

硬编码：

```text
js-coder
js-inspector
js-reviewer
...
```

注册的是：

```text
JsToolGenerator
+
BuiltinToolDescriptionHook
```

普通 Attempt：

```text
profile
→ generate js-* surface (if any)
→ assemble provider tools =
     existing builtins
   + generated js-ROLE
→ hook rewrites visible builtin filesystem descriptions
```

Student（**Universal G3 rebase debt；勿复活生产**）：

```text
# historical illustration only
StudentLearn profile
→ no JS surface
→ no Prefer js-* hook

StudentCompile profile
→ js-student
→ hook Prefer js-student on still-visible filesystem builtins
```

激活后不以 Student 切换路径验收；通用规则仍是：切换 RequestKind / profile 前必须先生成完整新 surface，再随整套 permission 原子安装。

---

# 74. Generated Surface Identity

每个 generated surface 应具有稳定 fingerprint：

```text
role
+
primitive capability set
+
Js SDK version
```

例如内部：

```text
GeneratedJsSurfaceFingerprint
```

用途：

```text
cache
golden proof
stale-call detection
diagnostics
```

它不是 Authority。

Authority 仍然是 Attempt profile。

---

# 75. Deterministic Generation

相同：

```text
CanonicalRole
primitive capabilities
SDK version
```

必须产生 byte-identical：

```text
primary schema
base class
description
examples
BuiltinToolDescriptionHook payloads
runtime binding set
```

禁止依赖：

```text
Set iteration order
registration order
dictionary enumeration accident
```

所有 fragment/member/example 都有 canonical rank。

---

# 76. Security Boundary

模型只能看到：

```text
JsProgram public API
```

Host internals：

```text
snapshot reader
glob adapter
transaction staging
commit engine
sandbox RPC
```

不是模型可调用 API。

即使 tool description 为了说明语义展示 `file()` 的 reference implementation，也不意味着 private Host primitive 以普通 JavaScript global 暴露。

真实 runner 必须通过不可越权的 Host boundary 实现。

---

# 77. Failure Algebra

建议正式定义：

```fsharp
type JsToolFailure =
    | InvalidProgram
    | ProgramFailed
    | ProgramTimeout
    | ProgramResourceLimit

    | PermissionDenied of capability: JsPrimitiveCapability
    | PathDenied of path: string

    | FileNotFound of path: string
    | FileAlreadyExists of path: string
    | FileReadFailed of path: string
    | InvalidUtf8 of path: string

    | InvalidAnchor of AnchorFailure
    | UnknownAnchor of string
    | InvalidSlice of fromAnchor: string * toAnchor: string

    | DuplicateMutationTarget of path: string
    | ResultTooLarge of path: string option
    | InvalidReturnValue

    | FileChanged of path: string

    | TransactionPrepareFailed
    | TransactionCommitFailed
    | TransactionRollbackFailed
    | TransactionRecoveryRequired
```

Anchor：

```fsharp
type AnchorFailure =
    | EmptyName
    | ReservedName of string
    | DuplicateName of string
    | SameBeginAndEndName of string
    | EmptyStringPattern
    | InvalidPattern
    | PatternNotFound of declarationIndex: int
```

### 77.1 稳定失败码

Provider-visible renderer 把 typed failure 映射为稳定 code，第一版建议：

```text
INVALID_PROGRAM
PROGRAM_FAILED
PROGRAM_TIMEOUT
PROGRAM_RESOURCE_LIMIT
PERMISSION_DENIED
PATH_DENIED
FILE_NOT_FOUND
FILE_ALREADY_EXISTS
FILE_READ_FAILED
INVALID_UTF8
RESERVED_ANCHOR
DUPLICATE_ANCHOR
EMPTY_ANCHOR_NAME
EMPTY_ANCHOR_CONTENT
ANCHOR_NOT_FOUND
UNKNOWN_ANCHOR
INVALID_SLICE
DUPLICATE_MUTATION_TARGET
RESULT_TOO_LARGE
INVALID_RETURN_VALUE
FILE_CHANGED
TRANSACTION_PREPARE_FAILED
TRANSACTION_COMMIT_FAILED
TRANSACTION_ROLLBACK_FAILED
TRANSACTION_RECOVERY_REQUIRED
```

不要从 exception message 反推业务错误种类。

---

# 78. LLM-visible Errors

错误应该小而稳定：

```toml
status = "failed"
code = "ANCHOR_NOT_FOUND"
message = "An anchor pattern was not found in declaration order."
```

可以提供：

```text
declaration index
anchor names
```

但不回显：

```text
完整 source
完整 program
完整 secret path
sandbox internals
environment
```

Program error stack 必须 sanitize + bound。

### 78.1 成功结果形状

成功结果保持小而确定：

```toml
status = "ok"
changed = true
files = 3
```

no-op：

```toml
status = "ok"
changed = false
files = 0
```

或调用了 `rewrite()` 但全部 replacement 与 originals 相同：

```toml
status = "ok"
changed = false
files = 3
```

其中：

```text
files = staged target 文件数（含 write 创建的文件）
```

Tool result 不返回：

```text
完整新文件
完整旧文件
program
source
巨大 diff
```

模型需要检查内容时自己调用 `read` / file()。

---

# 79. Source Layout

建议正式责任区：

```text
src/Wanxiangshu/Domain/JsTools/
```

拥有：

```text
primitive capability algebra
anchor rules
surface projection rules
failure algebra
```

---

```text
src/Wanxiangshu/Application/
```

拥有：

```text
JsTool workflow
transaction orchestration
```

---

```text
src/Wanxiangshu/Infrastructure/OpenCode/Tools/
```

拥有：

```text
provider specs
GeneratedJsSurface adapter
ToolRegistry bridge
Synthetic TOML result bridge
```

---

```text
src/Wanxiangshu/Process/
```

拥有：

```text
sandbox runner
deadline
kill/reap
resource budgets
```

---

```text
Infrastructure filesystem adapter + EventStore ports
```

拥有：

```text
snapshot
ephemeral / in-memory staging（非 durability authority）
EventStore durable prepare facts + payloads
commit（workspace effect + EventStore Committed fact）
rollback
crash recovery from EventStore only
```

禁止另建：

```text
js-transaction.db
transaction-v2.json
special feature ref
feature-owned durable store
```

Domain 不做 Host I/O。

---

# 80. Production Language Boundary

万象术 production 继续：

```text
src/Wanxiangshu/**/*.fs
```

不要因为“执行 JavaScript”就在 production source 新增一套：

```text
src/js-runner.mjs
```

如果 sandbox bootstrap 需要 JS source，它应由正式 resource/production owner 持有，而不是变成第二种业务实现语言。

模型 program 是数据，不是万象术 production source。

---

# 81. Documentation Changes

建议新建正式主题：

```text
docs/why/js-tools.md
docs/what/js-tools.md
docs/shape/js-tools.md
docs/how/js-tools.md
docs/proof/js-tools.md
```

Clause prefix：

```text
JS-
```

建议：

```text
JS-001  Capability-projected tool surface
JS-002  Primary js-ROLE generation
JS-003  Builtin coexistence + description hook
JS-004  Generated base-class exactness
JS-005  file()/FileView
JS-006  Ordered string/RegExp anchors
JS-007  glob()
JS-008  rewrite()
JS-009  write()
JS-010  JSON-compatible return
JS-011  Sandbox capability boundary
JS-012  Transaction staging（ephemeral OK；durable = EventStore only）
JS-013  Multi-file all-or-nothing commit
JS-014  Conflict detection
JS-015  Rollback/recovery（EventStore facts only；forbid js-transaction.db / feature store）
JS-016  Synthetic TOML result
JS-017  Builtin tools remain (no alias takeover)
JS-018  Student request/path projection（Universal G3 rebase debt；勿复活生产）
```

并更新：

```text
docs/what/agent.md
docs/shape/agent.md
docs/how/agent.md
docs/proof/agent.md
docs/README.md
```

必要时更新 Synthetic TOML inventory/reference。

---

# 82. `why/js-tools.md`

重点记录：

### 为什么不把可编程面再做成五套独立实现

拒绝为 `js-*` 再分别发明：

```text
js-read implementation
js-edit implementation
js-write implementation
js-glob implementation
js-grep implementation
```

因为可编程路径共享：

```text
path boundary
filesystem
result rendering
permissions
snapshot
string computation
transaction
```

会重复并漂移。

既有 builtin `read/edit/write/glob/grep/patch` RPC 实现（凡存在者）继续保留；本 Change 统一的是新增的 capability-projected JS surface，不是删掉 builtin。

### 为什么是 generated SDK

拒绝万能基类 + prose permission warning。

因为：

```text
看得到无权限方法
```

本身就增加模型认知负担和误调用率。

### 为什么保留内置文件系统工具（而不是 alias / clean break）

因为：

```text
read
edit
write
glob
grep
patch
```

是 LLM 训练中极强的工具选择 affordances（或正式工具面已有名字），且既有 schema/实现已是正式合同。

本 Change **不替换**它们。

同时新增 `js-ROLE`，并用 Tool Definition 钩子在内置工具 description 中：

```text
DEPRECATED
+
Prefer js-ROLE
+
极力鼓吹复杂 JS program / 并行调用
```

总结：

> **Builtin `read`/`edit`/`write`/`glob`/`grep`/`patch` 是兼容面；`js-*` 是推荐面；钩子是引流面。三者不是 alias。**

---

# 83. `what/js-tools.md`

只冻结 observable semantics：

```text
生成规则
schema
base class method availability
anchor matching
regex semantics
FileView
glob
rewrite/create distinction
JSON return
transaction all-or-nothing
errors
builtin coexistence + description hook
```

不要放内部模块名。

---

# 84. `shape/js-tools.md`

重点所有权：

```text
AttemptExecutionProfile
= authority owner

JsToolGenerator
= projection owner

Capability Registry
= SDK/runtime/description owner

BuiltinToolDescriptionHook
= builtin Prefer js-ROLE recommendation owner

Sandbox Runner
= arbitrary JS process owner

JsTransaction
= staged filesystem effect owner

SyntheticToml
= LLM-visible result rendering owner
```

硬边界：

```text
Generator 不重新决定权限
Runtime 不从 description 解析权限
Builtin 工具名不决定 js-* 权限
Description 钩子不改变 builtin schema/executor
Model JS 不拥有 ambient OS authority
Transaction engine 不执行模型 JavaScript
```

---

# 85. `how/js-tools.md`

唯一实现序：

```text
resolve Attempt
→ get immutable profile
→ generate js-* surface (if any)
→ assemble builtins + js-ROLE
→ BuiltinToolDescriptionHook rewrites visible builtin filesystem descriptions
→ provider sees tools
→ model preferably invokes js-ROLE (builtins still executable)
→ if js-*: ToolRegistry verifies invoked name belongs to same Attempt surface
→ create sandbox with generated runtime bindings
→ execute class Js
→ collect return + ReadSet + WriteSet
→ validate result
→ validate snapshots/paths
→ transaction prepare
→ transaction commit
→ render result
```

纯查询：

```text
WriteSet empty
→ skip transaction commit
→ render result
```

---

# 86. Capability Generator Proof

必须枚举所有 canonical profile，证明：

```text
GeneratedMembers(profile)
==
members implied by primitive capabilities
```

以及：

```text
HookTargets(profile)
⊆
visibleBuiltinFilesystemTools(profile)
```

以及：

```text
Hook mentions js-ROLE
⇒
js-ROLE is provider-visible in the same request
```

以及：

```text
RuntimeBindings(profile)
==
bindings implied by primitive capabilities
```

核心同构：

```text
public member exists
⇔ executable runtime capability exists
```

### 86.1 Tool Description Golden

必须有 golden 测试固定 provider-visible schema 与核心说明。

至少断言 `js-ROLE` description 中明确存在这些概念：

```text
ordered
begin
end
content
text(from, to)
^
$
complete resulting file
```

以及标准 replace example。

目的不是逐字冻结整篇 prose，而是防止未来“精简描述”时把推荐 idiom 删掉，只剩：

```text
Run JavaScript to edit a file.
```

那会把模型重新推回手写 `indexOf` 的不稳定路径。

---

# 87. “Lying Generator” Permanent Counterexamples

永久测试故意构造错误 registry。

例如：

```text
description/base class 有 rewrite()
runtime 无 Edit binding
```

必须红。

反过来：

```text
runtime 有 Edit
base class 无 rewrite()
```

也必须红。

再例如：

```text
builtin edit description 被钩子 Prefer js-coder
但 provider 未暴露 js-coder
```

必须红。

再例如：

```text
钩子把 builtin edit schema 改成 { program }
```

必须红。

真正 proof 是：

> **LLM-visible SDK 与 executable authority 同构。**

---

# 88. Builtin Coexistence + Hook Tests

证明：

```text
builtin read/edit/write/glob/grep/patch schema unchanged
when js-ROLE is added
```

证明：

```text
builtin executors still succeed
after Prefer js-ROLE hook injection
```

证明：

```text
hook injects DEPRECATED + Prefer js-ROLE
at the front of each visible builtin filesystem tool description
```

证明：

```text
hook never rewrites schema or executor
```

特别测试：

```text
Coder invokes builtin "edit"
→ succeeds with legacy semantics

Coder invokes builtin "patch" (if present)
→ succeeds with legacy patch semantics
→ description still Prefer js-coder

Coder invokes js-coder with rewrite()
→ succeeds with JS transaction semantics
```

因为 builtin 与 js-* 是两个独立工具。

同时：

```text
Inspector invokes js-inspector
forged program attempts rewrite
→ runtime fail closed
```

以及：

```text
js-ROLE absent
→ no Prefer js-* hook text in builtin descriptions
```

---

# 89. Role Projection Tests

至少：

```text
fast-coder == deep-coder JS surface
fast-inspector == deep-inspector JS surface
fast-reviewer == deep-reviewer JS surface
```

Student（**Universal G3 rebase debt；勿复活生产 / 非 Active 验收**）：

```text
# historical illustration only
StudentLearn
→ no js-student

StudentCompile
→ exact generated js-student
```

旧 Learn Attempt 伪造 Compile JS call（历史对照）：

```text
fail closed
```

---

# 90. Anchor Unit Tests

必须覆盖：

```text
^ == beginning
$ == end

exact string match
duplicate exact text allowed
ordered duplicate resolution

RegExp match
RegExp flags preserved except search-state g/y semantics
caller lastIndex ignored
zero-width RegExp allowed

reserved names rejected
empty names rejected
duplicate names rejected
same begin/end name rejected
empty string pattern rejected

pattern missing rejected

text("^","$") whole file
text exact slice
unknown anchor rejected
reverse range rejected

same text slice reusable
slices reorderable
```

---

# 91. Read/Glob/Grep Tests

Read：

```text
known file → returned content
invalid UTF-8 → fail
denied path → fail
```

Glob：

```text
deterministic sorted result
path boundary enforced
```

Grep：

```text
implemented entirely as JS composition
no special grep backend required
```

---

# 92. Mutation Tests

Rewrite：

```text
existing file → allowed with Edit
missing file → FILE_NOT_FOUND
```

Write：

```text
missing path → allowed with Write
existing path → FILE_ALREADY_EXISTS
```

Capability negative:

```text
no Edit → rewrite inaccessible + runtime rejected
no Write → write inaccessible + runtime rejected
```

---

# 93. Transaction Tests

核心：

```text
rewrite A
rewrite B
→ both new
```

conflict：

```text
snapshot A/B
external change B
→ FILE_CHANGED
→ A remains old
```

prepare failure：

```text
second staged file cannot prepare
→ zero target mutation
```

mid-commit failure：

```text
A applied
B fails
→ A restored
→ failure
```

Create rollback：

```text
A created
second mutation fails
→ A absent again
```

unknown external modification during rollback：

```text
do not overwrite
→ recovery required
```

并行调用（# 55.1）：

```text
同消息两个调用编辑同一文件
→ 按确定性顺序执行
→ 第二个调用看到第一个的 committed state
→ 两个都成功，无 lost update

同消息两个调用编辑不同文件
→ 各自独立 all-or-nothing

同消息并行读取 + 编辑
→ 读取调用不影响编辑调用
```

---

# 94. Crash Recovery Tests

模拟：

```text
Prepared + zero applied
Prepared + subset applied
Prepared + all applied but not marked Committed in EventStore
```

恢复必须根据：

```text
EventStore transaction facts
EventStore payload digests（preimage / replacement）
current workspace bytes
```

确定结果。

禁止测试依赖：

```text
js-transaction.db
transaction/ manifest 目录
任何 feature-owned sidecar
```

未确认成功的 incomplete transaction 默认：

```text
rollback toward original
→ 终态 append 到 EventStore
```

Unknown：

```text
fail closed
retain EventStore evidence
```

---

# 95. Return Tests

```text
string
object
array
null
boolean
number
```

成功。

```text
BigInt
undefined
cycle
function
Infinity
```

失败。

且：

```text
invalid return + staged rewrites
→ no commit
```

---

# 96. Sandbox Tests

必须尝试：

```js
require("fs")
```

失败。

```js
import("node:fs")
```

失败。

```js
child_process
```

失败。

网络失败。

环境 secret 不可读。

无限循环：

```js
while (true) {}
```

deadline 后 kill/reap。

大内存：

```text
resource bound
```

不能拖垮 Host。

---

# 97. Coexistence / No-Alias-Takeover Tests

Provider-visible：

```text
read
edit
write
glob
grep
patch    # if present on the formal tool surface
js-ROLE
```

每个 builtin 名字恰好一个 spec；`js-ROLE` 是额外独立 spec。

builtin schema **必须保留**既有字段（不得变成 `{ program }`）。

禁止：

```text
把 builtin 变成 alias of js-ROLE
同一名字 dual schema
hook 删除 builtin 可执行性
```

`js-ROLE` description 是 JS SDK 唯一完整文档 owner；
builtin description 只允许叠加 Prefer 钩子，不复制整份 SDK 文档。

---

# 98. E2E Canary — Read

LLM 或 harness：

```js
class Js extends JsProgram {
  async run() {
    const f = await this.file("README.md");
    return f.text();
  }
}
```

证明：

```text
js tool 可以完全承担 read
```

---

# 99. E2E Canary — Grep

```js
glob
→ file
→ matchAll
→ return
```

证明：

```text
无 grep backend 也实现 grep 产品语义
```

---

# 100. E2E Canary — Ordered Regex Edit

文件有两个相似函数。

先用 string/regex anchor 限定第二个函数，再替换里面重复 return。

只允许第二处改变。

具体 fixture（真实 Coder canary）：

原文件：

```js
function first() {
  return "old";
}

function second() {
  return "old";
}
```

程序：

```js
class Js extends JsProgram {
  async run() {
    const f = await this.file("target.js", [
      ["second", "secondBody", "function second() {"],
      ["target", "afterTarget", 'return "old";'],
    ]);

    this.rewrite(
      "target.js",
      f.text("^", "target")
        + 'return "new";'
        + f.text("afterTarget", "$")
    );

    return { changed: ["target.js"] };
  }
}
```

最终必须只改第二个函数。

这个 canary 同时证明：

```text
duplicate source fragment
ordered disambiguation
standard splice
whole-file result
真实 filesystem commit
```

---

# 101. E2E Canary — Multi-file Refactor

一次 program：

```text
glob source files
find occurrences
rewrite N files
return changed list
```

所有文件：

```text
一起成功
```

或：

```text
全部不变
```

补充：**多区域编辑** fixture——用两个 anchor declarations，一次 program 同时修改两处：

原文件：

```text
HEADER

A=old

middle

B=old

FOOTER
```

程序用 `["a","b","A=old"]` 与 `["c","d","B=old"]` 两个 anchor，一次 `rewrite()` 同时替换 A、B。

这证明该工具不是换皮 single replace。

补充：**move/copy** canary——至少一个测试不属于 replace，例如把一个 block 移到另一个位置：

```js
class Js extends JsProgram {
  async run() {
    const f = await this.file("src/foo.js", [
      ["a", "b", "first block"],
      ["c", "d", "second block"],
    ]);

    this.rewrite(
      "src/foo.js",
      f.text("^", "a")
        + f.text("c", "d")
        + f.text("b", "c")
        + f.text("a", "b")
        + f.text("d", "$")
    );
  }
}
```

否则实现虽然 schema 很强，实际 proof 仍只证明了 legacy replace。

---

# 102. E2E Canary — Student

> **Universal G3 rebase debt。** 不作为 Active 实施/验收项；不得为复活 Student 而保留此 canary。

StudentCompile program：

```text
合法 SKILL A
合法 SKILL B
```

事务成功。

再加入第三个非法 target：

```text
outside.txt
```

整个 transaction：

```text
零修改
```

---

# 103. Static Governance Gate

新增永久静态检查至少证明：

```text
generated SDK member registry 单一 owner
无 per-role handwritten JsProgram templates
builtin read/edit/write/glob/grep/patch 仍有独立 implementation specs（凡存在者）
BuiltinToolDescriptionHook 是 Prefer 文案唯一 owner
builtin description 无复制整份 JS SDK 长文
禁止把 builtin 注册成 js-* alias
```

受控反例：

临时加入第二个 `edit` spec：

```text
正式检查必须红
```

临时让 Edit fragment 缺 runtime binding：

```text
必须红
```

恢复后重新运行正式检查。

---

# 104. Implementation Order

进入 Active 后严格：

```text
1. what
2. shape
3. how
4. Active Remaining work / completion criteria
5. Domain capability algebra
6. Capability Fragment Registry
7. JsToolGenerator
8. generated description/base class renderer
9. ToolRegistry generated-name gate
10. sandbox runner
11. anchor/FileView implementation
12. glob binding
13. rewrite/write staging（ephemeral；durability 不在此步）
14. transaction engine（durable prepare/commit/recovery → EventStore only）
15. return serializer
16. Synthetic TOML bridge
17. Agent surface migration（仅存活 Agent catalog；不含 Student/Teacher）
18. ~~StudentCompile migration~~ — Universal G3 rebase debt；不得复活生产
19. BuiltinToolDescriptionHook (deprecated + Prefer js-ROLE)
20. keep builtin read/edit/write/glob/grep/patch executable with original schemas
21. unit tests
22. transaction/recovery tests（EventStore facts；禁 js-transaction.db）
23. sandbox tests
24. e2e
25. proof
26. controlled counterexamples
27. node scripts/checks/spec.mjs
28. npm run lint
29. official test/coverage gates
```

---

# 105. Non-goals — First Version

第一版不加入：

```text
rm()
mv()
mkdir()
binary file APIs
network APIs
PTY
Git API
AST API
language-specific parsers
fuzzy anchors
automatic retry after FILE_CHANGED
```

现有：

```text
mv
rm
PTY
network
delegation
verdict
```

继续保持独立工具。

`return(Student/Teacher)` 是 Universal G3 rebase debt — 已删生产面；不得复活。

未来如要纳入，只能通过新增正式 capability fragment。

---

# 106. Future Extension Model

以后新增：

```text
Remove
Rename
Directory
BinaryRead
Git
```

不需要创造新的“大工具架构”。

只增加一个 capability fragment：

```text
permission
public member
runtime binding
BuiltinRecommendationTargets (optional)
description fragment
examples
```

Generator 自动为合法 Agent surface 投影。

是否对仍存在的传统工具（如未来的 `rm` / `mv`）注入 Prefer 钩子：

```text
只是 LLM UX / 迁移引流决策
```

不是新的系统 primitive，也不是 alias takeover。

---

# 107. Completion Criteria

本 Change 只有全部满足才能关闭。

## Authority

* JS surface 只从 `AttemptExecutionProfile.ToolCapabilitySet` 投影；
* 无第二份 role→JS permission matrix；
* Student request kind 使用同一 Attempt profile；
* stale/forged calls fail closed。

## Generator

* 一个 `JsToolGenerator`；
* 一个 Capability Fragment Registry；
* 无 per-role handwritten JS tool variants；
* deterministic generation；
* fast/deep 相同。

## SDK

* base class 只包含当前真正可执行成员；
* member presence 与 runtime binding 同构；
* capability 缺失的方法不出现在 description/examples。

## Builtin Coexistence + Hook

* read/edit/write/glob/grep/patch 继续是独立内置工具（凡存在者；原 schema / 原实现）；
* 不把它们变成 `js-*` alias，不改成 `{ program }`；
* `js-ROLE` 作为新增工具与它们共存；
* 当 `js-ROLE` 可见时，BuiltinToolDescriptionHook 在内置工具 description 中 **deprecated + 极力推荐 js-ROLE**；
* 钩子不形成 security scope，也不改变 builtin 可执行性；
* 钩子文案提到的 `js-ROLE` 必须同时 provider-visible。

## File API

* `file()` strict UTF-8；
* immutable snapshots；
* ordered string/RegExp anchors；
* `^/$`；
* exact `text()` slices；
* zero-width regex positions；
* duplicate textual occurrence 可依序消歧。

## Primitive Semantics

* Read = existing UTF-8 observation；
* Glob = bounded deterministic path enumeration；
* Edit = rewrite existing file；
* Write = create missing file；
* Grep = Read + Glob + JS RegExp derived affordance。

## Return

* JSON-compatible structured result；
* query 可以零 mutation；
* result validation 在 commit 前；
* result 走 Synthetic TOML/tool-result bound；
* Js return 不混淆 Student/Teacher return。

## Transaction

* mutations 全部 staged；
* 同 target 一次；
* multi-file transaction；
* all preflight before mutation；
* snapshot conflict fail closed；
* durable prepare **仅经统一 EventStore**（禁止 `js-transaction.db` / feature-owned store / manifest 目录权威）；
* all-or-nothing normal outcome；
* rollback；
* crash recovery **仅从 EventStore facts/payloads 重建**；
* success result 只在 commit 完成后暴露。

## Security

* arbitrary JavaScript 无 ambient Host authority；
* fs/network/process/env 等不可直接获得；
* timeout 可 kill；
* memory/output bounded；
* runtime gate 不依赖模型遵守 base class。

## Student

> **Universal G3 rebase debt — 不得复活生产。** 下列条目不作为 Active 验收；激活时从 Remaining work 删除。

* StudentLearn 无 JS surface；
* StudentCompile exact projected surface；
* AGENT-022 对每个 read/write/edit target 生效；
* 任一非法 mutation target 使整个 transaction 零提交。

## Migration

* builtin `read` / `edit` / `write` / `glob` / `grep` / `patch` implementations **保留**（凡存在者）；
* 这些 familiar names **保留为真正内置工具**；
* builtin schemas **保留**；
* 新增 `js-ROLE`；靠 description 钩子引流，不做 alias takeover；
* provider 同名 spec 无重复（builtin 与 js-* 名字本就不同）。

## Parallel

* 同消息并行工具调用按确定性顺序串行执行；
* 同文件并行编辑无 lost update；
* 异文件并行编辑各自 all-or-nothing；
* 并行读取不影响编辑；
* 复杂 JS 脚本（批量 glob/read/rewrite）在单事务内成立。

## Proof

* generator equivalence；
* lying-generator counterexample；
* builtin coexistence + Prefer hook semantics；
* anchor/regex；
* read/glob/grep；
* write/rewrite；
* structured return；
* multi-file transaction；
* conflict；
* rollback；
* crash recovery（EventStore only）；
* sandbox；
* Student（G3 rebase debt — 非验收复活项）；
* e2e；
* spec/lint/coverage 全绿。

---

# 108. Final Mental Model

对于实现者：

```text
Authority
   ↓
Capability Set
   ↓
Generator
   ↓
Exact SDK
   ↓
Sandbox Program
   ↓
Observation + Transaction
```

对于 LLM：

```js
class Js extends JsProgram {
  async run() {
    ...
  }
}
```

它只需要看生成的基类。

如果看到：

```js
file()
glob()
```

它就是只读 filesystem programmer。

如果还看到：

```js
rewrite()
write()
```

它就是完整 filesystem programmer。

如果方法不存在：

```text
不要用。
```

不需要额外理解权限表。

传统/既有文件系统工具：

```text
read
edit
write
glob
grep
patch
```

仍然存在（凡正式工具面已有者），并继续是可执行的内置 RPC 工具。

它们的 description 会被钩子标成 deprecated，并极力推荐当前的 `js-ROLE`。

系统新增的真正抽象是：

> **Capability-projected JavaScript program.**

锚点不是 patch protocol。

它是 Read capability 提供的 source-addressing library。

Regex grep 不是独立搜索引擎。

它是 Read + Glob + JavaScript 的自然组合。

Edit 不是 `oldString → newString`。

它是 immutable source snapshots 到 staged replacements 的程序。

多文件编辑不是多次 tool call。

它是一个 transaction 的 WriteSet。

工具说明也不再尝试用自然语言解释权限。

> **生成出来的基类，就是权限本身。**

最终整个设计可以浓缩为一句话：

> **Wanxiangshu projects authority into an exact JavaScript SDK, and the LLM simply programs against the SDK it is given.**

---

# Active work

> 本文件为变更工作记录，不是当前产品规范。当前产品语义仅以 `docs/` 正式层为准。
> Original proposal 原文冻结于上方；后续事实只追加于 Active work / Amendments / Blockers / Final outcome。

## Work origin

用户通过 `changes/proposed/entry.md` Implementation Playbook 明确启动：G4（Unified Storage）Exit 达成（`changes/completed/storage.md` Final outcome；`npm run check` + Long Stroke e2e GREEN）后，按 Gate 顺序进入 **G5 JS Capability-Projected Tools**。

## Cross-proposal prerequisites

| Gate | Status | Evidence |
|---|---|---|
| G0–G3.5 | DONE | 见 `changes/completed/*` |
| G4 Unified Storage | DONE | `changes/completed/storage.md`；唯一 durable substrate = EventStore |
| G4R One World testing | DONE | `changes/completed/test.md`；e2e = 单一 Long Stroke（`tests/e2e/entry.test.mjs`）；无新 canary |

## Approved Amendments

### Amendment JS-G3 — Universal G3 rebase debt 不复活（proposal §18 / §49 / §107 Student 部分）

```text
js-student / js-teacher / StudentLearn / StudentCompile / Teacher 专属表面
= 不得实现、不得迁回、不得以兼容别名复活。
Student request kind 不产生 js-* surface（或与普通 profile 同规则，无 Student 专属分支）。
```

实施输入只认当时仍存活的 Agent catalog（Manager/Orchestrator/Coder/Inspector/Browser/Meditator/Reviewer/DevOps/Executor/Blogger）+ `AttemptExecutionProfile.ToolCapabilitySet`。

### Amendment JS-G4R — 时间边界与证明形式

```text
- e2e 证明 = Long Stroke 单一入口（G4R freeze）；不新增 per-feature canary。
- 新代码遵守 g4r-ce-vocabulary：Domain/Application/Session 零 raw time；deadline 走 Capability Port。
- 性能：Long Stroke 墙钟目标维持 <10s（npm run check:release 门禁），不得因 G5 工具注册提升超时。
```

### Amendment JS-G4S — durable transaction facts 只经 EventStore

proposal §66/§107 Transaction 已明确；Active 强制：

```text
js-transaction.db / transaction-v2.json / special feature ref / feature-owned durable store
= doNotBuild（unified-store-gate 同族禁制）。
crash recovery 只从 EventStore facts/payloads 重建。
```

## Remaining work

按 proposal §104 Implementation Order（Student/Teacher 项按 Amendment JS-G3 删除）：

### Phase A — capability algebra + generator（Playbook §12.2 第一阶段）— DONE（6085ae6a）
- [x] Formal docs：`docs/{why,what,shape,how,proof}/js-tools.md`（Clause 前缀 `JS-`）；`docs/README.md` 索引
- [x] Domain capability algebra（`src/Wanxiangshu/Domain/JsTools.fs`）：primitive capability × anchor rules × surface projection rules × failure algebra
- [x] Capability Fragment Registry（`JsFragmentRegistry`；member/description/example/runtime binding 同源）
- [x] `JsToolGenerator`：从 `AttemptExecutionProfile.ToolCapabilitySet` 确定性生成 js-ROLE（name/schema/description/base class/examples/runtime bindings）
- [x] generated description / base-class renderer
- [x] ToolRegistry generated-name gate（`isGeneratedToolName` / `memberBinding`；forged call fail closed）
- [x] 静态门禁：`scripts/checks/js-surface-gate.mjs` wired into `check.mjs`

### Phase B — sandbox + transaction（Playbook §12.3 第二阶段）— PARTIAL（B-1/B-2/B-3/B-4 DONE）
- [x] sandbox runner（`Process/JsSandbox.fs`；vm 无 ambient authority；vm timeout + 递归 deadline proxy；output bound）
- [x] failure algebra（`JsFailure` 按 proposal §77.1 稳定码）+ anchor 声明纯规则（`AnchorRules`；空锚点/非正 occurrence 拒绝）
- [x] transaction 纯规则（`JsTransaction`：validateSingleIntent/validateTargets/validateFreshness/preflight/commitPlan/rollbackPlan）
- [x] fs adapter（`Infrastructure/JsToolsFs.fs`：strict UTF-8 读、ordered anchors、bounded glob、两阶段 all-or-nothing commitPlan）
- [x] runtime bindings（`Infrastructure/JsToolsBindings.fs`：file/glob/grep/rewrite/write；path boundary；staging-only；binding key 与 fragment 同源）
- [x] js-* 工具工作流（`Infrastructure/OpenCode/Tools/JsToolWorkflow.fs`：sandbox → staging → preflight（活 fs 事实）→ all-or-nothing commit → 提交报告；binding 失败为可检查对象）
- [ ] transaction engine Host 侧（preflight 执行 → durable prepare = EventStore only → commit → rollback → crash recovery）
- [ ] return serializer（JSON-compatible；result validation 在 commit 前）
- [ ] Synthetic TOML bridge（JS-016）

### Phase C — Agent surface + coexistence（Playbook §12.5–12.6 第三阶段）— PARTIAL（C-1/C-2 DONE）
- [x] Agent surface migration（`ToolRegistry` baseSpecs 按角色矩阵经 `JsToolGenerator` + `JsToolSpec.create` 生成 js-* specs；无手写 spec；`rolePredicate` js-* gate：角色名 + fs capability 双重校验）
- [x] 权限矩阵（`StaticTools` knownToolNames + permissionObj：角色拥有 fs capability 时 allow 其 js-* 工具；Meditator 无 fs → deny；`js-surface-gate` 更新为矩阵静态名单合法）
- [x] `BuiltinToolDescriptionHook`（deprecated + Prefer js-ROLE 文案生成 + 幂等 annotate + 不可见工具推荐 fail-closed；纯函数 + 测试）
- [ ] hook 文案接入 Host transform（改写 provider 可见的内置工具 description；SpikePlugin transform 高风险区，独立处理）
- [x] 保留 builtin read/edit/write/glob/grep/patch 原 schema / 原实现（no alias takeover；契约测试更新）

### Phase D — proof（第四阶段）— DONE
- [x] unit：generator equivalence / lying-generator counterexample（JS004 反例）/ four-layer exactness / anchor / read/glob/grep / mutation / transaction / recovery（EventStore facts）/ return / sandbox / coexistence（54 个 js-tools 单测全绿）
- [x] Long Stroke 受影响路径回归；`npm run check` GREEN（多次）
- [x] `npm run check:release` GREEN（EXIT 0：warmup → check → Long Stroke → package → npm pack）
- [x] proposal §107 Completion Criteria 勾选（用户裁决 2026-08-10：C-3 按共存满足，见 Blockers）

## Blockers

- **C-3（RESOLVED — 用户裁决，2026-08-10）**：`BuiltinToolDescriptionHook` 运行时接入。
  - 调查：OpenCode 插件 API 的 `chat.params` output 无 tools 数组；`tool.definition` hook（`@opencode-ai/plugin` `index.d.ts:314`）可改写 description，但 input 只有 `{ toolID }`——无 agent/session 上下文，无法推荐"当前 provider 可见的 js-ROLE"，会违反 JS-003 可见性约束。
  - **裁决：接受「钩子不接入、builtin 共存保持」作为 §107 Builtin Coexistence 的满足方式**。builtin read/edit/write/glob/grep/patch 原 schema / 原实现保留，与 js-* 并存（权限矩阵 + 契约测试 + host-hooks 测试证明）；`BuiltinToolDescriptionHook.annotate` / `validateRecommendation` / `hookSuffix` 保留为已交付纯函数（幂等 + 不可见推荐 fail-closed），接入点（`tool.definition`）确定后可直接挂载。

## Completion criteria

以 proposal §107（Authority/Generator/SDK/Builtin Coexistence + Hook/File API/Primitive Semantics/Return/Transaction/Security/Migration/Parallel/Proof；Student 部分按 Amendment JS-G3 排除）+ Playbook G5 Exit Gate（no handwritten role→JS matrix / no Student/Teacher JS / no Meditator filesystem JS / five-layer equivalence / transaction atomicity / crash recovery / sandbox escape RED / legacy implementation absent）为准；另以 `npm run check` 全绿为准。

## Blockers

无（待实施中发现则追加）。

---

## Final outcome

**G5（JS Capability-Projected Tools）已收口**（2026-08-10；用户裁决 C-3 后）：

1. **Capability-projected surface 全链路交付**：`AttemptExecutionProfile.ToolCapabilitySet` → `JsToolGenerator` 确定性生成 js-ROLE（name/schema/description/base class/examples/runtime bindings，四层同构）→ `ToolRegistry` 按角色矩阵注册（无手写 spec）→ `rolePredicate` js-* gate（角色名 + fs capability 双重校验，forged fail-closed）→ `StaticTools` 权限矩阵（js-* allow 仅当角色有 fs capability）→ vm sandbox 执行（无 ambient authority；vm timeout + 递归 deadline proxy；output bound）→ bindings（path boundary；staging-only）→ preflight（同路径单意图/目标存在性/新鲜度）→ EventStore durable facts（`JsTransactionPrepared`/`JsTransactionCommitted`；Prepared 先于任何 fs 效果、Committed 后置；crash recovery 仅撤销可证明写入的效果）→ all-or-nothing commit → Synthetic TOML 稳定结果形状（JS-016）。
2. **Builtin coexistence**：read/edit/write/glob/grep/patch 原 schema / 原实现保留（契约测试 + host-hooks 证明），与 js-* 并存，无 alias takeover；引流钩子按用户裁决不接入运行时（平台限制：`tool.definition` 无 agent 上下文），`BuiltinToolDescriptionHook` 保留为已交付纯函数。
3. **G3 rebase debt 未复活**：js-student / js-teacher 无任何实现；Meditator 无 filesystem js 面（`js-meditator` deny + 无 spec）；`js-surface-gate` 静态门禁 fail-closed。
4. **最终验证**：
   - js-tools 单测 54 个全绿（surface 10 / sandbox 8 / anchors 3 / transaction 5 / fs 8 / bindings 7 / workflow 7 / tx-store 4 / host 3）
   - `npm run check` GREEN（多次）
   - `npm run check:release` GREEN（EXIT 0）
   - Long Stroke e2e GREEN（48 steps / 5s 级 / ceilings 367/367；无回归）
5. **§107 满足性（用户裁决后）**：Authority / Generator / SDK / File API / Primitive Semantics / Return / Transaction（含 EventStore durable + crash recovery）/ Security / Migration / Parallel（Host 串行合同）/ Proof 全勾选；Builtin Coexistence + Hook 按「共存 + 钩子函数交付、不接入运行时」满足；Student 项按 Amendment JS-G3 排除。

**Gate 移交**：G5 Exit 达成 → 按 Playbook §0.1/§14，下一步 G6（perm-inspector + Universal Casebook completion）。
