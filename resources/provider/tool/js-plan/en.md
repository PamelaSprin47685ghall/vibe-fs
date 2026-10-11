# Tool · js-plan

The programming surface for reading and writing the planning draft `P`. `P` is the only readable and writable file of this task; its path is bound implicitly by the system, and passing any path argument in method calls is strictly forbidden.

You must define exactly one class named `Js` that extends `JsProgram` and implements `async run()`.

## Contract and Usage

1. **Sole Target & Forbidden Paths**: operates strictly and solely on the planning draft `P`. All methods (`file`, `edit`, `rewrite`) take no path argument; passing any path will be rejected immediately with `PATH_DENIED`.
2. **Capability Primitives**:
   - `await this.file(matches = [])`: reads the current draft and resolves ordered anchors, returning an immutable FileView with `text(from, to)`.
   - `this.edit(changes)`: applies local find-and-replace patches on the draft snapshot (`changes` is `{ find, put, all? }` or an array of them).
   - `this.rewrite(newText)`: replaces the entire draft text.
3. **Review Required Before Write**: in each incumbency, `js-plan` is strictly read-only before calling the `review` tool. Any attempt to modify the draft via `edit` or `rewrite` prior to review will be rejected with an error instructing you to call Review first.
4. **No Repository Access**: this surface has no glob, no grep, and no access to repository files. For repository facts use `ask`.

## Example

```javascript
class Js extends JsProgram {
  async run() {
    const p = await this.file([["h1", "h1end", "# Plan"]]);
    this.edit({ find: "old step", put: "new step" });
    return { updated: true };
  }
}
```
