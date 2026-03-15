# Copilot Instructions

## Project Guidelines
- Wahapedia CSV files in this project are actually pipe-delimited; use '|' as the delimiter when parsing CSV lines.
- Unit composition and points bracket selection in the list builder should be automatic based on unit composition/options rather than showing a manual mismatch hint.
- Preserve potentially intentional navigation-related services/injections unless clearly unused across the app.
- In this codebase's Warhammer 40k 10th edition rules handling, mortal wound spillover should only occur when the mortal wounds are not caused by Devastating Wounds.

## Database Inspection
- Use the normal sqlite3 command for direct SQLite inspection in this workspace.
- The SQLite database for OmniTactica is located at: C:\Users\nerda\AppData\Local\User Name\com.companyname.OmniTactica\Data\wahapedia.db