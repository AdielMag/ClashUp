You are performing a session retrospective. Your goal is to review everything that happened in this conversation, extract lessons, and persist them as memory files so future sessions benefit.

## Step 1: Analyze the Session

Go through the full conversation and identify:

1. **Mistakes & Corrections** — Any time you made an error, the user corrected you, something failed unexpectedly, or you had to redo work. What was the root cause? What should you do differently?
2. **Patterns & Conventions** — Coding patterns, naming conventions, project structure rules, or architectural decisions that were established or reinforced. Things like "this project uses X for Y" or "files go in Z directory".
3. **User Preferences** — How the user likes to work, communicate, or structure requests. Workflow preferences, tool choices, style preferences.
4. **Domain Knowledge** — Project-specific knowledge gained: key file paths, how systems connect, important classes/interfaces, dependency relationships, config conventions.
5. **Techniques Used** — Any non-obvious approach or solution that worked well and could be reused in future sessions.

## Step 2: Read Existing Memory

Read all files in the memory directory to understand what's already documented:
- `C:\Users\Adiel\.claude\projects\C--Users-Adiel-Documents-ClashUp\memory\MEMORY.md`
- Check for any other `.md` files in that directory.

Do NOT duplicate information that already exists. Update existing entries if you have new or corrected information.

## Step 3: Write/Update Memory Files

Organize findings into memory files:

- **MEMORY.md** — Keep this as a concise index (under 200 lines). It should contain:
  - Quick-reference project facts (key paths, stack, conventions)
  - Links to topic files for details
  - User preferences summary

- **Topic files** — Create or update files like:
  - `project-structure.md` — Key directories, file locations, how things connect
  - `patterns.md` — Code patterns, naming conventions, architectural rules
  - `user-preferences.md` — How the user likes to work
  - `debugging.md` — Common pitfalls and their solutions
  - `domain.md` — Project-specific domain knowledge

Only create a topic file if there's enough substance for it. Don't create empty or near-empty files.

## Step 4: Report

After writing memories, give a brief summary to the user:
- Number of new lessons captured
- Number of existing memories updated
- List each lesson as a one-liner

## Rules

- Be specific and actionable. "Remember to check X before doing Y" is better than "be careful with X".
- Don't store session-specific state (current task, temp file paths, in-progress work).
- Don't store anything speculative — only things confirmed by what actually happened.
- If nothing meaningful was learned, say so. Don't fabricate lessons.
- Keep MEMORY.md under 200 lines total.
