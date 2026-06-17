module VibeFs.Opencode.BacktrackTools

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Kernel.Dyn
open VibeFs.Kernel.BacktrackPrompts
open VibeFs.Opencode.BacktrackSession
open VibeFs.Opencode.Sdk

let private resolveStr (s: string) : JS.Promise<string> = async { return s } |> Async.StartAsPromise

let backtrackTool (session: BacktrackSession) : obj =
    define toolDescription
        (box {| anchor = numOpt anchorDesc; note = strReq noteDesc |})
        (fun args context ->
            let sessionID = str context "sessionID"
            let anchorVal = get args "anchor"
            let note = str args "note"
            let visible = session.GetVisibleIds sessionID
            let anchorStr = if isNullish anchorVal then "" else string anchorVal
            match System.Int32.TryParse anchorStr with
            | false, _ -> resolveStr "Error: anchor must be a positive integer id."
            | true, anchor ->
                if not (List.contains anchor visible) then
                    let visibleStr = visible |> List.map string |> String.concat ", "
                    resolveStr ("Error: anchor #" + string anchor + " is not currently visible. Visible ids: " + visibleStr)
                elif note.Trim() = "" then
                    resolveStr "Error: note must be a non-empty concise summary."
                else
                    resolveStr ("History rewritten from anchor #" + string anchor + "."))
