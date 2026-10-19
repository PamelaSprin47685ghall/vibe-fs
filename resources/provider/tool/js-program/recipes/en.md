The old `glob`/`grep` members are retired. Enumeration and search are compositions over `ls` + `cat` + `whereAll`; `ls` already applies the workspace `.gitignore` rules (per-directory `.gitignore` plus the root `.git/info/exclude`), so a walk that starts at the workspace root sees exactly what a git-aware enumerator would.

## Recursive walk

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

Pick only what you need while walking (`entry.endsWith(".fs")`, `entry.includes("retry")`) so the recursion stays cheap on a large tree.

## Content search

```js
const hits = [];
for (const path of await walk.call(this, "src")) {
  const text = await this.cat(path);
  for (const [begin, end] of this.whereAll(text, "RetryPolicy")) {
    hits.push({ path, span: [begin, end] });
  }
}
```

`whereAll` is the normalized exact matcher (whitespace runs collapse). For a regex, filter the walked paths with JS `RegExp` before reading, or test `text.split("\n")` yourself — the primitives only promise file access and normalized search.

## Patterns the old members covered

- `glob("src/**/*.fs")` → walk from `src` and keep `path.endsWith(".fs")`.
- `glob("{src,tests}/**")` → walk each root in one loop.
- `grep(needle, "*.txt")` → walk, keep `path.endsWith(".txt")`, then `whereAll`.
- Ignored paths never appear: `ls` filters them, so neither does the walk.
