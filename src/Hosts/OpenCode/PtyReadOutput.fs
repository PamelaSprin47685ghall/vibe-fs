module Wanxiangshu.Hosts.Opencode.PtyReadOutput

open Fable.Core
open Fable.Core.JsInterop
open Fable.Core.JS
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.ToolOutputInfoTypes
open Wanxiangshu.Runtime.Tooling.ToolOutputToml
open Wanxiangshu.Runtime.Tooling.ToolOutputPtyToml
open Wanxiangshu.Hosts.Opencode.PtySpawn

open Wanxiangshu.Runtime.ToolOutputInfo

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
        let statusStr = string session?status

        let hintOpt =
            if hasMore then
                Some(sprintf "(Buffer has more lines. Use offset=%d to read beyond line %d)" (resultOffset + lines.Length) (resultOffset + lines.Length))
            else
                Some(sprintf "(End of buffer - total %d lines)" totalLines)

        let ptyRead: PtyReadInfo =
            { id = id
              status = statusStr
              offset = resultOffset
              returned = lines.Length
              totalLines = totalLines
              hasMore = hasMore
              pattern = None
              totalMatches = None
              lines = Array.toList lines
              continuationHint = hintOpt }

        return renderPtyRead ptyRead
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
    let statusStr = string session?status

    let matchLines =
        matches |> Array.map (fun m -> string m?text) |> Array.toList

    let hintOpt =
        if matches.Length = 0 then
            Some(sprintf "No lines matched the pattern '%s'. Total lines in buffer: %d" pattern totalLines)
        elif hasMore then
            Some(sprintf "(%d of %d matches shown. Use offset=%d to see more.)" matches.Length totalMatches (offset + matches.Length))
        else
            Some(sprintf "(%d match%s from %d total lines)" totalMatches (if totalMatches = 1 then "" else "es") totalLines)

    let ptyRead: PtyReadInfo =
        { id = id
          status = statusStr
          offset = offset
          returned = matches.Length
          totalLines = totalLines
          hasMore = hasMore
          pattern = Some pattern
          totalMatches = Some totalMatches
          lines = matchLines
          continuationHint = hintOpt }

    renderPtyRead ptyRead

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
