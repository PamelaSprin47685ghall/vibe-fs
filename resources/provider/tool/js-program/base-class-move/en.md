  mv(from, to) {
    // Moves or renames a file or directory (POSIX mv); overwrite is allowed and a missing source is an error; a cross-device (EXDEV) move errors and rolls back, never leaving both sides present.
    // Example: this.mv("src/old.js", "src/new.js");
  }
