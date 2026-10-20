  whereAll(content, needle, from = 0) {
    // Returns [[begin, end], ...]: normalized exact matching in the text (whitespace runs collapse to one space), reported as UTF-16 offsets into the original string; no match returns [].
    // Example: const spans = this.whereAll(text, "const value");
  }

  async where(content, needle, from = 0) {
    // Returns [begin, end]: exactly one match returns its span; zero or many matches suspend the program through the park channel with { needle, candidates, nearest }, and unpark injects the chosen span, so the program always receives one span.
    // Example: const [begin, end] = await this.where(text, "const value");
  }
