module VibeFs.Opencode.PlanCommand

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Kernel
open VibeFs.Kernel.PlanTypes
open VibeFs.Kernel.PlanEngine
open VibeFs.Opencode.Session
open VibeFs.Shell.Write

open System.Collections.Generic

let private rng = System.Random()

let private randomHex4 () : string = sprintf "%04x" (rng.Next(65536))

let private pushPart (arr: obj) (part: obj) : unit =
    (arr :?> ResizeArray<obj>).Add(part)

let private planFooter =
    "\n\nYou must output ONLY the JSON requested. Do not write files, run commands, or modify the workspace."

let private callModel (client: obj) (agent: string) (title: string) (directory: string) (sessionID: string) (prompt: string) : Async<string> =
    async {
        let! text = runSubagentWithCleanup client agent title (prompt + planFooter) directory sessionID null |> Async.AwaitPromise
        return text
    }

let handlePlanCommand (ctx: obj) (input: obj) (output: obj) : Async<unit> =
    async {
        let command = Dyn.str input "command"
        if command = "plan" then
            let rawRequirement = (Dyn.str input "arguments").Trim()
            let directory = Dyn.str ctx "directory"
            let sessionID = Dyn.str input "sessionID"
            let client = Dyn.get ctx "client"
            let parts = Dyn.get output "parts"
            if rawRequirement = "" then
                pushPart parts (box {| ``type`` = "text"; text = "Please provide a requirement, e.g. /plan design a login flow." |})
            else
                let hex4 = randomHex4 ()
                let request =
                    { requestId = sessionID + "-" + hex4
                      rawRequirement = rawRequirement
                      normalizedRequirement = PlanEngine.normalizeRequirement rawRequirement
                      branchCount = 5
                      branchModelName = "reverie"
                      judgeModelName = "reviewer"
                      outputFileName = PlanEngine.formatPlanFileName hex4
                      workspaceRoot = directory
                      existingContext = None }
                let branchAgent = if request.branchModelName = "" then "reverie" else request.branchModelName
                let judgeAgent = if request.judgeModelName = "" then "reviewer" else request.judgeModelName
                let branchCaller prompt = callModel client branchAgent "Plan branch" directory sessionID prompt
                let judgeCaller prompt = callModel client judgeAgent "Plan judge" directory sessionID prompt
                let! result = PlanEngine.runPlanPipeline request branchCaller judgeCaller
                let! writeMsg = write (Some directory) result.finalFileName result.finalMarkdown
                pushPart parts (box {| ``type`` = "text"; text = $"Plan written to {result.finalFileName}\n\n{writeMsg}" |})
    }
