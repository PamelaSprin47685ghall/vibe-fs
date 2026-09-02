namespace Wanxiangshu.Persistence.Journal

open System
open System.Threading.Tasks
open Wanxiangshu.Composition.Durable
open Wanxiangshu.Foundation.Identity

type IJournalEventStoreBoot =
    abstract ResumeOrCreate:
        RuntimeId * int * DateTimeOffset -> Task<Result<IJournalWriter * Envelope * ProjectionSet, FoldRejection>>
