# JavaScript Capability-Projected Tool Surface

## Summary

用一个**能力投影生成器**统一万象术传统的文件工具：

```text
read
edit
write
glob
grep
```

系统不再分别实现五套工具语义。

真正的 primitive 是：

> **一个由当前 Attempt 实际权限机械生成的、受 capability 约束的 JavaScript SDK。**

对于每次 provider request，万象术从唯一权威：

```text
AttemptExecutionProfile.ToolCapabilitySet
```

生成一个与当前 Agent、当前 RequestKind 实际能力**完全同构**的主工具，例如：

```text
js-coder
js-inspector
js-reviewer
js-devops
js-browser
js-meditator
js-student
js-teacher
```

生成结果同时包含：

```text
工具名称
工具 schema
工具描述
JsProgram 基类
允许出现的成员函数
canonical examples
read/edit/write/glob/grep aliases
runtime capability bindings
```

模型只需要完成一种任务：

```js
class Js extends JsProgram {
  async run() {
    // model program
  }
}
```

模型不再阅读一张权限矩阵，也不再记忆五种不同的 RPC 协议。

它只对一个准确生成的 SDK 编程。

核心原则：

> **能力不是写进工具说明里的；工具本身就是能力的投影。**

以及：

> **If a method is present, the capability exists.
> If a method is absent, it does not.**

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
       ├── familiar aliases
       └── runtime capability bindings
```

所有结果必须来自同一 registry。

禁止：

```text
权限表一份
tool description 一份
base class 一份
runtime switch 一份
alias 表再一份
```

这种设计最终一定漂移。

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

这对 Student 尤其重要：

```text
StudentLearn
StudentCompile
```

虽然 CanonicalRole 都是 Student，但能力完全不同。

Generator 不能再次发明 Student 状态判断。

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

> **derived affordance**

也就是给 LLM 的熟悉入口。

逻辑：

```text
grep alias visible
⇔ Read && Glob
```

第一版迁移期间如果现有 `ToolPermission.Grep` 尚未删除，它只能作为 compatibility assertion：

```text
legacy Grep bit
==
derived(Read && Glob)
```

任何 canonical profile 不满足这一等式：

```text
启动配置 fail fast
```

最终 runtime authority 不依赖 `Grep` bit。

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
js-student
js-teacher
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
aliases
runtime bindings
```

Tier 只改变模型绑定。

---

# 7. Alias Surface

传统名字继续全部保留，作为 LLM-facing affordances。

例如 Coder：

```text
js-coder
read
glob
grep
edit
write
```

其中：

```text
read
glob
grep
edit
write
```

全都是 `js-coder` 的 alias。

它们：

```text
schema 相同
description 除一句 alias 指引外无独立内容
执行入口相同
生成的 JsProgram 相同
runtime capabilities 相同
事务语义相同
```

推荐完整 alias description：

```text
Alias of js-coder. Same schema and semantics; see js-coder.
```

Inspector：

```text
Alias of js-inspector. Same schema and semantics; see js-inspector.
```

依此类推。

---

# 8. Alias 不是 Capability Scope

Alias 只解决模型的工具选择习惯。

它不是 security scope。

Coder 即使调用：

```text
read
```

然后 program 中使用：

```js
this.rewrite(...)
```

也合法。

因为：

```text
read
```

确实只是：

```text
js-coder
```

的另一个入口名字。

反过来，Inspector 调：

```text
read
```

时获得的是：

```text
js-inspector
```

生成出来的 SDK。

其基类里根本没有：

```js
rewrite()
write()
```

因此：

```text
alias name
```

永远不决定执行权限。

决定权限的是：

```text
当前 Attempt 生成出来的 capability projection
```

---

# 9. Alias Visibility

机械生成：

```text
Read
→ read

Glob
→ glob

Read + Glob
→ grep

Edit
→ edit

Write
→ write
```

并有硬不变量：

```text
任意 filesystem alias 可见
→ 对应 js-ROLE primary tool 必须同时可见
```

不能出现：

```text
read
glob
grep
```

都说：

```text
see js-inspector
```

但 provider 没有暴露 `js-inspector`。

---

# 10. Schema

所有生成的主工具与 alias 使用完全相同 schema：

```ts
type JsToolInput = {
  program: string
}
```

没有：

```text
path
oldString
newString
pattern
query
```

等顶层 RPC 参数。

这些都是 program 自己通过生成 SDK 表达的内容。

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

必须明确区分：

```text
Js.run() return
```

只是当前 JS tool call 的 observation/result。

它不会：

```text
结束 Student
回答 teacher
构造 RunCompletion
```

现有 Student / Teacher 专用：

```text
return
```

工具保持完全独立的 workflow 语义。

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
      Aliases: JsAliasSpec list
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

Aliases
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

Aliases
= edit
```

Write fragment：

```text
Capability
= Write

Members
= write()

RuntimeBindings
= stage creation of absent file

