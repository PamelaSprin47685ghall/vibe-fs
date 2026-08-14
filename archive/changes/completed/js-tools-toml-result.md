> 本文件是历史变更记录，不是当前产品规范。
> 当前产品语义仅以 `docs/` 正式层为准。

# JS Tools — 结构化 TOML 结果面

## Summary

`js-ROLE` 工具结果已经声称走 Synthetic TOML（JS-016 / ARCH-010），实际把 `run()` 的 JSON
`stringify` 进一个字符串字段。LLM 读到的是：

```toml
status = "ok"
result = "{\"fileCount\":256,\"greps\":{...}}"
written = "a.txt,b.md"
created = ""
```

外壳是 TOML，值树是转义 JSON，提交报告是逗号拼接字符串。两套语法叠在一起，既不能当
JSON 解析，也不能当 TOML 扫读。空 `created = ""` 是噪声；路径含逗号时 `written` 不可消歧。

本 Change 重做 **js-\* 工具结果这一个 LLM-visible surface**：

1. 沙箱合同仍是 JSON 兼容值（JS-010）；
2. 渲染把值树交给 `SyntheticToml` 编成真正的 field / table / array-of-tables；
3. Host 事实与程序值分表，禁止同键竞争；
4. 成败不再用 `status` discriminator。

Compatibility：**CleanBreak**，范围仅限 `js-*` 自定义 tool 文本结果 wire。不改 builtin
`read`/`edit`/`write`/`glob`/`grep`/`patch` 的结果形状，不改 EventStore 事实，不做旧
结果字符串迁移。激活后旧 golden（`status = "ok"` / `result = "{\\"` / 逗号拼接
`written`）作废并重写。

---

# 0. Executive Decision

`run()` 返回值继续是沙箱边界上的 JSON 兼容值。LLM 看见的是 ARCH-010 文档，不是 JSON 文本。

Host 事实和程序值禁止进入同一张表。程序可以返回名叫 `status` 的字段；那是它的数据，不是
工具成败。

两份文档，不要 discriminator：

- 失败：`# failed` + 根级 `code` / `reason`。无程序值，无提交表。
- 成功：可选一行 `# ok`；程序值进入 `data`；有磁盘效果时文末 `[fs]`。

`SyntheticToml` 是唯一字符串与值树 owner。js-coder 不私造第二套引号方言。Blogger 等现有
字符串面可继续只用 `renderString`；本 Change 给 codec 补值树，不强迫其它 surface 立刻改。

---

# 1. 当前病灶

`JsToolsResult.render`（`JsToolWorkflow.fs`）今天：

```text
Succeeded(resultJson, written, created)
  → status = "ok"
  → result = renderString(resultJson)     // JSON 文本当字符串
  → written = renderString(String.concat "," written)
  → created = renderString(String.concat "," created)
```

JS-016 写「经 Synthetic TOML 渲染」，只完成了信封，没完成值。`SyntheticToml` 目前只有
`renderString` / `field` / `tableArrayEntry` / `document`，所以第二表面一出现就只能把
结构化值压回字符串——这是 codec 能力缺口，不是 js-coder 的品味问题。

`tests/unit/js-tools/js-workflow.test.mjs` 的 JS-016 golden 把转义 JSON 锁成了正确形状。
那是在固化缺陷。

---

# 2. 分层

```text
sandbox run()
  → JSON-compatible value          JS-010；commit 前校验
  → JsToolOutcome                  结构化值 + 提交路径；禁止只留 stringify 文本
  → SyntheticToml 值树渲染         ARCH-010 唯一 owner
  → tool 文本结果                  ARCH-012 字节/行 bound
```

Workflow 必须把结构化值传到 renderer。禁止 `JSON.stringify` 后再包一层 TOML 字符串。

失败码集合仍是 JS-019。失败发生在 Host 裁决，不携带半截程序 `data`。

---

# 3. 两份文档

## 3.1 失败

```toml
# failed

code = "FILE_NOT_FOUND"
reason = "target file does not exist: a.txt"
```

- instruction：恰好一行 `# failed`。不把 code 再写进注释。
- data：根级 `code`、`reason`（JS-019 稳定码与可读 reason）。
- 无 `[data]`，无 `[fs]`，无 `status`。

程序 `throw`、`INVALID_RETURN_VALUE`、`FILE_CHANGED`、事务失败全部走这一份。commit 未发生。

## 3.2 成功

