module Wanxiangshu.Shell.Clock

let getTimestampMs () : int64 =
    System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
