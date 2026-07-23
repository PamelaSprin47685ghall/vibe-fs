namespace Wanxiangshu.Next.Agent

open System
open System.Threading
open System.Threading.Tasks
open Wanxiangshu.Next.Kernel
open Wanxiangshu.Next.Process

[<RequireQualifiedAccess>]
type ProgramError =
    | PermissionDenied of role: Role * permission: ToolPermission
    | InspectorAlreadyUsed
    | ExecutionError of reason: string

[<RequireQualifiedAccess>]
type ReviewVerdict =
    | Approved of summary: string
    | Rejected of reason: string
    | NeedsRevision of feedback: string list

type CommandRequest =
    { Command: Command
      Estimate: ProcessEstimate option
      Context: ProcessContext option }

module CommandRequest =
    let ofCommand (cmd: Command) : CommandRequest =
        { Command = cmd
          Estimate = None
          Context = None }

type RunnerPort = CommandRequest -> Task<Result<RunnerOutcome, RunnerError>>

type OneShotInspector =
    { Run: CommandRequest -> Task<Result<RunnerOutcome, RunnerError>>
      RunCommand: Command -> Task<Result<RunnerOutcome, RunnerError>> }

type FilePort =
    { Read: string -> Task<Result<string, string>>
      Write: string -> string -> Task<Result<unit, string>>
      Edit: string -> string -> string -> Task<Result<unit, string>> }

type SearchPort =
    { Glob: string -> Task<Result<string list, string>>
      Grep: string -> Task<Result<string list, string>> }

type BrowserPort =
    { Read: string -> Task<Result<string, string>>
      NetworkFetch: string -> Task<Result<string, string>> }

type ManagerPort =
    { Fork: string -> Task<Result<string, string>>
      Join: string -> Task<Result<unit, string>>
      List: unit -> Task<Result<string list, string>> }

type CoderCapability =
    { Read: string -> Task<Result<string, ProgramError>>
      Write: string -> string -> Task<Result<unit, ProgramError>>
      Edit: string -> string -> string -> Task<Result<unit, ProgramError>>
      CreateInspector: RunnerPort -> Result<OneShotInspector, ProgramError> }

type InspectorCapability = OneShotInspector

type BrowserCapability =
    { Read: string -> Task<Result<string, ProgramError>>
      NetworkFetch: string -> Task<Result<string, ProgramError>> }

type MeditatorCapability =
    { Read: string -> Task<Result<string, ProgramError>>
      Glob: string -> Task<Result<string list, ProgramError>>
      Grep: string -> Task<Result<string list, ProgramError>>
      CreateInspector: RunnerPort -> Result<OneShotInspector, ProgramError> }

type ReviewerCapability =
    { Read: string -> Task<Result<string, ProgramError>>
      Glob: string -> Task<Result<string list, ProgramError>>
      Grep: string -> Task<Result<string list, ProgramError>>
      CreateInspector: RunnerPort -> Result<OneShotInspector, ProgramError>
      SubmitVerdict: ReviewVerdict -> Result<ReviewVerdict, ProgramError> }

type ManagerCapability =
    { Fork: string -> Task<Result<string, ProgramError>>
      Join: string -> Task<Result<unit, ProgramError>>
      List: unit -> Task<Result<string list, ProgramError>> }

type OrchestratorCapability =
    { Fork: string -> Task<Result<string, ProgramError>>
      Join: string -> Task<Result<unit, ProgramError>> }

type ExecutorCapability = unit
type BloggerCapability = unit