```toml
# ok

[data]
paths = ["src/keep.fs", "readme.md"]
truncated = false
```

有磁盘效果时：

```toml
# ok

[data]
before = "x"

[fs]
rewritten = ["a.txt"]
created = ["notes.md"]
```

规则：

- `# ok` 一行；查询成功不要再写「nothing committed」。
- 程序值的唯一入口是 `data`（对象 → `[data]` 表；原始值/原始值数组 → 根级 `data = …`；
  对象数组 → `[[data]]`）。
- 没改文件则没有 `[fs]`。`[fs]` 里没有空数组、空字符串、缺席的键。
- `rewritten` 对应 `rewrite()`；`created` 对应 `write()`。路径用 inline 字符串数组，
  一行一个元素，禁止逗号拼接。
- `[fs]` 必须出现在所有 `data` 表之后（`document` 的 tables 生产者顺序；ARCH-012 留尾时
  提交事实仍在）。

顶层 `null` 成功：

```toml
# ok
```

无 data 体。`# ok` 可被 ARCH-012 从头截掉；成功的机械判别是：没有 `code`/`reason`，且不是
失败文档。顶层 null 且无 `[fs]` 时，截断后可能得到空文档——允许，因为查询 null 本来就没有
Host 事实。有 `[fs]` 时不得只剩空文档。

---

# 4. 值编码

JSON 值树 → TOML，由 `SyntheticToml` 一次编完。对象键按程序插入序；ARCH-010 仍把裸字段排到
表前面。`truncated = false` 出现在 `[[data.matches]]` 之上是 TOML 语义，不是排版事故；正式
how 必须写明。

| JSON | TOML |
|------|------|
| 对象 | 表。键可裸写则裸写（`[A-Za-z0-9_-]+`），否则 basic quoted key |
| 原始值（顶层） | `data = <scalar>` |
| 原始值数组 | inline array：`data = ["a", "b"]` 或嵌套字段同等 |
| 对象数组 | array of tables：`[[data]]` 或 `[[data.matches]]` |
| 顶层 `null` | 无 data 体 |
| 对象字段 `null` | 省略该键 |
| 数组元素 `null` | 非法（收紧 JS-010；TOML 不能诚实表示） |
| 同一数组混对象与原始值 | 非法 |
| 同一数组混对象数组与原始值数组 | 非法 |
| 整数（无小数、在 codec 冻结的有符号范围内） | TOML integer |
| 其它有限数 | TOML float，canonical（`1.5` 不是 `1.50`；整数不得写成 float） |
| 字符串 | 现有 `renderString`（无换行 basic；换行且 literal-safe 则 `'''`；否则 basic escape） |
| `true` / `false` | TOML boolean |
| 空对象 | `[data]` 无字段；若该对象不是根而是嵌套，发空表头，不伪造占位键 |
| 空数组 | `data = []` 或 `name = []`（这是程序值，不是 Host 空提交） |

嵌套对象只用表，不用 inline table。小而齐的原始值才用 inline array。

禁止用空表、空字符串、`{}` 假装 JSON `null`。

程序键 `data` / `fs` / `code` / `reason` 活在 `[data]` 内部（例如 `[data.fs]`），与 Host
`[fs]`、失败根级 `code` 不合体。TOML 同名表合并因此不可发生。

---

# 5. Codec 扩展（ARCH-010 owner）

`SyntheticToml` 增加值树 API，字符串规则不变：

```text
renderBool
renderInt
renderFloat
renderArray          // 原始值 inline array；非法混合拒绝
quotedKey
nested table blocks
array-of-tables blocks
```

js-tools renderer 只组 document：instruction 列表 + body blocks。它不决定引号、换行、
delimiter、裸字段/表排序——那些仍只在 `SyntheticToml.document` / `renderString`。

Blogger / Join / LWR 不在本 Change 范围。它们继续走现有字符串字段。不得借机改它们的
local schema。

---

# 6. JS-010 收紧

现条款允许 `null`、boolean、finite number、string、array、plain object。本 Change 在
**commit 前**把下列返回值定为 `INVALID_RETURN_VALUE`（与 `undefined` / `BigInt` / `NaN` /
`Infinity` / function / symbol / cyclic 同类）：

- 数组（任意深度）含 `null`
- 同一数组含对象与非对象
- 非有限数（已有）
- 对象键非字符串（已有；JS 对象键本来就是字符串）