Aliases
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

Aliases
= glob
```

Grep 不拥有 runtime fragment。

它是 Read+Glob 的 derived alias/example.

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
read
glob
grep
edit
write
```

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
read
glob
grep
```

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
read
glob
grep
```

没有：

```text
rewrite
write
edit alias
write alias
```

PTY、executor、coder、join/list 等继续独立。

DevOps 仍不能直接修改文件。

---

## Browser

生成其 filesystem capability 对应：

```text
js-browser
read
glob
grep
```

网络工具仍独立。

---

## Meditator

生成：

```text
js-meditator
read
glob
grep
```

其它委派能力独立。

---

## Reviewer

生成：

```text
js-reviewer
read
glob
grep
```

基类是只读 SDK。

`verdict` 独立。

---

## StudentLearn

filesystem primitive capability 为空。

因此：

```text
不生成 js-student
不生成 read/edit/write/glob/grep aliases
```

保持：

```text
teacher
```

---

## StudentCompile

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
read
glob
grep
edit
write
```

Student 专用终态：

```text
return
```

继续独立。

---

## Teacher

不在 Generator 中硬编码。

由 Teacher 当前 AttemptExecutionProfile 实际 filesystem capabilities 投影。

内部 Agent 是否可被其它 Agent 创建，与其自身 provider request 能看到什么工具是两个不同问题。

---

## Manager / Orchestrator / Blogger / Executor

如果 filesystem primitive set 为空：

```text
无 js-* 工具
无五大 aliases
```

---

# 49. Student AGENT-022

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

ToolRegistry 收到：

```text
js-coder
read
edit
...
```

任一生成工具调用时：

1. 用 `ToolContext.messageID` 找到准确 Attempt；
2. 读取该 Attempt 的 immutable execution profile；
3. 重新得到或取缓存的 `GeneratedJsSurface`；
4. 验证 invoked tool name 属于该 surface；
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

---

# 55. Transaction Model

每一次：

```text
js-* / alias tool call
```

对应恰好一个：

```text
JsTransaction
```

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

所有 replacement 先进入 transaction-private durable staging。

概念：

```text
transaction/
  manifest
  originals/
  replacements/
```

每个 replacement：

```text
write full bytes
→ fsync
```

manifest 至少包含：

```text
transaction id
canonical target
preimage kind/digest
replacement digest
transaction state
```

manifest durable 后进入：

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
Prepared
→ final snapshot validation
→ apply mutations in canonical order
→ durable directory sync
→ mark Committed
→ only now expose successful tool result
→ cleanup
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

并保留 durable evidence。

---

# 66. Crash Recovery

最小 transaction state：

```text
Prepared
Committed
RolledBack
```

不要把程序计数器固化成几十个领域状态。

启动恢复读取：

```text
manifest
original digest
replacement digest
current filesystem state
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
```

无法证明安全恢复：

```text
fail closed
retain manifest
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

拥有完整描述。

Aliases 永远一句话。

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

# 71. Legacy Tool Implementations

这是 clean break。

最终 provider surface 中：

```text
read
edit
write
glob
grep
```

名称仍然存在。

但它们全部是 generated aliases。

旧 Host/native：

```text
read(path)
edit(oldString,newString)
write(path,content)
glob(pattern)
grep(pattern,path)
```

schema 不得继续同时暴露。

必须证明：

```text
provider-visible name count == 1
```

例如：

```text
edit
```

不能同时存在：

```text
legacy edit
generated edit alias
```

---

# 72. No Compatibility Schema

不接受：

```text
read({path})
```

也不接受：

```text
edit({
  oldString,
  newString
})
```

作为隐藏兼容模式。

所有五个 alias schema 都变成：

```ts
{
  program: string
}
```

如果需要迁移 fixture/canary，统一迁移。

不保留 dual semantics。

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
```

普通 Attempt：

```text
profile
→ generate static-equivalent surface
```

Student：

```text
StudentLearn profile
→ no JS surface

StudentCompile profile
→ js-student + aliases
```

切换 Compile 前必须先生成完整新 surface，再随整套 permission 原子安装。

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
alias specs
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

不要从 exception prose 反推领域 failure。

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
Infrastructure filesystem adapter
```

拥有：

