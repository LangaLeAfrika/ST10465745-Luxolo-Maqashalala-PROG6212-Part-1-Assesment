# Azure images: setup and verification

The API stores image bytes in private Azure Blob Storage and saves the relative blob
path on `Event.BannerImagePath` or `User.ProfileImagePath`. MVC sends multipart uploads
to the API and displays images through its own image proxy. It never receives Azure
credentials or directly accesses SQL. No SAS keys or connection strings are returned.

## Local setup

1. In Azure create/verify a **private** container named `raceday-images`.
2. Right-click the **RaceDay API** project in Visual Studio → **Manage User Secrets**.
   Preserve any existing settings and add:

```json
{
  "AzureBlobStorage": {
    "ConnectionString": "YOUR_LOCAL_AZURE_CONNECTION_STRING",
    "ContainerName": "raceday-images"
  }
}
```

Keep the actual value out of source control and screenshots. Visual Studio adds a
`UserSecretsId` to the API project file; that identifier is safe to commit. Run the
API in Development so the default configuration loads user secrets. Never copy
secrets into Docker images; runtime environment configuration comes later.

3. Apply the included migration in Visual Studio **Package Manager Console**:

```powershell
Update-Database -Project RaceDay -StartupProject RaceDay -Context RaceDayDbContext
```

Do not add another migration: `AddImagePaths` is already included. Both columns are
nullable, so existing users/events keep working without images.

4. Run both API and MVC with their HTTPS profiles.

## Browser and Azure checks (must be done with your real account)

- Log into MVC → **My profile → Change profile photo**. Upload a small PNG/JPEG.
  Check the new photo appears; reload and log out/in to verify persistence.
- Log in as an Organiser → edit an existing event → **Change event banner**.
  Upload an image and check the preview. Create the event first if needed.
- Log in as a Participant → browse events and open the event entry page.
  Verify the banner is displayed there.
- In Azure → Containers → `raceday-images`, verify `users/{id}/...` and
  `events/{id}/...` blobs exist. Keep container access private.
- Replace an image; verify the new image appears and its previous blob is removed.
- Try a text file renamed `.png`, an SVG and a file over 5 MB. They must be rejected.
- A second Organiser must not change someone else's event banner. A Participant
  cannot upload event banners. Another account cannot read your profile photo.
- Confirm database paths using SSMS:

```sql
SELECT UserId, FirstName, ProfileImagePath FROM [User];
SELECT EventId, EventName, BannerImagePath FROM [Event];
```

Capture screenshots of successful MVC uploads, Azure blobs (no secrets), these
path columns and the newest CI run for the final submission.

## API routes

| Method | Route | Access |
|---|---|---|
| POST | `/api/events/{id}/banner` | Owning Organiser |
| GET | `/api/events/{id}/banner` | Public, matching event-list access |
| POST | `/api/users/me/photo` | Current authenticated user |
| GET | `/api/users/me/photo` | Current authenticated user |

POST bodies are multipart form data with field `file`. PNG/JPEG extensions and
binary signatures are checked; this is not a malware scanner or full image decoder.
The upload limit is 5 MB. Client filenames are replaced with generated GUID names.
Unconfigured/unavailable Azure returns a friendly 503 response. Missing images show
an MVC placeholder. Uploading to a public container is refused. Reads use no-store.

## Validation and limits

Automated API tests use fake storage (no Azure credentials/cost) and cover privacy,
ownership, replacement, persistence of paths, validation and storage failure. MVC
checks cover protected forms and antiforgery. Real Azure connectivity and browser
appearance still require the checks above; they were not verified from the coding
workspace. Old images are cleaned up on replacement; deleting an entire user/event
currently leaves its blob for manual storage housekeeping. Concurrent replacements
can also leave an unused blob; a production service would add scheduled cleanup.

The patch also restores enrolment-status badges missing from the pushed MVC views,
which caused the existing My Events rendering test to fail.
