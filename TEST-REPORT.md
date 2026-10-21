# Verification report

Date: 21 October 2026 (Asia/Kolkata).
Target: the installed Windows AntiGravity IDE, package version 1.107.0.
Artifact: `dist/AntiGravityRepeater.exe` (portable Windows application).

## Results

- Build: successful with the installed Microsoft .NET Framework C# compiler; no external package downloads.
- Automated regression tests: **28 assertions passed**.
- Real integration run: **5 additional assertions passed**, for **33 total assertions** in the final diagnostics run.
- Local service discovery: found the running AntiGravity service and actual conversation IDs without restarting the IDE.
- Same-history repeat: created isolated test conversation `52b5a2f0-23aa-467f-849b-b0e2f90033b2`, sent one initial prompt, then automatically sent two repeats after completion. Verified **3 user inputs and 3 REPEATER_OK replies in that single conversation**, with final status IDLE.
- Desktop UI: ran the compiled EXE, entered a harmless prompt, set the repeat limit to 1, and pressed Start. Verified one REPEATER_UI_OK reply in test conversation `be1a95d5-6573-4c46-ab7e-bc0975aa436d` and the displayed repeat-limit stop state.
- Stop button: started another countdown, pressed Stop, and verified 0 prompts sent.
- Emergency stop: started another countdown, pressed Ctrl+Alt+F12, and verified the Emergency stop pressed state with 0 prompts sent.
- UI layout: inspected the actual rendered Windows window and accessibility controls; all configuration, status, and log controls were visible.
- Preferences: prompt, delay, repeat limit, and selected conversation persist locally; process restart launches stopped.

## Regression coverage

Working/busy status, stable idle delay, repeat limits, duplicate suppression, unknown status, disconnection, reconnect debounce, explicit Stop, missing acknowledgement timeout, uncertain send result, fast responses that complete between polls, multiline Unicode prompt encoding, error-message steps that have DONE status, preservation of model and permission settings, recovery after an earlier turn error, and late callbacks after Stop.

The first exploratory live run revealed that omitting cascadeConfig causes this IDE to accept a request and then emit an error instead of replying. The implementation was corrected to preserve the selected conversation's latest user configuration, and subsequent live runs verified actual replies. Error-message detection is covered by a regression test.

## Evidence

- `tests/live-test-result.json`: final real integration result, timestamps, exact conversation ID, send events, and reply counts.
- `tests/ui-test-result.json`: UI reply, repeat-limit, Stop, and hotkey verification.
- Source: `src/Core.cs`, `src/Service.cs`, `src/MainForm.cs`, and `src/Diagnostics.cs`.

## Limits of verification

Tests establish operation on this laptop and the currently installed IDE. They do not establish perpetual reliability on future IDE versions, through laptop sleep/shutdown, service unavailability, expired authentication, model quota exhaustion, or network failures. Restarted language-server reconnection is implemented; an actual IDE restart was not forced during testing. The app waits for existing approval requirements and does not approve agent tool actions.

The supplied application opens stopped, with `continue`, a 10-second idle delay, and no repeat cap. Select the intended conversation and press Start repeating to arm it. Test conversations remain available in AntiGravity for inspection.

## Interface redesign — 21 October 2026

Rebuilt the Windows interface with a two-column dashboard, rounded cards, a dedicated prompt editor, purple action styling, a session monitor, custom number controls, a themed conversation picker, and a compact activity panel. Replaced forced scrollbars and stock checkbox styling. Added dark window chrome on supported Windows versions.

The redesigned EXE builds successfully and all 28 regression assertions pass. The rendered application and conversation dropdown were inspected on screen. Increment/decrement controls were exercised, and the delay restored to 10 seconds. The repeat engine and local-service send logic were unchanged. Earlier live integration evidence applies to the retained engine; no additional model prompts were sent for this visual update.

## Workflow polish — 21 October 2026

Added three prompt presets, a live character counter, a functional countdown ring, an animated monitoring state, elapsed session time, truthful connection badges, control tooltips, and a compact activity feed with colored timestamps. Countdown messages no longer flood the visible feed. Fixed layout calculations so the editor and conversation selector fill their cards correctly. Empty prompts disable Start, and presets lock while monitoring is active.

The rebuilt EXE passes all 28 existing regression checks. On-screen checks confirmed preset changes and live character counts, the active session display, and the emergency stop returning the app to a stopped state with zero prompts sent. This UI check did not send additional prompts to the IDE. The new binary and portable ZIP were updated together.

## Usage, budget and account features — 21 October 2026

