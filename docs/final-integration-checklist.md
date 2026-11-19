# Final integration check

Use this checkout on branch `codex/final-integration`. The original `develop`
working tree contains uncommitted teammate files and was not overwritten.

1. Open `Assets/_Project/Scenes/Main.unity` alone. Save any existing edits.
2. Run **Tools > Blackout Protocol > Audit Navigation**. Resolve any FAIL before
   continuing; the three new enemies require the baked Humanoid NavMesh.
3. Run **Tools > Blackout Protocol > Integrate Missing Enemies** once. Confirm an
   `Integrated Enemies` root contains Hunter, Breacher and Sentinel Drone. Save Main.
4. Open **Window > General > Test Runner**, run all Edit Mode tests, and capture the
   pass/fail count. The command-line runner cannot run here because this machine's
   Unity headless entitlement is unavailable.
5. Enter Play Mode. Check for zero Console errors. Hunter should wander/respond
   to noise, Breacher should plan and pursue, Sentinel should patrol/alarm, and
   the existing Flanker should still operate. Shoot each enemy and verify damage.
6. In Play Mode, listen for ambience. Use **Tools > Blackout Protocol > Test
   Security Alarm** and verify the alarm. Activate the shutdown console, verify
   confirmation and alarm stop, then reach the elevator. Restart with R after
   a win or loss.
7. Record the required 3-minute demo and evidence: all four AI roles together;
   Breacher GOAP plan and obstacle actions in its sandbox; player interaction
   physics (door, pushable crate, throwable); objective win/lose; Edit Mode tests;
   and the final Git history. Keep screenshots/video in the evidence location
   required by the course submission rules.

Main currently has no door/barricade route. The integrated Breacher uses its
direct-pursuit GOAP option there; the door and barricade actions remain available
in the Chethiya sandbox. Adding an authored obstacle route to Main is a separate
level-design choice and should be play-tested before changing the baked NavMesh.
