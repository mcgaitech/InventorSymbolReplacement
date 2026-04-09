# Claude Code — Prompt Guide for Real-World Situations

> **How to use this file:**
> Keep this open in a split panel in VS Code.
> Copy the prompt block, paste into Claude Code, adjust the `[ ]` placeholders, and send.
>
> **Golden rules before every prompt:**
>
> - Always let Claude read `CLAUDE.md` + `CONTEXT.md` + `SESSION_LOG.md` first
> - One task per prompt — don't bundle multiple requests
> - Confirm before Claude starts writing code
> - Save session before switching accounts or closing VS Code

---

## Table of Contents

1. [Starting a New Session](#1-starting-a-new-session)
2. [Resuming After Token Limit](#2-resuming-after-token-limit)
3. [Asking Claude to Build Something](#3-asking-claude-to-build-something)
4. [Reviewing & Auditing the Project](#4-reviewing--auditing-the-project)
5. [Fixing a Bug](#5-fixing-a-bug)
6. [Refactoring Existing Code](#6-refactoring-existing-code)
7. [Saving a Session](#7-saving-a-session)
8. [End of Day / Pausing Work](#8-end-of-day--pausing-work)
9. [Switching Accounts Mid-Task](#9-switching-accounts-mid-task)
10. [Enforcing Architecture Rules](#10-enforcing-architecture-rules)
11. [Adding a New Module](#11-adding-a-new-module)
12. [Working with AutoCAD API](#12-working-with-autocad-api)
13. [Asking for Advice / Design Decisions](#13-asking-for-advice--design-decisions)
14. [Claude Did Something Wrong](#14-claude-did-something-wrong)
15. [Quick Reference Card](#15-quick-reference-card)

---

## 1. Starting a New Session

**Why:** Claude has no memory between sessions. You must reload context every time you open VS Code.

### Standard start

```
Read CLAUDE.md, CONTEXT.md, SESSION_LOG.md in order.
Only read the latest session entry at the top of SESSION_LOG.md.

Report back:
- What phase / task is currently active?
- Which file was being worked on last?
- What is the next concrete step?

Then ask me: "Continue with the next step, or do you want to change direction?"
Wait for my confirmation before doing anything.
```

### Expected response from Claude

```
✅ Read all 3 files.

Current state:
- Phase 2 — Building Views layer
- Last file: Views/DetailDesign/DetailDesignView.xaml (complete)
- Next step: Create FittingManagementView.xaml using the same _Template

Continue with FittingManagementView.xaml, or do you want to change direction?
```

---

## 2. Resuming After Token Limit

**Why:** When you switch to a new Claude account mid-task, the new session has zero context. A broken file may exist from the interrupted session.

```
Read CLAUDE.md, CONTEXT.md, SESSION_LOG.md in order.
The previous session was interrupted by a token limit.

Before continuing:
1. Check if any file mentioned in the last SESSION_LOG entry is incomplete
   (missing closing braces, half-written methods, TODO left mid-way)
2. List any files that may be in a broken state
3. Propose a recovery plan

Do NOT write any new code until I confirm the recovery plan.
```

### Expected response from Claude

```
⚠️ Found potential issue:
- Services/Weight/WeightCalculationService.cs
  → Method Calculate() appears incomplete (no closing brace at line 47)

Recovery plan:
1. Complete the Calculate() method body
2. Verify it compiles
3. Then continue with next planned task

Confirm to proceed?
```

---

## 3. Asking Claude to Build Something

**Why:** Vague requests produce vague code. Give Claude the exact scope, constraints, and what NOT to do.

### Build a new Service

```
Read CLAUDE.md first — check namespace rules and logging pattern.

Task: Create [ServiceName] in Services/[ModuleName]/

Requirements:
- Namespace: MCGCadPlugin.Services.[ModuleName]
- Must have a corresponding interface I[ServiceName]
- Follow the logging pattern (LOG_PREFIX, try/catch, Debug.WriteLine)
- XML comments in Vietnamese
- No AutoCAD API imports in the interface file

Do NOT implement the actual business logic yet — create the skeleton only.
Show me the skeleton first, wait for my approval, then write it to disk.
```

### Expected response from Claude

```
Here is the skeleton for IWeightCalculationService and WeightCalculationService:

[shows code preview]

Namespace: MCGCadPlugin.Services.Weight ✅
Interface file: Services/Weight/IWeightCalculationService.cs
Implementation: Services/Weight/WeightCalculationService.cs
LOG_PREFIX: "[WeightCalculationService]" ✅

Approve to write to disk?
```

---

## 4. Reviewing & Auditing the Project

**Why:** After multiple sessions, inconsistencies accumulate. Regular audits catch issues early before they become hard to fix.

### Full audit (run monthly or after major changes)

```
Read CLAUDE.md, CONTEXT.md, SESSION_LOG.md.

Perform a full project audit across these 6 areas.
For each issue found, report: [FILE] | [ISSUE] | [REASON] | [SUGGESTED FIX]
Do NOT auto-fix anything — list only. Wait for my confirmation per item.

AREA 1 — Namespace & File Structure
- Does every .cs file's namespace match its folder location?
- Does each file contain exactly one class?
- Does the filename match the class name?

AREA 2 — Architecture & Layering
- Do any Commands contain business logic? (not allowed)
- Do any Models import AutoCAD namespaces? (not allowed)
- Does every Service have a matching Interface?
- Is PaletteSet created anywhere outside PaletteManager.cs? (not allowed)

AREA 3 — Logging & Error Handling
- Which classes are missing LOG_PREFIX?
- Which important methods are missing entry/exit log lines?
- Is there any swallowed exception (catch without throw)?
- Do all AutoCAD transactions have try/catch?

AREA 4 — Code Quality
- Any magic numbers or hardcoded strings?
- Any duplicated logic across modules?
- Any method longer than 50 lines?
- Any missing null checks before using COM objects?
- Any IDisposable objects missing using statements?

AREA 5 — Comments & Documentation
- Any public method missing XML doc comment?
- Any comment written in wrong language (must be Vietnamese)?
- Any unresolved TODO comments?

AREA 6 — PaletteSet & WPF
- Is the GUID hardcoded in more than one place?
- Do all ViewModels implement INotifyPropertyChanged?
- Any UI updates from background thread without Dispatcher?
- Any business logic inside View code-behind?

After listing all issues, provide a summary table:
| # | File | Area | Severity | Suggested Fix |
Severity: 🔴 Critical / 🟡 Should fix / 🟢 Nice to have
```

### Quick audit (run after each session)

```
Quick audit — scan only files changed in the last session.
Check: namespace correct? logging present? no swallowed exceptions?
List issues only. Do not fix.
```

---

## 5. Fixing a Bug

**Why:** Giving Claude full context about the bug prevents it from guessing and making unrelated changes.

```
Read CLAUDE.md.

Bug report:
- File: [filepath]
- Method: [method name]
- Symptom: [what happens — e.g., "NullReferenceException on line 42"]
- Reproduction: [steps to reproduce]
- Expected behavior: [what should happen]
- AutoCAD version: 2023

Before fixing:
1. Explain what you think is causing the bug
2. Describe the fix you plan to make
3. List any other files that might be affected

Wait for my approval before making any changes.
```

### Expected response from Claude

```
Root cause analysis:
- The PaletteSet is accessed before Initialize() is called
- This happens when MCG_FittingManagement is run before MCG_Show

Planned fix:
- Add null check in FittingManagementCommand.cs
- Call PaletteManager.Instance.Show() which handles lazy init internally

Files affected:
- Commands/FittingManagement/FittingManagementCommand.cs (change)
- No other files need to change

Approve fix?
```

---

## 6. Refactoring Existing Code

**Why:** Refactoring without clear boundaries causes Claude to change too much or too little.

```
Read CLAUDE.md.

Refactor task: [describe what needs to change]
File: [filepath]

Constraints:
- Do NOT change public method signatures (other code depends on them)
- Do NOT change the namespace
- Keep all existing XML comments
- Maintain the same LOG_PREFIX value

Scope: only touch [specific method / region / class] — nothing else.

Show me a diff-style summary of changes before writing to disk.
```

---

## 7. Saving a Session

**Why:** Claude will forget everything when the session ends or token limit is reached. SESSION_LOG.md is the only memory that persists.

### Standard save

```
Save session. Add a new entry at the TOP of SESSION_LOG.md.
Do NOT delete previous entries.

Include:
1. Session number (increment from last entry)
2. Date and time
3. Files created or modified (list every file)
4. Current status (phase, what is complete, what is not)
5. Pending issues (any bugs or unfinished tasks)
6. Next step (specific: which file, which method, what goal)
7. API notes (any AutoCAD API quirks discovered this session)

Write with enough detail that the next session can resume immediately
without asking any clarifying questions.
```

### Token limit warning save (use when Claude warns about context)

```
Running low on tokens — save session immediately.
Prioritize: next step must be written in maximum detail.
Skip any section that is not critical for resuming work.
```

### Expected SESSION_LOG.md entry

```markdown
## [SESSION 007] 2026-04-08 14:30

### Files Changed

- Created: Services/Weight/IWeightCalculationService.cs
- Created: Services/Weight/WeightCalculationService.cs
- Modified: Commands/PaletteManager.cs (added null guard in Show())

### Status

- Phase 2 complete: all 5 Views created
- Phase 3 in progress: Services layer — Weight module done, 4 remaining

### Pending Issues

- FittingManagementService.cs: Extract() method skeleton only, no logic yet

### Next Step

- File: Services/FittingManagement/FittingManagementService.cs
- Method: Extract()
- Goal: implement block reference selection using SelectionFilter for INSERT type

### API Notes

- PaletteSet.AddVisual() must be called AFTER setting Size, otherwise layout breaks
```

---

## 8. End of Day / Pausing Work

**Why:** End-of-day saves should include a higher-level summary to help you re-orient the next morning.

```
End of day — save session.
In addition to the standard session log, add a section:

### Today's Summary
- What was planned vs what was actually completed?
- Any unexpected problems encountered?
- Confidence level on current code quality: High / Medium / Low
- Recommended first action tomorrow morning
```

---

## 9. Switching Accounts Mid-Task

**Why:** When you run out of tokens and switch to a second Claude account, the new session must be oriented quickly without re-explaining everything.

### Step 1 — Save before switching (run on old account)

```
Token limit approaching — save session now.
Make next step extremely specific: exact file, exact method, exact line if possible.
```

### Step 2 — Start on new account

```
Read CLAUDE.md, CONTEXT.md, SESSION_LOG.md.
Previous session ended due to token limit mid-task.

Read the latest SESSION_LOG entry carefully.
Tell me exactly where we left off and what the next action is.
Do not start coding until I confirm.
```

---

## 10. Enforcing Architecture Rules

**Why:** As the project grows, Claude may drift from the established architecture. Use this prompt when you notice violations or want a proactive check.

```
Architecture check — do not write any code.

Verify the following rules from CLAUDE.md are being followed
in all files created or modified this session:

[ ] Namespace matches folder location exactly
[ ] Commands contain no business logic
[ ] Models have no AutoCAD namespace imports
[ ] Every new Service has a matching Interface
[ ] PaletteSet only exists in PaletteManager.cs
[ ] LOG_PREFIX declared in every class
[ ] All important methods have entry/exit log lines
[ ] No exception is swallowed (catch without throw or log)
[ ] All AutoCAD database access is wrapped in Transaction

Report: PASS or FAIL for each rule.
For any FAIL: show the file and line, explain why it fails.
```

---

## 11. Adding a New Module

**Why:** New modules must follow the exact same structure as existing ones. This prompt enforces consistency from the start.

```
Read CLAUDE.md and .claude/PALETTE_GUIDE.md.

Task: Add a new module named [ModuleName]

Required files to create (follow _Template in each folder):
1. Models/[ModuleName]/[ModuleName]Data.cs
   namespace: MCGCadPlugin.Models.[ModuleName]

2. Services/[ModuleName]/I[ModuleName]Service.cs
   namespace: MCGCadPlugin.Services.[ModuleName]

3. Services/[ModuleName]/[ModuleName]Service.cs
   namespace: MCGCadPlugin.Services.[ModuleName]

4. Views/[ModuleName]/[ModuleName]View.xaml
   namespace: MCGCadPlugin.Views.[ModuleName]

5. Commands/[ModuleName]/[ModuleName]Command.cs
   namespace: MCGCadPlugin.Commands.[ModuleName]
   → must call PaletteManager.Instance.Show() only, no PaletteSet creation

6. Add one line to PaletteManager.cs Initialize():
   _paletteSet.AddVisual("[Display Name]", new [ModuleName]View());
   → append to the END of the AddVisual list

Create skeletons only. No business logic.
Show me the file list and namespaces for approval before writing to disk.
```

---

## 12. Working with AutoCAD API

**Why:** AutoCAD API has many quirks. These prompts help Claude apply the correct patterns from CLAUDE.md.

### Selection with filter

```
Read CLAUDE.md section "AutoCAD API — Patterns".

In [filepath], method [methodName]:
Add a selection prompt that filters for [entity type, e.g. INSERT / LINE / CIRCLE].
Use the SelectionFilter pattern from CLAUDE.md.
Wrap in Transaction. Add LOG_PREFIX log lines.
Show code preview before writing.
```

### Transaction with undo support

```
In [filepath], wrap the following operation in a Transaction
with proper try/catch and log lines:
[describe the operation]

The transaction must:
- Commit on success
- Log on both success and failure
- Not swallow the exception on failure
```

---

## 13. Asking for Advice / Design Decisions

**Why:** Before building, it's worth asking Claude to compare options. Lock the decision into CLAUDE.md so it's not re-debated later.

```
I need to decide how to implement [feature].

Context:
- Project: MCGCadPlugin (AutoCAD .NET, WPF, 5 modules)
- Constraint: [any relevant constraint]

Give me 2–3 options. For each option:
- Brief description
- Pros
- Cons
- Recommendation for this project specifically

Do NOT write any code yet. I will choose an option and then we proceed.
After I choose, remind me to add the decision to CLAUDE.md.
```

---

## 14. Claude Did Something Wrong

**Why:** When Claude makes a mistake, you need to correct it precisely without triggering a cascade of unintended changes.

### Undo a specific change

```
The last change to [filepath] was incorrect.
Revert ONLY that file to its state before this session.
Do not touch any other file.
After reverting, show me the current content of the file for confirmation.
```

### Wrong namespace was used

```
You used the wrong namespace in [filepath].
The correct namespace is: MCGCadPlugin.[Layer].[ModuleName]
Fix the namespace declaration only. Do not change anything else in the file.
```

### Claude ignored a rule from CLAUDE.md

```
You violated a rule from CLAUDE.md: [quote the rule]
This happened in: [filepath], [method or line]

Fix only this violation. Explain why it happened so we can
consider updating CLAUDE.md to make the rule clearer.
```

---

## 15. Quick Reference Card

Copy this to a sticky note or keep it pinned in VS Code:

```
EVERY SESSION START
→ "Read CLAUDE.md, CONTEXT.md, SESSION_LOG.md. Report status. Ask before doing."

BEFORE EVERY TASK
→ "Show me a plan / skeleton first. Wait for approval before writing to disk."

WHEN TOKEN IS LOW
→ "Save session now. Make next step extremely specific."

END OF DAY
→ "End of day — save session with today's summary."

SWITCHING ACCOUNTS
→ Old account: save session with maximum detail on next step
→ New account: read 3 files, report exactly where we left off

CLAUDE MADE A MISTAKE
→ "Revert [file] only. Do not touch anything else."

ARCHITECTURE DRIFT
→ "Architecture check — verify all rules from CLAUDE.md. Report PASS/FAIL. No code."
```

---

_Last updated: 2026-04-08_
_Project: MCGCadPlugin — AutoCAD 2023 | C# | .NET 4.8 | WPF_