Implemented live per-model quota used/remaining and reset times, reported prompt/flow credits, a persisted daily request budget with midnight IST reset, Windows-user DPAPI encrypted saved logins, a normal Google sign-in flow through isolated IDE profiles, manual switching, and automatic rotation on explicit selected-model quota exhaustion.

Switching waits for all reachable agents to become explicitly idle and never force-closes the IDE. It stores an encrypted recovery snapshot, performs a SQLite transaction over only the OAuth/user-status/credit-cache rows, restarts the IDE, and verifies the new account identity, the original conversation, and available quota before resuming. Transactions roll back on write failure; failure to launch restores the previous credentials. Candidates already attempted in a rotation are excluded. The repeat limit and the global daily budget survive switching.

### Test results

- **77 assertions passed** in the final account diagnostics run: the original 28 repeat checks, 44 additional fixture checks, and 5 live account checks.
- Live account read: six models and six quota entries were returned by the installed IDE.
- Real login storage: the current genuine login was captured, encrypted using DPAPI, saved, loaded and compared in memory. No credential values are included in reports.
- Multiple-account fixtures: two distinct synthetic credentials, account identity changes, same-model eligibility, exhausted/unknown/stale/disabled quotas, rotation-loop suppression, and preservation of unrelated IDE state were checked.
- Recovery: injected failures after the first SQLite write and during IDE launch both preserved/restored the previous fixture credentials.
- Budget logic: unlimited mode, budget exhaustion and midnight IST reset were checked.
- Live budget UI: with an unlimited repeat limit and a daily budget of 1, the controller sent exactly one harmless request, received BUDGET_UI_OK, recorded one attempt, and stopped at the daily budget. That one genuine usage attempt remains in today's displayed count.
- Login UI: Add Google login launched an isolated AntiGravity profile and persisted its pending profile path. Attempting to save before sign-in was completed was correctly rejected. No credentials were entered through automation.
- Panel layout: the live allowance table, encrypted-login list, daily budget, automatic-switch toggle, Refresh and Save settings controls were inspected on screen. Save settings was exercised.

Evidence: `tests/accounts-test-result.json`, `tests/budget-ui-test-result.json`, and `tests/login-profile-test-result.json`.

### Explicit verification limit

Only one genuine account was available. **A real automatic switch between two Google accounts has not been verified.** The account-transfer, quota-selection and recovery paths were tested with distinct fixture accounts; that is not the same as a live provider login. The additional profile is open for the account owner to complete normal Google sign-in, after which Save new login stores its session securely. The app must not be described as 100% tested for real cross-account switching until this is completed.

Saved account data lives under the user's LocalAppData and is excluded from the EXE distribution and portable ZIP. Provider quota values are displayed as reported; no currency cost or unsupported aggregate allowance is invented.

## Fix: continuation stopped after a recovered tool failure — 21 October 2026

The controller incorrectly searched the entire recent turn for any error. In the reported conversation, a file-edit action failed, then the agent recovered and completed several successful actions and a final response. The old check still disabled repeating after the next countdown.

The completion check now evaluates the latest relevant agent step. Completed checkpoint/history bookkeeping does not mask a terminal failure, and a successful response/action correctly supersedes an earlier recovered error. Unresolved terminal errors and pending/blocked approvals still pause repeating. Attention states now show PAUSED rather than READY, with the complete status message available in a tooltip.

Verification:

- **85 regression assertions passed**, including 13 new recovery and second-cycle countdown checks.
- Read-only probe of the exact reported turn (steps 1289–1320): confirmed the intermediate failure, later successful completion, and that continuation is now allowed. That probe passed 88 assertions in its run.
- Live isolated conversation: **five automatic repeats**, **six user messages and six verified replies** in the same history. The repeat-limit stop also passed. This final live run passed **90 assertions**.
- No continuation prompt was sent into the user's original working conversation during verification.

Evidence: `tests/recovered-error-test-result.json` and the updated `tests/live-test-result.json`.

### Deployment block on the final GUI executable

After the successful diagnostics runs, Windows Application Control blocked launching the rebuilt GUI EXE. Code Integrity events 3077 and 3033 report that it does not meet the configured enterprise signing requirements (policy ID 0283ac0f-fff1-49ae-ada1-8a933130cad6). The artifact is unsigned, and no current-user code-signing certificate with a usable private key was available. No security policy or trust settings were changed. The code fix and live repeat diagnostics passed; reopening this GUI build remains blocked by the machine policy and requires an approved signing/deployment path.
