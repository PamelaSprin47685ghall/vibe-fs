module Wanxiangshu.Shell.OmpHostBindings

open Fable.Core
open Wanxiangshu.Shell.Dyn

let getCreateAgentSession (pi: obj) : obj =
    Dyn.get (Dyn.get pi "pi") "createAgentSession"

let createSessionManager (sessionManagerType: obj) (cwd: string) : obj =
    Dyn.call1 (Dyn.get sessionManagerType "create") (box cwd)

let sessionPrompt (session: obj) (prompt: string) : JS.Promise<unit> =
    unbox<JS.Promise<unit>> (Dyn.call1 (Dyn.get session "prompt") (box prompt))

let sessionWaitForIdle (session: obj) : JS.Promise<unit> =
    unbox<JS.Promise<unit>> (Dyn.call0 (Dyn.get session "waitForIdle"))

let sessionAbort (session: obj) : obj = Dyn.get session "abort"
