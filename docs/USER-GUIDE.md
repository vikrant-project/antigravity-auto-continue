# AntiGravity Repeater

A portable Windows desktop app that resends a prompt in one selected AntiGravity conversation after the agent becomes idle.

## Run

Open `dist/AntiGravityRepeater.exe`. No installer, Node.js, Python, browser debugging, or administrator access is required. It uses the Windows .NET Framework runtime.

1. Keep AntiGravity IDE open and signed in.
2. Select the conversation to continue. Press Refresh if you opened a new conversation.
3. Enter the repeat prompt, for example `continue`.
4. Choose the idle delay and repeat limit. A limit of **0 means unlimited**.
5. Press **Start repeating**. Press **Stop** or **Ctrl+Alt+F12** to stop future sends.

The checkbox allows a first send when the selected conversation is already idle. Uncheck it to wait until the agent has first been observed working.

Minimizing the controller moves it to the Windows notification area. Double-click its icon to reopen it; right-click for Stop or Exit. Closing the window exits and stops the controller. Stopping the controller does not cancel a prompt that AntiGravity has already accepted.

## Behavior

- Pins the selected conversation ID; changing the visible IDE tab does not redirect prompts.
- Uses AntiGravity's own local service and explicit running/idle status. It does not type into windows or capture your keyboard.
- Preserves the latest user model and policy configuration on every follow-up.
- Waits for a stable idle state and checks status again immediately before sending.
- Tracks acknowledgement and prevents duplicate sends for an unchanged completion.
- Stops on an unresolved terminal agent error, pending approval, uncertain send outcome, or repeat limit. Recovered tool errors followed by successful work do not stop the repeat loop.
- Reconnects to the same conversation if a service connection is lost. It never substitutes a different conversation.
- Saves the prompt and options, but always launches stopped.

The IDE may be minimized or behind another window. Both applications must remain running and the laptop must remain awake. Network access, model availability, and the IDE's own approval requirements still apply. The controller does not approve tool actions or change security settings. It cannot make an agent complete a task; it repeats your prompt as requested.

This implementation was verified against the locally installed AntiGravity IDE 1.107.0 on 21 October 2026. Its local service is an internal interface, so future AntiGravity updates may require an adapter change. No software can promise 100% reliability across all future IDE versions, network failures, or sleep/shutdown states.

## Local data

Preferences: `%LOCALAPPDATA%\AntiGravityRepeater\settings.json`.
Activity log: `%LOCALAPPDATA%\AntiGravityRepeater\activity.log`.

The prompt is saved as plain text in your Windows profile. Logs contain state changes and selected conversation IDs/titles, but no session tokens or prompt bodies. The controller reads the running local service's CSRF token into memory; it never writes it to disk or exposes a network listener.

## Usage, budget and multiple accounts

Click **Usage & accounts** in the main window. The panel shows the signed-in account, plan, model allowance used/remaining, quota reset times in IST, and credits exactly as reported by the IDE. Missing quotas remain **Not reported**. These are allowance figures, not a currency invoice.

The **daily request budget** limits controller send attempts across all accounts. `0` means unlimited. It resets at midnight India Standard Time. A request is counted before delivery, including an uncertain delivery, so account switching cannot bypass your chosen daily budget.

Your currently signed-in login is saved automatically when a reusable session is available. To add another account:

1. Click **Add Google login**. A separate AntiGravity profile opens.
2. Complete Google's normal sign-in yourself in that IDE window.
3. Return to this panel and click **Save new login**.
4. Keep **Automatically switch** enabled, then save settings.

The app never collects your Google password. An unfinished login is rejected. Pending sign-in profiles can be reopened after restarting the controller.

When the selected model reports zero remaining quota, the controller selects another saved login with cached available quota for that same model. It waits for all agents to become explicitly idle, backs up the current login, closes the IDE gracefully, changes only the login records in a SQLite transaction, and reopens the IDE. It then verifies the new account, original conversation ID and live quota before resuming. It tries another saved candidate if the newly verified account is exhausted, and prevents account loops. Unknown quota, no eligible login, an unchanged persistent server, or a failed identity/history check leaves repeating stopped. Ctrl+Alt+F12 also cancels a pending switch.

**Switching restarts the IDE.** The controller does not force-close the IDE, approve save prompts, alter device identifiers, change provider quotas, or purchase overages. Model allowances depend on your plan; see [Google's plans documentation](https://www.antigravity.google/docs/plans).

### Credential storage

The installed IDE's login records were located in `%APPDATA%\Antigravity IDE\User\globalStorage\state.vscdb`. The controller accesses only its OAuth session, user-status and credit-cache rows. It does not export their values.

Saved logins: `%LOCALAPPDATA%\AntiGravityRepeater\accounts.dpapi`.
Previous-login recovery backup, after switching: `%LOCALAPPDATA%\AntiGravityRepeater\last-login-backup.dpapi`.
Additional IDE profiles: `%LOCALAPPDATA%\AntiGravityRepeater\login-profiles\`.
Daily request count: `%LOCALAPPDATA%\AntiGravityRepeater\budget.json`.

The controller's saved login snapshots use Windows DPAPI encryption for the current Windows user. They are not included in the portable ZIP or test reports. Additional IDE profiles retain the IDE's own normal storage behavior. Never share your AppData login databases or encrypted vault as a portable account export.

### Verification boundary

The test suite verifies quota parsing, budget enforcement, encryption/tamper rejection, multiple saved logins, fixture account switching, transaction rollback, launch-failure recovery and switch-loop prevention. A real single-account read/encryption test and a real daily-budget send/stop test also passed. The isolated additional-account profile launch was tested, and an unfinished login was correctly rejected.

Only one genuine account was signed in during testing. A real switch between two Google accounts remains unverified until the second owner sign-in is completed. Do not interpret fixture tests as proof of a live second-account switch.

## Build and test

From this directory in PowerShell:

```powershell
.\build.ps1
.\dist\RepeaterDiagnostics.exe --self-test
.\dist\RepeaterDiagnostics.exe --accounts-test
.\dist\RepeaterDiagnostics.exe --probe
.\dist\RepeaterDiagnostics.exe --live-test
```

`--accounts-test` reads live usage and securely saves the current account; its report contains no credentials. `--probe` reads local conversation summaries. `--live-test` deliberately creates a separate harmless conversation, sends an initial test message, automatically repeats twice, and verifies three responses in the same history. Use `--live-test 5` to verify five repeat cycles. It uses your existing signed-in model and consumes ordinary model usage. The other conversations are left unchanged.

Source is in `src/`. `tests/live-test-result.json` records the live test. `TEST-REPORT.md` describes verification. `tools/inspect-schema.cjs` is a development utility for inspecting protobuf schema definitions bundled with the installed IDE; it is not needed to run the EXE.
