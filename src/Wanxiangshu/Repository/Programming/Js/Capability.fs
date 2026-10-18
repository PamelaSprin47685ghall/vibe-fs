namespace Wanxiangshu.Repository.Programming.Js

open Wanxiangshu.Foundation

/// DSL-class: Vocabulary — filesystem primitive capabilities projected into
/// the js-ROLE SDK. Pure projection of ToolPermission; never decided here
/// (JS-001: no second permission matrix).
[<RequireQualifiedAccess>]
type JsCapability =
    | Read
    | Write
    /// Plan-artifact write capability. No fragment projects it — the plan
    /// tenure surface carries its own file/edit/rewrite API — but the plan
    /// description gates its artifact-write teaching blocks on it.
    | Edit
    | Glob
    | Move
    | Remove
    /// Host-owned long-running command engineering (`js.run`).
    | Exec
    /// Delegation primitives: create a child work (`fork`) and wait for the
    /// caller's children (`join`).
    | Fork
    | Join
    /// Same-identity multi-lane execution (`js.fission`).
    | Fission
    | Park

module JsCapability =

    /// The only ToolPermission → JsCapability mapping. Non-filesystem
    /// permissions produce no js-* member (JS-004 four-layer exactness).
    let ofToolPermission (permission: ToolPermission) : JsCapability option =
        match permission with
        | ToolPermission.Read -> Some JsCapability.Read
        | ToolPermission.Write -> Some JsCapability.Write
        | ToolPermission.Glob -> Some JsCapability.Glob
        | ToolPermission.Move -> Some JsCapability.Move
        | ToolPermission.Remove -> Some JsCapability.Remove
        | ToolPermission.Exec -> Some JsCapability.Exec
        | ToolPermission.Fork -> Some JsCapability.Fork
        | ToolPermission.Join -> Some JsCapability.Join
        | ToolPermission.Fission -> Some JsCapability.Fission
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

    let write: JsCapabilityFragment =
        { Capability = JsCapability.Write
          MemberName = "write"
          Signature = "write(path, content)"
          Description =
            "write(path, content) — overwrite an existing UTF-8 file or create a missing one; "
            + "one atomic as-is write, with the write-before FILE_CHANGED check against read snapshots."
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
          Signature = "js.run(class extends js.Runnable { ... })"
          Description =
            "js.run(class extends js.Runnable { ... }) — the host-owned engineering loop, "
            + "called on the global js object. this.run is YOUR program entry, never this "
            + "member: the names collide on purpose, so the engineering run never lands on "
            + "the base class. The class must declare a positive maxDuration, next(ctx) and "
            + "check(ctx); the host runs each command, calls check once per interval slice, "
            + "dispatches onExit and enforces maxDuration. next is a LOOP callback, not a "
            + "one-shot declaration: after every command exits the host calls next again, so "
            + "it must return null when the work is done — typically "
            + "ctx.step === 0 ? \"command\" : null. If next never returns null, the host "
            + "faithfully re-runs what it returns every round until maxDuration: the outcome "
            + "is status 'timeout' with the command output repeated once per round — read "
            + "steps to see how many rounds ran. next returns a command string | null | "
            + "{ spawn, name } (spawn is a shell command string) | { signal, name } | "
            + "{ send: { name, input } } (nested is canonical; the flat { send: input, name } "
            + "spelling is accepted as the same thing) | { waitFor: { port, timeoutMs } } — "
            + "check keeps firing while waitFor polls. check should observe before ruling: "
            + "read the ctx deltas, keep state on instance fields, then decide — a check "
            + "that returns true without looking at anything is a watchdog that is not "
            + "watching; a next or check that throws fails the whole program. check ctx is "
            + "{ root, stdout, stderr, exited, exitCode, backgrounds, elapsedMs, remainingMs } "
            + "where stdout/stderr are this slice's merged deltas of the foreground command "
            + "plus every background child; onExit(result) receives { root, exitCode, stdout, "
            + "stderr, elapsedMs } with that child's full transcript. Returns { ok, status, "
            + "reason, exitCode, stdout, stderr, truncated, bytes, durationMs, steps } — steps "
            + "counts loop rounds — or { ok: false, code } with RUNNABLE_SPEC_INCOMPLETE / "
            + "RUN_COMMAND_UNSUPPORTED / RUN_WAIT_TIMEOUT / RUN_CHILD_UNKNOWN / "
            + "RUN_PARK_UNSUPPORTED."
          CanonicalExample =
            "class Js extends JsProgram {\n"
            + "  async run() {\n"
            + "    return await js.run(class extends js.Runnable {\n"
            + "      maxDuration = 60000;\n"
            + "      async next(ctx) {\n"
            + "        return ctx.step === 0 ? \"echo hello\" : null;\n"
            + "      }\n"
            + "      check(ctx) {\n"
            + "        this.lastExit = ctx.exitCode;\n"
            + "        if (ctx.exited && ctx.exitCode !== 0) return { abort: \"command failed\" };\n"
            + "        return null; // observed, nothing alarming — continue\n"
            + "      }\n"
            + "    });\n"
            + "  }\n"
            + "}"
          RuntimeBindingKey = "js.run" }

    let fork: JsCapabilityFragment =
        { Capability = JsCapability.Fork
          MemberName = "fork"
          Signature = "async fork(payload)"
          Description =
            "fork(payload) — delegate one charge to a child work. `payload` is a string (the "
            + "charge) or { charge, name?, calling?, keywords? }; a missing name is derived from "
            + "the charge. Returns the data envelope { ok, name, agentId, outcome } "
            + "(outcome: created | nudged | commissioned) or { ok: false, code }."
          CanonicalExample =
            "class Js extends JsProgram {\n"
            + "  async run() {\n"
            + "    const placed = await this.fork(\"index the parser module\");\n"
            + "    if (!placed.ok) return placed;\n"
            + "    return await this.join();\n"
            + "  }\n"
            + "}"
          RuntimeBindingKey = "js.fork" }

    let dag: JsCapabilityFragment =
        { Capability = JsCapability.Fork
          MemberName = "dag"
          Signature = "async dag({ nodes })"
          Description =
            "dag({ nodes: [{ id, charge, deps?, lanes? }] }) — declarative dependency sugar the "
            + "base class compiles into fork waves plus one join per wave; dependent nodes receive "
            + "their dependencies' records. Returns { ok, results: { [id]: record }, skipped }. "
            + "A failed node parks with { dag, node, code, deps, done, pending }: inject "
            + "\"retry\", \"skip\", a replacement result, or { abort: reason }."
          CanonicalExample =
            "class Js extends JsProgram {\n"
            + "  async run() {\n"
            + "    return await this.dag({ nodes: [\n"
            + "      { id: \"spec\", charge: \"write the interface spec\" },\n"
            + "      { id: \"impl\", charge: \"implement it\", deps: [\"spec\"] },\n"
            + "    ] });\n"
            + "  }\n"
            + "}"
          RuntimeBindingKey = "js.dag" }

    let join: JsCapabilityFragment =
        { Capability = JsCapability.Join
          MemberName = "join"
          Signature = "async join()"
          Description =
            "join() — wait for this session's children and return "
            + "{ ok, records: [{ name, agentId, role, status, workRecord }], byName } "
            + "(status: completed | undelivered | failed | abandoned) or { ok: false, code }. Membership-scoped: "
            + "it collects the children this caller created, not an explicit handle list."
          CanonicalExample =
            "class Js extends JsProgram {\n"
            + "  async run() {\n"
            + "    await this.fork({ charge: \"index the parser module\" });\n"
            + "    return await this.join();\n"
            + "  }\n"
            + "}"
          RuntimeBindingKey = "js.join" }

    let fission: JsCapabilityFragment =
        { Capability = JsCapability.Fission
          MemberName = "fission"
          Signature = "async fission({ charge, lanes })"
          Description =
            "fission({ charge, lanes }) — run the same identity over several lanes of one charge; "
            + "the host binds and converges the lanes. Returns { ok, groupId, lanes: "
            + "[{ index, sessionId }] } or { ok: false, code }. The owner's join() collects the "
            + "lanes of this group."
          CanonicalExample =
            "class Js extends JsProgram {\n"
            + "  async run() {\n"
            + "    const group = await this.fission({ charge: \"explore three designs\", lanes: 3 });\n"
            + "    return await this.join();\n"
            + "  }\n"
            + "}"
          RuntimeBindingKey = "js.fission" }

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
        [ write
          rm
          mv
          cat
          ls
          stat
          mkdir
          whereAll
          where
          run
          fork
          dag
          join
          fission
          park ]

    let byCapability: Map<JsCapability, JsCapabilityFragment list> =
        all |> List.groupBy _.Capability |> Map.ofList
