旧的 `glob`/`grep` 成员已退役。枚举与搜索改为 `ls` + `cat` + `whereAll` 的组合；`ls` 已经应用工作区的 `.gitignore` 规则（逐级 `.gitignore` 加上根 `.git/info/exclude`），因此从工作区根开始的 walk 看到的正是 git 感知枚举器会看到的东西。

## 递归 walk

```js
async function walk(dir = ".") {
  const found = [];
  const entries = await this.ls(dir);
  for (const entry of entries) {
    const full = dir === "." ? entry : `${dir}/${entry}`;
    if (entry.endsWith("/")) {
      found.push(...(await walk.call(this, full.slice(0, -1))));
    } else {
      found.push(full);
    }
  }
  return found;
}
```

边走边筛（`entry.endsWith(".fs")`、`entry.includes("retry")`），大树上递归才不会失控。

## 内容搜索

```js
const hits = [];
for (const path of await walk.call(this, "src")) {
  const text = await this.cat(path);
  for (const [begin, end] of this.whereAll(text, "RetryPolicy")) {
    hits.push({ path, span: [begin, end] });
  }
}
```

`whereAll` 是规范化精确匹配（空白折叠）。要正则就先用 JS `RegExp` 过滤 walk 出来的路径，或自己对 `text.split("\n")` 判断——原语只承诺文件访问与规范化搜索。

## 旧成员覆盖的写法

- `glob("src/**/*.fs")` → 从 `src` walk，保留 `path.endsWith(".fs")`。
- `glob("{src,tests}/**")` → 一个循环里对每个根各 walk 一次。
- `grep(needle, "*.txt")` → walk、保留 `path.endsWith(".txt")`，再 `whereAll`。
- 被忽略的路径不会出现：`ls` 已经过滤，walk 自然也看不到。
