namespace Wanxiangshu.Repository.Knowledge.Casebook

module CasebookAppendSurface =
    val failureToJs: failure: CasebookAppendFailure -> obj
    val mutationErrorToJs: error: CasebookMutationError -> obj
    val finalizeFailureToJs: failure: CasebookAppendFailure -> obj
