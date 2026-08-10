namespace Wanxiangshu.Infrastructure.Persist

open System
open System.Text
open Fable.Core
open Fable.Core.JsInterop

/// Git's own object database, addressed directly (§2.3 / §6).
///
/// ── why this exists ─────────────────────────────────────────────────────────
///
/// `ProcessGitRawStore` reaches the ODB through one synchronous `git` child process per
/// primitive. Measured: a single-event append cost **24 spawns / ~60ms** (11 / ~29ms once
/// immutable reads and trees were memoized) against **0.5ms** for the identical EventStore
/// logic over the in-memory raw store, and one `fallback-aabb-trace` canary spawned 1197 git
/// processes totalling 2.4s of its 5.3s wall. Because `execFileSync` blocks the Node event
/// loop, those spawns also serialize every other session in the Host behind whichever one is
/// appending — cost that is neither CPU- nor IO-bound, just process-creation latency taken
/// under a global lock.
///
/// A loose object is `zlib(<type> <length>\0<body>)` at `objects/<oid[0..1]>/<oid[2..]>`, and
/// the oid is `sha1` of the same uncompressed bytes. Both are stable, documented Git formats,
/// so computing them here writes exactly the object `hash-object` / `mktree` would write —
/// `objects-identity.test.mjs` pins that equality against the real binary rather than trusting
/// this comment. Nothing about the store's semantics changes: same oids, same on-disk layout,
/// same `git cat-file` readability.
///
/// Packed objects are the one shape this cannot read: a pack is a delta-compressed archive, and
/// re-implementing its reader would be a second Git. A miss therefore delegates to the injected
/// CLI runner — the physical fallback for a repository someone has `gc`'d, not a compatibility
/// shim for a retired format.
module GitObjectDatabase =

    [<Import("createHash", "node:crypto")>]
    let private createHash (algorithm: string) : obj = jsNative

    [<Import("deflateSync", "node:zlib")>]
    let private deflateSync (buffer: obj) : obj = jsNative

    [<Import("inflateSync", "node:zlib")>]
    let private inflateSync (buffer: obj) : obj = jsNative

    [<Import("existsSync", "node:fs")>]
    let private existsSync (path: string) : bool = jsNative

    [<Import("mkdirSync", "node:fs")>]
    let private mkdirSync (path: string) (options: obj) : unit = jsNative

    [<Import("readFileSync", "node:fs")>]
    let private readFileSyncBinary (path: string) : obj = jsNative

    [<Import("writeFileSync", "node:fs")>]
    let private writeFileSyncBinary (path: string) (content: obj) : unit = jsNative

    [<Import("renameSync", "node:fs")>]
    let private renameSync (oldPath: string) (newPath: string) : unit = jsNative

    [<Emit("Buffer.concat($0)")>]
    let private bufferConcat (parts: obj array) : byte[] = jsNative

    [<Emit("Buffer.from($0, 'latin1')")>]
    let private latin1Buffer (text: string) : obj = jsNative

    [<Emit("Buffer.from($0, 'hex')")>]
    let private hexBuffer (text: string) : obj = jsNative

    [<Emit("Buffer.from($0)")>]
    let private asBuffer (bytes: byte[]) : obj = jsNative

    [<Emit("Buffer.from($0).toString('hex')")>]
    let private toHex (bytes: byte[]) : string = jsNative

    /// A unique sibling name for the tmp+rename write. Two processes writing the same object
    /// must not share a temp path, or one truncates the other's half-written file.
    [<Emit("$0 + '.tmp-' + process.pid + '-' + Math.random().toString(36).slice(2)")>]
    let private tempSibling (file: string) : string = jsNative

    let private ensureDirectory (directory: string) =
        if not (existsSync directory) then
            mkdirSync directory (createObj [ "recursive", box true ])

    let private readBytes (file: string) : byte[] =
        emitJsExpr (readFileSyncBinary file) "Buffer.from($0)"

    /// Write via temp + rename, the way Git writes a loose object: a reader either sees the
    /// complete object or no file at all, never a truncated prefix.
    let private writeBytesAtomic (file: string) (bytes: byte[]) =
        let temp = tempSibling file
        writeFileSyncBinary temp (asBuffer bytes)
        renameSync temp file

    /// `sha1` of the framed object bytes — the oid Git itself would assign.
    let private sha1Hex (framed: byte[]) : string =
        let hash = createHash "sha1"
        hash?update (asBuffer framed) |> ignore
        hash?digest ("hex") |> unbox<string>

    let private frame (objectType: string) (body: byte[]) : byte[] =
        let header = latin1Buffer (sprintf "%s %d\u0000" objectType body.Length)
        bufferConcat [| header; asBuffer body |]

    /// Object body plus its Git type, or None when the loose object is absent.
    let private tryReadLoose (objectsDir: string) (oid: string) : (string * byte[]) option =
        let file = objectsDir + "/" + oid.Substring(0, 2) + "/" + oid.Substring(2)

        if not (existsSync file) then
            None
        else
            let framed: byte[] =
                emitJsExpr (inflateSync (asBuffer (readBytes file))) "Buffer.from($0)"

            let separator = Array.IndexOf(framed, 0uy)

            if separator < 0 then
                None
            else
                let header = Encoding.UTF8.GetString(framed, 0, separator)
                let body = framed.[separator + 1 ..]

                match header.Split(' ') with
                | [| objectType; _ |] -> Some(objectType, body)
                | _ -> None

    /// Write the framed object unless the oid already exists. Returns the oid either way.
    let private writeLoose (objectsDir: string) (objectType: string) (body: byte[]) : string =
        let framed = frame objectType body
        let oid = sha1Hex framed
        let directory = objectsDir + "/" + oid.Substring(0, 2)
        let file = directory + "/" + oid.Substring(2)

        if not (existsSync file) then
            ensureDirectory directory

            let compressed: byte[] =
                emitJsExpr (deflateSync (asBuffer framed)) "Buffer.from($0)"

            writeBytesAtomic file compressed

        oid

    let writeBlob (objectsDir: string) (content: byte[]) : string = writeLoose objectsDir "blob" content

    /// Canonical tree body: `<mode> <name>\0<20-byte oid>` records in Git's own entry order,
    /// no separators. Git writes tree modes without the leading zero (`40000`, not `040000`).
    let private treeBody (entries: TreeEntry list) : byte[] =
        entries
        |> StoreTree.canonicalOrder
        |> List.map (fun entry ->
            let mode =
                if StoreTree.isTreeMode entry.Mode then
                    "40000"
                else
                    entry.Mode.TrimStart('0')

            let prefix = latin1Buffer (sprintf "%s %s\u0000" mode entry.Name)
            bufferConcat [| prefix; hexBuffer (GitObjectId.value entry.Oid) |] |> asBuffer)
        |> List.toArray
        |> bufferConcat

    let writeTree (objectsDir: string) (entries: TreeEntry list) : string =
        writeLoose objectsDir "tree" (treeBody entries)

    /// Parse a tree body back into entries. Modes are normalized by the caller's store rules.
    let private parseTree (body: byte[]) : TreeEntry list =
        let rec loop (offset: int) (acc: TreeEntry list) =
            if offset >= body.Length then
                List.rev acc
            else
                let separator = Array.IndexOf(body, 0uy, offset)

                if separator < 0 || separator + 20 >= body.Length then
                    List.rev acc
                else
                    let meta = Encoding.UTF8.GetString(body, offset, separator - offset)
                    let space = meta.IndexOf(' ')
                    let oidBytes = body.[separator + 1 .. separator + 20]

                    let oidHex = toHex oidBytes

                    if space < 0 then
                        loop (separator + 21) acc
                    else
                        let entry =
                            { Mode = StoreTree.normalizeMode (meta.Substring(0, space))
                              Name = meta.Substring(space + 1)
                              Oid = GitObjectId.create oidHex }

                        loop (separator + 21) (entry :: acc)

        loop 0 []

    /// Loose object bytes for `oid`, or None when it is packed / absent.
    let tryReadObject (objectsDir: string) (oid: string) : byte[] option =
        tryReadLoose objectsDir oid |> Option.map snd

    /// Loose tree entries for `oid`, or None when it is packed / absent / not a tree.
    let tryReadTree (objectsDir: string) (oid: string) : TreeEntry list option =
        match tryReadLoose objectsDir oid with
        | Some("tree", body) -> Some(parseTree body)
        | _ -> None

    // ── refs ────────────────────────────────────────────────────────────────
    //
    // `rev-parse --verify` + `update-ref` were the last two spawns left on the append path
    // (~4ms of the ~7.5ms). A ref is a 41-byte file and the update protocol is a lockfile, both
    // documented and both used verbatim here — so a concurrent `git` process, another plugin
    // instance and this code all serialize through the same `<ref>.lock`, exactly as before.

    [<Import("openSync", "node:fs")>]
    let private openSync (path: string) (flags: string) : int = jsNative

    [<Import("writeSync", "node:fs")>]
    let private writeSync (fd: int) (data: obj) : int = jsNative

    [<Import("closeSync", "node:fs")>]
    let private closeSync (fd: int) : unit = jsNative

    [<Import("unlinkSync", "node:fs")>]
    let private unlinkSync (path: string) : unit = jsNative

    [<Import("readFileSync", "node:fs")>]
    let private readFileText (path: string) (encoding: string) : string = jsNative

    [<Emit("(() => { try { return $0(); } catch { return null; } })()")>]
    let private tryOrNull (thunk: unit -> 'T) : 'T = jsNative

    let private isOid (text: string) =
        text.Length = 40
        && text |> Seq.forall (fun c -> Char.IsDigit c || (c >= 'a' && c <= 'f'))

    /// The ref's value: the loose ref file first, then `packed-refs` (a `gc`/`pack-refs` may have
    /// moved it there). Symrefs are not a store shape and are not followed.
    let tryReadRef (gitDir: string) (refName: string) : string option =
        let loose = gitDir + "/" + refName

        let fromLoose =
            if existsSync loose then
                let text = (readFileText loose "utf8").Trim()
                if isOid text then Some text else None
            else
                None

        match fromLoose with
        | Some oid -> Some oid
        | None ->
            let packed = gitDir + "/packed-refs"

            if not (existsSync packed) then
                None
            else
                (readFileText packed "utf8").Split('\n')
                |> Array.tryPick (fun line ->
                    let row = line.Trim()

                    if row = "" || row.StartsWith "#" || row.StartsWith "^" then
                        None
                    else
                        match row.Split(' ') with
                        | [| oid; name |] when name = refName && isOid oid -> Some oid
                        | _ -> None)

    /// Git's own lockfile protocol: create `<ref>.lock` exclusively, verify the current value is
    /// still what the caller expected, write the new value into the lock, rename it over the ref.
    /// A lost race — lock taken, or the ref moved — returns false, which is the CAS answer.
    let compareAndSwapRef (gitDir: string) (refName: string) (expectedOld: string option) (newOid: string) : bool =
        let refPath = gitDir + "/" + refName
        let lockPath = refPath + ".lock"
        ensureDirectory (refPath.Substring(0, refPath.LastIndexOf '/'))

        let fd = tryOrNull (fun () -> box (openSync lockPath "wx"))

        if isNull fd then
            false
        else
            let descriptor = unbox<int> fd

            let release () =
                closeSync descriptor
                tryOrNull (fun () -> box (unlinkSync lockPath)) |> ignore

            let current = tryReadRef gitDir refName

            if current <> expectedOld then
                release ()
                false
            else
                writeSync descriptor (latin1Buffer (newOid + "\n")) |> ignore
                closeSync descriptor
                let renamed = tryOrNull (fun () -> box (renameSync lockPath refPath))

                if isNull renamed && not (existsSync refPath) then
                    tryOrNull (fun () -> box (unlinkSync lockPath)) |> ignore
                    false
                else
                    true
