# Part 3 MVC and CI patch

## Prerequisite

Apply the earlier enrolment-status patch and its migration first. This patch is
incremental from that state; it does not add another migration. Commit your local
work before applying it. The public repository still pointed to 8984353 during
this review; later local edits may cause a patch context conflict.

Save `raceday-mvc-ci.patch` in Downloads. Open a PowerShell terminal at the
repository root and run:

```powershell
git apply --check "$env:USERPROFILE/Downloads/raceday-mvc-ci.patch"
git apply "$env:USERPROFILE/Downloads/raceday-mvc-ci.patch"
```

If the check fails, stop and share its output. Do not force or partly apply it.

## Changes

- Repaired shared layout markup, duplicate menu items and missing mobile toggler.
- Restored signed-in name/role display and added home-page dashboard links.
- Corrected dashboard card nesting and enrolment/action column order.
- Added a consistent green sporting theme, visible keyboard focus, skip link,
  wrapping navigation and non-overlapping footer. Removed old scoped footer CSS.
- Fixed missing EventDate in API result-list projections (both participant and
  organiser list responses share this projection).
- Added a result-date regression assertion and 14 MVC smoke-test cases.
- Login routes users to a protected role dashboard; malformed API JSON produces
  a friendly login error. Unexpected MVC errors use the friendly error page.
- Set English form parsing explicitly so decimal input such as 10.5 is stable.
- Added root RaceDay.sln for all four projects; CI restores/builds that solution.
- Corrected nonexistent Swagger Annotations version 6.6.25 to 6.6.2, matching
  Swashbuckle.AspNetCore. Updated the artifact action to v7.
- Updated README honestly: Azure, Docker and final evidence remain outstanding.

## Validation performed

The 55-case test run passed: 41 existing API cases and 14 new MVC cases.
The MVC cases use the real Razor/session/antiforgery pipeline and a fake API.
The API tests use EF InMemory. Neither exercises LocalDB, Azure or Docker.
Patch application is checked against a clean copy of the prerequisite state.
Browser visual verification could not run: Chromium was blocked by the execution
sandbox. Responsive CSS and the navigation toggler still need the checks below.

## Checks on your Windows computer

1. Stop both applications. Open the new root RaceDay.sln, then build the solution.
2. Run Test Explorer > Run All. Expected total: 55 passing tests.
3. Set RaceDay and RaceDay.Mvc as startup projects using their https profiles.
4. Register/login as each role, update a demo profile, and log out.
5. Organiser: create/edit/delete an unused event; manage categories; view counts,
   enrolments, capture/edit results for an event dated today or earlier.
6. Participant: filter events, enter a category, check duplicate-entry feedback,
   My Events status and the correct event date/time/position in My Results.
7. Check ownership with two different organiser accounts. Check role denial and
   anonymous redirects by opening protected URLs directly.
8. Check at widths 390, 768 and 1280 pixels: menu opens/collapses, no page-wide
   horizontal overflow, tables scroll within their containers, footer does not
   cover forms. Also tab through navigation and form controls.
9. Push your reviewed changes; check Actions > latest run > Build and test.
   The workflow change has not been run on GitHub from this environment.

## Commit

Suggested message: `Polish MVC navigation and dashboard, fix result dates, and build all projects in CI`.
Do not commit bin, obj, TestResults, credentials or local secrets.

## Remaining Part 3 work

Azure uploads and image display; Docker build/run and host networking verification;
final screenshots, current CI evidence, tested Docker instructions and video.
Do not present these pending requirements as completed in the README or demo.
