# Releasing StageJack

Maintainer notes for building, packaging and signing a release. End users don't need any of this; see the [README](../README.md).

## Code style

Keep the C# 5 compatible style, since the app is built with the compiler that ships with Windows.

## Build and package

```
Build-StageJack.cmd
Package-StageJack.ps1
```

`Build-StageJack.cmd` compiles `src/*.cs` into `dist\StageJack.exe` and copies the fonts and NAudio WASAPI capture libraries beside it. `Package-StageJack.ps1` uses Inno Setup 7 to create the per-user Windows installer. The analyzer uses NAudio 2.2.1 to capture the Windows playback mix or a separately selected render device, and .NET Framework 4.8 is required.

## Code signing

### Free: SignPath Foundation (open-source program)

StageJack can apply to the [SignPath Foundation open-source program](https://signpath.org/apply.html). Acceptance is reviewed, not automatic: the project must meet the Foundation's [open-source requirements](https://signpath.org/terms.html), including an OSI-approved license, an existing release, a published code-signing policy, and verifiable builds. The Foundation's certificate identifies **SignPath Foundation** as publisher. Accepted projects submit release builds from GitHub Actions for manual signing approval; set up that workflow after the application is accepted.

### Paid: Microsoft Artifact Signing

Create an account and certificate profile, complete identity validation, and assign your identity the **Artifact Signing Certificate Profile Signer** role. It has no free or trial tier; review Microsoft's current [setup requirements](https://learn.microsoft.com/en-us/azure/artifact-signing/quickstart) and [pricing](https://learn.microsoft.com/en-us/azure/artifact-signing/how-to-change-sku) first. The client tools, Windows SDK SignTool, and .NET 8 Runtime are needed on the signing PC.

For this local-signing option only, copy `signing/artifact-signing.metadata.example.json` to `signing/artifact-signing.metadata.json`, fill in the endpoint, account name, and certificate profile name from Azure, then run:

```powershell
.\Package-StageJack.ps1 -ArtifactSigningMetadataPath .\signing\artifact-signing.metadata.json
```

The metadata file is ignored by Git. Artifact Signing authenticates using Azure's default credential chain; SignTool applies Microsoft's recommended trusted timestamp. The script signs the app before building the installer, then signs the installer. To use a separately installed signing library, pass `-ArtifactSigningDlibPath <path-to-Azure.CodeSigning.Dlib.dll>`.