```text
snapshot
staging
durable prepare
commit
rollback
recovery
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
JS-003  Alias projection
JS-004  Generated base-class exactness
JS-005  file()/FileView
JS-006  Ordered string/RegExp anchors
JS-007  glob()
JS-008  rewrite()
JS-009  write()
JS-010  JSON-compatible return
JS-011  Sandbox capability boundary
JS-012  Transaction staging
JS-013  Multi-file all-or-nothing commit
JS-014  Conflict detection
JS-015  Rollback/recovery
JS-016  Synthetic TOML result
JS-017  Clean break from legacy five tools
JS-018  Student request/path projection
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

### 为什么不是五个独立实现

拒绝：

```text
read implementation
edit implementation
write implementation
glob implementation
grep implementation
```

因为它们共享：

```text
path boundary
filesystem
result rendering
permissions
snapshot
string computation
```

会重复并漂移。

### 为什么是 generated SDK

拒绝万能基类 + prose permission warning。

因为：

```text
看得到无权限方法
```

本身就增加模型认知负担和误调用率。

### 为什么保留 aliases

因为：

```text
read
edit
write
glob
grep
```

是 LLM 训练中极强的工具选择 affordances。

保留名字对模型友好。

但实现语义不必跟着重复。

总结：

> **Alias 是 LLM UX，不是程序架构。**

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
legacy clean break
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
= SDK/runtime/alias description owner

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
Alias 不决定权限
Model JS 不拥有 ambient OS authority
Transaction engine 不执行模型 JavaScript
```

---

# 85. `how/js-tools.md`

唯一实现序：

```text
resolve Attempt
→ get immutable profile
→ generate surface
→ provider sees generated tools
→ model invokes primary/alias
→ ToolRegistry verifies invoked name belongs to same Attempt surface
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
GeneratedAliases(profile)
==
aliases implied by primitive capabilities
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
edit alias 可见
base class 无 rewrite()
```

必须红。

真正 proof 是：

> **LLM-visible SDK 与 executable authority 同构。**

---

# 88. Alias Tests

证明：

```text
primary tool schema
==
every alias schema
```

证明：

```text
alias execution
==
primary execution
```

特别测试：

```text
Coder invokes "read"
program uses rewrite()
→ succeeds
```

因为 alias 不是 scope。

同时：

```text
Inspector invokes "read"
forged program attempts rewrite
→ runtime fail closed
```

---

# 89. Role Projection Tests

至少：

```text
fast-coder == deep-coder JS surface
fast-inspector == deep-inspector JS surface
fast-reviewer == deep-reviewer JS surface
```

Student：

```text
StudentLearn
→ no js-student

StudentCompile
→ exact generated js-student
```

旧 Learn Attempt 伪造 Compile JS call：

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

---

# 94. Crash Recovery Tests

模拟：

```text
Prepared + zero applied
Prepared + subset applied
Prepared + all applied but not marked
```

恢复必须根据：

```text
manifest
preimage digest
replacement digest
current bytes
```

确定结果。

未确认成功的 incomplete transaction 默认：

```text
rollback toward original
```

Unknown：

```text
fail closed
retain evidence
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

# 97. Clean-break Tests

Provider-visible：

```text
read
edit
write
glob
grep
```

每个名字恰好一个 spec。

它们全部 schema：

```text
program
```

禁止 legacy fields：

```text
oldString
newString
path-as-top-level-read-arg
grep-query-schema
```

`js-ROLE` description 是唯一完整文档 owner。

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

---

# 102. E2E Canary — Student

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
无 legacy five-tool implementation specs
alias description 无复制长文
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
13. rewrite/write staging
14. transaction engine
15. return serializer
16. Synthetic TOML bridge
17. Agent surface migration
18. StudentCompile migration
19. suppress legacy five tools
20. aliases
21. unit tests
22. transaction/recovery tests
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
return(Student/Teacher)
```

继续保持独立工具。

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
aliases
description fragment
examples
```

Generator 自动为合法 Agent surface 投影。

是否增加一个传统 alias：

```text
rm
mv
```

只是 LLM UX 决策。

不是新的系统 primitive。

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

## Alias

* read/edit/write/glob/grep 全为 generated aliases；
* aliases 与 primary schema/semantics 完全相同；
* alias 不形成 security scope；
* primary js-ROLE 始终与 aliases 同时可见。

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
* durable prepare；
* all-or-nothing normal outcome；
* rollback；
* crash recovery；
* success result 只在 commit 完成后暴露。

## Security

* arbitrary JavaScript 无 ambient Host authority；
* fs/network/process/env 等不可直接获得；
* timeout 可 kill；
* memory/output bounded；
* runtime gate 不依赖模型遵守 base class。

## Student

* StudentLearn 无 JS surface；
* StudentCompile exact projected surface；
* AGENT-022 对每个 read/write/edit target 生效；
* 任一非法 mutation target 使整个 transaction 零提交。

## Migration

* 五个 legacy implementations 移除；
* 五个 familiar names 保留为 aliases；
* legacy schemas 移除；
* provider 同名 spec 无重复。

## Proof

* generator equivalence；
* lying-generator counterexample；
* alias semantics；
* anchor/regex；
* read/glob/grep；
* write/rewrite；
* structured return；
* multi-file transaction；
* conflict；
* rollback；
* crash recovery；
* sandbox；
* Student；
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

传统五大工具：

```text
read
edit
write
glob
grep
```

仍然存在，是因为这些名字对 LLM 极其友好。

但它们只是进入同一个精确生成 SDK 的五扇熟悉的门。

系统内部只有一个真正的抽象：

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
