# Official upstream research

Research date: **2026-10-01**.

The [official DeepCreative download page](https://downloads.deepcool.com/) and
[DeepCool's LT360 VISION ARGB announcement](https://www.deepcool.com/company/pressroom/newsrelease/2026/22984.shtml)
describe D-Cast as a screen-extension feature provided by DeepCreative. They do
not identify a public source repository, an open-source license, an SDK, or a
contribution process for D-Cast.

GitHub repository searches for `DeepCreative`, `D-Cast deepcool`, and
`deepcool official`, plus official-site searches for GitHub links, did not find
a repository that could be verified as the official D-Cast/DeepCreative upstream.
This is a search result, not proof that no public or private repository exists.

Related community projects include:

- [Nortank12/deepcool-digital-linux](https://github.com/Nortank12/deepcool-digital-linux): a Linux CLI recreating DeepCool Digital functionality, with device protocols and GPL-3.0 licensing.
- [naithy/deepcool-digital-service](https://github.com/naithy/deepcool-digital-service): a Windows HID service for LD-series digital displays.

These are community projects, not the official D-Cast project. Their driver/
protocol scope differs from Rei Cast's Windows extended-display window. No pull
request was filed just to advertise an unrelated application or claim official
support.

## If an official repository becomes available

Verify ownership from an official DeepCool link, read its license and contribution
instructions, and agree on the appropriate module boundary. A display example,
local-status adapter, or documentation example may be a better contribution than
placing an entire WinForms application in a driver repository. Run that
repository's required checks before submitting a focused pull request. Acceptance
and merging are decisions for the upstream maintainers.