对象字段 `null` 仍合法，渲染时省略。顶层 `null` 仍合法。

校验发生在 durable prepare / 磁盘 commit 之前（JS-013）。非法返回值不得留下半提交。

---

# 7. 截断（ARCH-012）

bound 打在整份渲染后 UTF-8：2000 行且 51200 字节；超限 marker + 确定性留尾。

instruction 在最前，超限时先丢 `# ok` / `# failed`。因此：

- 成败不靠那一行注释成立；
- 失败靠根级 `code` + `reason`；
- 成功靠有 `data` 和/或 `[fs]`、且没有失败 `code`；
- `[fs]` 放最后，改盘事实在留尾时优先还在。

`glob`/`grep` 自己的 `truncated` 仍是程序值字段，与 ARCH-012 工具结果截断不是同一件事。

---

# 8. 被拒方向

**把 JSON 美化后塞回 `result = '''...'''`。** 拒：合同仍是「JSON 文本当字符串」，只是换行了。

**保留 `status = "ok"|"failed"` 作根级 discriminator。** 拒：与程序自己的 `status` 键竞争；
成功文档不需要这个字段。

**程序对象扁平到文档根，Host 键靠保留字。** 拒：TOML 同名表静默合并；保留 `fs`/`commit`
是脚枪。包一层 `[data]` 比一份拒绝列表便宜。

**Host 提交报告放 instruction 注释（`# rewritten a.txt`）。** 拒：ARCH-012 留尾先丢注释；
路径是 data。注释只报告成败各一行。

**逗号拼接路径。** 拒：路径可含逗号。

**失败时附带半截 `[data]`。** 拒：失败意味着程序值未成为这次调用的事实。

**统一 `kind` / `origin` / `ok` 信封。** 拒：ARCH-011。局部最小 schema。

**js-tools 私有 TOML 方言（第二套 `"""`、自造 null 哨兵）。** 拒：ARCH-010 唯一 owner。

**从结果 TOML 反向解析控制流。** 拒：ARCH-011。golden / parseability 检查可以 parse；业务
不可以。

**激活时顺手改 Blogger/Join 的 local schema。** 拒：范围是 js-\* 结果面 + codec 值树能力。

---

# 9. 激活后正式层（本文件不定义 Clause）

本 Change 不发明正式 Clause ID。激活后按 GOV-003 更新：

| 层 | 文件 | 做什么 |
|----|------|--------|
| why | `docs/why/js-tools.md` | 拒 JSON-in-string；拒 `status` 信封；收紧数组 null 的理由 |
| why | `docs/why/synthetic-toml.md` | codec 从「只有字符串」扩展到值树；不改变 string 选择规则 |
| what | `docs/what/js-tools.md` JS-010 | 数组 null / 异构对象数组 → `INVALID_RETURN_VALUE` |
| what | `docs/what/js-tools.md` JS-016 | 两份文档形状；`[data]` / `[fs]`；无 `status`/`result`/`written`/`created` |
| what | `docs/what/js-tools.md` JS-019 | 不改失败码集合；失败文档形状指向 JS-016 |
| shape | `docs/shape/js-tools.md` | renderer 组 document；值树 API 属 SyntheticToml |
| shape | `docs/shape/synthetic-toml.md` | 值树是同一 owner 的能力，不是第二方言 |
| how | `docs/how/js-tools.md` | 渲染算法：outcome → instruction + data/fs blocks → document → ARCH-012 |
| how | `docs/how/synthetic-toml.md` | bool/int/float/array/table/quoted key 的决定性规则；裸字段先于表 |
| proof | `docs/proof/js-tools.md` | 废 JSON-string golden；锁新形状；非法返回值在 commit 前失败 |
| proof | `docs/proof/synthetic-toml.md` | 值树 inventory / golden；js-\* 结果面在列 |

JS-016 编号保留，改命题。不新开 JS-0xx，除非激活时发现 JS-016 装不下值编码细则——那时值
编码细则进 `how/synthetic-toml.md` 与 ARCH-010，不在 js-tools 另起一套记法。

---

# 10. Do not build

```text
doNotBuild:
  - status / result / written / created 兼容别名
  - 旧 JSON 字符串结果的读回 / 双写
  - 结果 TOML 的业务 parser
  - inline table 编码对象
  - """ 多行字符串
  - null 哨兵（空表、空字符串、~、undef）
  - js-transaction.db 或任何 feature store（既有禁令，重申）
  - 改 builtin filesystem 工具结果
  - 改 Blogger / Join / LWR local schema
```

