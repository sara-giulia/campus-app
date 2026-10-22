# PROMPTS.md

One entry per run of a skill. Everyone uses the same format. Newest entry at the bottom.
The professor grades the before and after, so write what went wrong and what you changed in the skill.

## Format (copy this block for every run)

### <YYYY-MM-DD> · <who> · <skill name> · run <number>

- **What I asked:** the exact prompt given to Claude Code
- **Skill that ran:** name and the commit of the SKILL.md used
- **Result:** build (errors and warnings), tests, Postman (for example 36/36), what was wrong
- **What I changed in the skill and why:** the rule that was added or fixed (write "nothing" if the first run was good)
- **Commits:** the skill commit and the generated-code commit (separate commits)

---

## Entries

### 2026-10-22 · Carina Coman · cqrs · review fixes

- **What I asked:** review by a teammate (CreateListing example built in the real project: 0 warnings, 90 tests pass)
- **Skill that ran:** cqrs (manual review, no generation yet)
- **Result:** four problems found in SKILL.md
- **What I changed in the skill and why:**
  1. "Never change Domain/" contradicted the new `IUnitOfWork.cs`; now only existing files are protected.
  2. Added `dotnet test` to the final check, because the existing tests prove the behaviour did not change.
  3. Added an UpdateListing example, the only slice that maps two error types (404 and 400).
  4. Wrote the `HandleAsync` signature as a decision (`Task<Result<T>>`, `ct` without default), different from the course.
- **Commits:** "Fix cqrs skill after review: Domain rule, test check, update example, handler signature decision"