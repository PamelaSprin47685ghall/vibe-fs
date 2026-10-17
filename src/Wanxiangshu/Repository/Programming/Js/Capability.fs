namespace Wanxiangshu.Repository.Programming.Js

open Wanxiangshu.Foundation

/// DSL-class: Vocabulary — filesystem primitive capabilities projected into
/// the js-ROLE SDK. Pure projection of ToolPermission; never decided here
/// (JS-001: no second permission matrix).
[<RequireQualifiedAccess>]
type JsCapability =
    | Read
    | Write
    | Edit
    | Glob
    | Grep
    | Move
    | Remove
    /// Host-owned long-running command engineering (`js.run`).
    | Exec
    | Park

module JsCapability =

    /// The only ToolPermission → JsCapability mapping. Non-filesystem
    /// permissions produce no js-* member (JS-004 four-layer exactness).
    let ofToolPermission (permission: ToolPermission) : JsCapability option =
        match permission with
        | ToolPermission.Read -> Some JsCapability.Read
        | ToolPermission.Write -> Some JsCapability.Write
        | ToolPermission.Edit -> Some JsCapability.Edit
        | ToolPermission.Glob -> Some JsCapability.Glob
        | ToolPermission.Grep -> Some JsCapability.Grep
        | ToolPermission.Move -> Some JsCapability.Move
        | ToolPermission.Remove -> Some JsCapability.Remove
        | ToolPermission.Exec -> Some JsCapability.Exec
        | _ -> None

    let ofToolCapabilities (capabilities: Set<ToolPermission>) : Set<JsCapability> =
        capabilities |> Set.toList |> List.choose ofToolPermission |> Set.ofList

/// One capability's complete SDK projection: the single source for member
/// name, description, canonical example, and runtime binding key. The four
/// layers must stay identical or the surface lies (JS-002/004).
type JsCapabilityFragment =
    {
        Capability: JsCapability
        MemberName: string
        /// LLM-visible method signature in the public base class (JS-002).
        Signature: string
        Description: string
        CanonicalExample: string
        RuntimeBindingKey: string
    }

