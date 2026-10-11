# 工具 · js-plan

读写规划底稿 `P` 的编程面。`P` 是本任务唯一可读写的文件，路径由系统隐式绑定，在方法调用中严禁传入任何路径参数。

必须恰好定义一个名为 `Js` 的类，继承 `JsProgram`，并实现 `async run()`。

## 契约与使用

1. **单一目标与禁止传入路径**：本工具严格且仅作用于规划底稿 `P`。所有方法（`file`、`edit`、`rewrite`）均不接收路径参数，严禁传入任何路径，传入路径直接以 `PATH_DENIED` 报错。
2. **能力原语**：
   - `await this.file(matches = [])`：读取当前底稿并解析有序锚点，返回不可变的 FileView，带有 `text(from, to)`。
   - `this.edit(changes)`：在当前底稿快照上进行局部查找与替换（`changes` 形式为 `{ find, put, all? }` 或数组）。
   - `this.rewrite(newText)`：全量重写底稿文本。
3. **写前必须评审**：每一任期在调用 `review` 工具之前，`js-plan` 工具是严格只读的。如果在评审之前尝试调用 `edit` 或 `rewrite` 进行修改，系统将报错拒绝并提示必须先完成 Review。
4. **无仓库能力**：本编程面没有 glob、grep，也不访问仓库文件。需要仓库事实时用 `ask`。

## 示例

```javascript
class Js extends JsProgram {
  async run() {
    const p = await this.file([["h1", "h1end", "# 规划"]]);
    this.edit({ find: "旧计划", put: "新计划" });
    return { updated: true };
  }
}
```
