namespace Wanxiangshu.Repository.Knowledge.Casebook.OpenCode

open System.Threading.Tasks

/// JS-native adapter boundary for the Bookkeeper's OpenCode tool binding.
module CasebookBookkeeperToolSurface =

    val runProgram: sessionId: string -> program: string -> Task<string>

    val contract: toolModule: obj -> obj