/// Fixed registry of all fragments: the only place a js-* member can be born.
module JsFragmentRegistry =

    let read: JsCapabilityFragment =
        { Capability = JsCapability.Read
          MemberName = "file"
          Signature = "async file(path, matches = [])"
          Description =
            "file(path, matches = []) — read a strict-UTF-8 immutable snapshot and "
            + "optionally resolve ordered begin/end anchors. Returns FileView with text(from, to)."
          CanonicalExample =
            "class Js extends JsProgram {\n"
            + "  async run() {\n"
            + "    const file = await this.file(\"README.md\");\n"
            + "    return file.text();\n"
            + "  }\n"
            + "}"
          RuntimeBindingKey = "js.read" }

    let glob: JsCapabilityFragment =
        { Capability = JsCapability.Glob
          MemberName = "glob"
          Signature = "async glob(pattern)"
          Description =
            "glob(pattern) — gitignore-style path enumeration. Returns "
            + "{ paths }. Does not grant Read on those paths."
          CanonicalExample =
            "class Js extends JsProgram {\n"
            + "  async run() {\n"
            + "    return await this.glob(\"src/**/*.fs\");\n"
            + "  }\n"
            + "}"
          RuntimeBindingKey = "js.glob" }

    let grep: JsCapabilityFragment =
        { Capability = JsCapability.Grep
          MemberName = "grep"
          Signature = "async grep(needle, pattern)"
          Description =
            "grep(needle, pattern) — search UTF-8 files selected by the same "
            + "gitignore-style glob. needle is a non-empty string or RegExp. Returns "
            + "{ matches } with 1-based line/column. Does not grant file()."
          CanonicalExample =
            "class Js extends JsProgram {\n"
            + "  async run() {\n"
            + "    return await this.grep(/TODO:.+/, \"src/**/*.js\");\n"
            + "  }\n"
            + "}"
          RuntimeBindingKey = "js.grep" }

    let edit: JsCapabilityFragment =
        { Capability = JsCapability.Edit
          MemberName = "edit"
          Signature = "edit(path, changes)"
          Description =
            "edit(path, changes) — atomically apply one { find, put, all? } change or a non-empty "
            + "array against one immutable target snapshot. Exact by default; 0, ambiguous, or "
            + "overlapping matches fail before staging."
          CanonicalExample =
            "class Js extends JsProgram {\n"
            + "  async run() {\n"
            + "    this.edit(\"src/foo.js\", [\n"
            + "      { find: \"const oldName = 1;\", put: \"const newName = 1;\" },\n"
            + "    ]);\n"
            + "    return { edited: \"src/foo.js\" };\n"
            + "  }\n"
            + "}"
          // edit() is a derived SDK affordance over the same guarded staging
          // executor as rewrite(); no second Edit permission exists.
          RuntimeBindingKey = "js.edit" }

    let rewrite: JsCapabilityFragment =
        { Capability = JsCapability.Edit
          MemberName = "rewrite"
          Signature = "rewrite(path, newText)"
          Description =
            "rewrite(path, newText) — stage replacement of an existing UTF-8 file. "
            + "Missing target is FILE_NOT_FOUND. Commits with the program (all-or-nothing)."
          CanonicalExample =
            "class Js extends JsProgram {\n"
            + "  async run() {\n"
            + "    this.rewrite(\"src/foo.js\", \"export const value = 2;\\n\");\n"
            + "    return { rewritten: \"src/foo.js\" };\n"
            + "  }\n"
            + "}"
          RuntimeBindingKey = "js.edit" }

    let write: JsCapabilityFragment =
        { Capability = JsCapability.Write
          MemberName = "write"
          Signature = "write(path, newText)"
          Description =
            "write(path, newText) — stage creation of a missing UTF-8 file. "
            + "Existing target is FILE_ALREADY_EXISTS. Commits with the program."
          CanonicalExample =
            "class Js extends JsProgram {\n"
            + "  async run() {\n"
            + "    this.write(\n"
            + "      \"generated/version.txt\",\n"
            + "      \"1.2.3\\n\"\n"
            + "    );\n"
            + "    return { created: \"generated/version.txt\" };\n"
            + "  }\n"
            + "}"
          RuntimeBindingKey = "js.write" }

    let rm: JsCapabilityFragment =
        { Capability = JsCapability.Remove
          MemberName = "rm"
          Signature = "rm(path)"
          Description =
            "rm(path) — delete a file or an empty directory; refuse a non-empty "
            + "directory. Takes effect immediately on success (as-is); FILE_CHANGED "
            + "is checked before the write like edit. The first irreversible "
            + "programming-surface operation; durable accounting is a single entry."
          CanonicalExample =
            "class Js extends JsProgram {\n"
            + "  async run() {\n"
            + "    this.rm(\"generated/scratch.tmp\");\n"
            + "    return { removed: \"generated/scratch.tmp\" };\n"
            + "  }\n"
            + "}"
          RuntimeBindingKey = "js.rm" }

    let mv: JsCapabilityFragment =
        { Capability = JsCapability.Move
          MemberName = "mv"
          Signature = "mv(from, to)"
          Description =
            "mv(from, to) — move or rename a file or directory (POSIX mv); "
            + "overwrite is allowed and a missing source is an error. Inherits the "
            + "programming-surface protections: resolveInside path boundary, "
            + "FILE_CHANGED before the write, single-entry durable accounting. "
            + "Cross-device (EXDEV) moves must error and roll back, never leaving "
            + "both sides present."
          CanonicalExample =
            "class Js extends JsProgram {\n"
            + "  async run() {\n"
            + "    this.mv(\"src/old.js\", \"src/new.js\");\n"
            + "    return { moved: \"src/old.js -> src/new.js\" };\n"
            + "  }\n"
            + "}"
          RuntimeBindingKey = "js.mv" }

    let cat: JsCapabilityFragment =
        { Capability = JsCapability.Read
          MemberName = "cat"
          Signature = "async cat(path)"
          Description =
            "cat(path) — read a strict-UTF-8 file as a string and register it as the "
            + "current read snapshot for the write-before FILE_CHANGED check. A missing "
            + "path is FILE_NOT_FOUND; non-UTF-8 content is INVALID_UTF8."
          CanonicalExample =
            "class Js extends JsProgram {\n"
            + "  async run() {\n"
            + "    const text = await this.cat(\"README.md\");\n"
            + "    return { bytes: text.length };\n"
            + "  }\n"
            + "}"
          RuntimeBindingKey = "js.cat" }

    let ls: JsCapabilityFragment =
        { Capability = JsCapability.Glob
          MemberName = "ls"
          Signature = "async ls(path)"
          Description =
            "ls(path) — list one directory in sorted order. Directory entries end "
            + "with \"/\". Non-recursive: recursion is the glob composition. A missing "
            + "path is FILE_NOT_FOUND."
          CanonicalExample =
            "class Js extends JsProgram {\n"
            + "  async run() {\n"
            + "    return await this.ls(\"src\");\n"
            + "  }\n"
            + "}"
          RuntimeBindingKey = "js.ls" }

    let stat: JsCapabilityFragment =
        { Capability = JsCapability.Read
          MemberName = "stat"
          Signature = "async stat(path)"
          Description =
            "stat(path) — return { exists, size, mtime, isDir }. A missing path is "
            + "a normal observation with exists = false, not a failure."
          CanonicalExample =
            "class Js extends JsProgram {\n"
            + "  async run() {\n"
            + "    const info = await this.stat(\"package.json\");\n"
            + "    return info;\n"
            + "  }\n"
            + "}"
          RuntimeBindingKey = "js.stat" }

    let mkdir: JsCapabilityFragment =
        { Capability = JsCapability.Write
          MemberName = "mkdir"
          Signature = "mkdir(path)"
          Description =
            "mkdir(path) — create one directory (POSIX, non-recursive). Existing "
            + "target is DIRECTORY_EXISTS; a missing parent is PARENT_DIRECTORY_MISSING. "
            + "Structural operation with no durable mutation entry."
          CanonicalExample =
            "class Js extends JsProgram {\n"
            + "  async run() {\n"
            + "    this.mkdir(\"generated\");\n"
            + "    this.write(\"generated/version.txt\", \"1.2.3\\n\");\n"
            + "    return { ok: true };\n"
            + "  }\n"
            + "}"
          RuntimeBindingKey = "js.mkdir" }

    let whereAll: JsCapabilityFragment =
        { Capability = JsCapability.Read
          MemberName = "whereAll"
          Signature = "whereAll(content, needle, from = 0)"
          Description =
            "whereAll(content, needle, from = 0) — normalized exact search: whitespace "
            + "runs collapse to one space. Returns every match as [[begin, end], ...] in "
            + "original UTF-16 offsets; no match returns []."
          CanonicalExample =
            "class Js extends JsProgram {\n"
            + "  async run() {\n"
            + "    const text = await this.cat(\"src/foo.js\");\n"
            + "    return this.whereAll(text, \"const value\");\n"
            + "  }\n"
            + "}"
          RuntimeBindingKey = "js.whereAll" }

    let where: JsCapabilityFragment =
        { Capability = JsCapability.Read
          MemberName = "where"
          Signature = "async where(content, needle, from = 0)"
          Description =
            "where(content, needle, from = 0) — exactly one normalized match returns "
            + "[begin, end]. Zero or many matches suspend the program through the park "
            + "channel with { needle, candidates, nearest }; unpark injects the chosen "
            + "span. The program always receives one span."
          CanonicalExample =
            "class Js extends JsProgram {\n"
            + "  async run() {\n"
            + "    const text = await this.cat(\"src/foo.js\");\n"
            + "    const [begin, end] = await this.where(text, \"const value\");\n"
            + "    this.write(\"src/foo.js\", text.slice(0, begin) + \"const next\" + text.slice(end));\n"
            + "    return { edited: true };\n"
            + "  }\n"
            + "}"
          RuntimeBindingKey = "js.where" }

    let run: JsCapabilityFragment =
        { Capability = JsCapability.Exec
          MemberName = "run"
          Signature = "async run(RunnableClass)"
          Description =
            "run(class extends js.Runnable { ... }) — the host-owned engineering loop. "
            + "The class must declare a positive maxDuration, next(ctx) and check(ctx); "
            + "the host runs each command, calls check once per interval slice, dispatches "
            + "onExit and enforces maxDuration. Returns "
            + "{ ok, status, exitCode, stdout, stderr, truncated, bytes, durationMs }."
          CanonicalExample =
            "class Js extends JsProgram {\n"
            + "  async run() {\n"
            + "    return await js.run(class extends js.Runnable {\n"
            + "      maxDuration = 60000;\n"
            + "      async next(ctx) {\n"
            + "        return ctx.step === 0 ? \"echo hello\" : null;\n"
            + "      }\n"
            + "      check(ctx) {\n"
            + "        return ctx.exited && ctx.exitCode !== 0 ? { abort: \"command failed\" } : true;\n"
            + "      }\n"
            + "    });\n"
            + "  }\n"
            + "}"
          RuntimeBindingKey = "js.run" }

    /// park fragment — the pause/resume mechanism (PLAN S1 B.7/B.17). Unlike
    /// the filesystem members this is not a ToolPermission projection: it is a
    /// lifecycle capability every js-* surface carries. The runtime binding is
    /// the deferred-Promise park member injected by ToolsBindings.
    let park: JsCapabilityFragment =
        { Capability = JsCapability.Park
          MemberName = "park"
          Signature = "async park(payload)"
          Description =
            "park(payload) — suspend this program at this await point and hand "
            + "control back to the orchestrator. The tool call returns a top-level "
            + "parked payload { handle, payload }; the caller resumes with "
            + "{ unpark: { handle, value } } or gives up with { abandon: { handle } }."
          CanonicalExample =
            "class Js extends JsProgram {\n"
            + "  async run() {\n"
            + "    const decision = await this.park({ stage: \"review\" });\n"
            + "    return { decided: decision.value };\n"
            + "  }\n"
            + "}"
          RuntimeBindingKey = "js.park" }

    /// Canonical member order inside capability families. Filtering this list
    /// is the deterministic projection; never rebuild member order elsewhere.
    let all: JsCapabilityFragment list =
        [ read
          glob
          grep
          edit
          rewrite
          write
          rm
          mv
          cat
          ls
          stat
          mkdir
          whereAll
          where
          run
          park ]

    let byCapability: Map<JsCapability, JsCapabilityFragment list> =
        all |> List.groupBy _.Capability |> Map.ofList
