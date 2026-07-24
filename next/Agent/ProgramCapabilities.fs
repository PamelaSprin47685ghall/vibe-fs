namespace Wanxiangshu.Next.Agent

open System.Threading.Tasks
open Wanxiangshu.Next.Kernel
open Wanxiangshu.Next.Process

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
                    if used = 0 then
                        used <- 1
                        return! runInspector req runnerPort
                    else
                        return Error(RunnerError.ExecutionFailed "One-shot inspector already used")
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
