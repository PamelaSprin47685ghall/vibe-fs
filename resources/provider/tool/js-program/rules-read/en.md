Read with `cat(path)`: it returns the file as a string and registers the read snapshot used by the write-before FILE_CHANGED check. A missing path is FILE_NOT_FOUND, invalid encoding is INVALID_UTF8. `stat(path)` returns `{ exists, size, mtime, isDir }` — a missing path is `exists: false`, not an error.

Locate with `where` / `whereAll`:

- `whereAll(content, needle, from = 0)` returns every normalized match as `[[begin, end], ...]`.
- `where(content, needle, from = 0)` returns the single span. Zero or many matches suspend the program through the same channel as `park`, with `{ needle, candidates, nearest }`; resume by injecting the chosen span.

Normalization collapses whitespace runs to one space and trims the needle; offsets always address the original string. `where` never throws a locating error and never silently picks a match: uniqueness is mechanical, ambiguity is yours.

Recompose with slices — do not re-search what you already located:

```js
const text = await this.cat(path);
const [begin, end] = await this.where(text, "old");
this.write(path, text.slice(0, begin) + "new" + text.slice(end));
```

`Promise.all` / `Promise.allSettled` give you parallel IO inside one program; the host adds no lock or queue, and a failed sibling never undoes a write that already landed. Same-file races are yours.

I have already paid for this mistake. I once treated a roughly 8k-line reorganization as manual string surgery: `indexOf`, `substring`, then join. One run turned it into roughly 31k lines, and I then spent more calls guessing at the damage — scanning for repeated headings and scraping away leftovers. Raw search was finding candidates, not owning structure. The primitives had already given me an immutable snapshot and exact spans; by computing boundaries by hand I threw those guards away and recreated the bugs they exist to prevent.

So when the job is structural — keep these spans, drop those spans, reorder these sections — locate the structure first, then splice only the slices that belong in the target file. Do not let familiarity impersonate facts: `indexOf` feels simple because you have seen it thousands of times; that familiarity says nothing about whether it owns this file's structure. The primitive with the stronger contract gets the presumption.
