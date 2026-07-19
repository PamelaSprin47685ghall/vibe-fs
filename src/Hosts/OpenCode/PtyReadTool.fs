module Wanxiangshu.Hosts.Opencode.PtyReadTool

open Fable.Core
open Fable.Core.JsInterop
open Fable.Core.JS
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Kernel.ToolPermission
open Wanxiangshu.Kernel.ToolCatalog
open Wanxiangshu.Runtime.PromptFrontMatter
open Wanxiangshu.Hosts.Opencode.ToolSchema
open Wanxiangshu.Hosts.Opencode.PtySpawn

module Dyn = Wanxiangshu.Runtime.Dyn

let readUnfiltered (mgr: obj) (id: string) (session: obj) (offset: int) (limit: int) : JS.Promise<string> =
    promise {
        let result = mgr?read (id, offset, limit)

        if Dyn.isNullish result then
            failwithf "PTY session not found: %s" id

        let lines: string array = unbox (result?lines)
        let totalLines = unbox<int> result?totalLines
        let hasMore = unbox<bool> result?hasMore
        let resultOffset = unbox<int> result?offset

        let fields =
            [ "id", box id
              "status", box (string session?status)
              "offset", box resultOffset
              "returned", box lines.Length
              "total_lines", box totalLines
              "has_more", box hasMore ]

        let sb = ResizeArray<string>()

        for i in 0 .. lines.Length - 1 do
            sb.Add(lines.[i])

        sb.Add("")

        if hasMore then
            sb.Add(
                sprintf
                    "(Buffer has more lines. Use offset=%d to read beyond line %d)"
                    (resultOffset + lines.Length)
                    (resultOffset + lines.Length)
            )
        else
            sb.Add(sprintf "(End of buffer - total %d lines)" totalLines)

        return frontMatterPrompt fields (String.concat "\n" sb)
    }

let private formatFilteredResult
    (id: string)
    (session: obj)
    (pattern: string)
    (offset: int)
    (matches: obj array)
    (totalLines: int)
    (totalMatches: int)
    (hasMore: bool)
    : string =
    let fields =
        [ "id", box id
          "status", box (string session?status)
          "pattern", box pattern
          "offset", box offset
          "returned", box matches.Length
          "total_matches", box totalMatches
          "total_lines", box totalLines
          "has_more", box hasMore ]

    let sb = ResizeArray<string>()

    if matches.Length = 0 then
        sb.Add(sprintf "No lines matched the pattern '%s'." pattern)
        sb.Add(sprintf "Total lines in buffer: %d" totalLines)
    else
        for i in 0 .. matches.Length - 1 do
            let m = matches.[i]
            sb.Add(string m?text)

        sb.Add("")

        if hasMore then
            sb.Add(
                sprintf
                    "(%d of %d matches shown. Use offset=%d to see more.)"
                    matches.Length
                    totalMatches
                    (offset + matches.Length)
            )
        else
            sb.Add(
                sprintf
                    "(%d match%s from %d total lines)"
                    totalMatches
                    (if totalMatches = 1 then "" else "es")
                    totalLines
            )

    frontMatterPrompt fields (String.concat "\n" sb)

let readFiltered
    (mgr: obj)
    (id: string)
    (session: obj)
    (pattern: string)
    (offset: int)
    (limit: int)
    (ignoreCase: bool)
    : JS.Promise<string> =
    promise {
        let flags = if ignoreCase then "i" else ""
        let regex = newRegex pattern flags
        let result = mgr?search (id, regex, offset, limit)

        if Dyn.isNullish result then
            failwithf "PTY session not found: %s" id

        let matches: obj array = unbox (result?matches)
        let totalLines = unbox<int> result?totalLines
        let totalMatches = unbox<int> result?totalMatches
        let hasMore = unbox<bool> result?hasMore

        return formatFilteredResult id session pattern offset matches totalLines totalMatches hasMore
    }

let ptyReadTool (host: Host) : obj =
    define
        "Read output buffer from a PTY session with pagination (offset/limit) and optional regex pattern filtering."
        (createObj
            [ "id", box (strReq "The PTY session ID (e.g., pty_a1b2c3d4)")
              "offset",
              box (
                  numOpt
                      "Line number to start reading from (0-based, defaults to 0). When using pattern, this applies to filtered matches."
              )
              "limit",
              box (
                  numOpt
                      "Number of lines to read (defaults to 500). When using pattern, this applies to filtered matches."
              )
              "pattern",
              box (
                  strOpt
                      "Regex pattern to filter lines. When set, only matching lines are returned, then offset/limit apply to the matches."
              )
              "ignoreCase", box (boolOpt "Case-insensitive pattern matching (default: false)") ])
        (fun args context ->
            checkExecPerm host context
            let id = string args?``id``
            let sessionId = Dyn.str context "sessionID"

            let offset' =
                if Dyn.isNullish (Dyn.get args "offset") then
                    0
                else
                    unbox<int> args?offset

            let limit' =
                if Dyn.isNullish (Dyn.get args "limit") then
                    500
                else
                    unbox<int> args?limit

            let pattern = Dyn.str args "pattern"

            promise {
                let! mgr = getManager ()
                let lm = mgr?lifecycleManager
                let sessionRaw = lm?getSession (id)

                if Dyn.isNullish sessionRaw || string sessionRaw?parentSessionId <> sessionId then
                    failwithf "PTY session not found: %s" id

                let session = lm?toInfo (sessionRaw)

                if pattern = "" then
                    return! readUnfiltered mgr id session offset' limit'
                else
                    let ignoreCaseBool =
                        if Dyn.isNullish (Dyn.get args "ignoreCase") then
                            false
                        else
                            unbox<bool> args?ignoreCase

                    return! readFiltered mgr id session pattern offset' limit' ignoreCaseBool
            })
