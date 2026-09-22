namespace Wanxiangshu.OpenCode

open System.Threading.Tasks

module SphinxCommand =
    type Request =
        { Question: string
          ExpectTurns: int option }

    val parse: arguments: string -> Result<Request, string>
    val questionAndAnswer: question: string -> answer: obj -> string

    val before:
        run: (string -> string -> int option -> Task<obj>) ->
        client: obj ->
        directory: string option ->
        input: obj ->
        output: obj ->
            Task<bool>
