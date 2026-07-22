namespace Wanxiangshu.Next.Tests.JournalTests

open System
open System.IO

module JournalTestSupport =

    let withTempDir (action: string -> unit) =
        let dir =
            Path.Combine(Path.GetTempPath(), "wanxiangshu_test_" + Guid.NewGuid().ToString("N"))

        try
            Directory.CreateDirectory(dir) |> ignore
            action dir
        finally
            try
                if Directory.Exists(dir) then
                    Directory.Delete(dir, true)
            with _ ->
                ()
