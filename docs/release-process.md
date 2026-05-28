# Release Process

DAX Studio ships two release channels:

| Channel | Audience | Distribution |
|---|---|---|
| **Stable**  | All users (default)                                | GitHub Releases (no `prerelease` flag), winget, chocolatey, daxstudio.org |
| **Preview** | Users who have explicitly opted in, or who are already running a preview build | GitHub Releases with the `prerelease` flag set |

Both channels install in the same location and share the same Inno Setup `AppId`,
so installing a build of either channel upgrades the existing installation in place.

## How users opt in to preview builds

There are two ways a DAX Studio install will start surfacing preview notifications:

1. **Manual opt-in (stable build).** Tools → Options → **Privacy** →
   **Show Pre-Release Notifications**. When this is `true`, the version check
   considers the `PreRelease` block in `CurrentReleaseVersion.json` in addition
   to the stable release.
2. **Automatic (preview build).** Any build compiled with the `PREVIEW` symbol
   (or a `DEBUG` build) is automatically opted in. This is the case for the
   `Preview` build configuration produced by AppVeyor.

In both cases, the user is offered whichever of the stable or preview versions is
newer than what they have installed. A user running a preview build will still be
prompted to update to a newer **stable** release when one becomes available — they
are never stuck on the preview channel.

## `CurrentReleaseVersion.json` schema

`src/CurrentReleaseVersion.json` is fetched by every running copy of DAX Studio
(via `HttpClientHelper.CurrentGithubVersionUrl`, which in release builds points
at `https://daxstudio.org/CurrentReleaseVersion.json`) and drives the
update-available prompt.

```json
{
  "Version": "3.2.0.1030",
  "DownloadUrl": "https://daxstudio.org",
  "PreRelease": {
    "Version": "3.2.0.1030",
    "DownloadUrl": "https://github.com/daxstudio/daxstudio/releases"
  }
}
```

| Field | Owner | Description |
|---|---|---|
| `Version`              | **Stable release job**  | 4-part assembly version of the latest stable build |
| `DownloadUrl`          | **Stable release job**  | Landing page shown to stable users when an update is offered |
| `PreRelease.Version`   | **Preview release job** | 4-part assembly version of the latest preview build |
| `PreRelease.DownloadUrl` | **Preview release job** | Landing page shown to opted-in users (typically the GitHub Releases page so the user can grab the pre-release asset) |

**Contract for release jobs:**

- The stable release job updates only the top-level `Version` and `DownloadUrl`.
- The preview release job updates only the `PreRelease.Version` and
  `PreRelease.DownloadUrl`.
- Neither job should remove the other channel's block.

Violating this contract risks pushing the wrong channel to all users. (For example,
if the preview job updates the top-level `Version`, every stable install on earth
will be prompted to "upgrade" to the preview build.)

## Cutting a stable release

1. Merge `develop` → `master`.
2. Bump `src/CurrentReleaseVersion.json` `Version` and `DownloadUrl` on `master`.
3. AppVeyor (external project) builds the signed installer from `master`.
4. Publish a new GitHub Release on `DaxStudio/DaxStudio` with the installer attached;
   **do not** check the "This is a pre-release" box.
5. `.github/workflows/winget_publish.yml` runs automatically and submits the new
   version to the Windows Package Manager Community Repository.

## Cutting a preview release

1. Ensure your feature branches are merged into `develop`.
2. Bump `src/CurrentReleaseVersion.json` `PreRelease.Version` (leave the top-level
   `Version` alone) on `develop`.
3. AppVeyor builds the installer using its preview configuration, which defines
   the `PREVIEW` compile symbol (see `build_preview.cmd` for the equivalent local
   command).
4. Publish a new GitHub Release on `DaxStudio/DaxStudio` with the installer
   attached and **check the "This is a pre-release" box**.
5. The winget workflow short-circuits when `github.event.release.prerelease == true`,
   so winget will not pick up the preview build.

## What gets prompted to whom

| Installed | Opted-in? | Stable on server | Preview on server | Prompted to install |
|---|---|---|---|---|
| Stable 1.0     | no  | 1.0 | 1.1-pre | nothing |
| Stable 1.0     | yes | 1.0 | 1.1-pre | preview 1.1-pre |
| Stable 1.0     | yes | 1.2 | 1.1-pre | stable 1.2 |
| Preview 1.1-pre | (auto-yes) | 1.0 | 1.1-pre | nothing |
| Preview 1.1-pre | (auto-yes) | 1.2 | 1.1-pre | stable 1.2 |
| Preview 1.1-pre | (auto-yes) | 1.0 | 1.2-pre | preview 1.2-pre |

The selection logic lives in `VersionCheck.SelectServerVersion` in
`src/DaxStudio.UI/Model/VersionCheck.cs`. Unit tests covering each row above are in
`tests/DaxStudio.Tests/VersionCheckTests.cs`.
