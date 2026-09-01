namespace Wanxiangshu.Foundation

open System.Threading.Tasks

module TaskValue =
    val map: mapper: ('value -> 'next) -> operation: Task<'value> -> Task<'next>

module TaskResult =
    val mapError:
        mapper: ('error -> 'nextError) -> operation: Task<Result<'value, 'error>> -> Task<Result<'value, 'nextError>>

module TaskResultList =
    val traverseM:
        mapper: ('item -> Task<Result<'value, 'error>>) -> items: 'item list -> Task<Result<'value list, 'error>>

module TaskResultListSurface =
    val traverseM: mapper: obj -> items: string array -> Task<string array>