---

# 11. Proof（激活后）

永久回归，禁止一次性探针：

- 成功对象 → `[data]` 字段 + 必要时 `[[data.*]]`；golden 不含 `result = "{`
- 成功原始值 → `data = …`
- 成功对象数组 → `[[data]]`
- 成功顶层 null → 只有 `# ok`（或 ARCH-012 后的空/留尾）
- 查询成功无 `[fs]`
- 有 rewrite/create → `[fs]` 在最后；路径数组；无空键
- 失败 → `# failed` + `code` + `reason`；无 `[data]`；磁盘未变
- `INVALID_RETURN_VALUE`：数组 null、异构对象数组；commit 未发生
- 程序返回 `{ fs: 1, code: "x" }` → 进入 `[data]`，不与 Host `[fs]` / 失败 `code` 合并
- 多行字符串走 `'''`；含 `'''` 的字符串走 basic escape（既有 ARCH-010）
- 裸字段出现在任何表头之前（既有 document 排序）
- ARCH-012：超限留尾仍能看见 `[fs]`（构造刚好超限的 `[data]` + 短 `[fs]`）
- parseability：渲染结果对 TOML 1.0 可 parse；测试可 parse，生产业务不可 parse 回控制流
- 既有 js-tools 事务 / sandbox / hook 测试改认新 wire，不削弱断言

门禁：`node scripts/checks/spec.mjs` → `npm run lint` → build → unit → 受影响 integration。
e2e 若断言旧 `status = "ok"` / JSON-in-string，一并改声明，不扭曲 canary 迎合旧生产。

---

# 12. 例子（激活后的目标 wire）

查询：

```toml
# ok

[data]
paths = ["src/keep.fs", "readme.md"]
truncated = false
```

Grep：

```toml
# ok

[data]
truncated = false

[[data.matches]]
path = "docs/what/js-tools.md"
line = 77
column = 4
text = "JS-016"
```

改文件：

```toml
# ok

[data]
before = "hello world"

[fs]
rewritten = ["a.txt"]
```

`PROGRAM_FAILED`：

```toml
# failed

code = "PROGRAM_FAILED"
reason = "program threw; see program error payload"
```

## Active work

Specification impact：提案 §9 所列 why/what/shape/how/proof。Compatibility = CleanBreak，仅限 `js-*` 自定义 tool 文本结果 wire。

Remaining work：

- 正式层对齐 JS-010 / JS-016 / ARCH-010 值树
- `SyntheticToml` 值树 codec + `JsToolsResult` 两份文档
- workflow：结构化值、commit 前数组 null / 异构对象数组拒绝
- 废止 `status` / `result` / `written` / `created` golden；补 parseability 回归

Completion criteria：提案 §11；`node scripts/checks/spec.mjs`；受影响 unit 绿；无旧信封别名。

Blockers：无。

## Final outcome

### Outcome

js-* 工具结果 CleanBreak 已闭环：LLM-visible wire 为 `# ok`/`# failed` + `[data]`/`[fs]`，不再把 JSON stringify 进 TOML 字符串字段。

### Final specification

- `docs/what/js-tools.md` JS-010 / JS-016 / JS-019
- `docs/how/js-tools.md` 主流程与 Result 渲染
- `docs/how/synthetic-toml.md` Value tree
- `docs/why/js-tools.md`、`docs/why/synthetic-toml.md` 被拒方向
- `docs/shape/js-tools.md`、`docs/shape/synthetic-toml.md` owner
- `docs/proof/js-tools.md`、`docs/proof/synthetic-toml.md` 证明义务

### Implementation result

- `SyntheticToml.DataValue` + `encodeData` / `encodeFs` / `tableEntry`
- `JsToolsData.parse`：commit 前拒绝数组 `null` 与异构对象数组
- `JsToolsResult.render`：两份文档；无 `status` / `result` / `written` / `created`

### Verification

- `npm run build` ok（Fable 355/355）
- `tests/unit/js-tools/js-workflow.test.mjs` 11/11
- `tests/unit/js-tools/js-tool-host.test.mjs` 3/3
- `tests/unit/context/synthetic-toml.test.mjs` 24/24

### References

- `src/Wanxiangshu/Domain/SyntheticToml.fs`
- `src/Wanxiangshu/Infrastructure/OpenCode/Tools/JsToolWorkflow.fs`
