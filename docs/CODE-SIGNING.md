# Code signing policy

## Current status

The supplied 0.7.0 beta downloads are **unsigned**. SignPath enrollment has not been submitted: the project must first have a public repository and release. The optional GitHub signing steps are prepared but disabled. No certificate, account, token or approval has been obtained by this project.

Windows may warn about an unrecognized download. Verify the source and SHA-256 checksums before deciding to run it. Signing identifies a publisher and protects signed file integrity; it does not guarantee that SmartScreen will immediately trust a new release. [Microsoft explanation](https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/smartscreen-reputation).

## Apply after the first public release

1. Publish the MIT source and the first beta release on GitHub.
2. Review the [SignPath Foundation conditions](https://signpath.org/terms.html), then use its [application page](https://signpath.org/apply.html). Provide the actual repository/release links and your contact details privately in their application, not in this repository.
3. Request free signing for both the app and its installer. Describe the project as a native Windows utility for selecting playback devices and microphones, with no third-party application libraries or runtime network traffic.
4. Wait for their eligibility review. Their program requires maintained, released open-source software and has licensing conditions, including restrictions on commercial dual licensing. Enable account MFA and review their required signing policy before enrollment.
5. After approval, configure the GitHub integration and the artifact rules below. Grant the SignPath GitHub App access only to this project when setting up its repository integration.

Maintainer, reviewer and release approver: **Fuuzan**. Review outside contributions before merging; approve production signing requests only for reviewed releases. If accepted, add the required SignPath provider credit here and on the release page. The current pending status must not be presented as sponsored or signed.

## GitHub configuration after approval

In the repository's Actions settings, add the **secret** `SIGNPATH_API_TOKEN`. Never commit a token or certificate private key.

Add these repository **variables** using the values supplied by SignPath:

| Variable | Purpose |
|---|---|
| `SIGNPATH_ORGANIZATION_ID` | Your SignPath organization |
| `SIGNPATH_PROJECT_SLUG` | The approved project |
| `SIGNPATH_SIGNING_POLICY_SLUG` | The production signing policy |
| `SIGNPATH_APP_ARTIFACT_SLUG` | Configuration for the app artifact |
| `SIGNPATH_SETUP_ARTIFACT_SLUG` | Configuration for the Setup artifact |
| `SIGNPATH_ENABLED` | Set to `true` only after setup and approval |

The two artifact configurations must accept GitHub's ZIP wrapper: one containing only `SwitchMyAudio.exe`, the other containing only `SwitchMyAudio-<version>-Setup.exe`. Restrict their PE metadata to this product and approved release versions, and bind the signing policy to this repository's trusted GitHub build. Confirm the allowed tag/workflow rules with SignPath. [Integration reference](https://docs.signpath.io/trusted-build-systems/github).

## Release behavior

The existing tag workflow runs the tests and builds, uploads the app for signing, validates the returned signature, and packages that signed app without recompiling it. Setup embeds that exact signed app. The workflow then signs Setup, regenerates checksums and verifies that the portable and installed payloads match. A signing failure stops the release; enabling signing never silently falls back to unsigned downloads. The final release remains a draft for review.

Two requests may need approval, one for the app and one for Setup. Each can wait up to 30 minutes. If approval times out, rerun after checking the policy and pending requests. The signed path cannot be verified end to end until real SignPath access is granted and the workflow runs in the public repository. Update the status in this document and the release notes when publishing the first signed build.

Code signing happens during release preparation only. It adds no background service, network calls or account requirement to the installed app. Only build artifacts and repository/build metadata go to the signing provider; local preferences, real device information and private diagnostic files are excluded.
