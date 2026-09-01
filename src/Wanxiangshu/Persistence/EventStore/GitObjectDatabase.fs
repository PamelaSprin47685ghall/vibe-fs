namespace Wanxiangshu.Persistence.EventStore

open Wanxiangshu.Enforcer
open Wanxiangshu.Repository.Programming.Js

open System
open System.Text
open System.Threading.Tasks
open Fable.Core
open Fable.Core.JsInterop

/// Git's own object database, addressed directly (§2.3 / §6).
///
/// ── why this exists ─────────────────────────────────────────────────────────
///
/// `ProcessGitRawStore` used to reach the ODB through one synchronous `git` child
/// process per primitive. Measured: a single-event append cost **24 spawns / ~60ms**
/// (11 / ~29ms once immutable reads and trees were memoized) against **0.5ms** for
/// the identical EventStore logic over the in-memory raw store, and one
/// `fallback-aabb-trace` canary spawned 1197 git processes totalling 2.4s of its
/// 5.3s wall. Because `execFileSync` blocked the Node event loop, those spawns also
/// serialized every other session in the Host behind whichever one was appending.
///
/// A loose object is `zlib(<type> <length>\0<body>)` at `objects/<oid[0..1]>/<oid[2..]>`,
/// and the oid is `sha1` of the same uncompressed bytes. Both are stable, documented
/// Git formats, so computing them here writes exactly the object `hash-object` /
/// `mktree` would write — `objects-identity.test.mjs` pins that equality against the
/// real binary rather than trusting this comment. Nothing about the store's semantics
/// changes: same oids, same on-disk layout, same `git cat-file` readability.
///
/// File I/O goes through `fs.promises` so EventStore write/CAS yields the Node event
/// loop. zlib inflate/deflate stay synchronous: they are CPU on an already-loaded
/// buffer, not a filesystem wait.
///
/// Packed objects are the one shape this cannot read: a pack is a delta-compressed
/// archive, and re-implementing its reader would be a second Git. A miss therefore
/// delegates to the injected CLI runner — the physical fallback for a repository
/// someone has `gc`'d, not a compatibility shim for a retired format.
module GitObjectDatabase =

    [<Import("createHash", "node:crypto")>]
    let private createHash (algorithm: string) : obj = jsNative

    [<Import("deflateSync", "node:zlib")>]
    let private deflateSync (buffer: obj) : obj = jsNative

    [<Import("inflateSync", "node:zlib")>]
    let private inflateSync (buffer: obj) : obj = jsNative

    [<Import("access", "node:fs/promises")>]
    let private access (path: string) : Task<unit> = jsNative

    [<Import("mkdir", "node:fs/promises")>]
    let private mkdir (path: string) (options: obj) : Task<obj> = jsNative

    [<Import("readFile", "node:fs/promises")>]
    let private readFileBinary (path: string) : Task<obj> = jsNative

    [<Import("readFile", "node:fs/promises")>]
    let private readFileText (path: string) (encoding: string) : Task<string> = jsNative

    [<Import("writeFile", "node:fs/promises")>]
    let private writeFile (path: string) (content: obj) : Task<unit> = jsNative

    [<Import("rename", "node:fs/promises")>]
    let private rename (oldPath: string) (newPath: string) : Task<unit> = jsNative

    [<Import("unlink", "node:fs/promises")>]
    let private unlink (path: string) : Task<unit> = jsNative

    [<Import("open", "node:fs/promises")>]
    let private fsOpen (path: string) (flags: string) : Task<obj> = jsNative

    [<Emit("$0.write($1)")>]
    let private handleWrite (handle: obj) (data: obj) : Task<obj> = jsNative

    [<Emit("$0.close()")>]
    let private handleClose (handle: obj) : Task<unit> = jsNative

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

    let private exists (path: string) : Task<bool> =
        task {
            try
                do! access path
                return true
            with _ ->
                return false
        }

    let private ensureDirectory (directory: string) : Task<unit> =
        task {
            let! present = exists directory

            if not present then
                let! _ = mkdir directory (createObj [ "recursive", box true ])
                return ()
        }

    let private readBytes (file: string) : Task<byte[]> =
        task {
            let! raw = readFileBinary file
            return emitJsExpr raw "Buffer.from($0)"
        }

    /// Write via temp + rename, the way Git writes a loose object: a reader either sees the
    /// complete object or no file at all, never a truncated prefix.
    ///
    /// Git maintenance is an external writer to the same ODB and may remove an
    /// empty `objects/xx` directory between our existence check and open(2). The
    /// object is content-addressed, so one bounded retry is deterministic: if a
    /// competing writer already installed `file`, the desired effect exists;
    /// otherwise recreate the prefix directory and retry with a fresh sibling.
    let private writeBytesAtomic (file: string) (bytes: byte[]) : Task<unit> =
        let directory = file.Substring(0, file.LastIndexOf '/')

        let rec write remaining =
            task {
                do! ensureDirectory directory
                let temp = tempSibling file

                try
                    do! writeFile temp (asBuffer bytes)
                    do! rename temp file
                with error ->
                    let! already = exists file

                    if already then return ()
                    elif remaining > 0 then return! write (remaining - 1)
                    else return raise error
            }

        write 1

    /// `sha1` of the framed object bytes — the oid Git itself would assign.
    let private sha1Hex (framed: byte[]) : string =
        let hash = createHash "sha1"
        hash?update (asBuffer framed) |> ignore
        hash?digest ("hex") |> unbox<string>

    let private frame (objectType: string) (body: byte[]) : byte[] =
        let header = latin1Buffer (sprintf "%s %d\u0000" objectType body.Length)
        bufferConcat [| header; asBuffer body |]

    let private parseLooseHeader (header: string) (body: byte[]) : (string * byte[]) option =
        match header.Split(' ') with
        | [| objectType; _ |] -> Some(objectType, body)
        | _ -> None

    let private parseFramedLoose (framed: byte[]) : (string * byte[]) option =
        let separator = Array.IndexOf(framed, 0uy)

        if separator < 0 then
            None
        else
            let header = Encoding.UTF8.GetString(framed, 0, separator)
            let body = framed.[separator + 1 ..]
            parseLooseHeader header body

    /// Object body plus its Git type, or None when the loose object is absent.
    let private tryReadLoose (objectsDir: string) (oid: string) : Task<(string * byte[]) option> =
        task {
            let file = objectsDir + "/" + oid.Substring(0, 2) + "/" + oid.Substring(2)
            let! present = exists file

            if not present then
                return None
            else
                let! fileBytes = readBytes file
                let framed: byte[] = emitJsExpr (inflateSync (asBuffer fileBytes)) "Buffer.from($0)"
                return parseFramedLoose framed
        }

    /// Write the framed object unless the oid already exists. Returns the oid either way.
    let private writeLoose (objectsDir: string) (objectType: string) (body: byte[]) : Task<string> =
        task {
            let framed = frame objectType body
            let oid = sha1Hex framed
            let directory = objectsDir + "/" + oid.Substring(0, 2)
            let file = directory + "/" + oid.Substring(2)
            let! present = exists file

            if not present then
                do! ensureDirectory directory

                let compressed: byte[] =
                    emitJsExpr (deflateSync (asBuffer framed)) "Buffer.from($0)"

                do! writeBytesAtomic file compressed

            return oid
        }

    let writeBlob (objectsDir: string) (content: byte[]) : Task<string> = writeLoose objectsDir "blob" content

    /// Canonical tree body: `<mode> <name>\0<20-byte oid>` records in Git's own entry order,
    /// no separators. Git writes tree modes without the leading zero (`40000`, not `040000`).
    let private treeBody (entries: TreeEntry list) : byte[] =
        entries
        |> GitTree.canonicalOrder
        |> List.map (fun entry ->
            let mode =
                if GitTree.isTreeMode entry.Mode then
                    "40000"
                else
                    entry.Mode.TrimStart('0')

            let prefix = latin1Buffer (sprintf "%s %s\u0000" mode entry.Name)
            bufferConcat [| prefix; hexBuffer (GitObjectId.value entry.Oid) |] |> asBuffer)
        |> List.toArray
        |> bufferConcat

    let writeTree (objectsDir: string) (entries: TreeEntry list) : Task<string> =
        writeLoose objectsDir "tree" (treeBody entries)

    let private decodeTreeMeta (meta: string) (oidHex: string) : TreeEntry option =
        let space = meta.IndexOf(' ')

        if space < 0 then
            None
        else
            Some
                { Mode = GitTree.normalizeMode (meta.Substring(0, space))
                  Name = meta.Substring(space + 1)
                  Oid = GitObjectId.create oidHex }

    /// None = end of tree; Some(entryOpt, next) = continue at next.
    let private treeRecordAfterOffset (body: byte[]) (offset: int) : (TreeEntry option * int) option =
        let separator = Array.IndexOf(body, 0uy, offset)

        if separator < 0 || separator + 20 >= body.Length then
            None
        else
            let meta = Encoding.UTF8.GetString(body, offset, separator - offset)
            let oidHex = toHex body.[separator + 1 .. separator + 20]
            Some(decodeTreeMeta meta oidHex, separator + 21)

    let private treeRecordAt (body: byte[]) (offset: int) : (TreeEntry option * int) option =
        if offset >= body.Length then
            None
        else
            treeRecordAfterOffset body offset

    /// Parse a tree body back into entries. Modes are normalized by the caller's store rules.
    let private parseTree (body: byte[]) : TreeEntry list =
        let rec loop (offset: int) (acc: TreeEntry list) =
            match treeRecordAt body offset with
            | None -> List.rev acc
            | Some(None, next) -> loop next acc
            | Some(Some entry, next) -> loop next (entry :: acc)

        loop 0 []

    /// Loose object bytes for `oid`, or None when it is packed / absent.
    let tryReadObject (objectsDir: string) (oid: string) : Task<byte[] option> =
        task {
            match! tryReadLoose objectsDir oid with
            | Some(_, body) -> return Some body
            | None -> return None
        }

    /// Loose tree entries for `oid`, or None when it is packed / absent / not a tree.
    let tryReadTree (objectsDir: string) (oid: string) : Task<TreeEntry list option> =
        task {
            match! tryReadLoose objectsDir oid with
            | Some("tree", body) -> return Some(parseTree body)
            | _ -> return None
        }

    // ── refs ────────────────────────────────────────────────────────────────
    //
    // `rev-parse --verify` + `update-ref` were the last two spawns left on the append path
    // (~4ms of the ~7.5ms). A ref is a 41-byte file and the update protocol is a lockfile, both
    // documented and both used verbatim here — so a concurrent `git` process, another plugin
    // instance and this code all serialize through the same `<ref>.lock`, exactly as before.

    let private isOid (text: string) =
        text.Length = 40
        && text |> Seq.forall (fun c -> Char.IsDigit c || (c >= 'a' && c <= 'f'))

    let private oidFromRefText (text: string) : string option =
        let trimmed = text.Trim()
        if isOid trimmed then Some trimmed else None

    let private tryReadLooseRefOid (loose: string) : Task<string option> =
        task {
            let! loosePresent = exists loose

            if not loosePresent then
                return None
            else
                let! text = readFileText loose "utf8"
                return oidFromRefText text
        }

    let private oidFromPackedRefRow (refName: string) (row: string) : string option =
        match row.Split(' ') with
        | [| oid; name |] when name = refName && isOid oid -> Some oid
        | _ -> None

    let private oidFromPackedRefLine (refName: string) (line: string) : string option =
        let row = line.Trim()

        if row = "" || row.StartsWith "#" || row.StartsWith "^" then
            None
        else
            oidFromPackedRefRow refName row

    let private tryReadPackedRefOid (gitDir: string) (refName: string) : Task<string option> =
        task {
            let packed = gitDir + "/packed-refs"
            let! packedPresent = exists packed

            if not packedPresent then
                return None
            else
                let! packedText = readFileText packed "utf8"

                return packedText.Split('\n') |> Array.tryPick (oidFromPackedRefLine refName)
        }

    /// The ref's value: the loose ref file first, then `packed-refs` (a `gc`/`pack-refs` may have
    /// moved it there). Symrefs are not a store shape and are not followed.
    let tryReadRef (gitDir: string) (refName: string) : Task<string option> =
        task {
            let! fromLoose = tryReadLooseRefOid (gitDir + "/" + refName)

            match fromLoose with
            | Some oid -> return Some oid
            | None -> return! tryReadPackedRefOid gitDir refName
        }

    let private closeQuietly (handle: obj) : Task<unit> =
        task {
            try
                do! handleClose handle
            with _ ->
                ()
        }

    let private unlinkQuietly (path: string) : Task<unit> =
        task {
            try
                do! unlink path
            with _ ->
                ()
        }

    let private tryAcquireLock (lockPath: string) : Task<obj option> =
        task {
            try
                let! handle = fsOpen lockPath "wx"
                return Some handle
            with _ ->
                return None
        }

    let private readRefTextQuietly (refPath: string) : Task<string option> =
        task {
            try
                let! text = readFileText refPath "utf8"
                return Some(text.Trim())
            with _ ->
                return None
        }

    /// DURABLE-EVENTS-004/006：CAS 未见证 newOid 不得假装提交。
    let private confirmInstalledOid (lockPath: string) (refPath: string) (newOid: string) : Task<bool> =
        task {
            let! current = readRefTextQuietly refPath

            if current = Some newOid then
                return true
            else
                do! unlinkQuietly lockPath
                return false
        }

    let private settleRenameFailure (lockPath: string) (refPath: string) (newOid: string) : Task<bool> =
        task {
            let! present = exists refPath

            if not present then
                do! unlinkQuietly lockPath
                return false
            else
                // rename 失败但 ref 已存在时，只有确认 ref 现在持有 newOid 才可报成功。
                return! confirmInstalledOid lockPath refPath newOid
        }

    let private commitLockedRef (handle: obj) (lockPath: string) (refPath: string) (newOid: string) : Task<bool> =
        task {
            let! _ = handleWrite handle (latin1Buffer (newOid + "\n"))
            do! handleClose handle

            try
                do! rename lockPath refPath
                return true
            with _ ->
                return! settleRenameFailure lockPath refPath newOid
        }

    let private decideCasAfterRead
        (handle: obj)
        (lockPath: string)
        (refPath: string)
        (expectedOld: string option)
        (newOid: string)
        (current: string option)
        : Task<bool> =
        if current <> expectedOld then
            task {
                do! closeQuietly handle
                do! unlinkQuietly lockPath
                return false
            }
        else
            commitLockedRef handle lockPath refPath newOid

    let private swapWithLock
        (gitDir: string)
        (refName: string)
        (expectedOld: string option)
        (newOid: string)
        (handle: obj)
        (lockPath: string)
        (refPath: string)
        : Task<bool> =
        task {
            try
                let! current = tryReadRef gitDir refName
                return! decideCasAfterRead handle lockPath refPath expectedOld newOid current
            with error ->
                do! closeQuietly handle
                do! unlinkQuietly lockPath
                return raise error
        }

    /// Git's own lockfile protocol: create `<ref>.lock` exclusively, verify the current value is
    /// still what the caller expected, write the new value into the lock, rename it over the ref.
    /// A lost race — lock taken, or the ref moved — returns false, which is the CAS answer.
    let compareAndSwapRef
        (gitDir: string)
        (refName: string)
        (expectedOld: string option)
        (newOid: string)
        : Task<bool> =
        task {
            let refPath = gitDir + "/" + refName
            let lockPath = refPath + ".lock"
            do! ensureDirectory (refPath.Substring(0, refPath.LastIndexOf '/'))
            let! handleOpt = tryAcquireLock lockPath

            match handleOpt with
            | None -> return false
            | Some handle -> return! swapWithLock gitDir refName expectedOld newOid handle lockPath refPath
        }