module Programs =

    let runInspector (req: CommandRequest) (runnerPort: RunnerPort) : Task<Result<RunnerOutcome, RunnerError>> =
        runnerPort req

    let createInspector (role: Role) (runnerPort: RunnerPort) : Result<OneShotInspector, ProgramError> =
        let isAllowed =
            Roles.isAllowed role ToolPermission.Exec
            || Roles.isAllowed role ToolPermission.Inspector

        if not isAllowed then
            Error(ProgramError.PermissionDenied(role, ToolPermission.Exec))
        else
            let mutable used = 0

            let run (req: CommandRequest) =
                task {
#if FABLE_COMPILER
                    if used = 0 then
                        used <- 1
                        return! runInspector req runnerPort
                    else
                        return Error(RunnerError.ExecutionFailed "One-shot inspector already used")
#else
                    if Interlocked.Exchange(&used, 1) = 0 then
                        return! runInspector req runnerPort
                    else
                        return Error(RunnerError.ExecutionFailed "One-shot inspector already used")
#endif
                }

            let runCommand (cmd: Command) = run (CommandRequest.ofCommand cmd)

            Ok { Run = run; RunCommand = runCommand }

    let createCoderCapability (role: Role) (filePort: FilePort) : Result<CoderCapability, ProgramError> =
        if not (Roles.isAllowed role ToolPermission.Read) then
            Error(ProgramError.PermissionDenied(role, ToolPermission.Read))
        elif not (Roles.isAllowed role ToolPermission.Write) then
            Error(ProgramError.PermissionDenied(role, ToolPermission.Write))
        elif not (Roles.isAllowed role ToolPermission.Edit) then
            Error(ProgramError.PermissionDenied(role, ToolPermission.Edit))
        elif not (Roles.isAllowed role ToolPermission.Inspector) then
            Error(ProgramError.PermissionDenied(role, ToolPermission.Inspector))
        else
            let read path =
                task {
                    match! filePort.Read path with
                    | Ok res -> return Ok res
                    | Error err -> return Error(ProgramError.ExecutionError err)
                }

            let write path content =
                task {
                    match! filePort.Write path content with
                    | Ok() -> return Ok()
                    | Error err -> return Error(ProgramError.ExecutionError err)
                }

            let edit path oldStr newStr =
                task {
                    match! filePort.Edit path oldStr newStr with
                    | Ok() -> return Ok()
                    | Error err -> return Error(ProgramError.ExecutionError err)
                }

            let createInst runner = createInspector role runner

            Ok
                { Read = read
                  Write = write
                  Edit = edit
                  CreateInspector = createInst }

    let createBrowserCapability (role: Role) (browserPort: BrowserPort) : Result<BrowserCapability, ProgramError> =
        if not (Roles.isAllowed role ToolPermission.Read) then
            Error(ProgramError.PermissionDenied(role, ToolPermission.Read))
        elif not (Roles.isAllowed role ToolPermission.Network) then
            Error(ProgramError.PermissionDenied(role, ToolPermission.Network))
        else
            let read urlOrPath =
                task {
                    match! browserPort.Read urlOrPath with
                    | Ok res -> return Ok res
                    | Error err -> return Error(ProgramError.ExecutionError err)
                }

            let fetch url =
                task {
                    match! browserPort.NetworkFetch url with
                    | Ok res -> return Ok res
                    | Error err -> return Error(ProgramError.ExecutionError err)
                }

            Ok { Read = read; NetworkFetch = fetch }

    let createMeditatorCapability
        (role: Role)
        (filePort: FilePort)
        (searchPort: SearchPort)
        : Result<MeditatorCapability, ProgramError> =
        if not (Roles.isAllowed role ToolPermission.Read) then
            Error(ProgramError.PermissionDenied(role, ToolPermission.Read))
        elif not (Roles.isAllowed role ToolPermission.Glob) then
            Error(ProgramError.PermissionDenied(role, ToolPermission.Glob))
        elif not (Roles.isAllowed role ToolPermission.Grep) then
            Error(ProgramError.PermissionDenied(role, ToolPermission.Grep))
        elif not (Roles.isAllowed role ToolPermission.Inspector) then
            Error(ProgramError.PermissionDenied(role, ToolPermission.Inspector))
        else
            let read path =
                task {
                    match! filePort.Read path with
                    | Ok res -> return Ok res
                    | Error err -> return Error(ProgramError.ExecutionError err)
                }

            let glob pat =
                task {
                    match! searchPort.Glob pat with
                    | Ok res -> return Ok res
                    | Error err -> return Error(ProgramError.ExecutionError err)
                }

            let grep pat =
                task {
                    match! searchPort.Grep pat with
                    | Ok res -> return Ok res
                    | Error err -> return Error(ProgramError.ExecutionError err)
                }

            let createInst runner = createInspector role runner

            Ok
                { Read = read
                  Glob = glob
                  Grep = grep
                  CreateInspector = createInst }

    let createReviewerCapability
        (role: Role)
        (filePort: FilePort)
        (searchPort: SearchPort)
        : Result<ReviewerCapability, ProgramError> =
        if not (Roles.isAllowed role ToolPermission.Read) then
            Error(ProgramError.PermissionDenied(role, ToolPermission.Read))
        elif not (Roles.isAllowed role ToolPermission.Glob) then
            Error(ProgramError.PermissionDenied(role, ToolPermission.Glob))
        elif not (Roles.isAllowed role ToolPermission.Grep) then
            Error(ProgramError.PermissionDenied(role, ToolPermission.Grep))
        elif not (Roles.isAllowed role ToolPermission.Inspector) then
            Error(ProgramError.PermissionDenied(role, ToolPermission.Inspector))
        elif not (Roles.isAllowed role ToolPermission.Verdict) then
            Error(ProgramError.PermissionDenied(role, ToolPermission.Verdict))
        else
            let read path =
                task {
                    match! filePort.Read path with
                    | Ok res -> return Ok res
                    | Error err -> return Error(ProgramError.ExecutionError err)
                }

            let glob pat =
                task {
                    match! searchPort.Glob pat with
                    | Ok res -> return Ok res
                    | Error err -> return Error(ProgramError.ExecutionError err)
                }

            let grep pat =
                task {
                    match! searchPort.Grep pat with
                    | Ok res -> return Ok res
                    | Error err -> return Error(ProgramError.ExecutionError err)
                }

            let createInst runner = createInspector role runner
            let submitVerdict verdict = Ok verdict

            Ok
                { Read = read
                  Glob = glob
                  Grep = grep
                  CreateInspector = createInst
                  SubmitVerdict = submitVerdict }

    let createManagerCapability (role: Role) (managerPort: ManagerPort) : Result<ManagerCapability, ProgramError> =
        if not (Roles.isAllowed role ToolPermission.Fork) then
            Error(ProgramError.PermissionDenied(role, ToolPermission.Fork))
        elif not (Roles.isAllowed role ToolPermission.Join) then
            Error(ProgramError.PermissionDenied(role, ToolPermission.Join))
        elif not (Roles.isAllowed role ToolPermission.List) then
            Error(ProgramError.PermissionDenied(role, ToolPermission.List))
        else
            let fork id =
                task {
                    match! managerPort.Fork id with
                    | Ok res -> return Ok res
                    | Error err -> return Error(ProgramError.ExecutionError err)
                }

            let join id =
                task {
                    match! managerPort.Join id with
                    | Ok() -> return Ok()
                    | Error err -> return Error(ProgramError.ExecutionError err)
                }

            let list () =
                task {
                    match! managerPort.List() with
                    | Ok res -> return Ok res
                    | Error err -> return Error(ProgramError.ExecutionError err)
                }

            Ok
                { Fork = fork
                  Join = join
                  List = list }

    let createOrchestratorCapability
        (role: Role)
        (managerPort: ManagerPort)
        : Result<OrchestratorCapability, ProgramError> =
        if not (Roles.isAllowed role ToolPermission.Fork) then
            Error(ProgramError.PermissionDenied(role, ToolPermission.Fork))
        elif not (Roles.isAllowed role ToolPermission.Join) then
            Error(ProgramError.PermissionDenied(role, ToolPermission.Join))
        else
            let fork id =
                task {
                    match! managerPort.Fork id with
                    | Ok res -> return Ok res
                    | Error err -> return Error(ProgramError.ExecutionError err)
                }

            let join id =
                task {
                    match! managerPort.Join id with
                    | Ok() -> return Ok()
                    | Error err -> return Error(ProgramError.ExecutionError err)
                }

            Ok { Fork = fork; Join = join }

    let createExecutorCapability (role: Role) : Result<ExecutorCapability, ProgramError> =
        let allowed = Roles.permissions role

        if allowed.IsEmpty then
            Ok()
        else
            Error(ProgramError.PermissionDenied(role, allowed |> Set.maxElement))

    let createBloggerCapability (role: Role) : Result<BloggerCapability, ProgramError> =
        let allowed = Roles.permissions role

        if allowed.IsEmpty then
            Ok()
        else
            Error(ProgramError.PermissionDenied(role, allowed |> Set.maxElement))
