class Js extends JsProgram {
  async run() {
    const host = this;
    return await js.run(class extends js.Runnable {
      maxDuration = 3600000; // the whole run: 60 min
      interval = 10000;      // check() runs once per 10 s slice

      // Three large files, strictly serial. -c is what makes a step idempotent:
      // running it again continues the file on disk instead of overwriting it.
      files = [
        { name: "dataset-1", url: "https://cdn.example.org/data/dataset-1.img", path: "downloads/dataset-1.img" },
        { name: "dataset-2", url: "https://cdn.example.org/data/dataset-2.img", path: "downloads/dataset-2.img" },
        { name: "dataset-3", url: "https://cdn.example.org/data/dataset-3.img", path: "downloads/dataset-3.img" },
      ];
      minBytesPerSlice = 524288; // under half a MiB per 10 s slice = too slow

      index = 0;
      current = null;   // the file whose wget is running; null on a hold slice
      report = null;    // a finished file waiting for check() to park it
      attempts = 0;
      seenBytes = -1;
      stalled = 0;
      slow = 0;

      // The Host owns the loop: it calls next(), runs what is returned, and
      // comes back only when that step is over. A pending report must outlive
      // one slice boundary, because check() is the only place a park can be
      // raised; the hold command keeps the loop alive for exactly that slice.
      next(ctx) {
        if (this.report) return `sleep ${this.interval / 1000 + 2}`;
        if (this.index >= this.files.length) return null;
        const file = this.files[this.index];
        this.current = file;
        this.seenBytes = -1;
        this.stalled = 0;
        this.slow = 0;
        return `mkdir -p downloads && wget -c -q -O ${file.path} ${file.url}`;
      }

      // Progress is read from stat growth, not from the log: 6 slices with no
      // new byte, or 12 slices under minBytesPerSlice, aborts the run. Abort
      // SIGKILLs the foreground wget; the partial file stays for wget -c.
      async check(ctx) {
        if (this.report) {
          const report = this.report;
          this.report = null;
          const s = await host.stat(report.path);
          // One durable record per finished file: a restart then repeats at
          // most the file that was in flight.
          return { park: { ...report, bytes: s.exists ? s.size : 0, elapsedMs: ctx.elapsedMs } };
        }
        if (!this.current) return true;

        const s = await host.stat(this.current.path);
        const size = s.exists ? s.size : 0;
        const grew = this.seenBytes < 0 ? size : size - this.seenBytes;
        this.seenBytes = size;
        this.stalled = grew <= 0 ? this.stalled + 1 : 0;
        this.slow = grew > 0 && grew < this.minBytesPerSlice ? this.slow + 1 : 0;

        if (this.stalled >= 6) return { abort: `${this.current.name}: no new bytes for 6 slices` };
        if (this.slow >= 12) return { abort: `${this.current.name}: under ${this.minBytesPerSlice} bytes per slice for 12 slices` };
        return true;
      }

      // Exit codes are mechanical: 0 advances, 4 and 8 retry (-c resumes from
      // the byte offset), 3 skips the file. Anything else is reported instead
      // of guessed at, and the run keeps its remaining files.
      onExit(result) {
        const file = this.current;
        if (!file) return; // a hold slice carries no work
        this.current = null;

        if (result.exitCode === 0) {
          this.attempts = 0;
          this.report = { file: file.name, path: file.path, status: "done", exitCode: 0 };
          this.index += 1;
          return;
        }
        if (result.exitCode === 4 || result.exitCode === 8) {
          if (this.attempts < 3) {
            this.attempts += 1;
            return; // same index: next() re-issues the same wget
          }
          this.attempts = 0;
          this.report = {
            file: file.name, path: file.path, status: "failed",
            exitCode: result.exitCode, attempts: 4,
          };
          this.index += 1;
          return;
        }
        if (result.exitCode === 3) {
          this.attempts = 0;
          this.report = { file: file.name, path: file.path, status: "skipped", exitCode: 3 };
          this.index += 1;
          return;
        }
        this.attempts = 0;
        this.report = { file: file.name, path: file.path, status: "unmapped", exitCode: result.exitCode };
        this.index += 1;
      }

      // onTimeout keeps its default: maxDuration expiry kills the foreground
      // and returns status "timeout", with the partial file kept on disk.
    });
  }
}
