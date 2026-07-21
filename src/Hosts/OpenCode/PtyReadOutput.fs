module Wanxiangshu.Hosts.Opencode.PtyReadOutput

open Fable.Core
open Fable.Core.JsInterop
open Fable.Core.JS
open Wanxiangshu.Kernel
open Wanxiangshu.Runtime.PromptHeader
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
