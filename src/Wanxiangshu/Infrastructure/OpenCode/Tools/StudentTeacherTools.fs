namespace Wanxiangshu.OpenCode

open Wanxiangshu.Domain
open Wanxiangshu.Session

module StudentTeacherTools =

    let private render =
        function
        | Ok value -> ToolResultBound.bound value
        | Error error -> ToolHostCodec.tomlObject [ "error", ToolHostCodec.TString error ]

    let teacherSpec (factory: HostToolFactory) (runtime: StudentTeacherRuntime) : ToolSpec =
        { Name = "teacher"
          Description = "Ask the private Teacher one natural-language question and wait for its return."
          Arguments = [ "message", ToolHostCodec.stringSchema factory ]
          Execute =
            fun args ctx ->
                task {
                    let! result = runtime.InvokeTeacher(ctx.SessionId, args.Text "message")
                    return render result
                } }

    let returnSpec (factory: HostToolFactory) (runtime: StudentTeacherRuntime) : ToolSpec =
        { Name = "return"
          Description = "Return a Teacher answer or finish a StudentCompile run after QA cleanup."
          Arguments = [ "message", ToolHostCodec.stringSchema factory ]
          Execute =
            fun args ctx ->
                task {
                    let! result = runtime.Return(ctx.SessionId, ctx.ProviderRunId, args.Text "message")
                    return render result
                } }
