# Code signing policy

## Current status

Builds are unsigned. This project has not been approved by SignPath Foundation and does not claim sponsorship or access to its signing service. A GitHub build or SHA-256 checksum is not a Windows code signature.

## Project responsibilities

Repository owner, maintainer, reviewer, and proposed signing approver: [4sibi](https://github.com/4sibi). External contributions require maintainer review. Signing requests, if a service is approved, will require explicit maintainer approval and must originate from verifiable builds of this repository.

Privacy information: [PRIVACY.md](PRIVACY.md).

## Before applying

- Publish the complete source, build scripts, assets, and MIT license.
- Enable multi-factor authentication on GitHub and on any future signing account. This is an account setting, not something this repository can enforce or verify.
- Obtain a successful Windows CI build and maintain a documented release history.
- Review the current [SignPath Foundation conditions](https://signpath.org/terms.html), including reputation, software eligibility, metadata, and approval requirements.
- Apply via the [official application page](https://signpath.org/apply.html). Acceptance is not guaranteed.
- Only after acceptance, configure the required signing integration and add the provider attribution required by the service. Never commit private keys or credentials.

Code signing does not guarantee immediate SmartScreen reputation.
