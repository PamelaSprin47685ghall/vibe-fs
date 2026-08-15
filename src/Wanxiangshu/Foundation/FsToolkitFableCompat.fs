namespace Wanxiangshu.Foundation

open System.Threading.Tasks

/// Fable-only async/result vocabulary owned by this repository.
/// FsToolkit.ErrorHandling excludes its Task helpers from the Fable build.
module TaskValue =
    let map (mapper: 'value -> 'next) (operation: Task<'value>) : Task<'next> =
        task {
            let! value = operation
            return mapper value
        }

module TaskResult =
    let mapError
        (mapper: 'error -> 'nextError)
        (operation: Task<Result<'value, 'error>>)
        : Task<Result<'value, 'nextError>> =
        task {
            match! operation with
            | Ok value -> return Ok value
            | Error error -> return Error(mapper error)
        }

module TaskResultList =
    let traverseM
        (mapper: 'item -> Task<Result<'value, 'error>>)
        (items: 'item list)
        : Task<Result<'value list, 'error>> =
        let rec collect reversed remaining =
            taskResult {
                match remaining with
                | [] -> return Microsoft.FSharp.Collections.List.rev reversed
                | item :: rest ->
                    let! value = mapper item
                    return! collect (value :: reversed) rest
            }

        collect [] items

